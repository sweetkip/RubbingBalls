using Fusion;
using UnityEngine;

public enum MatchMode : byte
{
    FreeForAll = 0,
    Teams = 1
}

public class GameModeController : NetworkBehaviour
{
    public static GameModeController Instance { get; private set; }

    [Networked] public byte ModeValue { get; private set; }

    public MatchMode Mode => (MatchMode)ModeValue;
    public bool IsTeams => Mode == MatchMode.Teams;

    public bool IsHost => Object != null && Object.IsValid && Object.HasStateAuthority;

    public bool CanChangeMode
    {
        get
        {
            if (!IsHost)
                return false;

            MapVoteController vote = MapVoteController.Instance;

            if (vote != null && vote.IsActive)
                return false;

            TeamSelectController teams = TeamSelectController.Instance;

            if (teams != null && teams.IsActive)
                return false;

            return true;
        }
    }

    public override void Spawned()
    {
        Instance = this;
    }

    public void RestoreMigration(byte mode)
    {
        if (!Object.HasStateAuthority)
            return;

        ModeValue = mode;
    }


    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetMode(MatchMode newMode)
    {
        if (!CanChangeMode)
            return;

        if ((byte)newMode == ModeValue)
            return;

        ModeValue = (byte)newMode;
        ResetEveryoneReady();
    }

    private void ResetEveryoneReady()
    {
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) || playerObject == null)
                continue;

            LobbyPlayer lobbyPlayer = playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer != null)
                lobbyPlayer.ResetReady();
        }
    }
}