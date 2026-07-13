using System.Collections.Generic;
using Nyoice.NPC;
using UnityEngine;

namespace Nyoice.Managers
{
    /// <summary>
    /// Owns the eight visible queue slots and the unlimited internal waiting list.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QueueManager : MonoBehaviour
    {
        private const int MaxVisibleNpcCount = 8;

        [SerializeField]
        private QueueSlot[] queueSlots;

        [SerializeField]
        private Transform decisionPoint;

        private readonly List<NPCController> _internalWaitingList = new List<NPCController>();
        private NPCController _decisionPointOccupant;
        private bool _hasLoggedInitializationError;

        public IReadOnlyList<NPCController> InternalWaitingList => _internalWaitingList;

        private void Awake()
        {
            EnsureRuntimeReferences();
        }

        public void Configure(QueueSlot[] slots, Transform decisionPointTransform)
        {
            queueSlots = slots;
            decisionPoint = decisionPointTransform;
        }

        public void Enqueue(NPCController npc)
        {
            npc.Initialize(this);

            if (!EnsureRuntimeReferences())
            {
                npc.WaitInternally();
                _internalWaitingList.Add(npc);
                return;
            }

            QueueSlot entrySlot = FindLastAvailableSlot();
            if (entrySlot == null || GetVisibleNpcCount() >= MaxVisibleNpcCount)
            {
                npc.WaitInternally();
                _internalWaitingList.Add(npc);
                return;
            }

            AssignToSlot(npc, entrySlot);
        }

        public bool EnsureRuntimeReferences()
        {
            if (HasValidQueueReferences())
            {
                return true;
            }

            GameObject queueRoot = GameObject.Find("GameStage/Queue");
            if (queueRoot == null)
            {
                LogInitializationError("QueueManager could not find GameStage/Queue.");
                return false;
            }

            var resolvedSlots = new QueueSlot[MaxVisibleNpcCount];
            for (int index = 0; index < resolvedSlots.Length; index++)
            {
                string slotName = $"Queue{index + 1:00}";
                Transform slotTransform = queueRoot.transform.Find(slotName);
                if (slotTransform == null)
                {
                    LogInitializationError($"QueueManager could not find {slotName}.");
                    return false;
                }

                QueueSlot slot = slotTransform.GetComponent<QueueSlot>();
                if (slot == n…660 tokens truncated…indLastAvailableSlot();
                if (entrySlot == null)
                {
                    return;
                }

                NPCController npc = _internalWaitingList[0];
                _internalWaitingList.RemoveAt(0);
                AssignToSlot(npc, entrySlot);
            }
        }

        private QueueSlot FindLastAvailableSlot()
        {
            for (int index = queueSlots.Length - 1; index >= 0; index--)
            {
                if (!queueSlots[index].IsOccupied)
                {
                    return queueSlots[index];
                }
            }

            return null;
        }

        private bool HasValidQueueReferences()
        {
            if (queueSlots == null || queueSlots.Length != MaxVisibleNpcCount || decisionPoint == null)
            {
                return false;
            }

            for (int index = 0; index < queueSlots.Length; index++)
            {
                QueueSlot slot = queueSlots[index];
                if (slot == null || slot.QueueNumber != index + 1)
                {
                    return false;
                }
            }

            return true;
        }

        private void LogInitializationError(string message)
        {
            if (_hasLoggedInitializationError)
            {
                return;
            }

            _hasLoggedInitializationError = true;
            Debug.LogError(message, this);
        }

        private int GetVisibleNpcCount()
        {
            int visibleCount = _decisionPointOccupant != null ? 1 : 0;

            foreach (QueueSlot slot in queueSlots)
            {
                if (slot.IsOccupied)
                {
                    visibleCount++;
                }
            }

            return visibleCount;
        }

        private static void AssignToSlot(NPCController npc, QueueSlot slot)
        {
            if (slot.TryAssign(npc))
            {
                npc.EnterVisibleQueue(slot);
            }
        }
    }
}
