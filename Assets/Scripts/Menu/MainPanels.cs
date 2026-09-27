using UnityEngine;

public class MainPanels : MonoBehaviour
{
    [Header("MainBtns")]
    [SerializeField] private GameObject mainBtns;

    [Header("Panels")]
    [SerializeField] private SlidePanel privatePanel;
    [SerializeField] private SlidePanel charPanel;
    [SerializeField] private LobbyPanelUI publicPanel;

    public void OpenPrivate()
    {
        mainBtns.SetActive(false);
        privatePanel.Open();
    }

    public void OpenPublic()
    {
        mainBtns.SetActive(false);
        publicPanel.Open();
    }

    public void OpenChar()
    {
        mainBtns.SetActive(false);
        charPanel.Open();
    }

    public void CloseCurrent()
    {
        privatePanel.Close();
        publicPanel.Close();
        charPanel.Close();
        mainBtns.SetActive(true);
    }
}