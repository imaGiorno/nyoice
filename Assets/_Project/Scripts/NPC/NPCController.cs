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
        private Renderer[] _renderers;
        private Collider[] _colliders;

        public QueueSlot CurrentSlot { get; private set; }
        public bool IsWaitingAtSlot { get; private set; }
        public bool IsWaitingForDecision { get; private set; }
        public bool IsPresentationVisible { get; private set; } = true;

        private void Awake()
        {
            _movement = GetComponent<NPCMovement>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        public void Initialize(QueueManager queueManager)
        {
            _queueManager = queueManager;
            if (_movement == null)
            {
                _movement = GetComponent<NPCMovement>();
            }
        }

        public void WaitInternally()
        {
            CurrentSlot = null;
            IsWaitingAtSlot = false;
            IsWaitingForDecision = false;
            SetPresentationVisible(false);
        }

        public void EnterVisibleQueue(QueueSlot slot)
        {
            SetPresentationVisible(true);
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

        private void SetPresentationVisible(bool visible)
        {
            foreach (Renderer targetRenderer in _renderers)
            {
                targetRenderer.enabled = visible;
            }

            foreach (Collider targetCollider in _colliders)
            {
                targetCollider.enabled = visible;
            }

            IsPresentationVisible = visible;
        }
    }
}
