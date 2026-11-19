using System;
using UnityEngine;

public sealed class BallSkin : ScriptableObject
{
    [SerializeField, HideInInspector] private int id;
    public string displayName;
    public Sprite image;
    [Min(0)] public int price;
    public int Id => id;
    private static BallSkin[] items;
    internal static BallSkin[] Items
    {
        get
        {
            if (items != null) return items;
            items = Resources.LoadAll<BallSkin>("BallSkins");
            Array.Sort(items, (a, b) => a.id.CompareTo(b.id));
            return items;
        }
    }

    internal static BallSkin Find(int id)
    {
        foreach (var skin in Items)
            if (skin.id == id) return skin;
        return null;
    }

    internal static int Step(int current, int direction)
    {
        var skins = Items;
        if (skins.Length == 0) return 0;
        int index = Array.FindIndex(skins, item => item.id == current);
        var profile = RubbingBalls.Accounts.LoginSession.Current.Profile;
        for (int i = 0; i < skins.Length; i++)
        {
            index = (index + direction + skins.Length) % skins.Length;
            if (profile.CanWear(skins[index].id)) return skins[index].id;
        }
        return 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCatalog() => items = null;
}
