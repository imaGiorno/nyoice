using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nyoice.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nyoice.Editor
{
    /// <summary>
    /// Creates the minimal Sprint 1 scenes without replacing user-authored scenes.
    /// </summary>
    public static class NyoiceProjectSetup
    {
        private const string MenuPath = "Nyoice/Setup Sprint 1";
        private const string ScenesDirectory = "Assets/_Project/Scenes";
        private const string TitleScenePath = ScenesDirectory + "/TitleScene.unity";
        private const string GameScenePath = ScenesDirectory + "/GameScene.unity";

        [MenuItem(MenuPath)]
        public static void SetupSprintOne()
        {
            string[] existingScenes = new[] { TitleScenePath, GameScenePath }
                .Where(File.Exists)
                .ToArray();

            if (existingScenes.Length > 0)
            {
                string sceneList = string.Join("\n", existingScenes);
                ShowDialog(
                    "既存のSceneを保護するため、セットアップを中止しました。\n\n" + sceneList);
                Debug.LogWarning($"Nyoice setup stopped because these scenes already exist:\n{sceneList}");
                return;
            }

            Directory.CreateDirectory(ScenesDirectory);

            CreateTitleScene();
            CreateGameScene();
            RegisterBuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(TitleScenePath);

            ShowDialog("TitleSceneとGameSceneを作成し、Build Settingsへ登録しました。");
            Debug.Log("Nyoice Sprint 1 setup completed.");
        }

        private static void CreateTitleScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.08f, 0.14f);
            cameraObject.tag = "MainCamera";

            var controllerObject = new GameObject("Title Scene Controller");
            controllerObject.AddComponent<TitleSceneController>();

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        private static void CreateGameScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void RegisterBuildScenes()
        {
            var projectScenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };

            var otherScenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != TitleScenePath && scene.path != GameScenePath);

            var buildScenes = new List<EditorBuildSettingsScene>(projectScenes);
            buildScenes.AddRange(otherScenes);
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        private static void ShowDialog(string message)
        {
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Nyoice Sprint 1 Setup", message, "OK");
            }
        }
    }
}
