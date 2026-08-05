using System;
using System.Reflection;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512BHudReferenceLayoutValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const float MaximumContainerWidth = 1500f;
        private const float ExpectedPanelHeight = 118f;
        private static readonly MethodInfo HandleExitPointReachedMethod = typeof(NPCController).GetMethod(
            "HandleExitPointReached",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem("Nyoice/Validate Sprint5-12B HUD Reference Layout")]
        public static void ValidateHudReferenceLayout()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();

            Transform canvas = GameObject.Find("UI/DiscomfortCanvas")?.transform;
            Require(canvas != null, "HUD canvas is missing.");
            Transform container = RequireSingleDescendant(canvas, "HUDContainer");
            ValidateHierarchy(canvas, container);
            ValidateBlockGauge(canvas);
            ValidateProcessedCountRuntime();
            ValidateRaycastAndScaler(canvas);
            ValidateReferenceResolutions(container.GetComponent<RectTransform>());
            NyoiceSprint512AHudLayoutPolishValidator.ValidateHudLayoutPolish();
            Debug.Log("Sprint5-12B HUD Reference Layout validation passed.");
        }

        private static void ValidateHierarchy(Transform canvas, Transform container)
        {
            RectTransform containerRect = container.GetComponent<RectTransform>();
            Require(containerRect != null, "HUDContainer has no RectTransform.");
            Require(containerRect.anchorMin == containerRect.anchorMax,
                "HUDContainer must not stretch across the Canvas.");
            Require(containerRect.sizeDelta.x >= 1180f &&
                    containerRect.sizeDelta.x <= MaximumContainerWidth,
                "HUDContainer width is outside the approved 1180-1500 range.");

            RectTransform discomfort = RequireDirectChild(container, "DiscomfortPanel").GetComponent<RectTransform>();
            RectTransform stats = RequireDirectChild(container, "StatsPanel").GetComponent<RectTransform>();
            RectTransform combo = RequireDirectChild(container, "ComboPanel").GetComponent<RectTransform>();
            Require(discomfort.sizeDelta.x > stats.sizeDelta.x && stats.sizeDelta.x > combo.sizeDelta.x,
                "Panel widths must be ordered Discomfort > Stats > Combo.");
            Require(Mathf.Approximately(discomfort.sizeDelta.y, ExpectedPanelHeight) &&
                    Mathf.Approximately(stats.sizeDelta.y, ExpectedPanelHeight) &&
                    Mathf.Approximately(combo.sizeDelta.y, ExpectedPanelHeight),
                "HUD panel heights are not unified.");
            float firstGap = stats.anchoredPosition.x - (discomfort.anchoredPosition.x + discomfort.sizeDelta.x);
            float secondGap = combo.anchoredPosition.x - (stats.anchoredPosition.x + stats.sizeDelta.x);
            Require(Mathf.Approximately(firstGap, 24f) && Mathf.Approximately(secondGap, 24f),
                "HUD panel gaps are not consistently 24 pixels.");

            Transform scoreGroup = RequireDirectChild(stats, "Score");
            Transform processedGroup = RequireDirectChild(stats, "ProcessedCount");
            Text scoreLabel = RequireDirectChild(scoreGroup, "ScoreLabel").GetComponent<Text>();
            Text scoreValue = RequireDirectChild(scoreGroup, "ScoreText").GetComponent<Text>();
            Text processedLabel = RequireDirectChild(processedGroup, "ProcessedCountLabel").GetComponent<Text>();
            Text processedValue = RequireDirectChild(processedGroup, "ProcessedCountText").GetComponent<Text>();
            Text comboValue = RequireDirectChild(combo, "ComboText").GetComponent<Text>();
            Require(scoreLabel.text == "SCORE" && !scoreValue.text.Contains("SCORE"),
                "Score label is duplicated.");
            Require(processedLabel != null && processedValue != null && !processedValue.text.Contains("/100"),
                "Processed count presentation is invalid.");
            Require(comboValue.alignment == TextAnchor.MiddleCenter,
                "Combo value is not centered.");
            Require(canvas.Find("HudPanel") == null && canvas.Find("DiscomfortSlider") == null,
                "Sprint5-12A horizontal HUD elements remain.");
        }

        private static void ValidateBlockGauge(Transform canvas)
        {
            DiscomfortUI ui = canvas.GetComponent<DiscomfortUI>();
            Require(ui != null && ui.BlockFills != null && ui.BlockFills.Length == 10,
                "Discomfort block gauge must contain ten fills.");
            foreach (Image fill in ui.BlockFills)
            {
                Require(fill != null && fill.type == Image.Type.Filled,
                    "Discomfort block fill is missing or not a Filled Image.");
            }

            ui.UpdateBlockGauge(0f, 100f);
            Require(Mathf.Approximately(SumFill(ui.BlockFills), 0f), "0 discomfort did not empty the gauge.");
            ui.UpdateBlockGauge(35f, 100f);
            Require(Mathf.Approximately(SumFill(ui.BlockFills), 3.5f) &&
                    Mathf.Approximately(ui.BlockFills[0].fillAmount, 1f) &&
                    Mathf.Approximately(ui.BlockFills[2].fillAmount, 1f) &&
                    Mathf.Approximately(ui.BlockFills[3].fillAmount, 0.5f) &&
                    Mathf.Approximately(ui.BlockFills[4].fillAmount, 0f),
                "35 discomfort did not produce three full blocks and one half block.");
            ui.UpdateBlockGauge(100f, 100f);
            Require(Mathf.Approximately(SumFill(ui.BlockFills), 10f),
                "100 discomfort did not fill all ten blocks.");
            Require(ui.BlockFills[0].color.g > ui.BlockFills[0].color.r &&
                    ui.BlockFills[9].color.r > ui.BlockFills[9].color.g,
                "Gauge colors do not progress from green to red.");
            ui.Refresh();
        }

        private static void ValidateProcessedCountRuntime()
        {
            Require(HandleExitPointReachedMethod != null, "NPC normal-exit completion hook is missing.");
            var root = new GameObject("Sprint5-12B Processed Count Fixture");
            try
            {
                GameStateManager gameState = root.AddComponent<GameStateManager>();
                DiscomfortManager discomfort = root.AddComponent<DiscomfortManager>();
                discomfort.Configure(Array.Empty<Nyoice.Toilet.UrinalController>(), gameState);
                ScoreManager score = root.AddComponent<ScoreManager>();
                score.Configure(discomfort, gameState);

                var uiRoot = new GameObject("ScoreUI", typeof(RectTransform));
                uiRoot.transform.SetParent(root.transform, false);
                Text scoreText = CreateText(uiRoot.transform, "ScoreText");
                Text comboText = CreateText(uiRoot.transform, "ComboText");
                Text processedText = CreateText(uiRoot.transform, "ProcessedCountText");
                ScoreUI scoreUi = uiRoot.AddComponent<ScoreUI>();
                scoreUi.Configure(score, scoreText, comboText, processedText, true);
                score.ResetSession();
                Require(score.ProcessedNpcCount == 0 && scoreUi.DisplayedProcessedCount == "0\u4eba",
                    "Processed count did not initialize to zero.");

                NPCController first = CreateLeavingNpc(root.transform, score, "NPC_001");
                HandleExitPointReachedMethod.Invoke(first, null);
                Require(score.ProcessedNpcCount == 1 && scoreUi.DisplayedProcessedCount == "1\u4eba",
                    "Processed count did not increment at normal exit completion.");
                HandleExitPointReachedMethod.Invoke(first, null);
                Require(score.ProcessedNpcCount == 1,
                    "The same NPC was counted twice.");

                NPCController second = CreateLeavingNpc(root.transform, score, "NPC_002");
                gameState.TriggerGameOver();
                HandleExitPointReachedMethod.Invoke(second, null);
                Require(score.ProcessedNpcCount == 1,
                    "Processed count increased after GameOver.");
                score.ResetSession();
                Require(score.ProcessedNpcCount == 0 && scoreUi.DisplayedProcessedCount == "0\u4eba",
                    "Processed count did not reset for a new session.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static NPCController CreateLeavingNpc(
            Transform parent,
            ScoreManager score,
            string objectName)
        {
            var npcObject = new GameObject(objectName);
            npcObject.transform.SetParent(parent, false);
            NPCController npc = npcObject.AddComponent<NPCController>();
            npc.Initialize(null);
            npc.ConfigureScore(score);
            SetPrivateField(npc, "_leavingStarted", true);
            SetPrivateField(npc, "_movingToExitPoint", true);
            FieldInfo stateField = typeof(NPCController).GetField("<State>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(stateField != null, "NPC state backing field is missing.");
            stateField.SetValue(npc, NPCState.Leaving);
            return npc;
        }

        private static void ValidateRaycastAndScaler(Transform canvas)
        {
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
            {
                Require(!graphic.raycastTarget, $"{graphic.name} blocks raycasts.");
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Require(scaler != null &&
                    scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                    scaler.referenceResolution == new Vector2(1920f, 1080f) &&
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight &&
                    Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f),
                "Canvas Scaler is not configured for 1920x1080 at Match 0.5.");
        }

        private static void ValidateReferenceResolutions(RectTransform container)
        {
            Vector2[] resolutions =
            {
                new Vector2(1920f, 1080f),
                new Vector2(2340f, 1080f),
                new Vector2(1280f, 720f),
                new Vector2(1600f, 900f)
            };
            foreach (Vector2 resolution in resolutions)
            {
                float widthScale = resolution.x / 1920f;
                float heightScale = resolution.y / 1080f;
                float scale = Mathf.Sqrt(widthScale * heightScale);
                float logicalWidth = resolution.x / scale;
                Require(container.sizeDelta.x <= logicalWidth - 48f,
                    $"HUD exceeds safe horizontal bounds at {resolution.x:0}x{resolution.y:0}.");
            }
        }

        private static Text CreateText(Transform parent, string objectName)
        {
            var child = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(parent, false);
            return child.GetComponent<Text>();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, $"Field {fieldName} is missing.");
            field.SetValue(target, value);
        }

        private static float SumFill(Image[] fills)
        {
            float total = 0f;
            foreach (Image fill in fills)
            {
                total += fill.fillAmount;
            }

            return total;
        }

        private static Transform RequireSingleDescendant(Transform root, string objectName)
        {
            Transform match = null;
            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                {
                    match = child;
                    count++;
                }
            }

            Require(count == 1, $"HUD must contain exactly one {objectName}.");
            return match;
        }

        private static Transform RequireDirectChild(Transform parent, string objectName)
        {
            Transform match = null;
            int count = 0;
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == objectName)
                {
                    match = child;
                    count++;
                }
            }

            Require(count == 1, $"{parent.name} must contain exactly one {objectName}.");
            return match;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
