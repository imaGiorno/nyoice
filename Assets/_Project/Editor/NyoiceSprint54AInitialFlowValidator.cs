using System;
using System.Reflection;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint54AInitialFlowValidator
    {
        private static readonly MethodInfo MovePointReachedMethod = GetNpcMethod("HandleMovePointReached");
        private static readonly MethodInfo UsePointReachedMethod = GetNpcMethod("HandleUsePointReached");
        private static readonly MethodInfo QueueSlotReachedMethod = GetNpcMethod("HandleQueueSlotReached");
        private static readonly MethodInfo ApproachPointReachedMethod = GetNpcMethod("HandleApproachPointReached");

        [MenuItem("Nyoice/Validate Sprint5-4A Initial Flow")]
        public static void ValidateInitialFlow()
        {
            var root = new GameObject("Sprint54AInitialFlowValidation");
            root.SetActive(false);

            try
            {
                Transform queueRoot = CreateChild(root.transform, "Queue");
                QueueSlot[] slots = CreateQueueSlots(queueRoot);
                Transform decisionPoint = CreatePoint(queueRoot, "DecisionPoint", Vector3.zero);
                Transform approachPoint = CreatePoint(queueRoot, "NyoiceApproachPoint", Vector3.left);
                Transform crossingTarget = CreatePoint(root.transform, "CrossingTarget", Vector3.left * 2f);
                Transform exitPoint = CreatePoint(root.transform, "ExitPoint", Vector3.right * 5f);

                GameStateManager gameState = Add<GameStateManager>(root.transform, "GameState");
                UrinalController[] urinals = CreateUrinals(root.transform);
                UrinalManager urinalManager = Add<UrinalManager>(root.transform, "UrinalManager");
                urinalManager.Configure(urinals, null, null);
                urinalManager.ConfigureGameState(gameState);
                UrinalTicketManager ticketManager = Add<UrinalTicketManager>(root.transform, "TicketManager");
                ticketManager.Configure(urinals.Length);
                ticketManager.ConfigureGameState(gameState);
                DiscomfortManager discomfort = Add<DiscomfortManager>(root.transform, "DiscomfortManager");
                discomfort.Configure(urinals, gameState);
                ScoreManager scoreManager = Add<ScoreManager>(root.transform, "ScoreManager");
                scoreManager.Configure(discomfort, gameState);

                QueueManager queueManager = Add<QueueManager>(root.transform, "QueueManager");
                queueManager.Configure(slots, decisionPoint);
                queueManager.ConfigureUrinalFlow(
                    urinalManager,
                    ticketManager,
                    approachPoint,
                    crossingTarget);
                queueManager.ConfigureExitFlow(exitPoint);
                queueManager.ConfigureGameState(gameState);
                queueManager.ConfigureScore(scoreManager);

                Require(queueManager.HasInitialFlowReferences, "Initial-flow references are incomplete.");
                Require(!scoreManager.IsComboTimingStarted, "Combo timing started before the first urinal use.");
                scoreManager.AdvanceTime(30f);
                Require(Mathf.Approximately(scoreManager.NoAdjacencyElapsed, 0f),
                    "Combo elapsed time advanced before the first urinal use.");
                Require(Mathf.Approximately(scoreManager.ComboMultiplier, 1f),
                    "Initial combo multiplier is not x1.0.");

                NPCController firstNpc = CreateNpc(root.transform, "NPC_001");
                queueManager.Enqueue(firstNpc);
                Require(queueManager.SelectionZoneOccupant == firstNpc,
                    "The first NPC cannot accept urinal selection at spawn.");
                Require(urinalManager.ActiveSelectionNpc == firstNpc,
                    "Urinal input was not assigned to the spawned NPC.");
                Require(firstNpc.State == NPCState.Queue && firstNpc.CanAcceptUrinalSelection,
                    "The spawned NPC cannot select while preserving its queue state.");
                Require(firstNpc.TargetUrinal == null, "Spawn or ticket acquisition selected a urinal prematurely.");
                Require(!scoreManager.IsComboTimingStarted,
                    "Spawn or selection readiness started combo timing.");

                NPCController secondNpc = CreateNpc(root.transform, "NPC_002");
                queueManager.Enqueue(secondNpc);
                Require(queueManager.PendingNpcs.Count == 2 &&
                        queueManager.PendingNpcs[0] == firstNpc &&
                        queueManager.PendingNpcs[1] == secondNpc,
                    "Queue registration did not preserve FIFO order.");
                Require(queueManager.SelectionZoneOccupant == firstNpc,
                    "A newer NPC replaced the oldest selection candidate.");

                UrinalController selectedUrinal = urinals[7];
                Require(urinalManager.SelectUrinal(selectedUrinal), "Urinal08 could not be selected.");
                Require(urinalManager.ConfirmActiveSelection(), "The selected urinal could not be assigned.");
                Require(firstNpc.TargetUrinal == selectedUrinal && selectedUrinal.ReservedBy == firstNpc,
                    "The spawn-time urinal selection was not reflected on the NPC.");
                Require(firstNpc.State == NPCState.Queue,
                    "Urinal assignment incorrectly removed the NPC from its queue route.");
                NPCMovement movement = firstNpc.GetComponent<NPCMovement>();
                Require(movement.IsMoving && movement.TargetPosition != selectedUrinal.MovePoint.position,
                    "Urinal assignment created a Spawn-to-MovePoint shortcut.");
                Require(queueManager.SelectionZoneOccupant == secondNpc,
                    "The oldest remaining NPC did not receive the next selection opportunity.");
                NPCController thirdNpc = CreateNpc(root.transform, "NPC_003");
                queueManager.Enqueue(thirdNpc);
                Require(queueManager.SelectionZoneOccupant == secondNpc &&
                        queueManager.PendingNpcs[2] == thirdNpc,
                    "A newly spawned NPC overtook an older waiting NPC.");
                Require(!scoreManager.IsComboTimingStarted,
                    "Urinal reservation started combo timing before use began.");

                QueueSlotReachedMethod.Invoke(firstNpc, null);
                Require(firstNpc.CurrentSlot != null && firstNpc.CurrentSlot.QueueNumber == 7,
                    "The first NPC did not advance from Queue08 to Queue07.");
                Require(secondNpc.CurrentSlot != null && secondNpc.CurrentSlot.QueueNumber == 8,
                    "The second NPC was not admitted to the released Queue08 slot.");
                Require(firstNpc.CurrentSlot != secondNpc.CurrentSlot,
                    "Multiple NPCs were assigned to the same queue slot.");

                for (int expectedQueue = 6; expectedQueue >= 1; expectedQueue--)
                {
                    QueueSlotReachedMethod.Invoke(firstNpc, null);
                    Require(firstNpc.CurrentSlot != null && firstNpc.CurrentSlot.QueueNumber == expectedQueue,
                        $"The first NPC did not compact to Queue{expectedQueue:00}.");
                }

                QueueSlotReachedMethod.Invoke(firstNpc, null);
                Require(movement.TargetPosition == decisionPoint.position,
                    "The first NPC did not travel from Queue01 to DecisionPoint.");
                firstNpc.HandleDecisionPointReached();
                Require(firstNpc.State == NPCState.ApproachingLine &&
                        movement.TargetPosition == approachPoint.position &&
                        queueManager.ApproachRouteOccupant == firstNpc,
                    "The reserved NPC did not continue from DecisionPoint to ApproachPoint.");
                ApproachPointReachedMethod.Invoke(firstNpc, null);
                Require(firstNpc.State == NPCState.WalkingToUrinal &&
                        movement.TargetPosition == selectedUrinal.MovePoint.position &&
                        queueManager.ApproachRouteOccupant == null,
                    "The NPC did not follow ApproachPoint before its urinal MovePoint.");

                MovePointReachedMethod.Invoke(firstNpc, null);
                Require(movement.TargetPosition == selectedUrinal.UsePoint.position,
                    "The NPC did not continue from MovePoint to UsePoint.");
                UsePointReachedMethod.Invoke(firstNpc, null);
                Require(firstNpc.State == NPCState.UsingUrinal && selectedUrinal.IsOccupied,
                    "The NPC did not enter urinal use after reaching UsePoint.");
                Require(scoreManager.IsComboTimingStarted,
                    "The first urinal use did not start combo timing.");
                scoreManager.AdvanceTime(5f);
                Require(Mathf.Approximately(scoreManager.ComboMultiplier, 1.5f),
                    "Combo timing did not advance after the first urinal use.");

                scoreManager.ResetSession();
                Require(!scoreManager.IsComboTimingStarted &&
                        Mathf.Approximately(scoreManager.NoAdjacencyElapsed, 0f) &&
                        Mathf.Approximately(scoreManager.ComboMultiplier, 1f),
                    "ResetSession did not reset combo timing state.");

                Debug.Log("Sprint5-4A Initial Flow validation passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static QueueSlot[] CreateQueueSlots(Transform parent)
        {
            var slots = new QueueSlot[8];
            for (int index = 0; index < slots.Length; index++)
            {
                Transform child = CreatePoint(parent, $"Queue{index + 1:00}", Vector3.right * index);
                slots[index] = child.gameObject.AddComponent<QueueSlot>();
                slots[index].Initialize(index + 1);
            }

            return slots;
        }

        private static UrinalController[] CreateUrinals(Transform parent)
        {
            var urinals = new UrinalController[8];
            for (int index = 0; index < urinals.Length; index++)
            {
                Transform urinalRoot = CreateChild(parent, $"Urinal{index + 1:00}");
                Transform movePoint = CreatePoint(urinalRoot, "MovePoint", Vector3.right * (index + 1));
                Transform usePoint = CreatePoint(urinalRoot, "UsePoint", Vector3.right * (index + 1));
                Transform exitStartPoint = CreatePoint(urinalRoot, "ExitStartPoint", Vector3.up);
                var highlight = new GameObject("Highlight");
                highlight.transform.SetParent(urinalRoot, false);
                UrinalController urinal = urinalRoot.gameObject.AddComponent<UrinalController>();
                urinal.Configure(index + 1, movePoint, usePoint, exitStartPoint, highlight, null);
                urinals[index] = urinal;
            }

            return urinals;
        }

        private static NPCController CreateNpc(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<NPCMovement>();
            return child.AddComponent<NPCController>();
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 position)
        {
            Transform point = CreateChild(parent, name);
            point.position = position;
            return point;
        }

        private static T Add<T>(Transform parent, string name) where T : Component
        {
            return CreateChild(parent, name).gameObject.AddComponent<T>();
        }

        private static MethodInfo GetNpcMethod(string name)
        {
            MethodInfo method = typeof(NPCController).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(typeof(NPCController).FullName, name);
            }

            return method;
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
