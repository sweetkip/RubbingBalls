using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class SessionListUI : MonoBehaviour
{
    [SerializeField] private SessionItemUI itemPrefab;
    [SerializeField] private Transform content;
    [SerializeField] private NetworkManager networkManager;

    public void UpdateList(List<SessionInfo> sessions)
    {
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        foreach (SessionInfo session in sessions)
        {
            if (!session.IsVisible) continue;

            SessionItemUI item = Instantiate(itemPrefab, content);
            item.Setup(session, networkManager);
        }
    }
}