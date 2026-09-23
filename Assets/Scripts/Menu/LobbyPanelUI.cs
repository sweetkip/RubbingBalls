using System.Collections;
using UnityEngine;

public class LobbyPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkManager manager;
    [SerializeField] private SessionListUI sessionListUI;
    [SerializeField] private RectTransform panel;

    [Header("Anim")]
    [SerializeField] private Vector2 hiddenPos;
    [SerializeField] private Vector2 shownPos;
    [SerializeField] private float sDuration = 0.25f;

    private bool isOpen;
    private Coroutine sRoutine;


    private void OnEnable()
    {
        manager.OnSessionListChanged += sessionListUI.UpdateList;
        panel.anchoredPosition = hiddenPos;
    }

    private void OnDisable()
    {
        manager.OnSessionListChanged -= sessionListUI.UpdateList;
    }

    public void ToggleLobby()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        isOpen = true;
        manager.JoinLobby();
        Slide(shownPos);
    }

    public void Close()
    {
        isOpen = false;
        Slide(hiddenPos);
    }

    private void Slide(Vector2 target)
    {
        if (sRoutine != null)
            StopCoroutine(sRoutine);

        sRoutine = StartCoroutine(SRoutine(target));
    }

    private IEnumerator SRoutine(Vector2 target)
    {
        Vector2 start = panel.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < sDuration)
        {
            float t = elapsed / sDuration;
            panel.anchoredPosition = Vector2.Lerp(start, target, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        panel.anchoredPosition = target;
    }
}