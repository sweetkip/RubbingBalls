using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;
    [SerializeField] private PlayerHudSlot[] playerSlots;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private TMP_Text victoryRewardText;
    private TMP_Text[] victoryTitles;
    private string[] originalTitles;
    [SerializeField] private Sprite victoryCoinSprite;
    private RectTransform coinIcon;
    private TMP_Text coinLabel;
    private int shownReward;
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
        if (victoryRewardText) victoryRewardText.text = "";
        else if (winPanel)
        {
            var title = winPanel.transform.Find("Victory") ?? winPanel.transform.Find("Txt_Victory");
            if (title)
            {
                victoryTitles = title.GetComponentsInChildren<TMP_Text>(true);
                originalTitles = System.Array.ConvertAll(victoryTitles, label => label.text);
            }
        }
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] != null)
                playerSlots[i].Hide();
        }
    }

    private void Update()
    {
        if (winPanel && winPanel.activeInHierarchy) RefreshVictoryReward();
        UpdateReturnTimer();
        UpdateStartCountdown();
    }

    private void RefreshVictoryReward()
    {
        int amount = 0;
        var game = GameManager.Instance;
        if (game && game.LocalAwardRound != null)
        {
            var session = RubbingBalls.Accounts.LoginSession.Current;
            if (session.HasAccount && !session.Coins.TryGetReceipt(game.LocalAwardRound, out amount))
                amount = -1;
        }
        if (shownReward == amount) return;
        shownReward = amount;
        string receipt = amount < 0 ? "…" : amount > 0 ? "+" + amount : "";
        if (victoryRewardText) victoryRewardText.text = receipt;
        else if (victoryTitles != null)
        {
            for (int i = 0; i < victoryTitles.Length; i++)
            {
                var title = victoryTitles[i];
                var suffix = title.richText ? "  <size=60%>" + receipt + "</size>" : "  " + receipt;
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.text = amount != 0 ? originalTitles[i] + suffix : originalTitles[i];
            }
        }
        UpdateCoinIcon(amount > 0);
    }

    private void UpdateCoinIcon(bool visible)
    {
        if (!visible || !victoryCoinSprite)
        {
            if (coinIcon) coinIcon.gameObject.SetActive(false);
            return;
        }

        if (!coinIcon)
        {
            coinLabel = victoryRewardText;
            if (!coinLabel && victoryTitles != null && victoryTitles.Length > 0)
                coinLabel = victoryTitles[victoryTitles.Length - 1];
            if (!coinLabel) return;

            var icon = new GameObject("Moneda de victoria", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            icon.layer = coinLabel.gameObject.layer;
            coinIcon = (RectTransform)icon.transform;
            coinIcon.SetParent(coinLabel.rectTransform, false);
            coinIcon.sizeDelta = new Vector2(30f, 30f);
            coinIcon.pivot = new Vector2(0f, 0.5f);
            var image = icon.GetComponent<UnityEngine.UI.Image>();
            image.sprite = victoryCoinSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            coinLabel.OnPreRenderText += PositionCoinIcon;
        }
        coinIcon.gameObject.SetActive(true);
        coinLabel.ForceMeshUpdate();
    }

    private void PositionCoinIcon(TMP_TextInfo info)
    {
        if (!coinIcon || shownReward <= 0 || info.characterCount == 0) return;
        var digit = info.characterInfo[info.characterCount - 1];
        coinIcon.localPosition = new Vector3(digit.topRight.x + 6f, (digit.topRight.y + digit.bottomLeft.y) * 0.5f, 0f);
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

    public void SetPlayerSpeaking(int id, bool speaking)
    {
        PlayerHudSlot slot = GetSlot(id);

        if (slot != null)
            slot.SetSpeaking(speaking);
    }

    public void GameIsOver(bool won)
    {
        winPanel.SetActive(won);
        losePanel.SetActive(!won);
    }
}