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
                if (slot == null)
                {
                    slot = slotTransform.gameObject.AddComponent<QueueSlot>();
                }

                slot.Initialize(index + 1);
                resolvedSlots[index] = slot;
            }

            Transform resolvedDecisionPoint = queueRoot.transform.Find("DecisionPoint");
            if (resolvedDecisionPoint == null)
            {
                LogInitializationError("QueueManager could not find DecisionPoint.");
                return false;
            }

            queueSlots = resolvedSlots;
            decisionPoint = resolvedDecisionPoint;

            if (!HasValidQueueReferences())
            {
                LogInitializationError("QueueManager failed to resolve its QueueSlots or DecisionPoint.");
                return false;
            }

            _hasLoggedInitializationError = false;
            return true;
        }

        public void NotifyQueueSlotReached(NPCController npc)
        {
            AdvanceQueue();
        }

        public void NotifyDecisionPointReached(NPCController npc)
        {
            _decisionPointOccupant = npc;
            AdvanceQueue();
        }

        private void AdvanceQueue()
        {
            MoveFrontNpcToDecisionPoint();

            for (int index = 1; index < queueSlots.Length; index++)
            {
                QueueSlot current = queueSlots[index];
                QueueSlot next = queueSlots[index - 1];
                NPCController npc = current.Occupant;

                if (npc == null || !npc.IsWaitingAtSlot || next.IsOccupied)
                {
                    continue;
                }

                current.Clear(npc);
                AssignToSlot(npc, next);
            }

            AdmitInternallyWaitingNpcs();
        }

        private void MoveFrontNpcToDecisionPoint()
        {
            if (_decisionPointOccupant != null || decisionPoint == null || queueSlots.Length == 0)
            {
                return;
            }

            QueueSlot front = queueSlots[0];
            NPCController npc = front.Occupant;
            if (npc == null || !npc.IsWaitingAtSlot)
            {
                return;
            }

            _decisionPointOccupant = npc;
            front.Clear(npc);
            npc.MoveToDecisionPoint(decisionPoint.position);
        }

        private void AdmitInternallyWaitingNpcs()
        {
            while (_internalWaitingList.Count > 0)
            {
                if (GetVisibleNpcCount() >= MaxVisibleNpcCount)
                {
                    return;
                }

                QueueSlot entrySlot = FindLastAvailableSlot();
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
