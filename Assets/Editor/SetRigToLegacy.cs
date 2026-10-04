using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把指定目录下所有模型（FBX）的 Rig → Animation Type 批量改成 Legacy。
///
/// 两个入口：
///   1. 菜单 Tools/Set Rig To Legacy  —— 扫描 Paths 里配置的目录
///   2. Project 窗口里选中若干 FBX → 右键 → Set Rig To Legacy
///
/// 改动是**永久性**的：直接改 .meta 里的 animationType，然后重新导入。
/// 项目没有版本控制的话，改之前先自己备份一份。
/// </summary>
public static class SetRigToLegacy
{
    /// <summary>
    /// 要扫描的目录。改这里来指向别的文件夹。
    /// </summary>
    private static readonly string[] Paths =
    {
        "Assets/CombatGirlsCharacterPack",
    };

    [MenuItem("Tools/Set Rig To Legacy")]
    private static void Run()
    {
        var targets = new List<string>();

        foreach (string folder in Paths)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[SetRigToLegacy] 目录不存在，已跳过：{folder}");
                continue;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                targets.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        Convert(targets, "目录扫描");
    }

    // Project 窗口里右键，只处理选中的那几个
    [MenuItem("Assets/Set Rig To Legacy")]
    private static void RunOnSelection()
    {
        var targets = new List<string>();

        foreach (string guid in Selection.assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is ModelImporter) targets.Add(path);
        }

        Convert(targets, "选中项");
    }

    [MenuItem("Assets/Set Rig To Legacy", true)]
    private static bool RunOnSelectionValidate()
    {
        foreach (string guid in Selection.assetGUIDs)
        {
            if (AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is ModelImporter)
                return true;
        }
        return false;
    }

    private static void Convert(List<string> paths, string source)
    {
        if (paths.Count == 0)
        {
            Debug.LogWarning($"[SetRigToLegacy]（{source}）没有找到任何模型。");
            return;
        }

        // 先只统计，让使用者能确认范围，避免误改一大片
        var pending = new List<(string path, ModelImporter importer)>();
        int alreadyLegacy = 0;

        foreach (string path in paths)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            if (importer.animationType == ModelImporterAnimationType.Legacy)
            {
                alreadyLegacy++;
                continue;
            }

            pending.Add((path, importer));
        }

        if (pending.Count == 0)
        {
            Debug.Log($"[SetRigToLegacy]（{source}）共 {paths.Count} 个模型，" +
                      $"全部已经是 Legacy，无需改动。");
            return;
        }

        bool ok = EditorUtility.DisplayDialog(
            "改为 Legacy",
            $"共扫描到 {paths.Count} 个模型。\n\n" +
            $"  已经是 Legacy：{alreadyLegacy}\n" +
            $"  将要改动：{pending.Count}\n\n" +
            $"改动会写入各自的 .meta 并重新导入，不可撤销（除非有版本控制）。\n" +
            $"确认继续？",
            "改动", "取消");

        if (!ok)
        {
            Debug.Log("[SetRigToLegacy] 已取消，没有改动任何文件。");
            return;
        }

        try
        {
            for (int i = 0; i < pending.Count; i++)
            {
                var (path, importer) = pending[i];

                EditorUtility.DisplayProgressBar(
                    "改为 Legacy",
                    $"{i + 1}/{pending.Count}  {path}",
                    (float)i / pending.Count);

                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.SaveAndReimport();
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[SetRigToLegacy]（{source}）完成：{pending.Count} 个模型已改为 Legacy。" +
                  $"（原本就是 Legacy 的 {alreadyLegacy} 个未改动）");
    }
}
