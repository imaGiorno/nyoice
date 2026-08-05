using System;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint511BEnvironmentCleanupValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private static readonly Vector3 ExpectedSpawnPosition = new Vector3(7f, 4.5f, 0f);
        private const float ExpectedQueueX = 7f;
        private const float MinimumExpandedPassageWidth = 2f;

        [MenuItem("Nyoice/Validate Sprint5-11B Environment Cleanup")]
        public static void ValidateEnvironmentCleanup()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();

            Transform stage = GameObject.Find("GameStage")?.transform;
            Require(stage != null, "Configured GameScene has no GameStage.");
            Require(stage.Find("BackgroundProps") == null,
                "Sprint5-11A background props were not removed.");
            ValidateFixedLayout(stage);
            NyoiceSprint511ALayoutPolishValidator.ValidateLayoutPolish();
            Debug.Log("Sprint5-11B Environment Cleanup validation passed.");
        }

        private static void ValidateFixedLayout(Transform stage)
        {
            Transform spawn = stage.Find("Entrance/SpawnPoint");
            Require(spawn != null && Approximately(spawn.position, ExpectedSpawnPosition),
                "Entrance position changed.");

            Transform queue = stage.Find("Queue");
            Require(queue != null, "Queue root is missing.");
            for (int index = 0; index < 8; index++)
            {
                Transform slot = queue.Find($"Queue{index + 1:00}");
                Require(slot != null && Mathf.Approximately(slot.position.x, ExpectedQueueX),
                    $"Queue{index + 1:00} position changed.");
            }

            Transform urinalRoot = stage.Find("Urinals");
            UrinalController[] urinals = urinalRoot != null
                ? urinalRoot.GetComponentsInChildren<UrinalController>(true)
                : Array.Empty<UrinalController>();
            Require(urinals.Length == 8, "The layout must contain exactly eight urinals.");
            float rightmostBodyEdge = float.MinValue;
            foreach (UrinalController urinal in urinals)
            {
                Transform body = urinal.transform.Find("Body");
                Require(body != null, $"{urinal.name} has no Body.");
                rightmostBodyEdge = Mathf.Max(
                    rightmostBodyEdge,
                    body.position.x + (Mathf.Abs(body.lossyScale.x) * 0.5f));
            }

            Require(ExpectedQueueX - rightmostBodyEdge >= MinimumExpandedPassageWidth,
                "Sprint5-11A passage width was not preserved.");
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
