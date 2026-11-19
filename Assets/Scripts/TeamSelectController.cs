using Fusion;
using UnityEngine;

public enum TeamPhase : byte
{
    Idle = 0,
    Selecting = 1,
    Countdown = 2
}

public class TeamSelectController : NetworkBehaviour
{
    public static TeamSelectController Instance { get; private set; }

    private const int MinPlayersForTeams = 3;

    [SerializeField] private float countdownSeconds = 3f;

    [Networked] public byte Phase { get; private set; }
    [Networked] public TickTimer PhaseTimer { get; private set; }

    public TeamPhase CurrentPhase => (TeamPhase)Phase;
    public bool IsActive => CurrentPhase != TeamPhase.Idle;
    public bool IsHost => Object != null && Object.IsValid && Object.HasStateAuthority;

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

    public override void Spawned()
    {
        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public void BeginSelecting()
    {
        if (!Object.HasStateAuthority || IsActive)
            return;

        SetPhase(TeamPhase.Selecting, 0f);
    }

    public void OnTeamChanged()
    {
        if (!Object.HasStateAuthority)
            return;

        if (CurrentPhase == TeamPhase.Countdown)
            SetPhase(TeamPhase.Selecting, 0f);
    }

    public void Cancel()
    {
        if (!Object.HasStateAuthority || !IsActive)
            return;

        ResetTeamsAndReady();
        SetPhase(TeamPhase.Idle, 0f);
        NetworkManager.Instance?.CancelTeamSelect();
    }


    public void RestoreMigration(byte phase)
    {
        if (!Object.HasStateAuthority)
            return;

        Phase = phase == (byte)TeamPhase.Idle ? (byte)TeamPhase.Idle : (byte)TeamPhase.Selecting;
        PhaseTimer = TickTimer.None;
    }

    public override void FixedUpdateNetwork()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMigrating) return;
        if (!Object.HasStateAuthority)
            return;

        if (CurrentPhase == TeamPhase.Idle)
            return;

        if (CountPlayers() < NetworkManager.MinPlayersToStart)
        {
            Cancel();
            return;
        }

        bool valid = Validate(out _);

        switch (CurrentPhase)
        {
            case TeamPhase.Selecting:
                if (valid)
                    SetPhase(TeamPhase.Countdown, countdownSeconds);
                break;

            case TeamPhase.Countdown:
                if (!valid)
                    SetPhase(TeamPhase.Selecting, 0f);
                else if (PhaseTimer.Expired(Runner))
                    Finish();
                break;
        }
    }

    private void SetPhase(TeamPhase newPhase, float seconds)
    {
        Phase = (byte)newPhase;
        PhaseTimer = seconds > 0f ? TickTimer.CreateFromSeconds(Runner, seconds) : TickTimer.None;
    }

    private void Finish()
    {
        SetPhase(TeamPhase.Idle, 0f);
        NetworkManager.Instance?.BeginMapVote();
    }

    public bool Validate(out string message)
    {
        int players = CountPlayers();
        int red = CountInTeam(LobbyPlayer.TeamRed);
        int blue = CountInTeam(LobbyPlayer.TeamBlue);
        int unassigned = players - red - blue;

        if (players < MinPlayersForTeams)
        {
            message = "Teams need at least " + MinPlayersForTeams + " players";
            return false;
        }

        if (unassigned > 0)
        {
            message = "Waiting for everyone to pick a team...";
            return false;
        }

        if (red == 0 || blue == 0)
        {
            message = "Both teams need at least one player";
            return false;
        }

        message = "";
        return true;
    }

    public int CountInTeam(byte team)
    {
        int count = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) || playerObject == null)
                continue;

            LobbyPlayer lobbyPlayer = playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer != null && lobbyPlayer.Team == team)
                count++;
        }

        return count;
    }

    public string NamesOfTeam(byte team)
    {
        string names = "";

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) || playerObject == null)
                continue;

            LobbyPlayer lobbyPlayer = playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer == null || lobbyPlayer.Team != team)
                continue;

            string nick = lobbyPlayer.Nickname.ToString();

            if (string.IsNullOrWhiteSpace(nick))
                nick = "Player " + player.PlayerId;

            names += nick + "\n";
        }

        return names;
    }

    private int CountPlayers()
    {
        int count = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
            count++;

        return count;
    }

    private void ResetTeamsAndReady()
    {
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) || playerObject == null)
                continue;

            LobbyPlayer lobbyPlayer = playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer == null)
                continue;

            lobbyPlayer.ResetTeam();
            lobbyPlayer.ResetReady();
        }
    }
}