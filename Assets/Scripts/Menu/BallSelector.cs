using RubbingBalls.Accounts;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class PlayerLocalData
{
    public static int SelectedColor = 0;
    public static int SelectedTexture = 0;
    public static string Nickname = "NoName";
}

public class BallSelector : MonoBehaviour
{
    private enum SelectionMode { Color, Texture }

    [Header("Nickname")]
    [SerializeField] private TMP_InputField nickInput;

    [Header("Preview")]
    [SerializeField] private Image colorPreview;
    [SerializeField] private Image texturePreview;

    [Header("Color Palette")]
    [SerializeField] private PlayerColorPalette palette;

    [Header("Textures")]
    [SerializeField] private Sprite[] textures;

    [Header("Btns")]
    [SerializeField] private Button colorBtn;
    [SerializeField] private Button textureBtn;

    [SerializeField] private int maxCharts;

    private SelectionMode mode = SelectionMode.Color;

    public PlayerColorPalette Palette => palette;
    private AccountProfile profile;

    private void OnEnable()
    {
        profile = LoginSession.Current.Profile;
        profile.Applied += RefreshLocal;
        RefreshLocal();
        RefreshBtns();
    }

    private void OnDisable()
    {
        if (profile) profile.Applied -= RefreshLocal;
    }

    private void RefreshLocal()
    {
        if (nickInput && !nickInput.isFocused) nickInput.SetTextWithoutNotify(PlayerLocalData.Nickname);
        RefreshPreview();
    }

    public void SetColor()
    {
        mode = SelectionMode.Color;
        RefreshBtns();
    }

    public void SetTexture()
    {
        mode = SelectionMode.Texture;
        RefreshBtns();
    }

    public void Next() => Step(1);
    public void Prev() => Step(-1);

    private void Step(int dir)
    {
        if (mode == SelectionMode.Color)
        {
            if (palette == null || palette.CountColors == 0)
                return;

            profile.SetColor(dir > 0 ? palette.NextIndex(PlayerLocalData.SelectedColor) : palette.PreviousIndex(PlayerLocalData.SelectedColor));
        }
        else
        {
            if (palette == null || palette.CountSprites == 0)
                return;

            profile.SetSkin(dir > 0 ? palette.NextSpriteIndex(PlayerLocalData.SelectedTexture) : palette.PreviousSpriteIndex(PlayerLocalData.SelectedTexture));
        }

        RefreshPreview();
    }

    public void RefreshPreview()
    {
        if (colorPreview != null && palette != null && palette.CountColors > 0)
            colorPreview.color = palette.GetColor(PlayerLocalData.SelectedColor);

        if (texturePreview != null && palette != null && palette.CountSprites > 0)
            texturePreview.sprite = textures != null && PlayerLocalData.SelectedTexture < textures.Length
                ? textures[PlayerLocalData.SelectedTexture] : palette.GetSprite(PlayerLocalData.SelectedTexture);
    }

    private void RefreshBtns()
    {
        if (colorBtn != null)
            colorBtn.interactable = mode != SelectionMode.Color;
        if (textureBtn != null)
            textureBtn.interactable = mode != SelectionMode.Texture;
    }

    public void CheckName()
    {
        if(nickInput.text.Length <= maxCharts)
        {
            ChangeName();
        }
        else
        {
            nickInput.text = "Too Long! Max " + maxCharts;
        }
    }

    public void ChangeName()
    {
        profile.SetName(nickInput.text);
        nickInput.SetTextWithoutNotify(PlayerLocalData.Nickname);
    }
}