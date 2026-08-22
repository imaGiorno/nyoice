using System;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512CLowerInfoVerticalPathValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const float ExpectedHudWidth = 1220f;
        private const float ExpectedLowerInfoWidth = 1220f;
        private const float ExpectedQueueX = 7f;
        private const float ExpectedQueueBottomY = -2.5f;
        private const float ExpectedWallRouteTurnY = -2.7f;
        private const float ExpectedQueueSpacing = 0.9f;
        private static readonly Vector3 ExpectedSpawnPosition = new Vector3(7f, 4.5f, 0f);

        [MenuItem("Nyoice/Validate Sprint5-12C Lower Info & Vertical Path")]
        public static void ValidateLowerInfoVerticalPath()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();

            Transform canvas = GameObject.Find("UI/DiscomfortCanvas")?.transform;
            Require(canvas != null, "HUD canvas is missing.");
            ValidateHudAndLowerInfo(canvas);
            ValidateVerticalPath();
            ValidateUrinalsAndPassage();
            ValidateCameraSeparation(canvas);
            NyoiceSprint512BHudReferenceLayoutValidator.ValidateHudReferenceLayout();
            Debug.Log("Sprint5-12C Lower Info & Vertical Path validation passed.");
        }

        private static void ValidateHudAndLowerInfo(Transform canvas)
        {
            Transform hudContainer = RequireSingleDescendant(canvas, "HUDContainer");
            RectTransform hudRect = hudContainer.GetComponent<RectTransform>();
            Require(Mathf.Approximately(hudRect.sizeDelta.x, ExpectedHudWidth) &&
                    hudRect.sizeDelta.x < 1500f,
                "HUDContainer does not match the current narrowed width.");

            Transform lowerRoot = RequireSingleDescendant(canvas, "LowerInfoRoot");
            RectTransform lowerRect = lowerRoot.GetComponent<RectTransform>();
            Require(lowerRect.anchorMin == new Vector2(0.5f, 0f) &&
                    lowerRect.anchorMax == new Vector2(0.5f, 0f) &&
                    lowerRect.pivot == new Vector2(0.5f, 0f) &&
                    lowerRect.anchoredPosition.y >= 0f,
                "LowerInfoRoot is not fixed inside the lower screen edge.");
            Require(Mathf.Approximately(lowerRect.sizeDelta.x, ExpectedLowerInfoWidth),
                "LowerInfoRoot width changed unexpectedly.");

            RequireDirectChild(lowerRoot, "RulePanel");
            RequireDirectChild(lowerRoot, "ComboGuidePanel");
            RequireDirectChild(lowerRoot, "StageInfoPanel");
            foreach (Graphic graphic in lowerRoot.GetComponentsInChildren<Graphic>(true))
            {
                Require(!graphic.raycastTarget, $"LowerInfo graphic {graphic.name} blocks raycasts.");
            }

            ValidateLogicalWidth(lowerRect, new Vector2(1920f, 1080f));
            ValidateLogicalWidth(lowerRect, new Vector2(2340f, 1080f));
        }

        private static void ValidateVerticalPath()
        {
            Transform stage = GameObject.Find("GameStage")?.transform;
            Require(stage != null, "GameStage is missing.");
            Transform spawn = stage.Find("Entrance/SpawnPoint");
            Require(spawn != null && Approximately(spawn.position, ExpectedSpawnPosition),
                "Entrance position changed.");

            Transform queue = stage.Find("Queue");
            Require(queue != null, "Queue root is missing.");
            float previousY = 0f;
            for (int index = 0; index < 8; index++)
            {
                Transform slot = RequireDirectChild(queue, $"Queue{index + 1:00}");
                Require(Mathf.Approximately(slot.position.x, ExpectedQueueX),
                    $"Queue{index + 1:00} is no longer at x=7.");
                if (index == 0)
                {
                    Require(Mathf.Approximately(slot.position.y, ExpectedQueueBottomY) &&
                            slot.position.y > -3.75f,
                        "Queue lower edge was not raised from Sprint5-12B.");
                }
                else
                {
                    Require(Mathf.Approximately(slot.position.y - previousY, ExpectedQueueSpacing),
                        "QueueSlot spacing is not uniformly 0.9 units.");
                }

                previousY = slot.position.y;
            }

            Require(Mathf.Approximately(queue.Find("DecisionPoint").position.y, ExpectedQueueBottomY) &&
                    Mathf.Approximately(queue.Find("NyoiceApproachPoint").position.y, ExpectedWallRouteTurnY),
                "Decision or wall approach path changed.");
            Require(Mathf.Approximately(stage.Find("NyoiceLine/CrossingTarget").position.y,
                    ExpectedWallRouteTurnY),
                "CrossingTarget wall turn Y changed.");
            Require(Mathf.Approximately(stage.Find("Exit/ExitPoint").position.y, ExpectedQueueBottomY),
                "ExitPoint was not raised consistently.");
        }

        private static void ValidateUrinalsAndPassage()
        {
            Transform urinalRoot = GameObject.Find("GameStage/Urinals")?.transform;
            UrinalController[] urinals = urinalRoot != null
                ? urinalRoot.GetComponentsInChildren<UrinalController>(true)
                : Array.Empty<UrinalController>();
            Require(urinals.Length == 8, "The stage must retain eight urinals.");
            float rowY = urinals[0].transform.position.y;
            float rightmostBodyEdge = float.MinValue;
            foreach (UrinalController urinal in urinals)
            {
                Require(Mathf.Approximately(urinal.transform.position.y, rowY),
                    "Urinals are no longer arranged in one row.");
                Transform body = urinal.transform.Find("Body");
                rightmostBodyEdge = Mathf.Max(
                    rightmostBodyEdge,
                    body.position.x + (Mathf.Abs(body.lossyScale.x) * 0.5f));
            }

            Require(ExpectedQueueX - rightmostBodyEdge >= 2f,
                "The 2.0-unit passage width was not preserved.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/NPC.prefab");
            Require(prefab != null && prefab.GetComponent<NPCController>() != null &&
                    prefab.transform.Find("UrinationGauge") != null,
                "NPC prefab or urination gauge is missing.");
        }

        private static void ValidateCameraSeparation(Transform canvas)
        {
            Camera camera = Camera.main;
            Require(camera != null && camera.orthographic, "Main Camera must remain orthographic.");
            Transform exit = GameObject.Find("GameStage/Exit/ExitPoint")?.transform;
            Require(exit != null, "ExitPoint is missing.");
            float exitViewportY = camera.WorldToViewportPoint(exit.position).y;
            RectTransform lower = canvas.Find("LowerInfoRoot")?.GetComponent<RectTransform>();
            float lowerTopAtReference = (lower.anchoredPosition.y + lower.sizeDelta.y) / 1080f;
            Require(exitViewportY > lowerTopAtReference + 0.08f,
                "LowerInfoRoot overlaps the lower NPC path.");
        }

        private static void ValidateLogicalWidth(RectTransform rect, Vector2 resolution)
        {
            float scale = Mathf.Sqrt(
                (resolution.x / 1920f) * (resolution.y / 1080f));
            float logicalWidth = resolution.x / scale;
            Require(rect.sizeDelta.x <= logicalWidth - 48f,
                $"LowerInfoRoot exceeds the screen at {resolution.x:0}x{resolution.y:0}.");
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

            Require(count == 1, $"Hierarchy must contain exactly one {objectName}.");
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

        private static bool Approximately(Vector3 first, Vector3 second)
        {
            return Mathf.Approximately(first.x, second.x) &&
                   Mathf.Approximately(first.y, second.y) &&
                   Mathf.Approximately(first.z, second.z);
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
