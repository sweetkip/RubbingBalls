using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;
    [SerializeField] private PlayerHudSlot[] playerSlots;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private TMP_Text returnTimerText;

    [Header("Start countdown")]
    [SerializeField] private GameObject startCountdownRoot;
    [SerializeField] private TMP_Text startCountdownText;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] != null)
                playerSlots[i].Hide();
        }
    }

    private void Update()
    {
        UpdateReturnTimer();
        UpdateStartCountdown();
    }

    private void UpdateReturnTimer()
    {
        if (returnTimerText == null)
            return;

        GameManager gameManager = GameManager.Instance;

        if (gameManager == null || !gameManager.IsGameOver)
        {
            returnTimerText.gameObject.SetActive(false);
            return;
        }

        returnTimerText.gameObject.SetActive(true);
        returnTimerText.text = "Back to the lobby in: " + Mathf.CeilToInt(gameManager.ReturnRemainingSeconds) + "s";
    }

    private void UpdateStartCountdown()
    {
        if (startCountdownRoot == null || startCountdownText == null)
            return;

        GameManager gameManager = GameManager.Instance;

        if (gameManager == null || gameManager.IsGameOver)
        {
            startCountdownRoot.SetActive(false);
            return;
        }

        StartPhase phase = gameManager.CurrentStartPhase;

        if (phase == StartPhase.None)
        {
            startCountdownRoot.SetActive(false);
            return;
        }

        startCountdownRoot.SetActive(true);

        if (phase == StartPhase.Ready)
            startCountdownText.text = "Ready";
        else if (phase == StartPhase.Set)
            startCountdownText.text = "Set";
        else
            startCountdownText.text = "Go!";
    }

    private PlayerHudSlot GetSlot(int id)
    {
        if (playerSlots == null || id < 0 || id >= playerSlots.Length)
            return null;

        return playerSlots[id];
    }

    public void PlayerJoined(int totalPlayers)
    {
        for (int i = 0; i < totalPlayers && i < playerSlots.Length; i++)
        {
            if (playerSlots[i] != null)
                playerSlots[i].Show();
        }
    }

    public void SetPlayerInfo(int id, string playerName, Sprite sprite, Color color)
    {
        PlayerHudSlot slot = GetSlot(id);

        if (slot != null)
            slot.SetPlayer(playerName, sprite, color);
    }

    public void SetPlayerStats(int id, float damage, int lives)
    {
        PlayerHudSlot slot = GetSlot(id);

        if (slot != null)
            slot.SetStats(damage, lives);
    }

    public void PlayerLost(int id, string name)
    {
        PlayerHudSlot slot = GetSlot(id);

        if (slot != null)
            slot.SetLost();
    }

    public void PlayerDisconnected(int id, string name)
    {
        PlayerHudSlot slot = GetSlot(id);

        if (slot != null)
            slot.SetDisconnected();
    }

    public void GameIsOver(bool won)
    {
        winPanel.SetActive(won);
        losePanel.SetActive(!won);
    }
}