using System;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint511ALayoutPolishValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private static readonly Vector3 ExpectedSpawnPosition = new Vector3(7f, 4.5f, 0f);
        private const float ExpectedQueueX = 7f;
        private const float MinimumExpandedPassageWidth = 2f;

        [MenuItem("Nyoice/Validate Sprint5-11A Layout Polish")]
        public static void ValidateLayoutPolish()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();

            Transform stage = GameObject.Find("GameStage")?.transform;
            Require(stage != null, "Configured GameScene has no GameStage.");
            ValidateEntranceAndQueue(stage);
            ValidateUrinalRowAndPassage(stage);
            NyoiceSprint59PixelArtFoundationValidator.ValidatePixelArtFoundation();
            Debug.Log("Sprint5-11A Layout Polish validation passed.");
        }

        private static void ValidateEntranceAndQueue(Transform stage)
        {
            Transform spawnPoint = stage.Find("Entrance/SpawnPoint");
            Require(spawnPoint != null && Approximately(spawnPoint.position, ExpectedSpawnPosition),
                "Entrance spawn position changed from the upper right.");

            Transform queue = stage.Find("Queue");
            Require(queue != null, "Queue root is missing.");
            for (int index = 0; index < 8; index++)
            {
                Transform slot = queue.Find($"Queue{index + 1:00}");
                Require(slot != null && Mathf.Approximately(slot.position.x, ExpectedQueueX),
                    $"Queue{index + 1:00} is no longer on the right side.");
            }
        }

        private static void ValidateUrinalRowAndPassage(Transform stage)
        {
            Transform urinalRoot = stage.Find("Urinals");
            Require(urinalRoot != null, "Urinals root is missing.");
            UrinalController[] controllers = urinalRoot.GetComponentsInChildren<UrinalController>(true);
            Require(controllers.Length == 8, "The layout must contain exactly eight urinals.");

            float rightmostBodyEdge = float.MinValue;
            float expectedY = controllers[0].transform.position.y;
            foreach (UrinalController controller in controllers)
            {
                Require(Mathf.Approximately(controller.transform.position.y, expectedY),
                    "Urinals are no longer arranged in one row.");
                Transform body = controller.transform.Find("Body");
                Require(body != null, $"{controller.name} has no Body.");
                rightmostBodyEdge = Mathf.Max(
                    rightmostBodyEdge,
                    body.position.x + (Mathf.Abs(body.lossyScale.x) * 0.5f));
            }

            float passageWidth = ExpectedQueueX - rightmostBodyEdge;
            Require(passageWidth >= MinimumExpandedPassageWidth,
                $"Passage width was not expanded enough: {passageWidth:0.##}.");
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
