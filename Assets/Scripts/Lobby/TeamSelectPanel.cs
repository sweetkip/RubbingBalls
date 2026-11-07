using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TeamSelectPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button redButton;
    [SerializeField] private Button blueButton;
    [SerializeField] private TMP_Text redNamesText;
    [SerializeField] private TMP_Text blueNamesText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button cancelButton;

    private TeamPhase lastPhase = TeamPhase.Idle;

    private void Start()
    {
        redButton.onClick.AddListener(() => Join(LobbyPlayer.TeamRed));
        blueButton.onClick.AddListener(() => Join(LobbyPlayer.TeamBlue));
        cancelButton.onClick.AddListener(Cancel);

        panelRoot.SetActive(false);
    }

    private void Update()
    {
        TeamSelectController controller = TeamSelectController.Instance;
        TeamPhase phase = controller != null ? controller.CurrentPhase : TeamPhase.Idle;

        if (phase != lastPhase)
            OnPhaseChanged(phase);

        if (phase == TeamPhase.Idle)
            return;

        controller.Validate(out string message);

        if (phase == TeamPhase.Countdown)
            message = "Teams ready! Map vote in " + Mathf.CeilToInt(controller.RemainingSeconds) + "...";

        messageText.text = message;
        redNamesText.text = controller.NamesOfTeam(LobbyPlayer.TeamRed);
        blueNamesText.text = controller.NamesOfTeam(LobbyPlayer.TeamBlue);

        LobbyPlayer local = GetLocalPlayer();
        byte myTeam = local != null ? local.Team : LobbyPlayer.NoTeam;

        redButton.interactable = local != null && myTeam != LobbyPlayer.TeamRed;
        blueButton.interactable = local != null && myTeam != LobbyPlayer.TeamBlue;

        cancelButton.gameObject.SetActive(controller.IsHost);
    }

    private void OnPhaseChanged(TeamPhase newPhase)
    {
        lastPhase = newPhase;
        panelRoot.SetActive(newPhase != TeamPhase.Idle);
        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    private void Join(byte team)
    {
        TeamSelectController controller = TeamSelectController.Instance;

        if (controller == null || !controller.IsActive)
            return;

        LobbyPlayer local = GetLocalPlayer();

        if (local != null)
            local.SelectTeam(team);
    }

    private void Cancel()
    {
        TeamSelectController controller = TeamSelectController.Instance;

        if (controller != null)
            controller.Cancel();
    }

    private LobbyPlayer GetLocalPlayer()
    {
        return PreGameLobbyUI.Instance != null ? PreGameLobbyUI.Instance.GetLocalLobbyPlayer() : null;
    }
}