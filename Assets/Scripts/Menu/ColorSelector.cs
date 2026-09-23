using UnityEngine;
using UnityEngine.UI;

public static class PlayerLocalData
{
    public static int SelectedColor = 0;
}

public class ColorSelector : MonoBehaviour
{
    [SerializeField] private Color[] colors;
    [SerializeField] private Image preview;

    private void Start()
    {
        if (colors.Length > 0)
            preview.color = colors[PlayerLocalData.SelectedColor];
    }

    public void NextColor()
    {
        PlayerLocalData.SelectedColor++;

        if (PlayerLocalData.SelectedColor >= colors.Length)
            PlayerLocalData.SelectedColor = 0;

        preview.color = colors[PlayerLocalData.SelectedColor];
    }

    public void PreviousColor()
    {
        PlayerLocalData.SelectedColor--;

        if (PlayerLocalData.SelectedColor < 0)
            PlayerLocalData.SelectedColor = colors.Length - 1;

        preview.color = colors[PlayerLocalData.SelectedColor];
    }
}