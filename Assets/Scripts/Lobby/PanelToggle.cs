using UnityEngine;

public class PanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private bool startOpen = false;

    private void Start()
    {
        panel.SetActive(startOpen);
    }

    public void Toggle()
    {
        panel.SetActive(!panel.activeSelf);
    }

    public void Open()
    {
        panel.SetActive(true);
    }

    public void Close()
    {
        panel.SetActive(false);
    }
}