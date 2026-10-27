using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;
    [SerializeField] private List<TextMeshProUGUI> playerHealth;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private TMP_Text returnTimerText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
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

    public void PlayerJoined(int totalPlayers)
    {
        for (int i = 0; i < totalPlayers; i++)
        {
            playerHealth[i].gameObject.SetActive(true);
        }
    }

    public void ChangeHealth(float health, int id, int lives, string name)
    {
        playerHealth[id].text = name + " " + " Damage Received: " + health.ToString("F2") + " - Lives Left: " + lives;
    }


    public void PlayerLost(int id, string name)
    {
        playerHealth[id].text = name + " " + " PERDIO";
    }

    public void PlayerDisconnected(int id, string name)
    {
        if (id < 0 || id >= playerHealth.Count) return;
        playerHealth[id].text = name + " " + " SE DESCONECTÓ";
    }

    public void GameIsOver(bool won)
    {
        winPanel.SetActive(won);
        losePanel.SetActive(!won);
    }
}