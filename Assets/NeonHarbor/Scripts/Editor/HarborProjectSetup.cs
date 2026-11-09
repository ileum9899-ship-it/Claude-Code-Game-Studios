using System.Collections.Generic;
using NeonHarbor.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeonHarbor.EditorTools
{
    /// <summary>
    /// One-time project setup on first import (linear colour space, classic input
    /// enabled, a Main scene in Build Settings) plus menu commands. Everything here
    /// is optional: <see cref="GameBootstrap"/> already starts the game in any scene.
    /// </summary>
    [InitializeOnLoad]
    public static class HarborProjectSetup
    {
        private const string ScenePath = "Assets/NeonHarbor/Scenes/Main.unity";
        private const string DoneKey = "NeonHarbor.SetupDone.v1";

        static HarborProjectSetup()
        {
            if (SessionState.GetBool(DoneKey, false) || EditorPrefs.GetBool(DoneKey + Application.dataPath, false)) return;
            SessionState.SetBool(DoneKey, true);
            EditorApplication.delayCall += () =>
            {
                if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
                if (!System.IO.File.Exists(ScenePath)) CreateMainScene(false);
                EditorPrefs.SetBool(DoneKey + Application.dataPath, true);
                Debug.Log("[NeonHarbor] Project ready: open Assets/NeonHarbor/Scenes/Main.unity (or any scene) and press Play.");
            };
        }

        [MenuItem("Neon Harbor/Create Main Scene")]
        public static void CreateMainSceneMenu() { CreateMainScene(true); }

        private static void CreateMainScene(bool ask)
        {
            if (ask && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("NeonHarbor").AddComponent<HarborGame>();
            System.IO.Directory.CreateDirectory("Assets/NeonHarbor/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
