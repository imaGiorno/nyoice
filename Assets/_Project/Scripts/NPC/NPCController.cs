using System.Collections;
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

        [SerializeField, Min(0f)]
        private float selectionWaitSeconds = 2f;

        [SerializeField, Min(0.1f)]
        private float urinationDurationSeconds = 3f;

        private NPCMovement _movement;
        private QueueManager _queueManager;
        private UrinalManager _urinalManager;
        private UrinalTicketManager _ticketManager;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Vector3 _lineCrossingTarget;
        private Coroutine _selectionWaitRoutine;
        private Coroutine _urinationRoutine;
        private bool _urinationStarted;

        public QueueSlot CurrentSlot { get; private set; }
        public UrinalController TargetUrinal { get; private set; }
        public NPCState State { get; private set; } = NPCState.Queue;
        public bool IsWaitingAtSlot { get; private set; }
        public bool IsWaitingForDecision => State == NPCState.FrontWaiting;
        public bool IsPresentationVisible { get; private set; } = true;
        public bool HasUrinalTicket => _ticketManager != null && _ticketManager.HasTicket(this);
        public float SelectionWaitSeconds => selectionWaitSeconds;
        public float UrinationDurationSeconds => urinationDurationSeconds;
        public float UrinationElapsed { get; private set; }
        public bool IsUrinationComplete => State == NPCState.ReadyToLeave;
        public bool IsUrinationTimerStarted => _urinationStarted;

        private void Awake()
        {
            EnsureComponentReferences();
        }

        private void OnEnable()
        {
            if (State == NPCState.UsingUrinal && !_urinationStarted)
            {
                BeginUrination();
            }
        }

        private void OnDisable()
        {
            CancelSelectionWait();
            CancelUrinationTimer();
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

        public void ConfigureUrinationDuration(float durationSeconds)
        {
            urinationDurationSeconds = Mathf.Max(0.1f, durationSeconds);
        }

        public void WaitInternally()
        {
            CancelSelectionWait();
            CancelUrinationTimer();
            CurrentSlot = null;
            IsWaitingAtSlot = false;
            TargetUrinal = null;
            UrinationElapsed = 0f;
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
            if (State != NPCState.CrossingLine)
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

            _movement.Stop();
            SetState(NPCState.SelectingUrinal);
            Log($"{name} waiting {selectionWaitSeconds:0.##} seconds for urinal selection");

            if (selectionWaitSeconds <= 0f)
            {
                CompleteSelectionWait();
            }
            else if (Application.isPlaying)
            {
                _selectionWaitRoutine = StartCoroutine(WaitBeforeCrossing());
            }
        }

        private IEnumerator WaitBeforeCrossing()
        {
            yield return new WaitForSeconds(selectionWaitSeconds);
            _selectionWaitRoutine = null;
            CompleteSelectionWait();
        }

        private void CompleteSelectionWait()
        {
            if (State != NPCState.SelectingUrinal)
            {
                return;
            }

            SetState(NPCState.CrossingLine);
            Log($"{name} selection wait completed");
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
            BeginUrination();
        }

        public bool BeginUrination()
        {
            if (_urinationStarted || State != NPCState.UsingUrinal ||
                TargetUrinal == null || !TargetUrinal.IsOccupied ||
                TargetUrinal.CurrentUser != this)
            {
                return false;
            }

            _urinationStarted = true;
            UrinationElapsed = 0f;
            Log($"{name} started urination at Urinal{TargetUrinal.UrinalNumber:00}");
            Log($"{name} urination time: {urinationDurationSeconds:0.0} seconds");

            if (Application.isPlaying)
            {
                _urinationRoutine = StartCoroutine(WaitForUrinationCompletion());
            }

            return true;
        }

        private IEnumerator WaitForUrinationCompletion()
        {
            yield return new WaitForSeconds(urinationDurationSeconds);
            _urinationRoutine = null;
            CompleteUrination();
        }

        private bool CompleteUrination()
        {
            if (!_urinationStarted || State != NPCState.UsingUrinal ||
                TargetUrinal == null || !TargetUrinal.IsOccupied ||
                TargetUrinal.CurrentUser != this)
            {
                return false;
            }

            UrinationElapsed = urinationDurationSeconds;
            Log($"{name} completed urination");
            SetState(NPCState.ReadyToLeave);
            return true;
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

        private void CancelSelectionWait()
        {
            if (_selectionWaitRoutine != null)
            {
                StopCoroutine(_selectionWaitRoutine);
                _selectionWaitRoutine = null;
            }
        }

        private void CancelUrinationTimer()
        {
            if (_urinationRoutine != null)
            {
                StopCoroutine(_urinationRoutine);
                _urinationRoutine = null;
            }

            if (State == NPCState.UsingUrinal)
            {
                _urinationStarted = false;
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
