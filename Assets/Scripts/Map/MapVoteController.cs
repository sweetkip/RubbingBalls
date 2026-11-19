using Fusion;
using UnityEngine;

public enum VotePhase : byte
{
    Idle = 0,
    Voting = 1,
    Roulette = 2,
    Countdown = 3,
    Loading = 4
}

public class MapVoteController : NetworkBehaviour
{
    public static MapVoteController Instance { get; private set; }

    private const int MaxMaps = 8;

    [SerializeField] private float voteSeconds = 10f;
    [SerializeField] private float rouletteSeconds = 2.5f;
    [SerializeField] private float countdownSeconds = 3f;

    [Networked] public byte Phase { get; private set; }
    [Networked] public TickTimer PhaseTimer { get; private set; }
    [Networked] public byte CandidatesMask { get; private set; }
    [Networked] public byte WinnerMap { get; private set; }

    public VotePhase CurrentPhase => (VotePhase)Phase;
    public bool IsActive => CurrentPhase != VotePhase.Idle;
    public bool IsVoting => CurrentPhase == VotePhase.Voting;

    public float RemainingSeconds
    {
        get
        {
            if (!IsActive || Runner == null)
                return 0f;

            float? remaining = PhaseTimer.RemainingTime(Runner);
            return remaining ?? 0f;
        }
    }

    public bool IsCandidate(int mapIndex)
    {
        return (CandidatesMask & (1 << mapIndex)) != 0;
    }

    public override void Spawned()
    {
        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public void BeginVoting()
    {
        if (!Object.HasStateAuthority || IsActive)
            return;

        ResetVotes();
        CandidatesMask = 0;
        SetPhase(VotePhase.Voting, voteSeconds);
    }

    public override void FixedUpdateNetwork()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMigrating) return;
        if (!Object.HasStateAuthority)
            return;

        VotePhase phase = CurrentPhase;

        if (phase == VotePhase.Idle || phase == VotePhase.Loading)
            return;

        if (CountPlayers() < NetworkManager.MinPlayersToStart)
        {
            CancelVoting();
            return;
        }

        switch (phase)
        {
            case VotePhase.Voting:
                if (AllPlayersVoted() || PhaseTimer.Expired(Runner))
                    ResolveVote();
                break;

            case VotePhase.Roulette:
                if (PhaseTimer.Expired(Runner))
                    SetPhase(VotePhase.Countdown, countdownSeconds);
                break;

            case VotePhase.Countdown:
                if (PhaseTimer.Expired(Runner))
                {
                    Phase = (byte)VotePhase.Loading;
                    NetworkManager.Instance.StartMatch(WinnerMap);
                }
                break;
        }
    }

    public MigrationVoteState CaptureMigration()
    {
        return new MigrationVoteState { Phase = Phase, CandidatesMask = CandidatesMask, WinnerMap = WinnerMap, Remaining = PhaseTimer.RemainingTime(Runner) ?? 0f };
    }

    public void RestoreMigration(MigrationVoteState state)
    {
        if (!Object.HasStateAuthority || state == null) return;
        Phase = state.Phase;
        CandidatesMask = state.CandidatesMask;
        WinnerMap = state.WinnerMap;
        PhaseTimer = state.Phase == (byte)VotePhase.Idle ? TickTimer.None : TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.01f, state.Remaining));
    }

    private void SetPhase(VotePhase newPhase, float seconds)
    {
        Phase = (byte)newPhase;
        PhaseTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    private void ResolveVote()
    {
        int mapCount = Mathf.Min(NetworkManager.Instance.MapCount, MaxMaps);
        int[] votes = CountVotes(Runner, mapCount);

        int maxVotes = 0;

        for (int i = 0; i < mapCount; i++)
        {
            if (votes[i] > maxVotes)
                maxVotes = votes[i];
        }

        int mask = 0;
        int candidateCount = 0;

        for (int i = 0; i < mapCount; i++)
        {
            if (maxVotes == 0 || votes[i] == maxVotes)
            {
                mask |= 1 << i;
                candidateCount++;
            }
        }

        int pick = UnityEngine.Random.Range(0, candidateCount);
        int winner = 0;

        for (int i = 0; i < mapCount; i++)
        {
            if ((mask & (1 << i)) == 0)
                continue;

            if (pick == 0)
            {
                winner = i;
                break;
            }

            pick--;
        }

        CandidatesMask = (byte)mask;
        WinnerMap = (byte)winner;

        Debug.Log("[Votación] Votos: " + string.Join(", ", votes) + " | Candidatos: " + candidateCount + " | Ganador: mapa " + winner);

        if (candidateCount > 1)
            SetPhase(VotePhase.Roulette, rouletteSeconds);
        else
            SetPhase(VotePhase.Countdown, countdownSeconds);
    }

    private void CancelVoting()
    {
        ResetVotes();
        Phase = (byte)VotePhase.Idle;
        NetworkManager.Instance?.CancelMapVote();
    }

    private void ResetVotes()
    {
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) || playerObject == null)
                continue;

            LobbyPlayer lobbyPlayer = playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer != null)
                lobbyPlayer.ResetMapVote();
        }
    }

    private bool AllPlayersVoted()
    {
        int mapCount = Mathf.Min(NetworkManager.Instance.MapCount, MaxMaps);

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) || playerObject == null)
                return false;

            LobbyPlayer lobbyPlayer = playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer == null || lobbyPlayer.MapVote >= mapCount)
                return false;
        }

        return true;
    }

    private int CountPlayers()
    {
        int count = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
            count++;

        return count;
    }

    public static int[] CountVotes(NetworkRunner runner, int mapCount)
    {
        int[] votes = new int[mapCount];

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject) || playerObject == null)
                continue;

            LobbyPlayer lobbyPlayer = playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer == null)
                continue;

            byte vote = lobbyPlayer.MapVote;

            if (vote < mapCount)
                votes[vote]++;
        }

        return votes;
    }
}