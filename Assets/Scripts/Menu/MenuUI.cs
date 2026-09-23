using System.Collections;
using UnityEngine;
using TMPro;

public class MenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkManager manager;
    [SerializeField] private TMP_InputField input;

    [Header("Shake")]
    [SerializeField] private RectTransform sTarget;
    [SerializeField] private float sDuration = 0.4f;
    [SerializeField] private float sStrength = 15f;

    private Coroutine sRoutine;

    private void OnEnable()
    {
        manager.OnJoinFailed += HandleJoinFailed;
    }

    private void OnDisable()
    {
        manager.OnJoinFailed -= HandleJoinFailed;
    }

    public void CreateGame()
    {
        manager.StartGameHost(input.text);
    }
    public void JoinGame()
    {
        manager.StartGameClient(input.text);
    }

    private void HandleJoinFailed()
    {
        if (sTarget == null) return;

        if (sRoutine != null)
            StopCoroutine(sRoutine);

        sRoutine = StartCoroutine(SRoutine());
    }

    private IEnumerator SRoutine()
    {
        Vector2 originalPos = sTarget.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < sDuration)
        {
            float damping = 1f - (elapsed / sDuration);
            float offsetX = Mathf.Sin(elapsed * 40f) * sStrength * damping;
            sTarget.anchoredPosition = originalPos + new Vector2(offsetX, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        sTarget.anchoredPosition = originalPos;
    }
}