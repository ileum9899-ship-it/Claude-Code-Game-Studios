using System.Collections.Generic;
using CityGame.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CityGame.EditorTools
{
    /// <summary>
    /// Menu "City Game/Create Main Scene": writes Assets/CityGame/Scenes/Main.unity
    /// with the controller in it and registers it in Build Settings. Optional —
    /// <see cref="GameBootstrap"/> already starts the game in any scene.
    /// </summary>
    public static class CitySceneSetup
    {
        private const string ScenePath = "Assets/CityGame/Scenes/Main.unity";

        [MenuItem("City Game/Create Main Scene")]
        public static void CreateMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("CityGame").AddComponent<CityGameController>();
            System.IO.Directory.CreateDirectory("Assets/CityGame/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[CityGame] Main scene created at " + ScenePath + ". Press Play.");
        }
    }
}
