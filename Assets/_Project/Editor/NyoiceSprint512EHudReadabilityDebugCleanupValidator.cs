using System;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512EHudReadabilityDebugCleanupValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";

        [MenuItem("Nyoice/Validate Sprint5-12E HUD Readability & Debug Cleanup")]
        public static void ValidateHudReadabilityDebugCleanup()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();

            Transform canvas = GameObject.Find("UI/DiscomfortCanvas")?.transform;
            Require(canvas != null, "HUD canvas is missing.");
            ValidateHudText(canvas);
            ValidateHighlightGap(canvas);
            ValidateDebugVisuals();
            NyoiceSprint512DHudWidthOverlapValidator.ValidateHudWidthOverlapFix();
            Debug.Log("Sprint5-12E HUD Readability & Debug Marker Cleanup validation passed.");
        }

        private static void ValidateHudText(Transform canvas)
        {
            RectTransform container = canvas.Find("HUDRoot/HUDContainer")?.GetComponent<RectTransform>();
            Require(container != null && Mathf.Approximately(container.sizeDelta.x, 1220f),
                "HUDContainer width changed from 1220 pixels.");
            Require(Mathf.Approximately(container.sizeDelta.y, 118f), "HUD height changed from 118 pixels.");

            CheckText(container.Find("DiscomfortPanel/DiscomfortLabel"), 22, false);
            CheckText(container.Find("DiscomfortPanel/DiscomfortText"), 26, true);
            CheckText(container.Find("StatsPanel/Score/ScoreLabel"), 22, false);
            CheckText(container.Find("StatsPanel/Score/ScoreText"), 32, true);
            CheckText(container.Find("StatsPanel/ProcessedCount/ProcessedCountLabel"), 22, false);
            CheckText(container.Find("StatsPanel/ProcessedCount/ProcessedCountText"), 32, true);
            CheckText(container.Find("ComboPanel/ComboLabel"), 20, false);
            CheckText(container.Find("ComboPanel/ComboText"), 30, true);

            Require(container.Find("DiscomfortPanel/DiscomfortText").GetComponent<Text>().fontSize >
                    container.Find("DiscomfortPanel/DiscomfortLabel").GetComponent<Text>().fontSize,
                "Discomfort value must be larger than its label.");
        }

        private static void CheckText(Transform target, int previousSize, bool isValue)
        {
            Text text = target?.GetComponent<Text>();
            Require(text != null && text.fontSize > previousSize, $"{target?.name ?? "HUD Text"} was not enlarged.");
            if (isValue)
            {
                Transform siblingLabel = target.parent.Find(target.name.Replace("Text", "Label"));
                if (target.name == "ProcessedCountText") siblingLabel = target.parent.Find("ProcessedCountLabel");
                Require(siblingLabel == null || text.fontSize > siblingLabel.GetComponent<Text>().fontSize,
                    $"{target.name} must be larger than its label.");
            }

            Canvas.ForceUpdateCanvases();
            Require(text.preferredWidth <= text.rectTransform.rect.width + 0.1f &&
                    text.preferredHeight <= text.rectTransform.rect.height + 0.1f,
                $"{target.name} does not fit inside its assigned HUD area.");
        }

        private static void ValidateHighlightGap(Transform canvas)
        {
            Camera camera = Camera.main;
            RectTransform hudRoot = canvas.Find("HUDRoot")?.GetComponent<RectTransform>();
            Require(camera != null && camera.orthographic && hudRoot != null, "Camera or HUDRoot is missing.");
            float hudBottomPixels = -hudRoot.anchoredPosition.y + hudRoot.sizeDelta.y;
            float hudBottomWorldY = camera.ViewportToWorldPoint(
                new Vector3(0.5f, 1f - (hudBottomPixels / 1080f), 0f)).y;
            float minimumGap = float.MaxValue;

            UrinalController[] urinals = GameObject.Find("GameStage/Urinals")
                ?.GetComponentsInChildren<UrinalController>(true) ?? Array.Empty<UrinalController>();
            Require(urinals.Length == 8, "The stage must retain eight urinals.");
            foreach (UrinalController controller in urinals)
            {
                Transform pixel = controller.transform.Find("VisualRoot/PixelVisual");
                SpriteRenderer spriteRenderer = pixel?.GetComponent<SpriteRenderer>();
                Transform highlight = controller.transform.Find("Highlight");
                Require(spriteRenderer != null && spriteRenderer.sprite != null && highlight != null,
                    $"{controller.name} visual or highlight is missing.");
                Require(Mathf.Approximately(pixel.localPosition.y, NyoicePixelArtSetup.UrinalPixelVisualYOffset),
                    $"{controller.name} PixelVisual position changed.");

                float spriteWidth = spriteRenderer.sprite.bounds.size.x * Mathf.Abs(pixel.localScale.x);
                float spriteHeight = spriteRenderer.sprite.bounds.size.y * Mathf.Abs(pixel.localScale.y);
                float frameWidth = highlight.Find("Top").localScale.x;
                float frameHeight = highlight.Find("Left").localScale.y;
                Vector3 spriteCenter = controller.transform.TransformPoint(pixel.localPosition);
                float frameLeft = highlight.position.x - (frameWidth * 0.5f);
                float frameRight = highlight.position.x + (frameWidth * 0.5f);
                float frameBottom = highlight.position.y - (frameHeight * 0.5f);
                float frameTop = highlight.position.y + (frameHeight * 0.5f);
                Require(frameLeft <= spriteCenter.x - (spriteWidth * 0.5f) &&
                        frameRight >= spriteCenter.x + (spriteWidth * 0.5f) &&
                        frameBottom <= spriteCenter.y - (spriteHeight * 0.5f) &&
                        frameTop >= spriteCenter.y + (spriteHeight * 0.5f),
                    $"{controller.name} highlight does not enclose its full sprite.");
                minimumGap = Mathf.Min(minimumGap, hudBottomWorldY - frameTop);
            }

            Require(minimumGap >= 0.1f, $"Highlight/HUD gap is only {minimumGap:0.000} world units.");
            Debug.Log($"Sprint5-12E closest Highlight/HUD gap: {minimumGap:0.000} world units.");
        }

        private static void ValidateDebugVisuals()
        {
            Transform stage = GameObject.Find("GameStage")?.transform;
            Require(stage != null, "GameStage is missing.");
            string[] requiredPaths =
            {
                "PixelEnvironmentTest",
                "Entrance/SpawnPoint",
                "Queue/DecisionPoint",
                "Queue/NyoiceApproachPoint",
                "NyoiceLine/CrossingTarget",
                "Exit/ExitPoint"
            };

            foreach (string path in requiredPaths)
            {
                Transform target = stage.Find(path);
                Require(target != null && target.gameObject.activeInHierarchy,
                    $"Required runtime path object {path} is missing or inactive.");
                foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
                {
                    Require(!renderer.enabled, $"Debug visual {path} is visible during normal play.");
                }
            }

            string[] debugRoots = { "PixelEnvironmentTest", "Queue", "Waypoints" };
            foreach (string path in debugRoots)
            {
                Transform root = stage.Find(path);
                Require(root != null, $"Debug/path root {path} is missing.");
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Require(!renderer.enabled, $"A path debug Renderer remains visible under {path}.");
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
