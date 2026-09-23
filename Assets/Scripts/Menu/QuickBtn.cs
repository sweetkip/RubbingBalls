using UnityEngine;
using UnityEngine.UI;

public class QuickBtn : MonoBehaviour
{
    [SerializeField] private NetworkManager manager;
    [SerializeField] private Button btn;

    private void OnEnable()
    {
        manager.OnJoinFailed += ReenableButton;
    }

    private void OnDisable()
    {
        manager.OnJoinFailed -= ReenableButton;
    }

    public void QuickPlay()
    {
        btn.interactable = false;
        manager.QuickPlay();
    }

    private void ReenableButton()
    {
        btn.interactable = true;
    }
}