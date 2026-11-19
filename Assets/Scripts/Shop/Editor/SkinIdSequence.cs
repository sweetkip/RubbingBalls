using UnityEditor;
using UnityEngine;

[FilePath("ProjectSettings/RubbingBallsSkinIds.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class SkinIdSequence : ScriptableSingleton<SkinIdSequence>
{
    [SerializeField] private int nextId = 8;
    internal int Reserve(int minimum)
    {
        int id = Mathf.Max(nextId, minimum);
        if (id <= 255) { nextId = id + 1; Save(true); }
        return id;
    }
}
