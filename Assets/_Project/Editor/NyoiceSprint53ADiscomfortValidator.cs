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
    public static class NyoiceSprint53ADiscomfortValidator
    {
        [MenuItem("Nyoice/Validate Sprint5-3A Discomfort Flow")]
        public static void ValidateDiscomfortFlow()
        {
            var root = new GameObject("Sprint53ADiscomfortValidation");
            root.SetActive(false);

            try
            {
                RunValidation(root.transform);
                Debug.Log("Sprint 5-3A discomfort flow validation passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void RunValidation(Transform root)
        {
            GameStateManager gameStateManager = CreateChildComponent<GameStateManager>(
                root,
                "GameStateManager");
            UrinalController[] urinals = CreateUrinals(root);

            DiscomfortManager discomfortManager = CreateChildComponent<DiscomfortManager>(
                root,
                "DiscomfortManager");
            discomfortManager.Configure(urinals, gameStateManager);
            discomfortManager.ConfigureRate(10f);

            Require(
                Mathf.Approximately(discomfortManager.CurrentDiscomfort, 0f),
                "Initial discomfort is not zero.");
            Require(
                Mathf.Approximately(discomfortManager.MaxDiscomfort, 100f),
                "Maximum discomfort is not 100.");
            Require(discomfortManager.CountAdjacentPairs() == 0, "Empty urinals have adjacent pairs.");

            discomfortManager.AdvanceTime(1f);
            Require(
                Mathf.Approximately(discomfortManager.CurrentDiscomfort, 0f),
                "Discomfort increased without an adjacent pair.");

            NPCController firstNpc = CreateNpc(root, "NPC_001");
            NPCController secondNpc = CreateNpc(root, "NPC_002");
            Occupy(urinals[0], firstNpc);
            Occupy(urinals[1], secondNpc);
            Require(discomfortManager.CountAdjacentPairs() == 1, "Urinal01-02 is not one adjacent pair.");

            discomfortManager.AdvanceTime(1f);
            Require(
                Mathf.Approximately(discomfortManager.CurrentDiscomfort, 10f),
                "One pair did not add ten points in one second.");

            Require(urinals[1].Release(secondNpc), "Urinal02 could not be released.");
            float beforeResolvedAdvance = discomfortManager.CurrentDiscomfort;
            discomfortManager.AdvanceTime(1f);
            Require(
                Mathf.Approximately(
                    discomfortManager.CurrentDiscomfort,
                    beforeResolvedAdvance),
                "Discomfort increased after adjacency was resolved.");
            Occupy(urinals[1], secondNpc);

            NPCController thirdNpc = CreateNpc(root, "NPC_003");
            Occupy(urinals[2], thirdNpc);
            Require(discomfortManager.CountAdjacentPairs() == 2, "Urinal01-03 is not two adjacent pairs.");

            float beforeTwoPairAdvance = discomfortManager.CurrentDiscomfort;
            discomfortManager.AdvanceTime(1f);
            Require(
                Mathf.Approximately(
                    discomfortManager.CurrentDiscomfort - beforeTwoPairAdvance,
                    20f),
                "Two pairs did not add twenty points in one second.");

            NPCController reservedNpc = CreateNpc(root, "NPC_004");
            Require(urinals[3].Reserve(reservedNpc), "Urinal04 could not be Reserved for validation.");
            Require(
                discomfortManager.CountAdjacentPairs() == 2,
                "Reserved urinal was counted as an adjacent occupied urinal.");

            NPCMovement movingNpc = CreateNpc(root, "MovingNPC").GetComponent<NPCMovement>();
            movingNpc.ConfigureGameState(gameStateManager);
            movingNpc.MoveTo(Vector3.one, null);
            Require(movingNpc.IsMoving, "NPCMovement did not start before GameOver.");

            NPCSpawner spawner = CreateChildComponent<NPCSpawner>(root, "NPCSpawner");
            spawner.ConfigureGameState(gameStateManager);
            QueueManager queueManager = CreateChildComponent<QueueManager>(root, "QueueManager");
            queueManager.ConfigureGameState(gameStateManager);
            UrinalManager urinalManager = CreateChildComponent<UrinalManager>(root, "UrinalManager");
            urinalManager.Configure(urinals, null, null);
            urinalManager.ConfigureGameState(gameStateManager);
            UrinalTicketManager ticketManager = CreateChildComponent<UrinalTicketManager>(
                root,
                "UrinalTicketManager");
            ticketManager.Configure(8);
            ticketManager.ConfigureGameState(gameStateManager);

            NPCController selectionNpc = CreateNpc(root, "SelectionNPC");
            Require(urinalManager.BeginSelection(selectionNpc), "Selection did not work before GameOver.");
            Require(urinalManager.SelectUrinal(urinals[7]), "Urinal selection failed before GameOver.");

            DiscomfortUI discomfortUI = CreateUi(
                root,
                discomfortManager,
                gameStateManager,
                out Text discomfortText,
                out Slider discomfortSlider,
                out Text gameOverText);

            int gameOverEventCount = 0;
            gameStateManager.GameOver += () => gameOverEventCount++;
            discomfortManager.AdvanceTime(10f);

            Require(
                Mathf.Approximately(discomfortManager.CurrentDiscomfort, 100f),
                "Discomfort was not clamped to 100.");
            Require(gameStateManager.IsGameOver, "Maximum discomfort did not trigger GameOver.");
            Require(gameOverEventCount == 1, "GameOver event did not fire exactly once.");
            Require(!gameStateManager.TriggerGameOver(), "GameOver triggered twice.");
            Require(gameOverEventCount == 1, "Duplicate trigger fired GameOver again.");

            Require(spawner.IsSpawningBlocked && !spawner.enabled, "NPCSpawner did not stop.");
            Require(queueManager.IsProgressionBlocked, "Queue progression is not blocked.");
            Require(!urinalManager.IsInputEnabled, "Urinal input is still enabled.");
            Require(urinalManager.ActiveSelectionNpc == null, "Active selection remained after GameOver.");
            Require(urinalManager.CurrentSelection == null, "Highlight selection remained after GameOver.");
            Require(!ticketManager.CanAcquireTickets, "Ticket acquisition is still enabled.");
            Require(
                !ticketManager.TryAcquireTicket(CreateNpc(root, "TicketNPC")),
                "Ticket was acquired after GameOver.");
            Require(!movingNpc.IsMoving, "NPCMovement continued after GameOver.");
            Require(movingNpc.IsMovementBlocked, "NPCMovement is not marked blocked.");

            NPCController stoppedNpc = CreateNpc(root, "StoppedNPC");
            stoppedNpc.ConfigureGameState(gameStateManager);
            stoppedNpc.HandleDecisionPointReached();
            Require(stoppedNpc.State == NPCState.Queue, "NPC entered a new state after GameOver.");

            discomfortUI.Refresh();
            Require(discomfortText.text == "DISCOMFORT 100 / 100", "UI text is not synchronized.");
            Require(Mathf.Approximately(discomfortSlider.value, 100f), "UI Slider is not at 100.");
            Require(gameOverText.gameObject.activeSelf, "GameOverText is not visible.");
            Require(discomfortUI.IsGameOverVisible, "UI does not report GameOver visibility.");

            discomfortManager.AdvanceTime(1f);
            Require(
                Mathf.Approximately(discomfortManager.CurrentDiscomfort, 100f),
                "Discomfort changed after GameOver.");
        }

        private static UrinalController[] CreateUrinals(Transform root)
        {
            var urinals = new UrinalController[8];
            for (int index = 0; index < urinals.Length; index++)
            {
                var urinalObject = new GameObject($"Urinal{index + 1:00}");
                urinalObject.transform.SetParent(root, false);
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(urinalObject.transform, false);
                var highlight = new GameObject("Highlight");
                highlight.transform.SetParent(urinalObject.transform, false);
                highlight.SetActive(false);

                UrinalController urinal = urinalObject.AddComponent<UrinalController>();
                urinal.Configure(
                    index + 1,
                    CreatePoint(urinalObject.transform, "MovePoint"),
                    CreatePoint(urinalObject.transform, "UsePoint"),
                    CreatePoint(urinalObject.transform, "ExitStartPoint"),
                    highlight,
                    body.GetComponent<Renderer>());
                urinals[index] = urinal;
            }

            return urinals;
        }

        private static DiscomfortUI CreateUi(
            Transform root,
            DiscomfortManager discomfortManager,
            GameStateManager gameStateManager,
            out Text discomfortText,
            out Slider discomfortSlider,
            out Text gameOverText)
        {
            var uiObject = new GameObject("DiscomfortUI", typeof(RectTransform));
            uiObject.transform.SetParent(root, false);
            discomfortText = CreateUiComponent<Text>(uiObject.transform, "DiscomfortText");
            discomfortSlider = CreateUiComponent<Slider>(uiObject.transform, "DiscomfortSlider");
            gameOverText = CreateUiComponent<Text>(uiObject.transform, "GameOverText");
            gameOverText.gameObject.SetActive(false);

            DiscomfortUI ui = uiObject.AddComponent<DiscomfortUI>();
            ui.Configure(
                discomfortManager,
                gameStateManager,
                discomfortText,
                discomfortSlider,
                gameOverText);
            return ui;
        }

        private static T CreateUiComponent<T>(Transform parent, string objectName)
            where T : Component
        {
            var child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.AddComponent<T>();
        }

        private static void Occupy(UrinalController urinal, NPCController npc)
        {
            Require(urinal.Reserve(npc), $"Urinal{urinal.UrinalNumber:00} reservation failed.");
            Require(urinal.Occupy(npc), $"Urinal{urinal.UrinalNumber:00} occupation failed.");
        }

        private static NPCController CreateNpc(Transform root, string objectName)
        {
            var npcObject = new GameObject(objectName);
            npcObject.transform.SetParent(root, false);
            npcObject.AddComponent<NPCMovement>();
            return npcObject.AddComponent<NPCController>();
        }

        private static Transform CreatePoint(Transform parent, string objectName)
        {
            var point = new GameObject(objectName);
            point.transform.SetParent(parent, false);
            return point.transform;
        }

        private static T CreateChildComponent<T>(Transform root, string objectName)
            where T : Component
        {
            var child = new GameObject(objectName);
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
