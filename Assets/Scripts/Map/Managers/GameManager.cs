using Fusion;
using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum StartPhase : byte { None = 0, Ready = 1, Set = 2, Go = 3 }

public class GameManager : NetworkBehaviour
{
    [Networked] public int totalPlayers { get; set; }
    [Networked] public int alivePlayers { get; set; }
    public static GameManager Instance;
    [SerializeField] private int maxPlayers;
    [SerializeField] private NetworkPrefabRef explosionPrefab;

    [Header("Feedback")]
    [SerializeField] private AudioClip fallSound;
    [SerializeField, Range(0f, 1f)] private float fallSoundVolume = 0.65f;
    [SerializeField] private AudioMixerGroup feedbackMixer;

    private List<int> aliveIds = new List<int>();
    private List<PlayerRef> alivePlayerRefs = new List<PlayerRef>();

    [Networked, OnChangedRender(nameof(OnWinnerChanged))]
    public int WinnerId { get; set; } = -1;

    [Networked, OnChangedRender(nameof(OnWinnerChanged))]
    public PlayerRef WinnerPlayer { get; set; }
    [SerializeField] private float returnToLobbySeconds = 8f;
    [Networked] public TickTimer ReturnTimer { get; private set; }

    [Header("Start countdown")]
    [SerializeField, Min(0.1f)] private float readySeconds = 1f;
    [SerializeField, Min(0.1f)] private float setSeconds = 1f;
    [SerializeField, Min(0.1f)] private float goSeconds = 0.75f;
    [SerializeField] private bool allowPlayDuringGo = true;
    [Networked] public TickTimer StartTimer { get; private set; }

    public override void Spawned()
    {
        aliveIds.Clear();
        alivePlayerRefs.Clear();

        if (!HasStateAuthority)
            return;

        totalPlayers = 0;
        alivePlayers = 0;
        WinnerId = -1;
        WinnerPlayer = PlayerRef.None;
        ReturnTimer = default;
        StartTimer = default;
    }

    public int IJoined(PlayerRef playerRef)
    {
        int id = totalPlayers;
        if (totalPlayers < maxPlayers)
        {
            totalPlayers++;
            alivePlayers++;
            aliveIds.Add(id);
            alivePlayerRefs.Add(playerRef);
        }
        else
        {
            Debug.Log("No more players can Join");
        }
        UIManager.Instance.PlayerJoined(totalPlayers);
        return id;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void PlayerOut(int id, Ball player)
    {
        Vector3 deathPosition = player.transform.position;
        RPC_SpawnExplosion(deathPosition);
        int playerLives = player.GetLives();
        if (playerLives - 1 <= 0)
        {
            RPC_PlayerLost(id, player.ballName);

            int index = aliveIds.IndexOf(id);

            Runner.Despawn(player.gameObject.GetComponent<NetworkObject>());
            alivePlayers--;
            aliveIds.Remove(id);
            if (index >= 0) alivePlayerRefs.RemoveAt(index);
            CheckWinner();
        }
        else
        {
            player.Respawn();
        }
    }

    public void PlayerDisconnected(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;

        int index = alivePlayerRefs.IndexOf(playerRef);
        if (index < 0) return;   

        int id = aliveIds[index];
        aliveIds.RemoveAt(index);
        alivePlayerRefs.RemoveAt(index);
        alivePlayers--;
        string name = NetworkManager.Instance.SendName(playerRef);
        if (string.IsNullOrWhiteSpace(name))
            name = $"Player {playerRef.PlayerId}";
        RPC_PlayerDisconnected(id, name);   
        CheckWinner();                
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayerDisconnected(int id, string name)
    {
        UIManager.Instance.PlayerDisconnected(id, name);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayerLost(int id, string name)
    {
        UIManager.Instance.PlayerLost(id, name);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnExplosion(Vector3 position)
    {
        BallFeedbackAudio.PlayDetached(fallSound, BallFeedbackSound.Fall, position, fallSoundVolume, feedbackMixer);
        if (!Object.HasStateAuthority)
            return;

        Runner.Spawn(explosionPrefab, position, Quaternion.identity);
    }


    private void CheckWinner()
    {
        if (!HasStateAuthority) return;
        if (alivePlayers > 1) return;
        if (ReturnTimer.IsRunning) return;

        if (aliveIds.Count == 1)
        {
            WinnerId = aliveIds[0];
            WinnerPlayer = alivePlayerRefs[0];
        }
        else
        {
            WinnerId = -1;
            WinnerPlayer = PlayerRef.None;
        }

        ReturnTimer = TickTimer.CreateFromSeconds(Runner, returnToLobbySeconds);
    }

    public void ShowResults()
    {
        UIManager.Instance.GameIsOver(true);
    }

    private void OnWinnerChanged()
    {
        if (WinnerPlayer == PlayerRef.None)
            return;

        bool won = WinnerPlayer == Runner.LocalPlayer;
        UIManager.Instance.GameIsOver(won);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!ReturnTimer.Expired(Runner))
            return;

        if (NetworkManager.Instance == null)
            return;

        NetworkManager.Instance.ReturnToLobby();
    }

    public bool IsGameOver
    {
        get
        {
            if (Object == null || !Object.IsValid)
                return false;

            return ReturnTimer.IsRunning;
        }
    }

    public float ReturnRemainingSeconds
    {
        get
        {
            if (!IsGameOver)
                return 0f;

            float? remaining = ReturnTimer.RemainingTime(Runner);

            if (remaining == null)
                return 0f;

            return remaining.Value;
        }
    }

    public void BeginStartCountdown()
    {
        if (!HasStateAuthority)
            return;

        StartTimer = TickTimer.CreateFromSeconds(Runner, readySeconds + setSeconds + goSeconds);
    }

    public StartPhase CurrentStartPhase
    {
        get
        {
            if (Object == null || !Object.IsValid)
                return StartPhase.None;

            float? remaining = StartTimer.RemainingTime(Runner);

            if (remaining == null)
                return StartPhase.None;

            float total = readySeconds + setSeconds + goSeconds;
            float elapsed = Mathf.Clamp(total - remaining.Value, 0f, total);

            if (elapsed < readySeconds)
                return StartPhase.Ready;

            if (elapsed < readySeconds + setSeconds)
                return StartPhase.Set;

            if (elapsed < total)
                return StartPhase.Go;

            return StartPhase.None;
        }
    }

    public bool CanPlay
    {
        get
        {
            StartPhase phase = CurrentStartPhase;

            if (phase == StartPhase.Ready || phase == StartPhase.Set)
                return false;

            if (phase == StartPhase.Go)
                return allowPlayDuringGo;

            return true;
        }
    }
}