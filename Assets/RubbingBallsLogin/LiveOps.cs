using System;
using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public struct RoundRules : INetworkStruct
{
    public int version, dashes;
    public float gravity, aim_gravity_ratio, bounce, max_force, drag_distance;
    public float spawn_immunity, damage_scale, push_scale, trampoline_scale;
}

namespace RubbingBalls.Accounts
{
    public sealed class LiveOps : MonoBehaviour
    {
        [Serializable] private class Offer { public int id, price; }
        [Serializable] private class Snapshot
        {
            public int format;
            public bool double_coins;
            public RoundRules rules;
            public Offer[] offers;
        }

        private LoginSession session;
        private Snapshot snapshot;
        private float received, next;
        private bool busy, fresh, refreshRequested;
        internal event Action Changed;
        internal bool Doubled => fresh && snapshot.double_coins;
        internal RoundRules LatestRules => snapshot == null ? default : snapshot.rules;

        internal void Initialize(LoginSession login) => session = login;
        internal void RefreshSoon()
        {
            refreshRequested = busy;
            next = 0;
        }

        internal int Price(int id)
        {
            if (fresh)
                foreach (var offer in snapshot.offers)
                    if (offer.id == id) return offer.price;
            return -1;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (fresh && now - received > 65f) { fresh = false; Changed?.Invoke(); }
            if (session && session.Configured && !busy && now >= next) StartCoroutine(Read());
        }

        private IEnumerator Read()
        {
            busy = true;
            refreshRequested = false;
            using (var req = session.Rpc("rb_live_config", "{}"))
            {
                yield return req.SendWebRequest();
                Snapshot data = null;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try { data = JsonUtility.FromJson<Snapshot>(req.downloadHandler.text); }
                    catch (ArgumentException) { }
                }
                busy = false;
                if (data == null || data.format != 1 || data.rules.version != 1 || data.offers == null)
                {
                    next = refreshRequested ? 0f : Time.unscaledTime + 8f;
                    yield break;
                }
                snapshot = data;
                received = Time.unscaledTime;
                next = refreshRequested ? 0f : received + 25f;
                fresh = true;
                Changed?.Invoke();
            }
        }
    }
}
