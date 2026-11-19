using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RubbingBalls.Accounts
{
    public sealed class LoginSession : MonoBehaviour
    {
        [Serializable] private sealed class Settings { public string projectUrl; public string publishableKey; }
        [Serializable] private sealed class Credentials { public string email; public string password; }
        [Serializable] private sealed class Refresh { public string refresh_token; }
        [Serializable] private sealed class User { public string id; public string email; }
        [Serializable] private sealed class Reply
        {
            public User user;
            public string id, access_token, refresh_token, error_code;
            public int expires_in;
        }

        private static LoginSession instance;
        private string baseUrl, key, access, refresh;
        private double expires, nextRefresh;
        private RememberedLogin store;
        private bool remember;
        public bool CanRemember => RememberedLogin.Supported;
        public string StorageNotice { get; private set; } = "";
        private enum Operation { SignIn, Register, Restore, Renew, SignOut }
        public string AccountId { get; private set; }
        public string Email { get; private set; }
        public bool IsGuest { get; private set; }
        public bool HasAccount => !string.IsNullOrEmpty(AccountId);
        public bool HasChosen => IsGuest || HasAccount;
        public bool IsRestoring { get; private set; }
        public bool IsBusy { get; private set; }
        public bool Configured => baseUrl != null;
        public bool CanParticipateInLeaderboard => HasAccount && !IsGuest && Time.realtimeSinceStartupAsDouble < expires;
        public string Message { get; private set; } = "";
        public event Action Changed;

        public static LoginSession Current
        {
            get
            {
                if (instance != null) return instance;
                var root = new GameObject("Rubbing Balls Account");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<LoginSession>();
                return instance;
            }
        }

        public LiveOps Ops { get; private set; }
        public CoinWallet Coins { get; private set; }

        public AccountProfile Profile { get; private set; }
        private string projectRoot;

        private void Awake()
        {
            Coins = gameObject.AddComponent<CoinWallet>();
            ConfigureConnection();
            Ops = gameObject.AddComponent<LiveOps>();
            Ops.Initialize(this);
            Profile = gameObject.AddComponent<AccountProfile>();
            Profile.Initialize(this, projectRoot ?? "local");
        }

        private void ConfigureConnection()
        {
            var file = Resources.Load<TextAsset>("RubbingBallsLogin/Settings");
            if (!file) return;
            Settings config;
            try { config = JsonUtility.FromJson<Settings>(file.text); }
            catch (ArgumentException) { return; }
            if (config == null || !Uri.TryCreate(config.projectUrl?.Trim(), UriKind.Absolute, out var url)) return;
            if (url.Scheme != Uri.UriSchemeHttps || url.AbsolutePath != "/" || url.UserInfo.Length != 0 || url.Query.Length != 0 || url.Fragment.Length != 0) return;
            var publicKey = config.publishableKey?.Trim();
            if (string.IsNullOrEmpty(publicKey) || !publicKey.StartsWith("sb_publishable_", StringComparison.Ordinal)) return;
            key = publicKey;
            baseUrl = url.GetLeftPart(UriPartial.Authority) + "/auth/v1/";
            store = new RememberedLogin(url.IdnHost);
            projectRoot = url.GetLeftPart(UriPartial.Authority);
            Coins.Configure(this, projectRoot);

            var saved = store.Read();
            if (string.IsNullOrEmpty(saved)) return;
            remember = true;
            IsRestoring = true;
            Begin(Operation.Restore, "token?grant_type=refresh_token", JsonUtility.ToJson(new Refresh { refresh_token = saved }));
        }

        internal UnityWebRequest Rpc(string method, string json, string token = null)
        {
            var request = new UnityWebRequest(projectRoot + "/rest/v1/rpc/" + method, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", key);
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 10;
            request.redirectLimit = 0;
            return request;
        }

        private void OnDestroy() { store?.Dispose(); }

        public bool TryGetAccessToken(out string token)
        {
            token = CanParticipateInLeaderboard ? access : null;
            return token != null;
        }

        private void Update()
        {
            if (HasAccount && !IsBusy && Time.realtimeSinceStartupAsDouble >= nextRefresh)
                Begin(Operation.Renew, "token?grant_type=refresh_token", JsonUtility.ToJson(new Refresh { refresh_token = refresh }));
        }

        public void Authenticate(string email, string password, bool register, bool rememberOnPc)
        {
            if (IsBusy || HasAccount) return;
            if (!Configured) { Notify("Account sign-in is not configured yet."); return; }
            email = (email ?? "").Trim();
            if (email.Length == 0 || !email.Contains("@") || string.IsNullOrEmpty(password))
            { Notify("Enter your email and password."); return; }
            remember = rememberOnPc && CanRemember;
            StorageNotice = "";
            Begin(register ? Operation.Register : Operation.SignIn, register ? "signup" : "token?grant_type=password",
                JsonUtility.ToJson(new Credentials { email = email, password = password }));
        }

        public void ContinueAsGuest()
        {
            if (IsBusy || HasAccount) return;
            IsGuest = true;
            Notify("");
        }

        public void SignOut()
        {
            if (IsBusy || !HasAccount) return;
            Begin(Operation.SignOut, "logout?scope=local", null);
        }

        private void Begin(Operation op, string path, string body)
        {
            IsBusy = true;
            if (op == Operation.Renew) Changed?.Invoke();
            else Notify("Connecting…");
            StartCoroutine(Send(op, path, body));
        }

        private IEnumerator Send(Operation op, string path, string body)
        {
            using (var request = new UnityWebRequest(baseUrl + path, "POST"))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 12;
                request.redirectLimit = 0;
                request.SetRequestHeader("apikey", key);
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (op == Operation.SignOut) request.SetRequestHeader("Authorization", "Bearer " + access);
                yield return request.SendWebRequest();

                bool ok = request.result == UnityWebRequest.Result.Success;
                Reply reply = null;
                try { if (!string.IsNullOrWhiteSpace(request.downloadHandler.text)) reply = JsonUtility.FromJson<Reply>(request.downloadHandler.text); }
                catch (ArgumentException) { }
                IsBusy = false;
                if (op == Operation.Restore) IsRestoring = false;
                if (op == Operation.SignOut)
                {
                    bool removed = store.DeleteOwned();
                    Clear();
                    if (!removed) { Notify("You signed out. Delete the RubbingBalls/Supabase entry from Windows Credential Manager."); yield break; }
                    Notify(ok ? "You signed out." : "You signed out of this game. The server did not confirm the remote sign-out.");
                }
                else if (ok && reply?.user != null && Guid.TryParse(reply.user.id, out _) && !string.IsNullOrEmpty(reply.access_token) && !string.IsNullOrEmpty(reply.refresh_token) && reply.expires_in > 0)
                {
                    if (op == Operation.Renew && reply.user.id != AccountId) { store.DeleteOwned(); Clear(); Notify("Please sign in again."); yield break; }
                    AccountId = reply.user.id;
                    Email = reply.user.email;
                    access = reply.access_token;
                    refresh = reply.refresh_token;
                    double now = Time.realtimeSinceStartupAsDouble;
                    expires = now + reply.expires_in;
                    nextRefresh = expires - Math.Min(60.0, reply.expires_in * 0.25);
                    IsGuest = false;
                    if (remember && !store.Save(refresh))
                    {
                        remember = false;
                        store.DeleteOwned();
                        StorageNotice = "Your session is only open for this run. Windows could not save it.";
                    }
                    else if (!remember) store.DeleteOwned();
                    Notify("");
                }
                else if (op == Operation.Renew || op == Operation.Restore)
                {
                    if (request.responseCode == 401 || request.responseCode == 403 || (request.responseCode == 400 && reply?.error_code != "request_timeout"))
                    { store.DeleteOwned(); Clear(); Notify("Your session expired. Sign in again or play as a guest."); }
                    else if (op == Operation.Restore)
                        Notify("Could not recover the saved session. Sign in or play as a guest.");
                    else { nextRefresh = Time.realtimeSinceStartupAsDouble + 20.0; Notify("Could not renew the session. Retrying…"); }
                }
                else if (ok && op == Operation.Register && Guid.TryParse(reply?.id, out _))
                    Notify("Check your email to confirm your account, then sign in.");
                else Notify(Error(request.responseCode, reply?.error_code));
            }
        }

        private static string Error(long status, string code)
        {
            switch (code)
            {
                case "email_not_confirmed": return "Confirm your email before signing in.";
                case "invalid_credentials": return "The email or password is incorrect.";
                case "user_already_exists": case "email_exists": return "Try signing in with that email.";
                case "weak_password": return "The password does not meet the requirements.";
                case "email_address_invalid": case "validation_failed": return "Check your email and password.";
                case "email_address_not_authorized": return "The email service does not accept that address yet.";
                case "signup_disabled": return "Account sign-up is disabled.";
            }
            if (status == 429) return "Too many attempts. Wait a moment and try again.";
            if (status == 0 || status >= 500) return "Could not connect. You can retry or play as a guest.";
            return "The request could not be completed (HTTP " + status + ").";
        }

        private void Clear()
        {
            AccountId = Email = access = refresh = null;
            IsGuest = false;
            remember = false;
            StorageNotice = "";
            expires = nextRefresh = 0;
        }

        private void Notify(string message) { Message = message; Changed?.Invoke(); }
    }
}
