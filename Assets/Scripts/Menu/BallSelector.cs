using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;

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

    private SelectionMode mode = SelectionMode.Color;

    private void Start()
    {
        if (!PlayerPrefs.HasKey("PlayerName"))
        {
            PlayerPrefs.SetString("PlayerName", PlayerLocalData.Nickname);
        }
        if (!PlayerPrefs.HasKey("PlayerColor"))
        {
            PlayerPrefs.SetInt("PlayerColor", PlayerLocalData.SelectedColor);
        }
        if (!PlayerPrefs.HasKey("PlayerTexture"))
        {
            PlayerPrefs.SetInt("PlayerTexture", PlayerLocalData.SelectedTexture);
        }

        PlayerLocalData.Nickname = PlayerPrefs.GetString("PlayerName");
        PlayerLocalData.SelectedColor = PlayerPrefs.GetInt("PlayerColor");
        PlayerLocalData.SelectedTexture = PlayerPrefs.GetInt("PlayerTexture");
        if (nickInput != null)
            nickInput.text = PlayerLocalData.Nickname;
        RefreshPreview();
        RefreshBtns();
    }

    private void OnEnable()
    {
        if (nickInput != null)
            nickInput.text = PlayerLocalData.Nickname;

        RefreshPreview();
        RefreshBtns();
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
            if (palette == null || palette.Count == 0)
                return;

            PlayerLocalData.SelectedColor = dir > 0 ? palette.NextIndex(PlayerLocalData.SelectedColor) : palette.PreviousIndex(PlayerLocalData.SelectedColor);
        }
        else
        {
            if (textures == null || textures.Length == 0)
                return;

            int next = PlayerLocalData.SelectedTexture + dir;
            if (next < 0)
                next = textures.Length - 1;
            if (next >= textures.Length)
                next = 0;

            PlayerLocalData.SelectedTexture = next;
        }

        RefreshPreview();
    }

    public void RefreshPreview()
    {
        if (colorPreview != null && palette != null && palette.Count > 0)
            colorPreview.color = palette.GetColor(PlayerLocalData.SelectedColor);

        if (texturePreview != null && textures != null && textures.Length > 0)
            texturePreview.sprite = textures[PlayerLocalData.SelectedTexture];
    }

    private void RefreshBtns()
    {
        if (colorBtn != null)
            colorBtn.interactable = mode != SelectionMode.Color;
        if (textureBtn != null)
            textureBtn.interactable = mode != SelectionMode.Texture;
    }

    public void ChangeName()
    {
        PlayerLocalData.Nickname = nickInput.text;
        PlayerPrefs.SetString("PlayerName", PlayerLocalData.Nickname);
    }
}