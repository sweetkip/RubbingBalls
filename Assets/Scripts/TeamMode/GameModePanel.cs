using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameModePanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text selectedLabel;
    [SerializeField] private TMP_Text otherLabel;

    [Header("Optional")]
    [Tooltip("The Ready button (with its shadow) when it lives OUTSIDE ModePanel. It is hidden while the team select or the map vote is running, exactly like ModePanel used to hide it.")]
    [SerializeField] private GameObject readyButtonRoot;

    // El orden tiene que coincidir con el enum MatchMode: 0 = FreeForAll, 1 = Teams
    private static readonly string[] modeNames = { "Free 4 All", "Teams" };

    private void Start()
    {
        previousButton.onClick.AddListener(() => Step(-1));
        nextButton.onClick.AddListener(() => Step(1));
    }

    private void Step(int direction)
    {
        GameModeController controller = GameModeController.Instance;

        if (controller == null)
            return;

        int count = modeNames.Length;
        int next = ((int)controller.Mode + direction + count) % count;

        controller.SetMode((MatchMode)next);
    }

    private void Update()
    {
        MapVoteController vote = MapVoteController.Instance;
        TeamSelectController teams = TeamSelectController.Instance;
        bool busy = (vote != null && vote.IsActive) || (teams != null && teams.IsActive);

        if (readyButtonRoot != null)
            readyButtonRoot.SetActive(!busy);

        GameModeController controller = GameModeController.Instance;

        // Labels are updated for everybody, because the label on the middle button
        // may still be visible to clients.
        if (controller != null)
        {
            int current = (int)controller.Mode;
            int other = (current + 1) % modeNames.Length;

            selectedLabel.text = modeNames[current];
            otherLabel.text = modeNames[other];
        }

        // Only the host sees the panel. Until the controller exists nobody knows who the host is,
        // so the panel stays hidden (no flash on the clients).
        bool showPanel = controller != null && controller.IsHost && !busy;
        panelRoot.SetActive(showPanel);

        if (!showPanel)
            return;

        previousButton.interactable = controller.CanChangeMode;
        nextButton.interactable = controller.CanChangeMode;
    }
}
