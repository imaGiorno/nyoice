using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nyoice.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

            var controllerObject = new GameObject("Title Scene Controller");
            TitleSceneController controller = controllerObject.AddComponent<TitleSceneController>();

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CreateBackground(canvasObject.transform);
            CreateText(canvasObject.transform, "Title", "尿意's（Nyoice）", 48, 0.62f, 0.78f);
            CreateText(canvasObject.transform, "Start Prompt", "クリック / タップでスタート", 24, 0.25f, 0.38f);
            CreateStartButton(canvasObject.transform, controller);
            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        private static void CreateBackground(Transform parent)
        {
            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(parent, false);
            StretchToParent(backgroundObject.GetComponent<RectTransform>());
            backgroundObject.GetComponent<Image>().color = new Color(0.04f, 0.08f, 0.14f);
        }

        private static void CreateText(
            Transform parent,
            string objectName,
            string content,
            int fontSize,
            float anchorMinY,
            float anchorMaxY)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.1f, anchorMinY);
            rectTransform.anchorMax = new Vector2(0.9f, anchorMaxY);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        private static void CreateStartButton(Transform parent, TitleSceneController controller)
        {
            var buttonObject = new GameObject("Start Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            StretchToParent(buttonObject.GetComponent<RectTransform>());

            Image image = buttonObject.GetComponent<Image>();
            image.color = Color.clear;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            UnityEventTools.AddPersistentListener(button.onClick, controller.StartGame);
        }

        private static void CreateEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.GetComponent<EventSystem>().firstSelectedGameObject = null;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
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
