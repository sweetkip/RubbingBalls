using System.Collections;
using UnityEngine;

public class SlidePanel : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private Vector2 hiddenPos;
    [SerializeField] private Vector2 shownPos;
    [SerializeField] private float duration = 0.25f;

    private Coroutine routine;

    private void Awake()
    {
        if (panel == null)
            panel = (RectTransform)transform;

        panel.anchoredPosition = hiddenPos;
    }

    public void Open() => Slide(shownPos);
    public void Close() => Slide(hiddenPos);

    private void Slide(Vector2 target)
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(SlideRoutine(target));
    }

    private IEnumerator SlideRoutine(Vector2 target)
    {
        Vector2 start = panel.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            panel.anchoredPosition = Vector2.Lerp(start, target, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        panel.anchoredPosition = target;
    }
}