using Fusion;
using TMPro;
using UnityEngine;

public class LobbyName : NetworkBehaviour
{
    [Header("Nickname")]
    [SerializeField] private TMP_InputField nickInput;
    [SerializeField] private int maxCharts;

    public void ChangeName()
    {
        PlayerLocalData.Nickname = nickInput.text;
        PlayerPrefs.SetString("PlayerName", PlayerLocalData.Nickname);
        nickInput.gameObject.SetActive(false);
        PreGameLobbyUI.Instance.GetLocalLobbyPlayer().SetNick(PlayerLocalData.Nickname);
        Debug.Log("Se cambio el nombre");
    }

    public void CheckName()
    {
        if (nickInput.text.Length <= maxCharts)
        {
            ChangeName();
        }
        else
        {
            nickInput.text = "Too Long! Max " + maxCharts;
        }
    }


}
