using System;
using System.Collections.Generic;
using System.Reflection;
using Nyoice.Managers;
using Nyoice.NPC;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    /// <summary>
    /// Runs a package-free Editor validation of the Sprint 3 queue state transitions.
    /// </summary>
    public static class NyoiceQueueProgressionValidator
    {
        private const int QueueSlotCount = 8;
        private const int TestNpcCount = 9;
        private const int MaxSimulationPasses = 128;

        private static readonly MethodInfo QueueSlotReachedMethod = GetNpcMethod("HandleQueueSlotReached");
        private static readonly MethodInfo DecisionPointReachedMethod = GetNpcMethod("HandleDecisionPointReached");

        [MenuItem("Nyoice/Validate NPC Queue Progression")]
        public static void ValidateQueueProgression()
        {
            var testRoot = new GameObject("NyoiceQueueProgressionValidation");

            try
            {
                Transform decisionPoint = CreatePoint("DecisionPoint", testRoot.transform, new Vector3(6.5f, -3.75f, 0f));
                QueueSlot[] slots = CreateQueueSlots(testRoot.transform);
                QueueManager manager = CreateQueueManager(testRoot.transform, slots, decisionPoint);
                List<NPCController> npcs = CreateAndEnqueueNpcs(testRoot.transform, manager);

                SimulateAllArrivals(npcs, decisionPoint);
                AssertFinalState(npcs, slots, manager, decisionPoint);

                Debug.Log("Nyoice queue progression validation passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testRoot);
            }
        }

        private static QueueSlot[] CreateQueueSlots(Transform parent)
        {
            var slots = new QueueSlot[QueueSlotCount];
            for (int index = 0; index < slots.Length; index++)
            {
                Transform point = CreatePoint(
                    $"Queue{index + 1:00}",
                    parent,
                    new Vector3(7f, -3.75f + index, 0f));
                QueueSlot slot = point.gameObject.AddComponent<QueueSlot>();
                slot.Initialize(index + 1);
                slots[index] = slot;
            }

            return slots;
        }

        private static QueueManager CreateQueueManager(
            Transform parent,
            QueueSlot[] slots,
            Transform decisionPoint)
        {
            var managerObject = new GameObject("QueueManager");
            managerObject.SetActive(false);
            managerObject.transform.SetParent(parent, false);
            QueueManager manager = managerObject.AddComponent<QueueManager>();
            manager.Configure(slots, decisionPoint);
            managerObject.SetActive(true);
            return manager;
        }

        private static List<NPCController> CreateAndEnqueueNpcs(Transform parent, QueueManager manager)
        {
            var npcs = new List<NPCController>(TestNpcCount);
            for (int index = 0; index < TestNpcCount; index++)
            {
                var npcObject = new GameObject($"NPC_{index + 1:000}");
                npcObject.SetActive(false);
                npcObject.transform.SetParent(parent, false);
                npcObject.AddComponent<NPCMovement>();
                NPCController npc = npcObject.AddComponent<NPCController>();
                npcObject.SetActive(true);
                manager.Enqueue(npc);
                npcs.Add(npc);
            }

            return npcs;
        }

        private static void SimulateAllArrivals(
            IReadOnlyList<NPCController> npcs,
            Transform decisionPoint)
        {
            for (int pass = 0; pass < MaxSimulationPasses; pass++)
            {
                bool simulatedArrival = false;

                foreach (NPCController npc in npcs)
                {
                    if (!npc.IsPresentationVisible)
                    {
                        continue;
                    }

                    if (npc.CurrentSlot != null && !npc.IsWaitingAtSlot)
                    {
                        npc.transform.position = npc.CurrentSlot.transform.position;
                        QueueSlotReachedMethod.Invoke(npc, null);
                        simulatedArrival = true;
                        continue;
                    }

                    if (npc.CurrentSlot == null && !npc.IsWaitingForDecision)
                    {
                        npc.transform.position = decisionPoint.position;
                        DecisionPointReachedMethod.Invoke(npc, null);
                        simulatedArrival = true;
                    }
                }

                if (!simulatedArrival)
                {
                    return;
                }
            }

            throw new InvalidOperationException("Queue progression did not settle within the simulation limit.");
        }

        private static void AssertFinalState(
            IReadOnlyList<NPCController> npcs,
            IReadOnlyList<QueueSlot> slots,
            QueueManager manager,
            Transform decisionPoint)
        {
            Require(npcs[0].IsWaitingForDecision, "The first NPC did not reach DecisionPoint.");
            Require(npcs[0].transform.position == decisionPoint.position, "The first NPC is not at DecisionPoint.");
            Require(
                npcs[1].CurrentSlot == slots[0] && npcs[1].IsWaitingAtSlot,
                "The second NPC did not reach Queue01.");

            var occupants = new HashSet<NPCController>();
            foreach (QueueSlot slot in slots)
            {
                if (slot.Occupant != null)
                {
                    Require(occupants.Add(slot.Occupant), "An NPC occupies more than one QueueSlot.");
                    Require(slot.Occupant.CurrentSlot == slot, "QueueSlot and NPC assignment disagree.");
                }
            }

            var visiblePositions = new HashSet<Vector3>();
            int visibleCount = 0;
            foreach (NPCController npc in npcs)
            {
                if (!npc.IsPresentationVisible)
                {
                    continue;
                }

                visibleCount++;
                Require(visiblePositions.Add(npc.transform.position), "Visible NPCs share the same position.");
            }

            Require(visibleCount == 8, "The visible NPC count is not exactly eight.");
            Require(manager.InternalWaitingList.Count == 1, "The ninth NPC is not waiting internally.");
            Require(!npcs[8].IsPresentationVisible, "The ninth NPC is visible.");
        }

        private static Transform CreatePoint(string name, Transform parent, Vector3 position)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent, false);
            point.transform.position = position;
            return point.transform;
        }

        private static MethodInfo GetNpcMethod(string methodName)
        {
            MethodInfo method = typeof(NPCController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(typeof(NPCController).FullName, methodName);
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

