using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapVotePanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text timerText;

    [Header("Mapas (mismo orden que Map Scene Indices del NetworkManager)")]
    [SerializeField] private Button[] mapButtons;
    [SerializeField] private TMP_Text[] voteTexts;
    [SerializeField] private Image[] outlines;

    [Header("Contornos")]
    [SerializeField] private Color myVoteColor = Color.yellow;
    [SerializeField] private Color rouletteColor = Color.white;
    [SerializeField] private Color winnerColor = Color.green;
    [SerializeField] private float rouletteStepSeconds = 0.15f;

    private VotePhase lastPhase = VotePhase.Idle;
    private float rouletteStartTime;

    private void Start()
    {
        for (int i = 0; i < mapButtons.Length; i++)
        {
            if (mapButtons[i] == null)
                continue;

            int mapIndex = i;
            mapButtons[i].onClick.AddListener(() => VoteMap(mapIndex));
        }

        panelRoot.SetActive(false);
    }

    private void Update()
    {
        MapVoteController controller = MapVoteController.Instance;
        VotePhase phase = controller != null ? controller.CurrentPhase : VotePhase.Idle;

        if (phase != lastPhase)
            OnPhaseChanged(phase);

        if (phase == VotePhase.Idle)
            return;

        UpdateTimer(controller, phase);
        UpdateVotesAndOutlines(controller, phase);
    }

    private void OnPhaseChanged(VotePhase newPhase)
    {
        lastPhase = newPhase;
        panelRoot.SetActive(newPhase != VotePhase.Idle);

        if (newPhase == VotePhase.Roulette)
            rouletteStartTime = Time.time;

        if (titleText != null)
            titleText.text = TitleFor(newPhase);

        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    private string TitleFor(VotePhase phase)
    {
        switch (phase)
        {
            case VotePhase.Voting: return "¡Vote for the map!";
            case VotePhase.Roulette: return "Tie! Drawing...";
            case VotePhase.Countdown: return "¡Chosen map!";
            case VotePhase.Loading: return "Loading...";
            default: return "";
        }
    }

    private void UpdateTimer(MapVoteController controller, VotePhase phase)
    {
        if (timerText == null)
            return;

        bool showNumber = phase == VotePhase.Voting || phase == VotePhase.Countdown;

        timerText.text = showNumber ? Mathf.CeilToInt(controller.RemainingSeconds).ToString() : "";
    }

    private void UpdateVotesAndOutlines(MapVoteController controller, VotePhase phase)
    {
        NetworkRunner runner = NetworkManager.Instance != null ? NetworkManager.Instance.Runner : null;

        if (runner == null)
            return;

        int[] votes = MapVoteController.CountVotes(runner, mapButtons.Length);

        LobbyPlayer localPlayer = PreGameLobbyUI.Instance != null ? PreGameLobbyUI.Instance.GetLocalLobbyPlayer() : null;
        int myVote = localPlayer != null ? localPlayer.MapVote : -1;

        int rouletteMap = phase == VotePhase.Roulette ? GetRouletteMap(controller) : -1;

        for (int i = 0; i < mapButtons.Length; i++)
        {
            if (i < voteTexts.Length && voteTexts[i] != null)
                voteTexts[i].text = votes[i].ToString();

            if (mapButtons[i] != null)
                mapButtons[i].interactable = phase == VotePhase.Voting;

            if (i >= outlines.Length || outlines[i] == null)
                continue;

            bool show;
            Color color;

            if (phase == VotePhase.Voting)
            {
                show = myVote == i;
                color = myVoteColor;
            }
            else if (phase == VotePhase.Roulette)
            {
                show = rouletteMap == i;
                color = rouletteColor;
            }
            else
            {
                show = controller.WinnerMap == i;
                color = winnerColor;
            }

            outlines[i].enabled = show;
            outlines[i].color = color;
        }
    }

    // Salta de a un mapa empatado por vez, según cuánto pasó desde que empezó la ruleta
    private int GetRouletteMap(MapVoteController controller)
    {
        int candidateCount = 0;

        for (int i = 0; i < mapButtons.Length; i++)
        {
            if (controller.IsCandidate(i))
                candidateCount++;
        }

        if (candidateCount == 0)
            return -1;

        int step = (int)((Time.time - rouletteStartTime) / rouletteStepSeconds);
        int target = step % candidateCount;

        for (int i = 0; i < mapButtons.Length; i++)
        {
            if (!controller.IsCandidate(i))
                continue;

            if (target == 0)
                return i;

            target--;
        }

        return -1;
    }

    private void VoteMap(int mapIndex)
    {
        MapVoteController controller = MapVoteController.Instance;

        if (controller == null || !controller.IsVoting)
            return;

        LobbyPlayer localPlayer = PreGameLobbyUI.Instance != null ? PreGameLobbyUI.Instance.GetLocalLobbyPlayer() : null;

        if (localPlayer == null)
            return;

        localPlayer.SelectMap((byte)mapIndex);
    }
}