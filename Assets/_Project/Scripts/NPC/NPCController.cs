using Nyoice.Managers;
using UnityEngine;

namespace Nyoice.NPC
{
    /// <summary>
    /// Coordinates one NPC's queue and decision-waiting state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NPCMovement))]
    public sealed class NPCController : MonoBehaviour
    {
        private NPCMovement _movement;
        private QueueManager _queueManager;

        public QueueSlot CurrentSlot { get; private set; }
        public bool IsWaitingAtSlot { get; private set; }
        public bool IsWaitingForDecision { get; private set; }

        public void Initialize(QueueManager queueManager)
        {
            _queueManager = queueManager;
            _movement = GetComponent<NPCMovement>();
        }

        public void MoveToQueueSlot(QueueSlot slot)
        {
            CurrentSlot = slot;
            IsWaitingAtSlot = false;
            IsWaitingForDecision = false;
            _movement.MoveTo(slot.transform.position, HandleQueueSlotReached);
        }

        public void MoveToDecisionPoint(Vector3 decisionPosition)
        {
            CurrentSlot = null;
            IsWaitingAtSlot = false;
            _movement.MoveTo(decisionPosition, HandleDecisionPointReached);
        }

        private void HandleQueueSlotReached()
        {
            IsWaitingAtSlot = true;
            _queueManager.NotifyQueueSlotReached(this);
        }

        private void HandleDecisionPointReached()
        {
            IsWaitingForDecision = true;
            _queueManager.NotifyDecisionPointReached(this);
        }
    }
}
