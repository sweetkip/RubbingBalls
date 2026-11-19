using TMPro;
using UnityEngine;

public class GameModePanel : MonoBehaviour
{
    [System.Serializable]
    private class ModeOption
    {
        public MatchMode mode;
        public RectTransform row;
        public TMP_Text label;
    }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform selector;
    [SerializeField] private ModeOption[] options;
  
    [Header("Colores del texto")]
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color unselectedColor = new Color(0f, 0.38f, 0.58f);

    [Header("Comportamiento")]
    [SerializeField] private bool selectOnHover = true;
    [SerializeField] private float selectorSpeed = 15f;

    [Header("Optional")]
    [SerializeField] private GameObject readyButtonRoot;

    private Vector3 lastMousePosition;
    private bool mouseMoved;

    private void Update()
    {
        MapVoteController vote = MapVoteController.Instance;
        TeamSelectController teams = TeamSelectController.Instance;
        bool busy = (vote != null && vote.IsActive) || (teams != null && teams.IsActive);

        mouseMoved = Input.mousePosition != lastMousePosition;
        lastMousePosition = Input.mousePosition;

        GameModeController controller = GameModeController.Instance;

        bool showPanel = controller != null && controller.IsHost && !busy;
        panelRoot.SetActive(showPanel);

        if (readyButtonRoot != null)
            readyButtonRoot.SetActive(!busy);

        if (!showPanel || options == null || options.Length == 0)
            return;

        if (controller.CanChangeMode)
            HandleInput(controller);

        RefreshVisuals(controller);
    }

    private void HandleInput(GameModeController controller)
    {
        int current = IndexOf(controller.Mode);
        int target = current;

        if (!TextInputFocus.IsTyping())
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                target--;

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                target++;
        }

        int count = options.Length;
        target = (target + count) % count;

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