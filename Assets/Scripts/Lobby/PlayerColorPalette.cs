using UnityEngine;

[CreateAssetMenu(fileName = "PlayerColorPalette", menuName = "Game/Player Color Palette")]
public class PlayerColorPalette : ScriptableObject
{
    public Color[] colors;
    public Sprite[] sprites;
    public Sprite defaultSprite;

    public int CountColors
    {
        get
        {
            if (colors == null)
                return 0;
            return colors.Length;
        }
    }

    public int CountSprites
    {
        get
        {
            if (sprites == null)
                return 0;
            return sprites.Length;
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

    public Sprite GetSprite(int index)
    {
        if(sprites == null || sprites.Length == 0)
            return defaultSprite;
        index = Mathf.Clamp(index, 0, sprites.Length - 1);
        return sprites[index];
    }

    public int NextIndex(int current)
    {
        if (CountColors == 0)
            return 0;

        int next = current + 1;
        return next >= CountColors ? 0 : next;
    }

    public int NextSpriteIndex(int current)
    {
        if (CountSprites == 0)
            return 0;

        int next = current + 1;
        return next >= CountSprites ? 0 : next;
    }

    public int PreviousIndex(int current)
    {
        if (CountColors == 0)
            return 0;

        int prev = current - 1;
        return prev < 0 ? CountColors - 1 : prev;
    }

    public int PreviousSpriteIndex(int current)
    {
        if (CountSprites == 0)
            return 0;

        int prev = current - 1;
        return prev < 0 ? CountSprites - 1 : prev;
    }
}