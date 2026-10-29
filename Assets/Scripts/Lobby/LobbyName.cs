using System.Collections;
using TMPro;
using UnityEngine;

public class LobbyName : MonoBehaviour
{
    [Header("Nickname")]
    [SerializeField] private TMP_InputField nickInput;
    [SerializeField] private int maxCharts = 20;

    [Header("Botón Nombre")]
    [SerializeField] private GameObject nameButton;
    [SerializeField] private TMP_Text nameButtonLabel;
    [SerializeField] private string defaultLabel = "Name";

    private int Limit => maxCharts > 0 ? maxCharts : 20;

    private void Awake()
    {
        nickInput.characterLimit = Limit;
        nickInput.onEndEdit.AddListener(OnEndEdit);
    }

    private void Start()
    {
        RefreshLabel();
    }

    private void OnDestroy()
    {
        if (nickInput != null)
            nickInput.onEndEdit.RemoveListener(OnEndEdit);
    }

    public void StartEditing()
    {
        nameButton.SetActive(false);
        nickInput.gameObject.SetActive(true);
        nickInput.text = PlayerLocalData.Nickname;
        StartCoroutine(FocusNextFrame());
    }

    private IEnumerator FocusNextFrame()
    {
        yield return null;

        if (nickInput != null && nickInput.gameObject.activeInHierarchy)
        {
            nickInput.Select();
            nickInput.ActivateInputField();
        }
    }

    private void OnEndEdit(string text)
    {
        if (!nickInput.gameObject.activeInHierarchy)
            return;

        string nick = text.Trim();

        if (nick.Length > 0)
            SaveNick(nick.Length > Limit ? nick.Substring(0, Limit) : nick);

        StopEditing();
    }

    private void SaveNick(string nick)
    {
        PlayerLocalData.Nickname = nick;
        PlayerPrefs.SetString("PlayerName", nick);

        LobbyPlayer localPlayer = PreGameLobbyUI.Instance != null ? PreGameLobbyUI.Instance.GetLocalLobbyPlayer() : null;

        if (localPlayer != null)
            localPlayer.SetNick(nick);

        RefreshLabel();
    }

    private void StopEditing()
    {
        nickInput.gameObject.SetActive(false);
        nameButton.SetActive(true);
    }

    private void RefreshLabel()
    {
        if (nameButtonLabel == null)
            return;

        string nick = PlayerLocalData.Nickname;
        nameButtonLabel.text = string.IsNullOrWhiteSpace(nick) ? defaultLabel : nick;
    }
}