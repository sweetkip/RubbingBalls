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

        public CoinWallet Coins { get; private set; }

        private void Awake()
        {
            Coins = gameObject.AddComponent<CoinWallet>();
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
            Coins.Configure(this, url.GetLeftPart(UriPartial.Authority), key);

            var saved = store.Read();
            if (string.IsNullOrEmpty(saved)) return;
            remember = true;
            IsRestoring = true;
            Begin(Operation.Restore, "token?grant_type=refresh_token", JsonUtility.ToJson(new Refresh { refresh_token = saved }));
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
            if (!Configured) { Notify("El acceso con cuenta todavía no está configurado."); return; }
            email = (email ?? "").Trim();
            if (email.Length == 0 || !email.Contains("@") || string.IsNullOrEmpty(password))
            { Notify("Ingresá tu correo y tu contraseña."); return; }
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
            else Notify("Conectando…");
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
                    if (!removed) { Notify("Cerraste sesión. Borrá la entrada RubbingBalls/Supabase del Administrador de credenciales de Windows."); yield break; }
                    Notify(ok ? "Cerraste sesión." : "Cerraste la sesión en este juego. El servidor no confirmó el cierre remoto.");
                }
                else if (ok && reply?.user != null && Guid.TryParse(reply.user.id, out _) && !string.IsNullOrEmpty(reply.access_token) && !string.IsNullOrEmpty(reply.refresh_token) && reply.expires_in > 0)
                {
                    if (op == Operation.Renew && reply.user.id != AccountId) { store.DeleteOwned(); Clear(); Notify("Volvé a iniciar sesión."); yield break; }
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
                        StorageNotice = "La sesión está abierta solo en esta ejecución. Windows no pudo recordarla.";
                    }
                    else if (!remember) store.DeleteOwned();
                    Notify("");
                }
                else if (op == Operation.Renew || op == Operation.Restore)
                {
                    if (request.responseCode == 401 || request.responseCode == 403 || (request.responseCode == 400 && reply?.error_code != "request_timeout"))
                    { store.DeleteOwned(); Clear(); Notify("La sesión venció. Volvé a iniciar sesión o jugá como invitado."); }
                    else if (op == Operation.Restore)
                        Notify("No se pudo recuperar la sesión guardada. Iniciá sesión o jugá como invitado.");
                    else { nextRefresh = Time.realtimeSinceStartupAsDouble + 20.0; Notify("No se pudo renovar la sesión. Reintentando…"); }
                }
                else if (ok && op == Operation.Register && Guid.TryParse(reply?.id, out _))
                    Notify("Revisá tu correo para confirmar la cuenta y después iniciá sesión.");
                else Notify(Error(request.responseCode, reply?.error_code));
            }
        }

        private static string Error(long status, string code)
        {
            switch (code)
            {
                case "email_not_confirmed": return "Confirmá tu correo antes de iniciar sesión.";
                case "invalid_credentials": return "El correo o la contraseña son incorrectos.";
                case "user_already_exists": case "email_exists": return "Probá iniciar sesión con ese correo.";
                case "weak_password": return "La contraseña no cumple los requisitos del proyecto.";
                case "email_address_invalid": case "validation_failed": return "Revisá el correo y la contraseña.";
                case "email_address_not_authorized": return "El servicio de correo todavía no admite esa dirección.";
                case "signup_disabled": return "El registro de cuentas está desactivado.";
            }
            if (status == 429) return "Demasiados intentos. Esperá un momento y volvé a probar.";
            if (status == 0 || status >= 500) return "No se pudo conectar. Podés reintentar o jugar como invitado.";
            return "No se pudo completar la solicitud (HTTP " + status + ").";
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
