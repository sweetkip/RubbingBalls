using TMPro;
using UnityEngine;

public class GameModePanel : MonoBehaviour
{
    // Una fila de la lista: qué modo representa, dónde está y su texto.
    // [System.Serializable] hace que aparezca en el Inspector, igual que los campos de siempre.
    [System.Serializable]
    private class ModeOption
    {
        public MatchMode mode;
        public RectTransform row;
        public TMP_Text label;
    }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform selector;   // la caja amarilla con las flechas
    [SerializeField] private ModeOption[] options;     // en el orden de arriba hacia abajo
  
    [Header("Colores del texto")]
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color unselectedColor = new Color(0f, 0.38f, 0.58f); // #006194

    [Header("Comportamiento")]
    [SerializeField] private bool selectOnHover = true;
    [SerializeField] private float selectorSpeed = 15f;

    [Header("Optional")]
    [Tooltip("El botón Ready cuando vive FUERA de ModePanel. Se oculta durante la votación o selección de equipos.")]
    [SerializeField] private GameObject readyButtonRoot;

    private Vector3 lastMousePosition;
    private bool mouseMoved;

    private void Update()
    {
        MapVoteController vote = MapVoteController.Instance;
        TeamSelectController teams = TeamSelectController.Instance;
        bool busy = (vote != null && vote.IsActive) || (teams != null && teams.IsActive);

        // Se calcula siempre, para que al volver a poder jugar no cuente un "salto" viejo del mouse
        mouseMoved = Input.mousePosition != lastMousePosition;
        lastMousePosition = Input.mousePosition;

        GameModeController controller = GameModeController.Instance;

        // Solo el host ve el panel de modo. Hasta que el controller exista no sabemos quién es el host,
        // así que queda oculto y no parpadea en el cliente.
        bool showPanel = controller != null && controller.IsHost && !busy;
        panelRoot.SetActive(showPanel);

        // El botón Ready lo ven todos, se oculta solo durante votación o selección de equipos
        if (readyButtonRoot != null)
            readyButtonRoot.SetActive(!busy);

        if (!showPanel || options == null || options.Length == 0)
            return;

        // Desde acá solo llega el host
        if (controller.CanChangeMode)
            HandleInput(controller);

        RefreshVisuals(controller);
    }

    private void HandleInput(GameModeController controller)
    {
        int current = IndexOf(controller.Mode);
        int target = current;

        // TextInputFocus ya existe en el proyecto: dice si estás escribiendo en el chat.
        // Sin esto, escribir una "w" o una "s" en el chat cambiaría el modo.
        if (!TextInputFocus.IsTyping())
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                target--;

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                target++;
        }

        // Da la vuelta: si pasás del último, vuelve al primero
        int count = options.Length;
        target = (target + count) % count;

        // Mouse: si el cursor se movió (o hiciste clic) y está sobre una fila, esa fila se elige
        if ((selectOnHover && mouseMoved) || Input.GetMouseButtonDown(0))
        {
            for (int i = 0; i < count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(options[i].row, Input.mousePosition))
                    target = i;
            }
        }

        if (target != current)
            controller.SetMode(options[target].mode);
    }

    private void RefreshVisuals(GameModeController controller)
    {
        int current = IndexOf(controller.Mode);

        for (int i = 0; i < options.Length; i++)
            options[i].label.color = (i == current) ? selectedColor : unselectedColor;

        // La caja se desliza hacia la fila elegida (Lerp = acercarse un poco cada frame)
        Vector2 target = options[current].row.anchoredPosition;
        float t = Mathf.Min(1f, selectorSpeed * Time.deltaTime);
        selector.anchoredPosition = Vector2.Lerp(selector.anchoredPosition, target, t);
    }

    private int IndexOf(MatchMode mode)
    {
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i].mode == mode)
                return i;
        }

        return 0;
    }
}