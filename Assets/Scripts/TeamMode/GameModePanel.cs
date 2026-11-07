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

        panelRoot.SetActive(!busy);
        GameModeController controller = GameModeController.Instance;

        if (controller == null)
            return;

        int current = (int)controller.Mode;
        int other = (current + 1) % modeNames.Length;

        selectedLabel.text = modeNames[current];
        otherLabel.text = modeNames[other];

        bool isHost = controller.IsHost;

        previousButton.gameObject.SetActive(isHost);
        nextButton.gameObject.SetActive(isHost);
        previousButton.interactable = controller.CanChangeMode;
        nextButton.interactable = controller.CanChangeMode;
    }
}