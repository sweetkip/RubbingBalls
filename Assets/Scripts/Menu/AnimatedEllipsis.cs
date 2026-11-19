using System;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class AnimatedEllipsis : MonoBehaviour
{
    private const int ShowEverything = 99999;
    private const string Dots = "...";

    [SerializeField] private float stepSeconds = 0.4f;

    private TMP_Text label;
    private string lastText;
    private int lastDots = -1;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void OnDisable()
    {
        if (label != null)
            label.maxVisibleCharacters = ShowEverything;

        lastText = null;
        lastDots = -1;
    }

    private void LateUpdate()
    {
        string current = label.text;

        if (string.IsNullOrEmpty(current) || !current.EndsWith(Dots, StringComparison.Ordinal))
        {
            if (label.maxVisibleCharacters != ShowEverything)
                label.maxVisibleCharacters = ShowEverything;

            lastText = null;
            lastDots = -1;
            return;
        }

        int dots = DotsNow(stepSeconds);

        if (dots == lastDots && string.Equals(current, lastText))
            return;

        lastText = current;
        lastDots = dots;

        label.maxVisibleCharacters = ShowEverything;
        label.ForceMeshUpdate();
        label.maxVisibleCharacters = label.textInfo.characterCount - (Dots.Length - dots);
    }

    public static int DotsNow(float stepSeconds)
    {
        float step = Mathf.Max(0.05f, stepSeconds);
        int phase = (int)(Time.unscaledTime / step) % 4;
        return (phase + 1) % 4;
    }

    public static string Animate(string text, float stepSeconds = 0.4f)
    {
        if (string.IsNullOrEmpty(text) || !text.EndsWith(Dots, StringComparison.Ordinal))
            return text;

        int dots = DotsNow(stepSeconds);
        string withoutDots = text.Substring(0, text.Length - Dots.Length);

        return withoutDots + new string('.', dots) + new string(' ', Dots.Length - dots);
    }
}
