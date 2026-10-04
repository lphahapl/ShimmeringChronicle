using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class InstallCCDebugView
{
    static InstallCCDebugView() { EditorApplication.delayCall += Once; }
    static void Once()
    {
        if (!File.Exists("Tools/Environment/cc-debug-installed.txt") && !EditorApplication.isPlayingOrWillChangePlaymode)
            Install();
    }
    [MenuItem("Tools/Debug/Attach CC Scene Debug View")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity") return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player || !player.GetComponent<CharacterController>()) return;
        if (!player.GetComponent<CharacterControllerDebugView>()) Undo.AddComponent<CharacterControllerDebugView>(player);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Tools/Environment");
        File.WriteAllText("Tools/Environment/cc-debug-installed.txt", "PASS: CharacterControllerDebugView attached to " + player.name);
        SceneView.RepaintAll();
    }
}
