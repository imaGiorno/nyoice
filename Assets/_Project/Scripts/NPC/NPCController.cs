using Nyoice.Managers;
using Nyoice.Toilet;
using UnityEngine;

namespace Nyoice.NPC
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NPCMovement))]
    public sealed class NPCController : MonoBehaviour
    {
        [SerializeField]
        private bool enableStateLogs = true;

        private NPCMovement _movement;
        private QueueManager _queueManager;
        private UrinalManager _urinalManager;
        private UrinalTicketManager _ticketManager;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Vector3 _lineCrossingTarget;

        public QueueSlot CurrentSlot { get; private set; }
        public UrinalController TargetUrinal { get; private set; }
        public NPCState State { get; private set; } = NPCState.Queue;
        public bool IsWaitingAtSlot { get; private set; }
        public bool IsWaitingForDecision => State == NPCState.FrontWaiting;
        public bool IsPresentationVisible { get; private set; } = true;
        public bool HasUrinalTicket => _ticketManager != null && _ticketManager.HasTicket(this);

        private void Awake()
        {
            EnsureComponentReferences();
        }

        public void Initialize(QueueManager queueManager)
        {
            _queueManager = queueManager;
            EnsureComponentReferences();
            SetState(NPCState.Queue);
        }

        public void ConfigureUrinalFlow(
            UrinalManager urinalManager,
            UrinalTicketManager ticketManager)
        {
            _urinalManager = urinalManager;
            _ticketManager = ticketManager;
        }

        public void WaitInternally()
        {
            CurrentSlot = null;
            IsWaitingAtSlot = false;
            TargetUrinal = null;
            SetState(NPCState.Queue);
            SetPresentationVisible(false);
        }

        public void EnterVisibleQueue(QueueSlot slot)
        {
            SetPresentationVisible(true);
            CurrentSlot = slot;
            IsWaitingAtSlot = false;
            SetState(NPCState.Queue);
            _movement.MoveTo(slot.transform.position, HandleQueueSlotReached);
        }

        public void MoveToDecisionPoint(Vector3 decisionPosition)
        {
            CurrentSlot = null;
            IsWaitingAtSlot = false;
            SetState(NPCState.Queue);
            _movement.MoveTo(decisionPosition, HandleDecisionPointReached);
        }

        public void BeginUrinalApproach(Vector3 approachPosition, Vector3 crossingTarget)
        {
            if (State != NPCState.FrontWaiting || !HasUrinalTicket)
            {
                return;
            }

            _lineCrossingTarget = crossingTarget;
            SetState(NPCState.ApproachingLine);
            _movement.MoveTo(approachPosition, HandleApproachPointReached);
        }

        public void HandleNyoiceLineCrossed()
        {
            if (State != NPCState.ApproachingLine && State != NPCState.CrossingLine)
            {
                return;
            }

            Log($"{name} crossed NyoiceLine");
            UrinalController confirmedUrinal = _urinalManager != null
                ? _urinalManager.ConfirmSelection(this)
                : null;

            if (confirmedUrinal == null)
            {
                _movement.Stop();
                ReleaseUrinalTicket();
                SetState(NPCState.FrontWaiting);
                Debug.LogWarning($"{name} stopped because no urinal is available.", this);
                return;
            }

            TargetUrinal = confirmedUrinal;
            _queueManager?.NotifySelectionZoneCrossed(this);
            SetState(NPCState.WalkingToUrinal);
            Log($"{name} moving to Urinal{TargetUrinal.UrinalNumber:00} MovePoint");
            _movement.MoveTo(TargetUrinal.MovePoint.position, HandleMovePointReached);
        }

        public bool ReleaseUrinalTicket()
        {
            return _ticketManager != null && _ticketManager.ReleaseTicket(this);
        }

        private void HandleQueueSlotReached()
        {
            IsWaitingAtSlot = true;
            _queueManager.NotifyQueueSlotReached(this);
        }

        public void HandleDecisionPointReached()
        {
            SetState(NPCState.FrontWaiting);
            if (_queueManager != null)
            {
                _queueManager.NotifyDecisionPointReached(this);
            }
        }

        private void HandleApproachPointReached()
        {
            if (State != NPCState.ApproachingLine)
            {
                return;
            }

            SetState(NPCState.CrossingLine);
            _movement.MoveTo(_lineCrossingTarget, HandleCrossingTargetReached);
        }

        private void HandleCrossingTargetReached()
        {
            HandleNyoiceLineCrossed();
        }

        private void HandleMovePointReached()
        {
            if (State != NPCState.WalkingToUrinal || TargetUrinal == null)
            {
                return;
            }

            _movement.MoveTo(TargetUrinal.UsePoint.position, HandleUsePointReached);
        }

        private void HandleUsePointReached()
        {
            if (State != NPCState.WalkingToUrinal || TargetUrinal == null)
            {
                return;
            }

            if (!TargetUrinal.Occupy(this))
            {
                Debug.LogWarning($"{name} could not occupy its reserved urinal.", this);
                return;
            }

            SetState(NPCState.UsingUrinal);
            Log($"{name} reached Urinal{TargetUrinal.UrinalNumber:00} UsePoint");
            Log($"Urinal{TargetUrinal.UrinalNumber:00} state: Reserved -> Occupied");
        }

        private void SetPresentationVisible(bool visible)
        {
            EnsureComponentReferences();

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

        private void EnsureComponentReferences()
        {
            if (_movement == null)
            {
                _movement = GetComponent<NPCMovement>();
            }

            if (_renderers == null)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }

            if (_colliders == null)
            {
                _colliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void SetState(NPCState nextState)
        {
            if (State == nextState)
            {
                return;
            }

            NPCState previousState = State;
            State = nextState;
            Log($"{name} state: {previousState} -> {nextState}");
        }

        private void Log(string message)
        {
            if (enableStateLogs)
            {
                Debug.Log(message, this);
            }
        }
    }
}
