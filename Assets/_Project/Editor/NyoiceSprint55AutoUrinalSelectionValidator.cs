using System;
using System.Reflection;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint55AutoUrinalSelectionValidator
    {
        private static readonly MethodInfo QueueSlotReached = GetNpcMethod("HandleQueueSlotReached");

        [MenuItem("Nyoice/Validate Sprint5-5 Auto Urinal Selection")]
        public static void ValidateAutoUrinalSelection()
        {
            ValidatePriorityAndFixedAssignment();
            ValidatePlayerOverrideAndFailureSafety();
            ValidateDecisionPointAndGameOverLocks();
            ValidateFifoAndNormalRoute();
            Debug.Log("Sprint5-5 Auto Urinal Selection validation passed.");
        }

        private static void ValidatePriorityAndFixedAssignment()
        {
            using (var fixture = new Fixture("AllAvailable"))
            {
                NPCController npc = fixture.Enqueue("NPC_001");
                RequireAssigned(npc, fixture.Urinals[7], "All available did not choose Urinal08.");
            }

            using (var fixture = new Fixture("EightOccupied"))
            {
                NPCController blocker = fixture.CreateNpc("Blocker");
                Require(fixture.Urinals[7].Reserve(blocker) && fixture.Urinals[7].Occupy(blocker),
                    "Could not prepare occupied Urinal08.");
                NPCController npc = fixture.Enqueue("NPC_001");
                RequireAssigned(npc, fixture.Urinals[6], "Occupied Urinal08 did not fall back to Urinal07.");
                Require(fixture.Urinals[7].Release(blocker), "Could not release occupied Urinal08.");
                RequireAssigned(npc, fixture.Urinals[6], "Assignment changed merely because Urinal08 became free.");

                NPCController next = fixture.Enqueue("NPC_002");
                Require(fixture.Queue.NotifySelectionZoneCrossed(npc),
                    "Could not pass selection control to the next FIFO NPC.");
                RequireAssigned(next, fixture.Urinals[7], "Next NPC did not reserve newly available Urinal08.");
            }

            using (var fixture = new Fixture("EightReserved"))
            {
                NPCController blocker = fixture.CreateNpc("Blocker");
                Require(fixture.Urinals[7].Reserve(blocker), "Could not prepare reserved Urinal08.");
                NPCController npc = fixture.Enqueue("NPC_001");
                RequireAssigned(npc, fixture.Urinals[6], "Reserved Urinal08 did not fall back to Urinal07.");
            }
        }

        private static void ValidatePlayerOverrideAndFailureSafety()
        {
            using (var fixture = new Fixture("Override"))
            {
                NPCController npc = fixture.Enqueue("NPC_001");
                UrinalController oldUrinal = fixture.Urinals[7];
                UrinalController replacement = fixture.Urinals[4];
                Require(fixture.UrinalManager.TryChangeAssignment(npc, replacement),
                    "Pre-DecisionPoint assignment change failed.");
                RequireAssigned(npc, replacement, "Replacement reservation was not committed.");
                Require(oldUrinal.IsAvailable && oldUrinal.ReservedBy == null,
                    "Old urinal was not released after assignment change.");
                Require(fixture.UrinalManager.CurrentSelection == replacement && replacement.IsSelected,
                    "Automatic/current assignment is not visible through selection highlighting.");

                NPCController blocker = fixture.CreateNpc("Blocker");
                Require(fixture.Urinals[3].Reserve(blocker), "Could not prepare reservation conflict.");
                Require(!fixture.UrinalManager.TryChangeAssignment(npc, fixture.Urinals[3]),
                    "Conflicting replacement reservation unexpectedly succeeded.");
                RequireAssigned(npc, replacement, "Failed replacement did not preserve the old assignment.");
                Require(fixture.UrinalManager.SelectUrinal(replacement),
                    "Selecting the same urinal should be a no-op success.");
            }
        }

        private static void ValidateDecisionPointAndGameOverLocks()
        {
            using (var fixture = new Fixture("DecisionPointLock"))
            {
                NPCController npc = fixture.Enqueue("NPC_001");
                npc.HandleDecisionPointReached();
                npc.BeginUrinalApproach(fixture.ApproachPoint.position, fixture.CrossingTarget.position);
                Require(npc.State == NPCState.ApproachingLine, "Could not simulate DecisionPoint passage.");
                Require(!fixture.UrinalManager.TryChangeAssignment(npc, fixture.Urinals[5]),
                    "Assignment changed after DecisionPoint passage.");
            }

            using (var fixture = new Fixture("GameOverLock"))
            {
                NPCController npc = fixture.Enqueue("NPC_001");
                UrinalController assigned = npc.TargetUrinal;
                fixture.GameState.TriggerGameOver();
                Require(!fixture.UrinalManager.IsInputEnabled && fixture.UrinalManager.ActiveSelectionNpc == null,
                    "GameOver did not disable selection input.");
                Require(!fixture.UrinalManager.TryChangeAssignment(npc, fixture.Urinals[5]),
                    "Assignment changed after GameOver.");
                int pendingBefore = fixture.Queue.PendingNpcs.Count;
                fixture.Enqueue("NPC_002");
                Require(fixture.Queue.PendingNpcs.Count == pendingBefore,
                    "Automatic selection or enqueue progressed after GameOver.");
                RequireAssigned(npc, assigned, "GameOver unexpectedly changed the existing reservation.");
            }
        }

        private static void ValidateFifoAndNormalRoute()
        {
            using (var fixture = new Fixture("FifoAndRoute"))
            {
                NPCController first = fixture.Enqueue("NPC_001");
                NPCController second = fixture.Enqueue("NPC_002");
                Require(fixture.Queue.PendingNpcs.Count == 2 &&
                        fixture.Queue.PendingNpcs[0] == first && fixture.Queue.PendingNpcs[1] == second,
                    "FIFO pending order changed.");
                Require(fixture.Queue.SelectionZoneOccupant == first && second.TargetUrinal == null,
                    "A later NPC bypassed selection serialization.");
                Require(first.TargetUrinal != second.TargetUrinal,
                    "Multiple NPCs reserved the same urinal.");

                NPCMovement movement = first.GetComponent<NPCMovement>();
                Require(movement.IsMoving && movement.TargetPosition != first.TargetUrinal.MovePoint.position,
                    "Automatic assignment created a Spawn-to-urinal shortcut.");
                for (int expected = 7; expected >= 1; expected--)
                {
                    QueueSlotReached.Invoke(first, null);
                    Require(first.CurrentSlot != null && first.CurrentSlot.QueueNumber == expected,
                        $"Normal queue route did not reach Queue{expected:00}.");
                }

                QueueSlotReached.Invoke(first, null);
                Require(movement.TargetPosition == fixture.DecisionPoint.position,
                    "Normal route did not continue from Queue01 to DecisionPoint.");
            }
        }

        private static void RequireAssigned(NPCController npc, UrinalController urinal, string message)
        {
            Require(npc.TargetUrinal == urinal && urinal.State == UrinalState.Reserved &&
                    urinal.ReservedBy == npc, message);
        }

        private static MethodInfo GetNpcMethod(string name)
        {
            MethodInfo method = typeof(NPCController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
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

        private sealed class Fixture : IDisposable
        {
            public Fixture(string name)
            {
                Root = new GameObject($"Sprint55_{name}");
                Root.SetActive(false);
                Transform queueRoot = Child(Root.transform, "Queue");
                var slots = new QueueSlot[8];
                for (int index = 0; index < slots.Length; index++)
                {
                    Transform slot = Point(queueRoot, $"Queue{index + 1:00}", Vector3.right * index);
                    slots[index] = slot.gameObject.AddComponent<QueueSlot>();
                    slots[index].Initialize(index + 1);
                }

                DecisionPoint = Point(queueRoot, "DecisionPoint", Vector3.left);
                ApproachPoint = Point(queueRoot, "NyoiceApproachPoint", Vector3.left * 2f);
                CrossingTarget = Point(Root.transform, "CrossingTarget", Vector3.left * 3f);
                Urinals = CreateUrinals(Root.transform);
                GameState = Add<GameStateManager>("GameState");
                UrinalManager = Add<UrinalManager>("UrinalManager");
                UrinalManager.Configure(Urinals, null, null);
                UrinalManager.ConfigureGameState(GameState);
                UrinalTicketManager tickets = Add<UrinalTicketManager>("Tickets");
                tickets.Configure(8);
                tickets.ConfigureGameState(GameState);
                Queue = Add<QueueManager>("QueueManager");
                Queue.Configure(slots, DecisionPoint);
                Queue.ConfigureUrinalFlow(UrinalManager, tickets, ApproachPoint, CrossingTarget);
                Queue.ConfigureGameState(GameState);
            }

            public GameObject Root { get; }
            public UrinalController[] Urinals { get; }
            public GameStateManager GameState { get; }
            public UrinalManager UrinalManager { get; }
            public QueueManager Queue { get; }
            public Transform DecisionPoint { get; }
            public Transform ApproachPoint { get; }
            public Transform CrossingTarget { get; }

            public NPCController Enqueue(string name)
            {
                NPCController npc = CreateNpc(name);
                Queue.Enqueue(npc);
                return npc;
            }

            public NPCController CreateNpc(string name)
            {
                GameObject child = new GameObject(name);
                child.transform.SetParent(Root.transform, false);
                child.AddComponent<NPCMovement>();
                return child.AddComponent<NPCController>();
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }

            private T Add<T>(string name) where T : Component
            {
                return Child(Root.transform, name).gameObject.AddComponent<T>();
            }

            private static UrinalController[] CreateUrinals(Transform parent)
            {
                var result = new UrinalController[8];
                for (int index = 0; index < result.Length; index++)
                {
                    Transform root = Child(parent, $"Urinal{index + 1:00}");
                    Transform move = Point(root, "MovePoint", Vector3.forward);
                    Transform use = Point(root, "UsePoint", Vector3.forward * 2f);
                    Transform exit = Point(root, "ExitStartPoint", Vector3.back);
                    GameObject highlight = Child(root, "Highlight").gameObject;
                    var urinal = root.gameObject.AddComponent<UrinalController>();
                    urinal.Configure(index + 1, move, use, exit, highlight, null);
                    result[index] = urinal;
                }

                return result;
            }

            private static Transform Child(Transform parent, string name)
            {
                var child = new GameObject(name);
                child.transform.SetParent(parent, false);
                return child.transform;
            }

            private static Transform Point(Transform parent, string name, Vector3 position)
            {
                Transform point = Child(parent, name);
                point.position = position;
                return point;
            }
        }
    }
}
