using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace RubbingBalls.Accounts
{
    public sealed class AccountProfile : MonoBehaviour
    {
        private const int NameField = 1, ColorField = 2, SkinField = 4;

        [Serializable] private class Look
        {
            public string nickname = "NoName";
            public int color_index, skin_index, dirty;
        }

        [Serializable] private class ProfileRequest
        {
            public string seed_name;
            public int seed_color, seed_skin, dirty_fields;
        }

        private LoginSession session;
        private string prefix, activeKey;
        private Look look;
        private bool pending, sending;
        private float due, backoff;
        private int generation;

        // Applied actualiza las vistas cuando cambia el perfil desde fuera del editor de la pelota.
        public event Action Applied;
        internal event Action Synced;

        internal void Initialize(LoginSession login, string project)
        {
            session = login;
            prefix = "RB.Profile." + project + ".";
            if (!PlayerPrefs.HasKey(prefix + "guest"))
            {
                var previous = new Look {
                    nickname = CleanName(PlayerPrefs.GetString("PlayerName", "NoName")),
                    color_index = Mathf.Clamp(PlayerPrefs.GetInt("PlayerColor", 0), 0, 255),
                    skin_index = Mathf.Clamp(PlayerPrefs.GetInt("PlayerTexture", 0), 0, 255)
                };
                PlayerPrefs.SetString(prefix + "guest", JsonUtility.ToJson(previous));
            }
            session.Changed += SwitchAccount;
            SwitchAccount();
        }

        private void OnDestroy()
        {
            if (session) session.Changed -= SwitchAccount;
        }

        private static string CleanName(string name)
        {
            name = (name ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (name.Length == 0) return "NoName";
            if (name.Length <= 20) return name;
            return name.Substring(0, char.IsHighSurrogate(name[19]) ? 19 : 20);
        }

        private void SwitchAccount()
        {
            string next = prefix + (session.AccountId ?? "guest");
            if (next == activeKey) return;
            activeKey = next;
            generation++;
            string saved = PlayerPrefs.GetString(next, "");
            // La primera cuenta de esta instalación puede importar la personalización previa.
            if (saved.Length == 0 && session.HasAccount && !PlayerPrefs.HasKey(prefix + "firstAccount"))
            {
                PlayerPrefs.SetString(prefix + "firstAccount", session.AccountId);
                saved = PlayerPrefs.GetString(prefix + "guest", "");
            }
            look = null;
            if (saved.Length > 0)
            {
                try { look = JsonUtility.FromJson<Look>(saved); }
                catch (ArgumentException) { }
            }
            if (look == null) look = new Look();
            look.nickname = CleanName(look.nickname);
            look.color_index = Mathf.Clamp(look.color_index, 0, 255);
            look.skin_index = Mathf.Clamp(look.skin_index, 0, 255);
            pending = session.HasAccount;
            due = backoff = 0;
            Persist();
            Applied?.Invoke();
        }

        private void Persist()
        {
            PlayerLocalData.Nickname = look.nickname;
            PlayerLocalData.SelectedColor = look.color_index;
            PlayerLocalData.SelectedTexture = look.skin_index;
            // El chat existente lee estas claves para el jugador activo.
            PlayerPrefs.SetString("PlayerName", look.nickname);
            PlayerPrefs.SetInt("PlayerColor", look.color_index);
            PlayerPrefs.SetInt("PlayerTexture", look.skin_index);
            PlayerPrefs.SetString(activeKey, JsonUtility.ToJson(look));
            PlayerPrefs.Save();
        }

        public void SetName(string value)
        {
            value = CleanName(value);
            if (value == look.nickname) return;
            look.nickname = value;
            Edited(NameField);
        }

        public void SetColor(int value)
        {
            value = Mathf.Clamp(value, 0, 255);
            if (value == look.color_index) return;
            look.color_index = value;
            Edited(ColorField);
        }

        public void SetSkin(int value)
        {
            value = Mathf.Clamp(value, 0, 255);
            if (value == look.skin_index) return;
            look.skin_index = value;
            Edited(SkinField);
        }

        private void Edited(int field)
        {
            if (session.HasAccount)
            {
                look.dirty |= field;
                pending = true;
                due = Time.unscaledTime + 0.35f;
            }
            Persist();
        }

        private void Update()
        {
            if (pending && !sending && Time.unscaledTime >= due && session.TryGetAccessToken(out string token))
                StartCoroutine(Sync(generation, token));
        }

        private IEnumerator Sync(int owner, string token)
        {
            sending = true;
            var patch = new ProfileRequest {
                seed_name = look.nickname, seed_color = look.color_index, seed_skin = look.skin_index,
                dirty_fields = look.dirty
            };
            using (var req = session.Rpc("rb_ball_profile", JsonUtility.ToJson(patch), token))
            {
                yield return req.SendWebRequest();
                sending = false;
                if (owner != generation) yield break;
                Look remote = null;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try { remote = JsonUtility.FromJson<Look>(req.downloadHandler.text); }
                    catch (ArgumentException) { }
                }
                if (remote == null || string.IsNullOrWhiteSpace(remote.nickname))
                {
                    if (backoff == 0) Debug.LogWarning("Perfil: sincronización pendiente (HTTP " + req.responseCode + ").");
                    backoff = Mathf.Clamp(backoff * 2, 2, 60);
                    due = Time.unscaledTime + backoff;
                    yield break;
                }
                // Conservá los cambios locales hechos mientras la solicitud estaba en vuelo.
                if (look.nickname == patch.seed_name) look.dirty &= ~(patch.dirty_fields & NameField);
                if (look.color_index == patch.seed_color) look.dirty &= ~(patch.dirty_fields & ColorField);
                if (look.skin_index == patch.seed_skin) look.dirty &= ~(patch.dirty_fields & SkinField);
                if ((look.dirty & NameField) == 0) look.nickname = remote.nickname;
                if ((look.dirty & ColorField) == 0) look.color_index = remote.color_index;
                if ((look.dirty & SkinField) == 0) look.skin_index = remote.skin_index;
                pending = look.dirty != 0;
                backoff = 0;
                bool changed = PlayerLocalData.Nickname != look.nickname || PlayerLocalData.SelectedColor != look.color_index
                    || PlayerLocalData.SelectedTexture != look.skin_index;
                Persist();
                if (changed) Applied?.Invoke();
                Synced?.Invoke();
            }
        }
    }
}
