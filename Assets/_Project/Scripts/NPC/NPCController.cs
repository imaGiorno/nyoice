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
        private const int FirstWallBottomHorizontalUrinalNumber = 7;
        public const float DefaultMinimumUrinationDuration = 2f;
        public const float DefaultMaximumUrinationDuration = 10f;
        public const float WalkFrameIntervalSeconds = 0.2f;

        [SerializeField]
        private bool enableStateLogs = true;

        [SerializeField, Min(0f)]
        private float selectionWaitSeconds = 2f;

        [SerializeField, Min(0.1f)]
        private float minimumUrinationDurationSeconds = DefaultMinimumUrinationDuration;

        [SerializeField, Min(0.1f)]
        private float maximumUrinationDurationSeconds = DefaultMaximumUrinationDuration;

        [SerializeField]
        private NPCBusinessSpriteHolder spriteHolder;

        [SerializeField]
        private SpriteRenderer spriteRenderer;

        private NPCMovement _movement;
        private QueueManager _queueManager;
        private UrinalManager _urinalManager;
        private UrinalTicketManager _ticketManager;
        private GameStateManager _gameStateManager;
        private ScoreManager _scoreManager;
        private Transform _exitPoint;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Vector3 _lineCrossingTarget;
        private Coroutine _selectionWaitRoutine;
        private bool _urinationStarted;
        private bool _leavingStarted;
        private bool _exitStartReached;
        private bool _movingToExitPoint;
        private bool _finished;
        private bool _destroyScheduled;
        private bool _hasAssignedUrinationDuration;
        private float _walkFrameElapsed;
        private bool _walkFrameAlternate;
        private bool _isWalkSpriteAnimationActive;

        public QueueSlot CurrentSlot { get; private set; }
        public UrinalController TargetUrinal { get; private set; }
        public UrinalController CurrentUrinal => TargetUrinal;
        public NPCState State { get; private set; } = NPCState.Queue;
        public bool IsWaitingAtSlot { get; private set; }
        public bool IsWaitingForDecision => State == NPCState.FrontWaiting;
        public bool IsPresentationVisible { get; private set; } = true;
        public bool HasUrinalTicket => _ticketManager != null && _ticketManager.HasTicket(this);
        public float SelectionWaitSeconds => selectionWaitSeconds;
        public float UrinationDurationSeconds => AssignedUrinationDuration;
        public float MinimumUrinationDuration => minimumUrinationDurationSeconds;
        public float MaximumUrinationDuration => maximumUrinationDurationSeconds;
        public float AssignedUrinationDuration { get; private set; }
        public float RemainingUrinationTime { get; private set; }
        public float UrinationElapsed { get; private set; }
        public bool IsUrinationComplete => State == NPCState.ReadyToLeave;
        public bool IsUrinationTimerStarted => _urinationStarted;
        public bool IsUrinationStarted => _urinationStarted;
        public Transform ExitPoint => _exitPoint;
        public bool IsLeavingStarted => _leavingStarted;
        public bool IsMovingToExitPoint => _movingToExitPoint;
        public bool IsDestroyScheduled => _destroyScheduled;
        public bool IsGameOver => _gameStateManager != null && _gameStateManager.IsGameOver;
        public bool UsesWallBottomHorizontalRoute =>
            TargetUrinal != null &&
            TargetUrinal.UrinalNumber >= FirstWallBottomHorizontalUrinalNumber;
        public NPCBusinessSpriteHolder SpriteHolder => spriteHolder;
        public bool CanAcceptUrinalSelection =>
            !IsGameOver && HasUrinalTicket && TargetUrinal == null &&
            (State == NPCState.Queue || State == NPCState.FrontWaiting);
        public bool CanChangeUrinalAssignment =>
            !IsGameOver && HasUrinalTicket && TargetUrinal != null &&
            (State == NPCState.Queue || State == NPCState.FrontWaiting);

        private void Awake()
        {
            EnsureComponentReferences();
            AssignUrinationDurationOnce();
            SetIdle();
        }

        private void Update()
        {
            AdvanceUrinationTime(Time.deltaTime);
            AdvanceWalkSpriteTime(Time.deltaTime);
            if (State == NPCState.CrossingLine || State == NPCState.WalkingToUrinal)
            {
                _queueManager?.NotifyCrossingCorridorProgress(this);
            }
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
            StopWalkSpriteAnimation();
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
            AssignUrinationDurationOnce();
            SetState(NPCState.Queue);
            StopWalkSpriteAnimation();
            SetIdle();
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
            float safeDuration = Mathf.Max(0.1f, durationSeconds);
            minimumUrinationDurationSeconds = safeDuration;
            maximumUrinationDurationSeconds = safeDuration;
            AssignedUrinationDuration = safeDuration;
            RemainingUrinationTime = safeDuration;
            _hasAssignedUrinationDuration = true;
        }

        public void ConfigureUrinationDurationRange(float minimumSeconds, float maximumSeconds)
        {
            minimumUrinationDurationSeconds = Mathf.Max(DefaultMinimumUrinationDuration, minimumSeconds);
            maximumUrinationDurationSeconds = Mathf.Max(minimumUrinationDurationSeconds, maximumSeconds);
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

        public void ConfigureScore(ScoreManager scoreManager)
        {
            _scoreManager = scoreManager;
        }

        public void ConfigureSpriteHolder(
            NPCBusinessSpriteHolder holder,
            SpriteRenderer targetRenderer)
        {
            spriteHolder = holder;
            spriteRenderer = targetRenderer;
            SetIdle();
        }

        public void SetIdle()
        {
            SetSprite(spriteHolder != null ? spriteHolder.NpcBusinessFront01 : null);
        }

        public void SetWalkFrame(bool alternate)
        {
            SetSprite(spriteHolder == null
                ? null
                : alternate
                    ? spriteHolder.NpcBusinessLeft02
                    : spriteHolder.NpcBusinessLeft01);
        }

        public void SetPee()
        {
            SetSprite(spriteHolder != null ? spriteHolder.NpcBusinessBackPee : null);
        }

        public void SetExit()
        {
            SetSprite(spriteHolder != null ? spriteHolder.NpcBusinessFront01 : null);
        }

        private void StartWalkSpriteAnimation()
        {
            _isWalkSpriteAnimationActive = true;
            _walkFrameElapsed = 0f;
            _walkFrameAlternate = false;
            SetWalkFrame(false);
        }

        private void StopWalkSpriteAnimation()
        {
            _isWalkSpriteAnimationActive = false;
            _walkFrameElapsed = 0f;
            _walkFrameAlternate = false;
        }

        private void AdvanceWalkSpriteTime(float deltaTime)
        {
            if (!_isWalkSpriteAnimationActive || deltaTime <= 0f)
            {
                return;
            }

            _walkFrameElapsed += deltaTime;
            while (_walkFrameElapsed >= WalkFrameIntervalSeconds)
            {
                _walkFrameElapsed -= WalkFrameIntervalSeconds;
                _walkFrameAlternate = !_walkFrameAlternate;
                SetWalkFrame(_walkFrameAlternate);
            }
        }

        private void SetSprite(Sprite sprite)
        {
            if (spriteRenderer != null && sprite != null)
            {
                spriteRenderer.sprite = sprite;
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
            RemainingUrinationTime = AssignedUrinationDuration;
            SetState(NPCState.Queue);
            StopWalkSpriteAnimation();
            SetIdle();
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
            StopWalkSpriteAnimation();
            SetIdle();
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
            StopWalkSpriteAnimation();
            SetIdle();
            _movement.MoveTo(decisionPosition, HandleDecisionPointReached);
        }

        public bool BeginUrinalSelection()
        {
            if (!CanAcceptUrinalSelection)
            {
                return false;
            }

            Log($"{name} ready for early urinal selection while preserving its queue route");
            return true;
        }

        public bool AcceptUrinalAssignment(UrinalController urinal)
        {
            if (!CanAcceptUrinalSelection || urinal == null ||
                urinal.State != UrinalState.Reserved || urinal.ReservedBy != this ||
                urinal.MovePoint == null || urinal.UsePoint == null)
            {
                return false;
            }

            TargetUrinal = urinal;
            Log($"{name} reserved Urinal{TargetUrinal.UrinalNumber:00} and remains on the FIFO queue route");
            return true;
        }

        public bool ReplaceUrinalAssignment(UrinalController expectedCurrent, UrinalController replacement)
        {
            if (!CanChangeUrinalAssignment || TargetUrinal != expectedCurrent || replacement == null ||
                replacement.State != UrinalState.Reserved || replacement.ReservedBy != this ||
                replacement.MovePoint == null || replacement.UsePoint == null)
            {
                return false;
            }

            TargetUrinal = replacement;
            Log($"{name} changed assignment from Urinal{expectedCurrent.UrinalNumber:00} " +
                $"to Urinal{replacement.UrinalNumber:00}");
            return true;
        }

        public bool ClearInvalidUrinalAssignment()
        {
            if (IsGameOver || (State != NPCState.Queue && State != NPCState.FrontWaiting))
            {
                return false;
            }

            TargetUrinal = null;
            return true;
        }

        public bool RestoreUrinalAssignment(UrinalController expectedCurrent, UrinalController previous)
        {
            if (IsGameOver || TargetUrinal != expectedCurrent || previous == null ||
                (State != NPCState.Queue && State != NPCState.FrontWaiting))
            {
                return false;
            }

            TargetUrinal = previous;
            return true;
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
            if (TargetUrinal == null || TargetUrinal.ReservedBy != this)
            {
                _movement.Stop();
                _queueManager?.ReleaseCrossingCorridor(this);
                ReleaseUrinalTicket();
                SetState(NPCState.FrontWaiting);
                Debug.LogWarning($"{name} stopped because no urinal is available.", this);
                return;
            }

            SetState(NPCState.WalkingToUrinal);
            Vector3 postCrossingTarget = GetPostCrossingTarget();
            if (!UsesWallBottomHorizontalRoute)
            {
                Log($"{name} moving directly to Urinal{TargetUrinal.UrinalNumber:00} MovePoint");
                _movement.MoveTo(postCrossingTarget, HandleMovePointReached);
                return;
            }

            Log($"{name} moving left below the wall toward Urinal{TargetUrinal.UrinalNumber:00}");
            _movement.MoveTo(postCrossingTarget, HandleWallClearanceReached);
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

            if (TargetUrinal != null && TargetUrinal.ReservedBy == this)
            {
                if (_queueManager != null)
                {
                    if (!_queueManager.NotifyApproachPointReached(this))
                    {
                        BeginCrossingCorridor();
                    }
                }
                else
                {
                    BeginCrossingCorridor();
                }
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

            Log($"{name} selection wait completed");
            if (_queueManager != null)
            {
                if (!_queueManager.NotifyApproachPointReached(this))
                {
                    BeginCrossingCorridor();
                }
            }
            else
            {
                BeginCrossingCorridor();
            }
        }

        public void BeginCrossingCorridor()
        {
            if (IsGameOver || (State != NPCState.ApproachingLine && State != NPCState.SelectingUrinal))
            {
                return;
            }

            CancelSelectionWait();
            SetState(NPCState.CrossingLine);
            _movement.MoveTo(_lineCrossingTarget, HandleCrossingTargetReached);
        }

        private void HandleCrossingTargetReached()
        {
            HandleNyoiceLineCrossed();
        }

        private void HandleWallClearanceReached()
        {
            if (IsGameOver || State != NPCState.WalkingToUrinal || TargetUrinal == null)
            {
                return;
            }

            _movement.MoveTo(TargetUrinal.MovePoint.position, HandleMovePointReached);
        }

        private Vector3 GetPostCrossingTarget()
        {
            if (TargetUrinal == null || TargetUrinal.MovePoint == null)
            {
                return transform.position;
            }

            return UsesWallBottomHorizontalRoute
                ? new Vector3(
                    TargetUrinal.MovePoint.position.x,
                    _lineCrossingTarget.y,
                    TargetUrinal.MovePoint.position.z)
                : TargetUrinal.MovePoint.position;
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
                HandleInvalidUrinalAssignment();
                return;
            }

            SetState(NPCState.UsingUrinal);
            Log($"{name} reached Urinal{TargetUrinal.UrinalNumber:00} UsePoint");
            Log($"Urinal{TargetUrinal.UrinalNumber:00} state: Reserved -> Occupied");
            _scoreManager?.NotifyUrinalUseStarted();
            BeginUrination();
        }

        private void HandleInvalidUrinalAssignment()
        {
            UrinalController invalidUrinal = TargetUrinal;
            if (invalidUrinal != null && invalidUrinal.ReservedBy == this)
            {
                invalidUrinal.Release(this);
            }

            TargetUrinal = null;
            ReleaseUrinalTicket();
            SetState(NPCState.Queue);
            _queueManager?.Enqueue(this);
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
            RemainingUrinationTime = AssignedUrinationDuration;
            Log($"{name} started urination at Urinal{TargetUrinal.UrinalNumber:00}");
            Log($"{name} urination time: {AssignedUrinationDuration:0.0} seconds");

            return true;
        }

        public void AdvanceUrinationTime(float deltaTime)
        {
            if (IsGameOver || !_urinationStarted || State != NPCState.UsingUrinal || deltaTime <= 0f)
            {
                return;
            }

            RemainingUrinationTime = Mathf.Max(0f, RemainingUrinationTime - deltaTime);
            UrinationElapsed = AssignedUrinationDuration - RemainingUrinationTime;
            if (RemainingUrinationTime <= 0f)
            {
                CompleteUrination();
            }
        }

        private bool CompleteUrination()
        {
            if (IsGameOver || !_urinationStarted || State != NPCState.UsingUrinal ||
                TargetUrinal == null || !TargetUrinal.IsOccupied ||
                TargetUrinal.CurrentUser != this)
            {
                return false;
            }

            RemainingUrinationTime = 0f;
            UrinationElapsed = AssignedUrinationDuration;
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
            _scoreManager?.NotifyNpcFinished();
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

        private void AssignUrinationDurationOnce()
        {
            if (_hasAssignedUrinationDuration)
            {
                return;
            }

            minimumUrinationDurationSeconds = Mathf.Max(
                DefaultMinimumUrinationDuration,
                minimumUrinationDurationSeconds);
            maximumUrinationDurationSeconds = Mathf.Max(
                minimumUrinationDurationSeconds,
                maximumUrinationDurationSeconds);
            AssignedUrinationDuration = Random.Range(
                minimumUrinationDurationSeconds,
                maximumUrinationDurationSeconds);
            RemainingUrinationTime = AssignedUrinationDuration;
            _hasAssignedUrinationDuration = true;
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
        }

        private void SetState(NPCState nextState)
        {
            if (IsGameOver || State == nextState)
            {
                return;
            }

            NPCState previousState = State;
            State = nextState;
            ApplySpritePresentation(nextState);
            Log($"{name} state: {previousState} -> {nextState}");
        }

        private void ApplySpritePresentation(NPCState state)
        {
            switch (state)
            {
                case NPCState.ApproachingLine:
                case NPCState.CrossingLine:
                case NPCState.WalkingToUrinal:
                    StartWalkSpriteAnimation();
                    break;
                case NPCState.UsingUrinal:
                case NPCState.ReadyToLeave:
                    StopWalkSpriteAnimation();
                    SetPee();
                    break;
                case NPCState.Leaving:
                case NPCState.Finished:
                    StopWalkSpriteAnimation();
                    SetExit();
                    break;
                default:
                    StopWalkSpriteAnimation();
                    SetIdle();
                    break;
            }
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
