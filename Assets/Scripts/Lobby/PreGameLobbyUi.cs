using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PreGameLobbyUI : MonoBehaviour
{
    public static PreGameLobbyUI Instance { get; private set; }

    [SerializeField] private PlayerColorPalette palette;

    [SerializeField] private LobbyPlayerSlot[] playerSlots;

    [SerializeField] private Image localColorPreview;

    [SerializeField] private Button previousColorButton;
    [SerializeField] private Button nextColorButton;
    [SerializeField] private Button readyButton;

    [SerializeField] private TMP_Text readyButtonText;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RefreshLobby();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void NextColor()
    {
        LobbyPlayer player = GetLocalLobbyPlayer();

        if (player == null)
            return;

        if (player.IsReady)
            return;

        if (palette == null || palette.Count == 0)
            return;

        int nextColor = player.ColorIndex + 1;

        if (nextColor >= palette.Count)
            nextColor = 0;

        player.SelectColor((byte)nextColor);
    }

    public void PreviousColor()
    {
        LobbyPlayer player = GetLocalLobbyPlayer();

        if (player == null)
            return;

        if (player.IsReady)
            return;

        if (palette == null || palette.Count == 0)
            return;

        int previousColor = player.ColorIndex - 1;

        if (previousColor < 0)
            previousColor = palette.Count - 1;

        player.SelectColor((byte)previousColor);
    }

    public void ToggleReady()
    {
        LobbyPlayer player = GetLocalLobbyPlayer();

        if (player == null)
            return;

        player.SetReady(!player.IsReady);
    }

    public void RefreshLobby()
    {
        if (NetworkManager.Instance == null)
            return;

        NetworkRunner runner = NetworkManager.Instance.Runner;

        if (runner == null)
            return;

        foreach (LobbyPlayerSlot slot in playerSlots)
        {
            if (slot != null)
                slot.Hide();
        }

        List<PlayerRef> players = new List<PlayerRef>();

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            players.Add(player);
        }

        players.Sort(
            (a, b) => a.PlayerId.CompareTo(b.PlayerId)
        );

        int slotIndex = 0;

        foreach (PlayerRef player in players)
        {
            if (slotIndex >= playerSlots.Length)
                break;

            if (!runner.TryGetPlayerObject(
                    player,
                    out NetworkObject playerObject))
                continue;

            LobbyPlayer lobbyPlayer =
                playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer == null)
                continue;

            bool isLocalPlayer =
                player == runner.LocalPlayer;

            playerSlots[slotIndex].Show(
                player,
                lobbyPlayer,
                palette,
                isLocalPlayer
            );

            slotIndex++;
        }

        LobbyPlayer localPlayer =
            GetLocalLobbyPlayer();

        if (localPlayer == null)
        {
            if (previousColorButton != null)
                previousColorButton.interactable = false;

            if (nextColorButton != null)
                nextColorButton.interactable = false;

            if (readyButton != null)
                readyButton.interactable = false;

            return;
        }

        if (localColorPreview != null && palette != null)
        {
            localColorPreview.color =
                palette.GetColor(localPlayer.ColorIndex);
        }

        bool ready = localPlayer.IsReady;

        if (previousColorButton != null)
            previousColorButton.interactable = !ready;

        if (nextColorButton != null)
            nextColorButton.interactable = !ready;

        if (readyButton != null)
            readyButton.interactable = true;

        if (readyButtonText != null)
        {
            readyButtonText.text =
                ready
                ? "Cancel"
                : "Ready";
        }
    }

    private LobbyPlayer GetLocalLobbyPlayer()
    {
        if (NetworkManager.Instance == null)
            return null;

        NetworkRunner runner =
            NetworkManager.Instance.Runner;

        if (runner == null)
            return null;

        if (!runner.TryGetPlayerObject(
                runner.LocalPlayer,
                out NetworkObject playerObject))
            return null;

        return playerObject.GetComponent<LobbyPlayer>();
    }
}