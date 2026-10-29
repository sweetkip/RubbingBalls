using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHudSlot : MonoBehaviour
{
    private enum SlotState { Playing, Lost, Disconnected }

    [Header("Lives (one image per life)")]
    [SerializeField] private Image[] lifeImages;
    [SerializeField] private Color lifeColor = Color.white;
    [SerializeField] private Color lostLifeColor = new Color(0.15f, 0.15f, 0.15f, 0.35f);

    [Header("Player")]
    [SerializeField] private Image spriteImage;
    [SerializeField] private TMP_Text nameText;

    [Header("Damage")]
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private TMP_Text percentShadowText;
    [SerializeField] private float percentPerDamageUnit = 100f;

    [Header("Out of the match")]
    [SerializeField] private string lostLabel = "OUT";
    [SerializeField] private string disconnectedLabel = "LEFT";
    [SerializeField] private Color outSpriteColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    private SlotState state = SlotState.Playing;

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetPlayer(string playerName, Sprite sprite, Color color)
    {
        if (nameText != null)
            nameText.text = playerName;

        if (spriteImage == null)
            return;

        if (sprite != null)
            spriteImage.sprite = sprite;

        if (state == SlotState.Playing)
            spriteImage.color = color;
    }

    public void SetStats(float damage, int lives)
    {
        if (state != SlotState.Playing)
            return;

        int percent = Mathf.RoundToInt(Mathf.Max(0f, damage) * percentPerDamageUnit);
        SetPercentText(percent + "%");
        SetLives(lives);
    }

    public void SetLost()
    {
        SetOut(SlotState.Lost, lostLabel);
    }

    public void SetDisconnected()
    {
        SetOut(SlotState.Disconnected, disconnectedLabel);
    }

    private void SetOut(SlotState newState, string label)
    {
        if (state != SlotState.Playing)
            return;

        state = newState;
        SetLives(0);
        if (nameText != null)
            nameText.text = label;

        if (spriteImage != null)
            spriteImage.color = outSpriteColor;
    }

    private void SetLives(int lives)
    {
        if (lifeImages == null)
            return;

        for (int i = 0; i < lifeImages.Length; i++)
        {
            if (lifeImages[i] == null)
                continue;

            if (i < lives)
                lifeImages[i].color = lifeColor;
            else
                lifeImages[i].color = lostLifeColor;
        }
    }

    private void SetPercentText(string text)
    {
        if (percentText != null)
            percentText.text = text;

        if (percentShadowText != null)
            percentShadowText.text = text;
    }
}