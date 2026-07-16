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
        private float urinationDurationSeconds = 6f;

        private NPCMovement _movement;
        private QueueManager _queueManager;
        private UrinalManager _urinalManager;
        private UrinalTicketManager _ticketManager;
        private GameStateManager _gameStateManager;
        private Transform _exitPoint;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Vector3 _lineCrossingTarget;
        private Coroutine _selectionWaitRoutine;
        private Coroutine _urinationRoutine;
        private bool _urinationStarted;
        private bool _leavingStarted;
        private bool _exitStartReached;
        private bool _movingToExitPoint;
        private bool _finished;
        private bool _destroyScheduled;

        public QueueSlot CurrentSlot { get; private set; }
        public UrinalController TargetUrinal { get; private set; }
        public UrinalController CurrentUrinal => TargetUrinal;
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
        public Transform ExitPoint => _exitPoint;
        public bool IsLeavingStarted => _leavingStarted;
        public bool IsMovingToExitPoint => _movingToExitPoint;
        public bool IsDestroyScheduled => _destroyScheduled;
        public bool IsGameOver => _gameStateManager != null && _gameStateManager.IsGameOver;

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
            _movement?.Stop();
        }

        private void OnDestroy()
        {
            if (_gameStateManager != null)
            {
                _gameStateManager.GameOver -= HandleGameOver;
            }
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

        public void ConfigureExitFlow(Transform exitPoint)
        {
            _exitPoint = exitPoint;
        }

        public void ConfigureGameState(GameStateManager gameStateManager)
        {
            if (_gameStateManager != null)
            {
                _gameStateManager.GameOver -= HandleGameOver;
            }

            _gameStateManager = gameStateManager;
            EnsureComponentReferences();
            _movement.ConfigureGameState(_gameStateManager);

            if (_gameStateManager != null)
            {
                _gameStateManager.GameOver += HandleGameOver;
                if (_gameStateManager.IsGameOver)
                {
                    HandleGameOver();
                }
            }
        }

        public void WaitInternally()
        {
            if (IsGameOver)
            {
                return;
            }

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
            if (IsGameOver || slot == null)
            {
                return;
            }

            SetPresentationVisible(true);
            CurrentSlot = slot;
            IsWaitingAtSlot = false;
            SetState(NPCState.Queue);
            _movement.MoveTo(slot.transform.position, HandleQueueSlotReached);
        }

        public void MoveToDecisionPoint(Vector3 decisionPosition)
        {
            if (IsGameOver)
            {
                return;
            }

            CurrentSlot = null;
            IsWaitingAtSlot = false;
            SetState(NPCState.Queue);
            _movement.MoveTo(decisionPosition, HandleDecisionPointReached);
        }

        public void BeginUrinalApproach(Vector3 approachPosition, Vector3 crossingTarget)
        {
            if (IsGameOver || State != NPCState.FrontWaiting || !HasUrinalTicket)
            {
                return;
            }

            _lineCrossingTarget = crossingTarget;
            SetState(NPCState.ApproachingLine);
            _movement.MoveTo(approachPosition, HandleApproachPointReached);
        }

        public void HandleNyoiceLineCrossed()
        {
            if (IsGameOver || State != NPCState.CrossingLine)
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
            if (IsGameOver)
            {
                return;
            }

            IsWaitingAtSlot = true;
            _queueManager.NotifyQueueSlotReached(this);
        }

        public void HandleDecisionPointReached()
        {
            if (IsGameOver)
            {
                return;
            }

            SetState(NPCState.FrontWaiting);
            if (_queueManager != null)
            {
                _queueManager.NotifyDecisionPointReached(this);
            }
        }

        private void HandleApproachPointReached()
        {
            if (IsGameOver || State != NPCState.ApproachingLine)
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
            if (IsGameOver || State != NPCState.SelectingUrinal)
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
            if (IsGameOver || State != NPCState.WalkingToUrinal || TargetUrinal == null)
            {
                return;
            }

            _movement.MoveTo(TargetUrinal.UsePoint.position, HandleUsePointReached);
        }

        private void HandleUsePointReached()
        {
            if (IsGameOver || State != NPCState.WalkingToUrinal || TargetUrinal == null)
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
            if (IsGameOver || _urinationStarted || State != NPCState.UsingUrinal ||
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
            if (IsGameOver || !_urinationStarted || State != NPCState.UsingUrinal ||
                TargetUrinal == null || !TargetUrinal.IsOccupied ||
                TargetUrinal.CurrentUser != this)
            {
                return false;
            }

            UrinationElapsed = urinationDurationSeconds;
            Log($"{name} completed urination");
            SetState(NPCState.ReadyToLeave);
            BeginLeaving();
            return true;
        }

        public bool BeginLeaving()
        {
            if (IsGameOver || _leavingStarted || State != NPCState.ReadyToLeave)
            {
                return false;
            }

            UrinalController departingUrinal = TargetUrinal;
            if (departingUrinal == null || departingUrinal.CurrentUser != this ||
                departingUrinal.ExitStartPoint == null || _exitPoint == null ||
                _ticketManager == null || !_ticketManager.HasTicket(this))
            {
                return false;
            }

            _leavingStarted = true;
            _exitStartReached = false;
            _movingToExitPoint = false;
            _finished = false;
            _destroyScheduled = false;

            Transform exitStartPoint = departingUrinal.ExitStartPoint;
            int urinalNumber = departingUrinal.UrinalNumber;
            SetState(NPCState.Leaving);

            if (!departingUrinal.Release(this))
            {
                _leavingStarted = false;
                SetState(NPCState.ReadyToLeave);
                return false;
            }

            Log($"{name} released Urinal{urinalNumber:00}");
            Log($"Urinal{urinalNumber:00} state: Occupied -> Available");

            bool ticketReleased = _ticketManager.ReleaseTicket(this);
            if (!ticketReleased)
            {
                Debug.LogWarning($"{name} could not release its UrinalTicket.", this);
            }

            TargetUrinal = null;
            Log($"{name} moving to Urinal{urinalNumber:00} ExitStartPoint");
            _movement.MoveTo(exitStartPoint.position, HandleExitStartPointReached);
            return true;
        }

        private void HandleExitStartPointReached()
        {
            if (IsGameOver || !_leavingStarted || _exitStartReached || State != NPCState.Leaving)
            {
                return;
            }

            _exitStartReached = true;
            Log($"{name} reached ExitStartPoint");

            if (_movingToExitPoint || _exitPoint == null)
            {
                return;
            }

            _movingToExitPoint = true;
            Log($"{name} moving to ExitPoint");
            _movement.MoveTo(_exitPoint.position, HandleExitPointReached);
        }

        private void HandleExitPointReached()
        {
            if (IsGameOver || !_leavingStarted || _finished || State != NPCState.Leaving)
            {
                return;
            }

            _finished = true;
            _movement.Stop();
            Log($"{name} reached ExitPoint");
            SetState(NPCState.Finished);
            _destroyScheduled = true;
            Log($"{name} destroyed");

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
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
            if (IsGameOver || State == nextState)
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

        private void HandleGameOver()
        {
            CancelSelectionWait();
            CancelUrinationTimer();
            _movement?.Stop();
        }
    }
}
