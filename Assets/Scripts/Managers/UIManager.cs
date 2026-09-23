using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;
    [SerializeField] private List<TextMeshProUGUI> playerHealth;
    private int lastPlayerJoined = 0;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        lastPlayerJoined = 0;
    }

    public int IJoined()
    {
        int id = lastPlayerJoined;
        playerHealth[id].gameObject.SetActive(true);
        if (lastPlayerJoined + 1 < playerHealth.Count)
        {
            lastPlayerJoined++;
        }
        else
        {
            Debug.Log("No more players can Join");
        }
        return id;
    }

    public void ChangeHealth(float health, int id)
    {
        playerHealth[id].text = "Player" + (id + 1) + " Health: " + health;
    }


}
