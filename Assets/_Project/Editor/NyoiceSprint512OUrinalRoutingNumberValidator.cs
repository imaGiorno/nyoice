using System;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512OUrinalRoutingNumberValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";

        [MenuItem("Nyoice/Validate Sprint5-12O Urinal Routing Number Fix")]
        public static void ValidateUrinalRoutingNumberFix()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            ValidateLeftToRightSceneNumbering();
            NyoiceSprint512NUrinal78DirectRouteValidator.ValidateUrinal78DirectRouteCorrection();
            Debug.Log("Sprint5-12O Urinal Routing Number Fix validation passed.");
        }

        private static void ValidateLeftToRightSceneNumbering()
        {
            float previousX = float.NegativeInfinity;
            for (int number = 1; number <= 8; number++)
            {
                Transform urinal = GameObject.Find($"GameStage/Urinals/Urinal{number:00}")?.transform;
                Require(urinal != null, $"Scene Urinal{number:00} is missing.");
                UrinalController controller = urinal.GetComponent<UrinalController>();
                Require(controller != null && controller.UrinalNumber == number,
                    $"Scene Urinal{number:00} component number is incorrect.");
                Require(urinal.position.x > previousX,
                    "Game View urinal numbering is not ordered left-to-right from 01 to 08.");
                previousX = urinal.position.x;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
