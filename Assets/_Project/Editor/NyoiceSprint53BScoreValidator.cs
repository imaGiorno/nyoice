using System;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.Toilet;
using Nyoice.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.Editor
{
    public static class NyoiceSprint53BScoreValidator
    {
        [MenuItem("Nyoice/Validate Sprint5-3B Score Flow")]
        public static void ValidateScoreFlow()
        {
            var root = new GameObject("Sprint53BScoreValidation");
            root.SetActive(false);
            try
            {
                GameStateManager gameState = Add<GameStateManager>(root.transform, "GameState");
                UrinalController[] urinals = CreateUrinals(root.transform);
                DiscomfortManager discomfort = Add<DiscomfortManager>(root.transform, "Discomfort");
                discomfort.Configure(urinals, gameState);
                ScoreManager score = Add<ScoreManager>(root.transform, "Score");
                score.Configure(discomfort, gameState);

                ScoreUI scoreUi = CreateScoreUi(root.transform, score);
                RequireMultiplier(score, scoreUi, 1.0f, "COMBO ×1.0");
                Require(score.CurrentScore == 0, "Initial score is not zero.");
                Require(!score.IsBaseScoreConfigured, "An unconfirmed base score is configured.");

                score.AdvanceTime(4.99f);
                RequireMultiplier(score, scoreUi, 1.0f, "COMBO ×1.0");
                score.AdvanceTime(0.01f);
                RequireMultiplier(score, scoreUi, 1.5f, "COMBO ×1.5");
                score.AdvanceTime(5f);
                RequireMultiplier(score, scoreUi, 2.0f, "COMBO ×2.0");
                score.AdvanceTime(5f);
                RequireMultiplier(score, scoreUi, 2.5f, "COMBO ×2.5");
                score.AdvanceTime(5f);
                RequireMultiplier(score, scoreUi, 3.0f, "COMBO ×3.0");
                score.AdvanceTime(50f);
                RequireMultiplier(score, scoreUi, 3.0f, "COMBO ×3.0");

                Require(score.NotifyNpcFinished(), "Finished NPC notification was rejected.");
                Require(score.ProcessedNpcCount == 1, "Finished NPC count was not recorded.");
                Require(score.CurrentScore == 0, "Unconfirmed base score changed the score.");

                NPCController first = CreateNpc(root.transform, "NPC_001");
                NPCController second = CreateNpc(root.transform, "NPC_002");
                Occupy(urinals[0], first);
                Occupy(urinals[1], second);
                discomfort.AdvanceTime(0.01f);
                RequireMultiplier(score, scoreUi, 1.0f, "COMBO ×1.0");
                Require(Mathf.Approximately(score.NoAdjacencyElapsed, 0f), "Adjacency did not reset elapsed time.");

                score.AdvanceTime(20f);
                RequireMultiplier(score, scoreUi, 1.0f, "COMBO ×1.0");
                Require(urinals[1].Release(second), "Validation urinal release failed.");
                Require(urinals[0].Release(first), "Validation urinal release failed.");
                discomfort.AdvanceTime(0.01f);

                NPCController reservedNpc = CreateNpc(root.transform, "NPC_Reserved");
                Require(urinals[2].Reserve(reservedNpc), "Reserved validation setup failed.");
                discomfort.AdvanceTime(0.01f);
                score.AdvanceTime(5f);
                RequireMultiplier(score, scoreUi, 1.5f, "COMBO ×1.5");

                gameState.TriggerGameOver();
                score.AdvanceTime(5f);
                Require(!score.NotifyNpcFinished(), "Finished notification was accepted after GameOver.");
                RequireMultiplier(score, scoreUi, 1.5f, "COMBO ×1.5");
                Require(score.CurrentScore == 0 && score.ProcessedNpcCount == 1,
                    "GameOver changed score or processed count.");
                Debug.LogWarning(
                    "Sprint 5-3B: base score, award timing, calculation, and rounding remain unconfirmed; scoring is disabled.");
                Debug.Log("Sprint 5-3B score flow validation passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static ScoreUI CreateScoreUi(Transform root, ScoreManager scoreManager)
        {
            var uiObject = new GameObject("ScoreUI", typeof(RectTransform));
            uiObject.transform.SetParent(root, false);
            Text scoreText = CreateText(uiObject.transform, "ScoreText");
            Text comboText = CreateText(uiObject.transform, "ComboText");
            ScoreUI scoreUi = uiObject.AddComponent<ScoreUI>();
            scoreUi.Configure(scoreManager, scoreText, comboText);
            Require(scoreUi.DisplayedScore == "SCORE 0", "Initial score UI is invalid.");
            return scoreUi;
        }

        private static Text CreateText(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.AddComponent<Text>();
        }

        private static void RequireMultiplier(
            ScoreManager scoreManager,
            ScoreUI scoreUi,
            float expectedMultiplier,
            string expectedText)
        {
            Require(Mathf.Approximately(scoreManager.ComboMultiplier, expectedMultiplier),
                $"Expected multiplier {expectedMultiplier:0.0} but found {scoreManager.ComboMultiplier:0.0}.");
            Require(scoreUi.DisplayedCombo == expectedText,
                $"Expected UI '{expectedText}' but found '{scoreUi.DisplayedCombo}'.");
        }

        private static UrinalController[] CreateUrinals(Transform root)
        {
            var result = new UrinalController[8];
            for (int index = 0; index < result.Length; index++)
            {
                var urinalObject = new GameObject($"Urinal{index + 1:00}");
                urinalObject.transform.SetParent(root, false);
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.transform.SetParent(urinalObject.transform, false);
                var highlight = new GameObject("Highlight");
                highlight.transform.SetParent(urinalObject.transform, false);
                var urinal = urinalObject.AddComponent<UrinalController>();
                urinal.Configure(index + 1, Point(urinalObject.transform), Point(urinalObject.transform),
                    Point(urinalObject.transform), highlight, body.GetComponent<Renderer>());
                result[index] = urinal;
            }

            return result;
        }

        private static void Occupy(UrinalController urinal, NPCController npc)
        {
            Require(urinal.Reserve(npc) && urinal.Occupy(npc), "Validation urinal occupation failed.");
        }

        private static NPCController CreateNpc(Transform root, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root, false);
            child.AddComponent<NPCMovement>();
            return child.AddComponent<NPCController>();
        }

        private static Transform Point(Transform parent)
        {
            var point = new GameObject("Point");
            point.transform.SetParent(parent, false);
            return point.transform;
        }

        private static T Add<T>(Transform root, string name) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(root, false);
            return child.AddComponent<T>();
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
