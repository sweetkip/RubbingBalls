using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RubbingBalls.Accounts
{
    public sealed class LoginMenu : MonoBehaviour
    {
        private enum Page { Closed, Login, Guest, Account, Restoring }
        private Page page;
        private LoginSession session;
        private MainPanels main;
        private CanvasGroup original;
        [SerializeField] private CanvasGroup formGroup;
        private bool locked, oldInteractable, oldRaycasts, wasAccount;
        [SerializeField] private GameObject overlay, loginForm, guestForm, accountForm;
        [SerializeField] private TMP_InputField email, password;
        [SerializeField] private TMP_Text status, address;
        [SerializeField, InspectorName("Botón Cuenta (Badge)")] private Button badge;
        [SerializeField] private Button login, register, cancelLogin, logout, rememberButton;
        [SerializeField] private TMP_Text rememberLabel;
        [SerializeField] private Button guest, acceptGuest, backToLogin, backToGame;
        [SerializeField] private Toggle rememberToggle;
        [SerializeField] private TMP_Text coinLabel;
        private static readonly Color Panel = new Color32(28, 36, 59, 255);
        private static readonly Color Accent = new Color32(46, 101, 163, 255);
        private static readonly Color Secondary = new Color32(57, 69, 93, 255);

        private void Awake()
        {
            if (overlay) overlay.SetActive(false);
        }

        public static void Attach(MainPanels main)
        {
            var canvas = main.GetComponentInParent<Canvas>().rootCanvas;
            var menu = FindInScene(main.gameObject.scene);
            if (!menu) menu = CreateLayout(main.gameObject.scene);
            if (menu.session) return;
            menu.gameObject.SetActive(true);
            menu.main = main;
            if (!canvas.TryGetComponent<CanvasGroup>(out menu.original))
                menu.original = canvas.gameObject.AddComponent<CanvasGroup>();
            menu.Connect();
        }

        private static LoginMenu FindInScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var menu = root.GetComponentInChildren<LoginMenu>(true);
                if (menu) return menu;
            }
            return null;
        }

#if UNITY_EDITOR
        [MenuItem("Tools/Rubbing Balls/Cerrar sesión en Play")]
        private static void SignOutInEditor()
        {
            var active = Object.FindAnyObjectByType<LoginSession>();
            if (!active || !active.HasAccount || active.IsBusy)
            {
                Debug.Log(active && active.IsBusy ? "Cuenta: esperá a que termine la solicitud y volvé a elegir Cerrar sesión." : "Cuenta: no hay una sesión autenticada en esta instancia.");
                return;
            }
            active.SignOut();
        }

        [MenuItem("Tools/Rubbing Balls/Cerrar sesión en Play", true)]
        private static bool CanSignOutInEditor() => EditorApplication.isPlaying;
        
        [MenuItem("Tools/Rubbing Balls/Crear o seleccionar interfaz de cuenta")]
        private static void EditInScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || !System.Array.Exists(scene.GetRootGameObjects(), go => go.GetComponentInChildren<MainPanels>(true) != null))
            {
                EditorUtility.DisplayDialog("Interfaz de cuenta", "Abrí MainMenu y volvé a elegir esta opción.", "Aceptar");
                return;
            }
            var menu = FindInScene(scene);
            if (!menu)
            {
                menu = CreateLayout(scene);
                Undo.RegisterCreatedObjectUndo(menu.gameObject, "Crear interfaz de cuenta");
                EditorSceneManager.MarkSceneDirty(scene);
            }
            if (!menu.rememberToggle || !menu.coinLabel)
            {
                Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Actualizar interfaz de cuenta");
                menu.EnsureControls();
                EditorUtility.SetDirty(menu);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            Selection.activeGameObject = menu.loginForm ? menu.loginForm : menu.gameObject;
            EditorGUIUtility.PingObject(Selection.activeGameObject);
        }

        [MenuItem("Tools/Rubbing Balls/Crear o seleccionar interfaz de cuenta", true)]
        private static bool CanEdit() => !EditorApplication.isPlayingOrWillChangePlaymode && PrefabStageUtility.GetCurrentPrefabStage() == null;
#endif

        private static LoginMenu CreateLayout(Scene scene)
        {
            var root = new GameObject("Account UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            var layer = root.GetComponent<Canvas>();
            layer.renderMode = RenderMode.ScreenSpaceOverlay;
            layer.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 600);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var menu = root.AddComponent<LoginMenu>();
            menu.BuildLayout();
            return menu;
        }

        private void BuildLayout()
        {
            badge = MakeButton(transform, "Cuenta", 0, 0, 124);
            var corner = badge.GetComponent<RectTransform>();
            corner.anchorMin = corner.anchorMax = corner.pivot = new Vector2(0, 1);
            corner.anchoredPosition = new Vector2(16, -16);

            overlay = Box(transform, "Pantallas", new Color32(14, 18, 34, 225), 0, 0, 0, 0).gameObject;
            Stretch((RectTransform)overlay.transform);
            var card = Box(overlay.transform, "Panel", Panel, 0, 0, 452, 440);
            loginForm = Group(card, "Acceso");
            guestForm = Group(card, "Confirmar invitado");
            accountForm = Group(card, "Mi cuenta");
            formGroup = loginForm.AddComponent<CanvasGroup>();
            var form = loginForm.transform;
            Label(form, "Cuenta de Rubbing Balls", 0, 185, 405, 43, 29);
            Label(form, "Correo", 0, 142, 376, 26, 17, true);
            email = Field(form, "Campo de correo", 111, false);
            Label(form, "Contraseña", 0, 73, 376, 26, 17, true);
            password = Field(form, "Campo de contraseña", 43, true);
            var rememberRow = Rect(form, "Recordarme en esta PC", 0, 1, 378, 30);
            rememberLabel = Label(rememberRow, "Recordarme en esta PC", 0, 0, 370, 30, 18);
            EnsureControls();
            login = MakeButton(form, "Iniciar sesión", -97, -44, 182, true);
            register = MakeButton(form, "Crear cuenta", 97, -44, 182);
            status = Label(form, "", 0, -94, 378, 63, 16);
            guest = MakeButton(form, "Jugar como invitado", 0, -151, 378);
            cancelLogin = MakeButton(form, "Volver", 0, -192, 128, false, 30);

            Label(guestForm.transform, "Jugar como invitado", 0, 99, 396, 44, 29);
            Label(guestForm.transform, "Podés jugar como invitado, pero no vas a participar en el leaderboard.", 0, 15, 354, 92, 22);
            acceptGuest = MakeButton(guestForm.transform, "Continuar como invitado", 0, -74, 352, true);
            backToLogin = MakeButton(guestForm.transform, "Volver al login", 0, -129, 352);

            Label(accountForm.transform, "Mi cuenta", 0, 104, 398, 48, 29);
            address = Label(accountForm.transform, "", 0, 39, 382, 72, 20);
            logout = MakeButton(accountForm.transform, "Cerrar sesión", 0, -52, 352);
            backToGame = MakeButton(accountForm.transform, "Volver al juego", 0, -111, 352, true);

            badge.gameObject.SetActive(false);
            cancelLogin.gameObject.SetActive(false);
            guestForm.SetActive(false);
            accountForm.SetActive(false);
        }

        private void EnsureControls()
        {
            if (!rememberToggle)
            {
                var row = rememberLabel.transform.parent;
                if (rememberButton)
                {
                    rememberButton.enabled = false;
                    if (rememberButton.targetGraphic) rememberButton.targetGraphic.enabled = false;
                }
                var hit = Box(row, "Casilla", Color.clear, 0, 0, 0, 0);
                Stretch(hit);
                var frame = Box(hit, "Borde", Color.white, -116, 0, 19, 19);
                var center = Box(frame, "Fondo", Panel, 0, 0, 15, 15);
                var mark = Box(center, "Marca", Color.white, 0, 0, 9, 9).GetComponent<Image>();
                rememberToggle = hit.gameObject.AddComponent<Toggle>();
                rememberToggle.targetGraphic = frame.GetComponent<Image>();
                rememberToggle.graphic = mark;
                rememberToggle.SetIsOnWithoutNotify(false);
                mark.canvasRenderer.SetAlpha(0f);
            }
            if (!coinLabel && badge)
            {
                coinLabel = Label(badge.transform, "", 0, 0, 230, 30, 18, true);
                coinLabel.name = "Saldo de monedas";
                var rect = coinLabel.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(0, -3);
            }
        }

        private void Connect()
        {
            session = LoginSession.Current;
            wasAccount = session.HasAccount;
            EnsureControls();
            rememberLabel.transform.parent.gameObject.SetActive(session.CanRemember);
            rememberToggle.SetIsOnWithoutNotify(false);
            session.Coins.Changed += RefreshBalance;
            if (badge)
            {
                if (!badge.targetGraphic) badge.targetGraphic = badge.GetComponent<Graphic>();
                if (badge.targetGraphic) badge.targetGraphic.raycastTarget = true;
                badge.interactable = true;
                badge.onClick.AddListener(OpenAccount);
            }
            else Debug.LogWarning("LoginMenu: asigná el botón visible de Cuenta en Botón Cuenta (Badge).", this);
            login.onClick.AddListener(() => Submit(false));
            register.onClick.AddListener(() => Submit(true));
            guest.onClick.AddListener(() => Show(Page.Guest));
            acceptGuest.onClick.AddListener(AcceptGuest);
            backToLogin.onClick.AddListener(() => Show(Page.Login));
            cancelLogin.onClick.AddListener(() => Show(Page.Closed));
            logout.onClick.AddListener(session.SignOut);
            backToGame.onClick.AddListener(() => Show(Page.Closed));
            session.Changed += OnSessionChanged;
            Show(session.IsRestoring ? Page.Restoring : session.HasChosen ? Page.Closed : Page.Login);
        }

        public void OpenAccount()
        {
            if (!session)
            {
                Debug.LogWarning("Cuenta recibió el clic, pero este LoginMenu no está conectado. Revisá que el botón apunte al Account UI que usa MainPanels.", this);
                return;
            }
            var destination = session.HasAccount ? Page.Account : Page.Login;
            var form = session.HasAccount ? accountForm : loginForm;
            if (!form || !overlay)
            {
                Debug.LogError("Cuenta: falta la referencia al panel de destino o a Pantallas en LoginMenu.", this);
                return;
            }
            Show(destination);
#if UNITY_EDITOR
            Debug.Log("Cuenta recibió el clic y abrió " + form.name + ". Activo en la jerarquía: " + form.activeInHierarchy, form);
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("Diagnosticar clic en Cuenta (Play)")]
        private void DiagnoseAccountClick()
        {
            if (!Application.isPlaying || !session || !badge || !badge.isActiveAndEnabled || !EventSystem.current)
            {
                Debug.LogWarning("Abrí el menú en Play con Cuenta visible. Revisá el campo Botón Cuenta (Badge), su componente Button y el EventSystem.", this);
                return;
            }
            if (!badge.IsInteractable())
            {
                Debug.LogWarning("Cuenta no admite clics. Revisá Interactable en el Button y en sus CanvasGroup padres.", badge);
                return;
            }
            var rect = (RectTransform)badge.transform;
            var canvas = badge.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0)
                Debug.LogWarning("El centro de Cuenta no recibe raycasts. Revisá GraphicRaycaster, Raycast Target y Blocks Raycasts.", badge);
            else if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != badge.gameObject)
                Debug.LogWarning("Este objeto recibe el clic encima de Cuenta: " + hits[0].gameObject.name, hits[0].gameObject);
            else Debug.Log("El clic en el centro llega al botón Cuenta asignado.", badge);
        }
#endif

        private void RefreshBalance()
        {
            if (coinLabel) coinLabel.text = session.HasAccount ? session.Coins.BalanceText : "";
        }

        private void Submit(bool create)
        {
            session.Authenticate(email.text, password.text, create, rememberToggle.isOn);
            if (session.IsBusy) password.SetTextWithoutNotify("");
        }

        private void AcceptGuest()
        {
            session.ContinueAsGuest();
            if (session.IsGuest) Show(Page.Closed);
        }

        private void OnSessionChanged()
        {
            if (session.HasAccount != wasAccount)
            {
                wasAccount = session.HasAccount;
                rememberToggle.SetIsOnWithoutNotify(false);
                password.SetTextWithoutNotify("");
                Show(wasAccount ? Page.Closed : Page.Login);
            }
            else if (page == Page.Restoring && !session.IsRestoring) Show(Page.Login);
            else Refresh();
        }

        private void Show(Page next)
        {
            page = next;
            bool modal = page != Page.Closed;
            if (modal && !locked)
            {
                if (overlay.transform != original.transform && overlay.transform.IsChildOf(original.transform))
                {
                    if (!overlay.TryGetComponent<CanvasGroup>(out var modalGroup))
                        modalGroup = overlay.AddComponent<CanvasGroup>();
                    modalGroup.ignoreParentGroups = true;
                }
                oldInteractable = original.interactable;
                oldRaycasts = original.blocksRaycasts;
                original.interactable = original.blocksRaycasts = false;
                locked = true;
            }
            else if (!modal) Unlock();
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            password.SetTextWithoutNotify("");
            overlay.SetActive(modal && page != Page.Restoring);
            loginForm.SetActive(page == Page.Login);
            guestForm.SetActive(page == Page.Guest);
            accountForm.SetActive(page == Page.Account);
            var visible = page == Page.Account ? accountForm : page == Page.Login ? loginForm : page == Page.Guest ? guestForm : null;
            if (visible && visible.transform.IsChildOf(transform))
            {
                for (var node = visible.transform; node && node != transform; node = node.parent)
                    if (!node.gameObject.activeSelf) node.gameObject.SetActive(true);
            }
            Refresh();
            if (page == Page.Login && session.Configured && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(email.gameObject);
        }

        private void Refresh()
        {
            formGroup.interactable = !session.IsBusy;
            email.interactable = password.interactable = login.interactable = register.interactable = session.Configured && !session.IsBusy;
            cancelLogin.gameObject.SetActive(session.IsGuest);
            status.text = session.Configured ? session.Message : "El acceso con cuenta todavía no está configurado.";
            rememberToggle.interactable = session.Configured && !session.IsBusy;
            RefreshBalance();
            address.text = string.IsNullOrEmpty(session.StorageNotice) ? session.Email ?? "" : session.Email + "\n" + session.StorageNotice;
            logout.interactable = !session.IsBusy;
            if (badge && !BadgeContainsCurrentPage())
                badge.gameObject.SetActive(page == Page.Closed && main && main.isActiveAndEnabled);
        }

        private bool BadgeContainsCurrentPage()
        {
            var current = page == Page.Account ? accountForm : page == Page.Login ? loginForm : page == Page.Guest ? guestForm : null;
            return current && current.transform.IsChildOf(badge.transform);
        }

        private void LateUpdate()
        {
            if (page == Page.Closed && main && badge && badge.gameObject.activeSelf != main.isActiveAndEnabled)
                badge.gameObject.SetActive(main.isActiveAndEnabled);
            if (session) session.Coins.ShowBalance(page == Page.Closed && main && main.isActiveAndEnabled && session.HasAccount);
        }

        private void Unlock()
        {
            if (!locked || !original) return;
            original.interactable = oldInteractable;
            original.blocksRaycasts = oldRaycasts;
            locked = false;
        }

        private void OnDestroy()
        {
            if (session)
            {
                session.Changed -= OnSessionChanged;
                if (session.Coins)
                {
                    session.Coins.Changed -= RefreshBalance;
                    session.Coins.ShowBalance(false);
                }
            }
            Unlock();
        }

        private static GameObject Group(Transform parent, string name)
        {
            var rect = Rect(parent, name, 0, 0, 0, 0);
            Stretch(rect);
            return rect.gameObject;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static RectTransform Box(Transform parent, string name, Color color, float x, float y, float w, float h)
        {
            var rect = Rect(parent, name, x, y, w, h);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private static TMP_Text Label(Transform parent, string text, float x, float y, float w, float h, float size, bool left = false)
        {
            var label = Rect(parent, string.IsNullOrEmpty(text) ? "Texto dinámico" : text, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.richText = false;
            label.text = text;
            label.alignment = left ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static Button MakeButton(Transform parent, string text, float x, float y, float width, bool primary = false, float height = 42)
        {
            var box = Box(parent, text, primary ? Accent : Secondary, x, y, width, height);
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            Label(box, text, 0, 0, width - 8, height, 18);
            return button;
        }

        private static TMP_InputField Field(Transform parent, string name, float y, bool secret)
        {
            var box = Box(parent, name, new Color32(15, 22, 39, 255), 0, y, 378, 39);
            box.gameObject.SetActive(false);
            var viewport = Rect(box, "Área de texto", 0, 0, 354, 35);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label(viewport, "", 0, 0, 354, 35, 19, true);
            var input = box.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = box.GetComponent<Image>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = secret ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.keyboardType = secret ? TouchScreenKeyboardType.Default : TouchScreenKeyboardType.EmailAddress;
            input.richText = false;
            box.gameObject.SetActive(true);
            return input;
        }
    }
}
