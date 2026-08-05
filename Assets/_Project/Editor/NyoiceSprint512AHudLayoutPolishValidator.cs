using System;
using Nyoice.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512AHudLayoutPolishValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";

        [MenuItem("Nyoice/Validate Sprint5-12A HUD Layout Polish")]
        public static void ValidateHudLayoutPolish()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();
            Transform canvas = GameObject.Find("UI/DiscomfortCanvas")?.transform;
            Require(canvas != null, "HUD canvas is missing.");
            Require(canvas.Find("HUDRoot/HUDContainer") != null, "HUD container is missing.");
            Require(canvas.GetComponent<ScoreUI>() != null && canvas.GetComponent<DiscomfortUI>() != null,
                "HUD runtime bindings are missing.");
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
            {
                Require(!graphic.raycastTarget, $"{graphic.name} still blocks raycasts.");
            }

            NyoiceSprint511BEnvironmentCleanupValidator.ValidateEnvironmentCleanup();
            Debug.Log("Sprint5-12A HUD Layout Polish validation passed.");
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
