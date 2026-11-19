using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            [NonSerialized] public bool sending;
            [NonSerialized] public float retryAt, delay;
        }

        [Serializable] private class Claim { public string round_id; }

        private LoginSession session;
        private string folder, displayedAccount;
        private readonly Dictionary<string, int> receipts = new Dictionary<string, int>();
        private readonly Queue<string> receiptOrder = new Queue<string>();

        internal bool TryGetReceipt(string round, out int amount)
        {
            amount = 0;
            return round != null && receipts.TryGetValue(round, out amount);
        }

        private bool reading, wanted, readFailed;
        private long? balance;
        private float nextRead;
        private int balanceVersion;
        private readonly Dictionary<string, Award> pending = new Dictionary<string, Award>();
        public event Action Changed;

        public string BalanceText => balance.HasValue ? balance.Value.ToString(CultureInfo.InvariantCulture) : "";

        internal void Configure(LoginSession login, string root)
        {
            session = login;
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
            if (receipts.ContainsKey(round)) return;
            if (pending.TryGetValue(key, out var existing))
            {
                TrySend(key, existing);
                return;
            }
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
            TrySend(key, award);
        }

        private IEnumerator SendPending()
        {
            while (true)
            {
                if (pending.Count > 0 && session.HasAccount)
                {
                    foreach (var entry in pending)
                    {
                        if (entry.Value.account == session.AccountId && !entry.Value.sending &&
                            Time.unscaledTime >= entry.Value.retryAt)
                        {
                            TrySend(entry.Key, entry.Value);
                            break;
                        }
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        private void TrySend(string key, Award award)
        {
            if (!award.sending && session.AccountId == award.account && session.TryGetAccessToken(out string token))
                StartCoroutine(SendAward(key, award, token));
        }

        private IEnumerator SendAward(string key, Award award, string token)
        {
            award.sending = true;
            using (var request = session.Rpc("rb_win_receipt", JsonUtility.ToJson(new Claim { round_id = award.round_id }), token))
            {
                yield return request.SendWebRequest();
                award.sending = false;
                if (request.result != UnityWebRequest.Result.Success ||
                    !int.TryParse(request.downloadHandler.text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int amount) ||
                    amount < 30 || amount > 100 || (amount > 50 && (amount < 60 || amount % 2 != 0)))
                {
                    if (award.delay == 0) Debug.LogWarning("Monedas: cobro pendiente (HTTP " + request.responseCode + ").");
                    award.delay = Mathf.Clamp(award.delay * 2, 1, 60);
                    award.retryAt = Time.unscaledTime + award.delay;
                    yield break;
                }
                pending.Remove(key);
                try { File.Delete(Path.Combine(folder, key + ".json")); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { Debug.LogWarning("Monedas: " + error.Message); }
                if (session.AccountId == award.account)
                {
                    receipts[award.round_id] = amount;
                    receiptOrder.Enqueue(award.round_id);
                    if (receiptOrder.Count > 32) receipts.Remove(receiptOrder.Dequeue());
                    balanceVersion++;
                    nextRead = 0;
                    Changed?.Invoke();
                }
            }
        }

        internal void ApplyShopBalance(long amount)
        {
            balanceVersion++;
            balance = amount;
            nextRead = 0;
            Changed?.Invoke();
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
            balanceVersion++;
            receipts.Clear();
            receiptOrder.Clear();
            balance = null;
            readFailed = false;
            nextRead = 0;
            Changed?.Invoke();
        }

        private void Update()
        {
            if (wanted && !reading && Time.unscaledTime >= nextRead && session && session.TryGetAccessToken(out string token))
                StartCoroutine(ReadBalance(balanceVersion, token));
        }

        private IEnumerator ReadBalance(int version, string token)
        {
            reading = true;
            using (var request = session.Rpc("rb_coins_balance", "{}", token))
            {
                yield return request.SendWebRequest();
                if (version == balanceVersion && request.result == UnityWebRequest.Result.Success &&
                    long.TryParse(request.downloadHandler.text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long amount))
                {
                    readFailed = false;
                    balance = amount;
                    Changed?.Invoke();
                }
                else if (version == balanceVersion && !balance.HasValue && !readFailed)
                {
                    readFailed = true;
                    Debug.LogWarning("Monedas: no se pudo leer el saldo (HTTP " + request.responseCode + ").");
                    Changed?.Invoke();
                }
            }
            reading = false;
            nextRead = version == balanceVersion ? Time.unscaledTime + 10f : 0f;
        }

    }
}
