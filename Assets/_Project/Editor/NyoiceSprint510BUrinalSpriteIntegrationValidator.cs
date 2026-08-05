using System;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint510BUrinalSpriteIntegrationValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string HolderPath =
            "Assets/_Project/Art/Pixel/Urinals/UrinalSpriteHolder.asset";

        [MenuItem("Nyoice/Validate Sprint5-10B Urinal Sprite Integration")]
        public static void ValidateUrinalSpriteIntegration()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();

            UrinalSpriteHolder holder = AssetDatabase.LoadAssetAtPath<UrinalSpriteHolder>(HolderPath);
            Require(holder != null, "Urinal SpriteHolder asset is missing.");
            var serializedHolder = new SerializedObject(holder);
            Require(serializedHolder.FindProperty("urinalNormal") != null &&
                    serializedHolder.FindProperty("urinalSelected") != null,
                "Urinal SpriteHolder does not expose both formal sprite slots.");

            Transform urinalRoot = GameObject.Find("GameStage/Urinals")?.transform;
            Require(urinalRoot != null, "Configured GameScene has no Urinals root.");
            for (int index = 0; index < 8; index++)
            {
                UrinalController controller = urinalRoot
                    .Find($"Urinal{index + 1:00}")?.GetComponent<UrinalController>();
                Require(controller != null, $"Urinal{index + 1:00} has no UrinalController.");
                Require(controller.SpriteHolder == holder,
                    $"Setup Game Stage did not restore Urinal{index + 1:00}'s SpriteHolder reference.");
            }

            var fixture = new GameObject("Sprint5-10B Null Sprite Fixture");
            try
            {
                UrinalController controller = fixture.AddComponent<UrinalController>();
                controller.ConfigureSpriteHolder(null, null);
                controller.SetNormal();
                controller.SetSelected();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture);
            }

            NyoiceSprint510BusinessSpriteHolderValidator.ValidateBusinessSpriteHolder();
            Debug.Log("Sprint5-10B Urinal Sprite Integration validation passed.");
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
