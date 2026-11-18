using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Fusion;
using UnityEngine;
using UnityEngine.Networking;

namespace RubbingBalls.Accounts
{
    public sealed class CoinWallet : MonoBehaviour
    {
        [Serializable] private class Award
        {
            public string round_id, account;
        }

        [Serializable] private class Claim { public string round_id; }

        private LoginSession session;
        private string endpoint, apiKey, folder, displayedAccount;
        private bool reading, wanted, readFailed;
        private long? balance;
        private float nextRead;
        private readonly Dictionary<string, Award> pending = new Dictionary<string, Award>();
        public event Action Changed;

        public string BalanceText => balance.HasValue ? balance.Value.ToString(CultureInfo.InvariantCulture) : "";

        internal void Configure(LoginSession login, string root, string publicKey)
        {
            session = login;
            endpoint = root + "/rest/v1/rpc/";
            apiKey = publicKey;
            folder = Path.Combine(Application.persistentDataPath, "CoinAwards", new Uri(root).IdnHost);
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var file in Directory.GetFiles(folder, "*.json"))
                {
                    try
                    {
                        var award = JsonUtility.FromJson<Award>(File.ReadAllText(file));
                        if (award != null && Guid.TryParse(award.round_id, out _) && Guid.TryParse(award.account, out _))
                            pending[Path.GetFileNameWithoutExtension(file)] = award;
                    }
                    catch (Exception error) { Debug.LogWarning("Monedas: " + error.Message); }
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { Debug.LogWarning("Monedas: " + error.Message); }
            session.Changed += AccountChanged;
            StartCoroutine(SendPending());
        }

        private void OnDestroy() { if (session) session.Changed -= AccountChanged; }

        internal Dictionary<string, SessionProperty> CreateRoomProof() => null;

        internal void RecordWin(string round)
        {
            if (folder == null || !session.HasAccount || !Guid.TryParse(round, out _)) return;
            string key = round + "_" + session.AccountId;
            if (pending.ContainsKey(key)) return;
            var award = new Award { round_id = round, account = session.AccountId };
            pending.Add(key, award);
            var file = Path.Combine(folder, key + ".json");
            try
            {
                if (!File.Exists(file))
                {
                    string tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    File.WriteAllText(tmp, JsonUtility.ToJson(award));
                    File.Move(tmp, file);
                }
            }
            catch (Exception error) { Debug.LogWarning("Monedas: envío pendiente en memoria. " + error.Message); }
        }

        private IEnumerator SendPending()
        {
            float delay = 1f;
            while (true)
            {
                if (pending.Count == 0 || !session.HasAccount) { yield return new WaitForSecondsRealtime(0.5f); continue; }
                bool failed = false;
                foreach (var entry in new List<KeyValuePair<string, Award>>(pending))
                {
                    if (entry.Value.account != session.AccountId || !session.TryGetAccessToken(out string token)) continue;
                    var claim = new Claim { round_id = entry.Value.round_id };
                    using (var request = Post("rb_claim_win", JsonUtility.ToJson(claim)))
                    {
                        request.SetRequestHeader("Authorization", "Bearer " + token);
                        yield return request.SendWebRequest();
                        if (request.result != UnityWebRequest.Result.Success || request.downloadHandler.text.Trim() != "true")
                        {
                            if (delay <= 1f) Debug.LogWarning("Monedas: premio pendiente (HTTP " + request.responseCode + "). Revisá la función rb_claim_win en Supabase.");
                            failed = true;
                            continue;
                        }
                    }
                    pending.Remove(entry.Key);
                    try { File.Delete(Path.Combine(folder, entry.Key + ".json")); }
                    catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { Debug.LogWarning("Monedas: " + error.Message); }
                    nextRead = 0;
                }
                delay = failed ? Mathf.Min(delay * 2f, 60f) : 1f;
                yield return new WaitForSecondsRealtime(delay);
            }
        }

        internal void ShowBalance(bool visible)
        {
            if (visible && !wanted) nextRead = 0;
            wanted = visible;
        }

        private void AccountChanged()
        {
            if (displayedAccount == session.AccountId) return;
            displayedAccount = session.AccountId;
            balance = null;
            readFailed = false;
            nextRead = 0;
            Changed?.Invoke();
        }

        private void Update()
        {
            if (wanted && !reading && Time.unscaledTime >= nextRead && session && session.TryGetAccessToken(out string token))
                StartCoroutine(ReadBalance(session.AccountId, token));
        }

        private IEnumerator ReadBalance(string account, string token)
        {
            reading = true;
            using (var request = Post("rb_coins_balance", "{}"))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
                yield return request.SendWebRequest();
                if (session.AccountId == account && request.result == UnityWebRequest.Result.Success &&
                    long.TryParse(request.downloadHandler.text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long amount))
                {
                    readFailed = false;
                    balance = amount;
                    Changed?.Invoke();
                }
                else if (session.AccountId == account && !balance.HasValue && !readFailed)
                {
                    readFailed = true;
                    Debug.LogWarning("Monedas: no se pudo leer el saldo (HTTP " + request.responseCode + ").");
                    Changed?.Invoke();
                }
            }
            reading = false;
            nextRead = session.AccountId == account ? Time.unscaledTime + 10f : 0f;
        }

        private UnityWebRequest Post(string method, string body)
        {
            var request = new UnityWebRequest(endpoint + method, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", apiKey);
            request.timeout = 10;
            request.redirectLimit = 0;
            return request;
        }
    }
}
