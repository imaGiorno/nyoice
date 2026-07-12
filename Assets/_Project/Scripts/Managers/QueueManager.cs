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

        public IReadOnlyList<NPCController> InternalWaitingList => _internalWaitingList;

        public void Configure(QueueSlot[] slots, Transform decisionPointTransform)
        {
            queueSlots = slots;
            decisionPoint = decisionPointTransform;
        }

        public void Enqueue(NPCController npc)
        {
            npc.Initialize(this);

            QueueSlot entrySlot = FindLastAvailableSlot();
            if (entrySlot == null || GetVisibleNpcCount() >= MaxVisibleNpcCount)
            {
                npc.WaitInternally();
                _internalWaitingList.Add(npc);
                return;
            }

            AssignToSlot(npc, entrySlot);
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
