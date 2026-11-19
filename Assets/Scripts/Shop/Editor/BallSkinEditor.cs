using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class BallSkinEditor : IPreprocessBuildWithReport
{
    private const string Folder = "Assets/Resources/BallSkins";
    private static BallSkin[] Assets() => AssetDatabase.FindAssets("t:BallSkin", new[] { Folder })
        .Select(guid => AssetDatabase.LoadAssetAtPath<BallSkin>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();

    [MenuItem("Assets/Create/Rubbing Balls/Ball Skin")]
    private static void Create()
    {
        int id = SkinIdSequence.instance.Reserve(Assets().Select(item => item.Id).DefaultIfEmpty(-1).Max() + 1);
        if (id > 255) { Debug.LogError("Fusion supports skin IDs from 0 to 255."); return; }
        var item = ScriptableObject.CreateInstance<BallSkin>();
        item.displayName = "New Skin";
        item.price = 100;
        var data = new SerializedObject(item);
        data.FindProperty("id").intValue = id;
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(item, AssetDatabase.GenerateUniqueAssetPath(Folder + "/New Skin.asset"));
        AssetDatabase.SaveAssets();
        Selection.activeObject = item;
    }

    private static string CatalogSQL()
    {
        var skins = Assets().OrderBy(item => item.Id).ToArray();
        var ids = new HashSet<int>();
        var sql = new StringBuilder("begin;\n");
        foreach (var item in skins)
        {
            if (!item.image || string.IsNullOrWhiteSpace(item.displayName) || item.price < 0 ||
                item.Id < 0 || item.Id > 255 || !ids.Add(item.Id) || (item.Id == 0 && item.price != 0))
                throw new BuildFailedException("Invalid skin or duplicate ID: " + AssetDatabase.GetAssetPath(item));
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
            sql.AppendLine($"select rb_coins.publish_skin({item.Id}, '{guid}', {item.price});");
        }
        if (!ids.Contains(0)) throw new BuildFailedException("Starter skin (ID 0) is required.");
        sql.AppendLine("update rb_coins.skin_catalog set listed = false where id not in (" + string.Join(",", skins.Select(s => s.Id)) + ");");
        return sql.AppendLine("commit;").ToString();
    }

    [MenuItem("Tools/Rubbing Balls/Copy skin catalog SQL")]
    private static void Copy()
    {
        EditorGUIUtility.systemCopyBuffer = CatalogSQL();
        Debug.Log("Catalog copied. Run it in your Supabase SQL Editor to publish the prices.");
    }

    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => CatalogSQL();
}
