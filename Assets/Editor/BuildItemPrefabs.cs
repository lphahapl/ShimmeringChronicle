using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Creates one held-item prefab for each existing inventory configuration.</summary>
[InitializeOnLoad]
public static class BuildItemPrefabs
{
    const string Request = "Tools/build-item-prefabs.request";
    const string Report = "Tools/ItemPrefabs-result.txt";
    const string Folder = "Assets/Prefabs";

    static BuildItemPrefabs() { EditorApplication.update += RunRequested; }

    static void RunRequested()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Build();
    }

    [MenuItem("Tools/Items/Create Missing Item Prefabs")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var report = new StringBuilder();
        try
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Prefabs");
            var sword = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Weapons/Sword/Sword (1).prefab");
            foreach (var guid in AssetDatabase.FindAssets("t:ItemSO", new[] { "Assets/Configs/ItemSO" }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                var path = Folder + "/" + item.name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab)
                {
                    GameObject root = null;
                    try
                    {
                        // An isolated prefab editing scene avoids changing the open scene.
                        root = new GameObject(item.name);
                        var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                        try
                        {
                            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                            if (item.prefab)
                            {
                                var visual = UnityEngine.Object.Instantiate(item.prefab, root.transform, false);
                                visual.name = "Model";
                            }
                            else if (item is WeaponSO weapon)
                            {
                                if (!sword) throw new InvalidOperationException("Missing sword model.");
                                var visual = UnityEngine.Object.Instantiate(sword, root.transform, false);
                                visual.name = "Model";
                                root.AddComponent<WeaponObj>().SO = weapon;
                            }
                            else
                            {
                                if (!item.icon) throw new InvalidOperationException("Missing icon: " + item.name);
                                var visual = new GameObject("Icon");
                                visual.transform.SetParent(root.transform, false);
                                var renderer = visual.AddComponent<SpriteRenderer>();
                                renderer.sprite = item.icon;
                                float size = Mathf.Max(item.icon.bounds.size.x, item.icon.bounds.size.y);
                                visual.transform.localScale = Vector3.one * (0.35f / Mathf.Max(size, 0.001f));
                            }
                            prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                        }
                        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
                    }
                    finally { if (root) UnityEngine.Object.DestroyImmediate(root); }
                }
                if (!prefab || prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidOperationException("Missing prefab or renderer: " + path);
                if (item is WeaponSO expected && prefab.GetComponent<WeaponObj>() && prefab.GetComponent<WeaponObj>().SO != expected)
                    throw new InvalidOperationException("Wrong weapon configuration: " + path);
                item.prefab = prefab;
                EditorUtility.SetDirty(item);
                report.AppendLine("PASS " + item.name + " -> " + path);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Report, report.ToString());
            Debug.Log("[ItemPrefabs] Completed. " + report);
        }
        catch (Exception error)
        {
            File.WriteAllText(Report, report + "FAIL " + error);
            Debug.LogException(error);
        }
    }
}
