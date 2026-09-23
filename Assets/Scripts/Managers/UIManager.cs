using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIManager : NetworkBehaviour
{
    [Networked] public int lastPlayerJoined {  get; set; }
    public static UIManager Instance;
    [SerializeField] private List<TextMeshProUGUI> playerHealth;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void Spawned()
    {
        lastPlayerJoined = 0;
    }

    public int IJoined()
    {
        int id = lastPlayerJoined;
        if (lastPlayerJoined + 1 < playerHealth.Count)
        {
            lastPlayerJoined++;
        }
        else
        {
            Debug.Log("No more players can Join");
        }
        PlayerJoined();
        return id;
    }

    public void PlayerJoined()
    {
        for(int i = 0; i < lastPlayerJoined; i++)
        {
            playerHealth[i].gameObject.SetActive(true);
        }
    }

    public void ChangeHealth(float health, int id)
    {
        //if (!Object.HasStateAuthority) return;
        playerHealth[id].text = "P" + (id + 1) + " Damage Received: " + health.ToString("F2") + " - Lives Left: 3";
    }


}
