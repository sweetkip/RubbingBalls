using UnityEngine;

[CreateAssetMenu(fileName = "PlayerColorPalette", menuName = "Game/Player Color Palette")]
public class PlayerColorPalette : ScriptableObject
{
    public Color[] colors;

    public int Count
    {
        get
        {
            if (colors == null)
                return 0;

            return colors.Length;
        }
    }

    public Color GetColor(int index)
    {
        if (colors == null || colors.Length == 0)
            return Color.white;

        index = Mathf.Clamp(index, 0, colors.Length - 1);

        Color color = colors[index];
        color.a = 1f;

        return color;
    }

    //<3
    public int NextIndex(int current)
    {
        if (Count == 0)
            return 0;

        int next = current + 1;
        return next >= Count ? 0 : next;
    }
    //<3
    public int PreviousIndex(int current)
    {
        if (Count == 0)
            return 0;

        int prev = current - 1;
        return prev < 0 ? Count - 1 : prev;
    }
    //<3
}