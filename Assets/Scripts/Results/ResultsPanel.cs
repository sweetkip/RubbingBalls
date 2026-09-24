using UnityEngine;

public class ResultsPanel : MonoBehaviour
{
    private void OnEnable()
    {
        NetworkManager.Instance?.SetInputEnabled(false);
    }

    private void OnDisable()
    {
        NetworkManager.Instance?.SetInputEnabled(true);
    }
}