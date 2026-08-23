using System.Collections.Generic;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEngine;

namespace Nyoice.Managers
{
    [DisallowMultipleComponent]
    public sealed class QueueManager : MonoBehaviour
    {
        private const int MaxVisibleNpcCount = 8;
        public const float DefaultCrossingMinimumCenterSpacing = 2f;

        [SerializeField]
        private QueueSlot[] queueSlots;

        [SerializeField]
        private Transform decisionPoint;

        [SerializeField]
        private UrinalManager urinalManager;

        [SerializeField]
        private UrinalTicketManager ticketManager;

        [SerializeField]
        private Transform nyoiceApproachPoint;

        [SerializeField]
        private Transform lineCrossingTarget;

        [SerializeField]
        private Transform exitPoint;

        [SerializeField]
        private GameStateManager gameStateManager;

        [SerializeField]
        private ScoreManager scoreManager;

        [SerializeField]
        private bool enableQueueDebugLogs = true;

        [SerializeField, Min(0.8f)]
        private float crossingMinimumCenterSpacing = DefaultCrossingMinimumCenterSpacing;

        private readonly List<NPCController> _internalWaitingList = new List<NPCController>();
        private readonly List<NPCController> _pendingNpcs = new List<NPCController>();
        private NPCController _decisionPointOccupant;
        private NPCController _selectionZoneOccupant;
        private NPCController _approachRouteOccupant;
        private NPCController _crossingCorridorOccupant;
        private bool _hasLoggedInitializationError;
        private bool _ticketEventSubscribed;
        private bool _gameOverLogged;

        public IReadOnlyList<NPCController> InternalWaitingList => _internalWaitingList;
        public IReadOnlyList<NPCController> PendingNpcs => _pendingNpcs;
        public NPCController SelectionZoneOccupant => _selectionZoneOccupant;
        public NPCController ApproachRouteOccupant => _approachRouteOccupant;
        public NPCController CrossingCorridorOccupant => _crossingCorridorOccupant;
        public float CrossingMinimumCenterSpacing => crossingMinimumCenterSpacing;
        public bool IsSelectionZoneOccupied => _selectionZoneOccupant != null;
        public int VisibleNpcCount => GetVisibleNpcCount();
        public bool IsProgressionBlocked => gameStateManager != null && gameStateManager.IsGameOver;
        public bool HasInitialFlowReferences =>
            urinalManager != null && ticketManager != null && decisionPoint != null &&
            nyoiceApproachPoint != null && lineCrossingTarget != null;

        private void Awake()
        {
            EnsureRuntimeReferences();
            ResolveUrinalFlowReferences();
        }

        private void OnDestroy()
        {
            UnsubscribeFromTicketManager();
            UnsubscribeFromGameState();
        }

        public void Configure(QueueSlot[] slots, Transform decisionPointTransform)
        {
            queueSlots = slots;
            decisionPoint = decisionPointTransform;
        }

        public void ConfigureUrinalFlow(
            UrinalManager configuredUrinalManager,
            UrinalTicketManager configuredTicketManager,
            Transform approachPoint,
            Transform crossingTarget)
        {
            UnsubscribeFromTicketManager();
            urinalManager = configuredUrinalManager;
            ticketManager = configuredTicketManager;
            nyoiceApproachPoint = approachPoint;
            lineCrossingTarget = crossingTarget;
            SubscribeToTicketManager();
        }

        public void ConfigureExitFlow(Transform configuredExitPoint)
        {
            exitPoint = configuredExitPoint;
        }

        public void ConfigureGameState(GameStateManager configuredGameStateManager)
        {
            UnsubscribeFromGameState();
            gameStateManager = configuredGameStateManager;
            SubscribeToGameState();
        }

        public void ConfigureScore(ScoreManager configuredScoreManager)
        {
            scoreManager = configuredScoreManager;
        }

        public void Enqueue(NPCController npc)
        {
            if (npc == null || IsProgressionBlocked)
            {
                LogGameOverBlock();
                return;
            }

            npc.ConfigureGameState(gameStateManager);
            npc.ConfigureScore(scoreManager);
            npc.Initialize(this);
            npc.ConfigureUrinalFlow(urinalManager, ticketManager);
            npc.ConfigureExitFlow(exitPoint);
            _pendingNpcs.Add(npc);
            npc.WaitInternally();
            _internalWaitingList.Add(npc);

            if (EnsureRuntimeReferences())
            {
                ResolveUrinalFlowReferences();
                CompactQueue();
                TryOfferSelectionToOldestNpc();
            }
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
            _hasLoggedInitializationError = false;
            return HasValidQueueReferences();
        }

        public void NotifyQueueSlotReached(NPCController npc)
        {
            if (IsProgressionBlocked)
            {
                LogGameOverBlock();
                return;
            }

            QueueSlot reachedSlot = npc.CurrentSlot;
            if (reachedSlot != null)
            {
                LogQueueEvent($"{npc.name} reached Queue{reachedSlot.QueueNumber:00}");
            }

            CompactQueue();
        }

        public void NotifyDecisionPointReached(NPCController npc)
        {
            if (IsProgressionBlocked)
            {
                LogGameOverBlock();
                return;
            }

            LogQueueEvent($"{npc.name} reached DecisionPoint");
            TryStartFrontWaitingNpc();
            CompactQueue();
        }

        public bool NotifySelectionZoneCrossed(NPCController npc)
        {
            if (IsProgressionBlocked || npc == null || _selectionZoneOccupant != npc)
            {
                return false;
            }

            _selectionZoneOccupant = null;
            urinalManager?.EndSelection(npc);
            LogQueueEvent($"{npc.name} completed early urinal selection");
            CompactQueue();
            TryOfferSelectionToOldestNpc();
            return true;
        }

        public bool TryEnterSelectionZone(NPCController npc)
        {
            if (IsProgressionBlocked || npc == null || _selectionZoneOccupant != null ||
                ticketManager == null || !ticketManager.HasTicket(npc) ||
                urinalManager == null || !urinalManager.BeginSelection(npc))
            {
                return false;
            }

            _selectionZoneOccupant = npc;
            LogQueueEvent($"{npc.name} entered SelectionZone");
            return true;
        }

        public bool NotifyApproachPointReached(NPCController npc)
        {
            if (npc == null || _approachRouteOccupant != npc)
            {
                return false;
            }

            if (_crossingCorridorOccupant != null)
            {
                LogQueueEvent($"{npc.name} is waiting at ApproachPoint for Crossing Corridor spacing");
                return true;
            }

            AdmitApproachOccupantToCrossingCorridor(npc);
            return true;
        }

        public void NotifyCrossingCorridorProgress(NPCController npc)
        {
            if (npc == null || _crossingCorridorOccupant != npc || lineCrossingTarget == null)
            {
                return;
            }

            float minimumSpacing = Mathf.Max(0.8f, crossingMinimumCenterSpacing);
            if (Vector3.Distance(npc.transform.position, lineCrossingTarget.position) + 0.0001f < minimumSpacing)
            {
                return;
            }

            _crossingCorridorOccupant = null;
            LogQueueEvent($"{npc.name} cleared Crossing Corridor with {minimumSpacing:0.00} spacing");

            if (_approachRouteOccupant != null && nyoiceApproachPoint != null &&
                Vector3.Distance(_approachRouteOccupant.transform.position, nyoiceApproachPoint.position) <= 0.02f)
            {
                AdmitApproachOccupantToCrossingCorridor(_approachRouteOccupant);
                return;
            }

            CompactQueue();
        }

        public void ReleaseCrossingCorridor(NPCController npc)
        {
            if (npc == null || _crossingCorridorOccupant != npc)
            {
                return;
            }

            _crossingCorridorOccupant = null;
            CompactQueue();
        }

        private void AdmitApproachOccupantToCrossingCorridor(NPCController npc)
        {
            _crossingCorridorOccupant = npc;
            _approachRouteOccupant = null;
            LogQueueEvent($"{npc.name} entered Crossing Corridor");
            npc.BeginCrossingCorridor();
            CompactQueue();
        }

        private void CompactQueue()
        {
            if (IsProgressionBlocked || !HasValidQueueReferences())
            {
                LogGameOverBlock();
                return;
            }

            TryStartFrontWaitingNpc();
            TryMoveToDecisionPoint();

            for (int sourceIndex = 1; sourceIndex < queueSlots.Length; sourceIndex++)
            {
                TryAdvanceOneSlot(sourceIndex);
            }

            TryAdmitInternalWaiter();
            TryOfferSelectionToOldestNpc();
        }

        private bool TryStartFrontWaitingNpc()
        {
            NPCController npc = _decisionPointOccupant;
            if (npc == null || npc.State != NPCState.FrontWaiting)
            {
                return false;
            }

            if (_approachRouteOccupant != null)
            {
                return false;
            }

            if (HasValidReservation(npc))
            {
                if (nyoiceApproachPoint == null || lineCrossingTarget == null)
                {
                    return false;
                }

                _decisionPointOccupant = null;
                _pendingNpcs.Remove(npc);
                _approachRouteOccupant = npc;
                npc.BeginUrinalApproach(nyoiceApproachPoint.position, lineCrossingTarget.position);
                NotifySelectionZoneCrossed(npc);
                return true;
            }

            TryRecoverInvalidReservation(npc);
            return false;
        }

        private bool TryOfferSelectionToOldestNpc()
        {
            ResolveUrinalFlowReferences();
            if (_selectionZoneOccupant != null || urinalManager == null ||
                urinalManager.GetAutomaticSelection() == null || ticketManager == null)
            {
                return false;
            }

            NPCController npc = GetOldestSelectionCandidate();
            if (npc == null || !ticketManager.TryAcquireTicket(npc))
            {
                return false;
            }

            npc.ConfigureUrinalFlow(urinalManager, ticketManager);
            if (!TryEnterSelectionZone(npc) || !npc.BeginUrinalSelection() ||
                !urinalManager.TryAssignAutomatic(npc))
            {
                urinalManager.EndSelection(npc);
                _selectionZoneOccupant = null;
                ticketManager.ReleaseTicket(npc);
                return false;
            }

            LogQueueEvent($"{npc.name} can select a urinal by FIFO priority");
            return true;
        }

        private NPCController GetOldestSelectionCandidate()
        {
            for (int index = 0; index < _pendingNpcs.Count; index++)
            {
                NPCController npc = _pendingNpcs[index];
                if (npc != null && npc.TargetUrinal == null &&
                    (npc.State == NPCState.Queue || npc.State == NPCState.FrontWaiting))
                {
                    return npc;
                }
            }

            return null;
        }

        private bool HasValidReservation(NPCController npc)
        {
            return npc != null && npc.TargetUrinal != null &&
                   npc.TargetUrinal.State == UrinalState.Reserved &&
                   npc.TargetUrinal.ReservedBy == npc && npc.HasUrinalTicket &&
                   npc.TargetUrinal.MovePoint != null && npc.TargetUrinal.UsePoint != null;
        }

        private bool TryRecoverInvalidReservation(NPCController npc)
        {
            if (npc == null || urinalManager == null || !npc.HasUrinalTicket ||
                (npc.State != NPCState.Queue && npc.State != NPCState.FrontWaiting))
            {
                return false;
            }

            UrinalController invalid = npc.TargetUrinal;
            if (invalid != null && invalid.ReservedBy == npc)
            {
                invalid.Release(npc);
            }

            if (!npc.ClearInvalidUrinalAssignment())
            {
                return false;
            }

            return urinalManager.TryAssignAutomatic(npc);
        }

        private bool TryMoveToDecisionPoint()
        {
            if (_decisionPointOccupant != null || decisionPoint == null)
            {
                return false;
            }

            QueueSlot frontSlot = queueSlots[0];
            NPCController npc = frontSlot.Occupant;
            if (npc == null || !npc.IsWaitingAtSlot)
            {
                return false;
            }

            _decisionPointOccupant = npc;
            frontSlot.Clear(npc);
            LogQueueEvent($"{npc.name} moved Queue01 -> DecisionPoint");
            npc.MoveToDecisionPoint(decisionPoint.position);
            return true;
        }

        private bool TryAdvanceOneSlot(int sourceIndex)
        {
            QueueSlot sourceSlot = queueSlots[sourceIndex];
            QueueSlot destinationSlot = queueSlots[sourceIndex - 1];
            NPCController npc = sourceSlot.Occupant;

            if (npc == null || !npc.IsWaitingAtSlot || destinationSlot.IsOccupied)
            {
                return false;
            }

            if (!destinationSlot.TryAssign(npc))
            {
                return false;
            }

            sourceSlot.Clear(npc);
            LogQueueEvent(
                $"{npc.name} moved Queue{sourceSlot.QueueNumber:00} -> Queue{destinationSlot.QueueNumber:00}");
            npc.EnterVisibleQueue(destinationSlot);
            return true;
        }

        private bool TryAdmitInternalWaiter()
        {
            if (_internalWaitingList.Count == 0 || GetVisibleNpcCount() >= MaxVisibleNpcCount)
            {
                return false;
            }

            QueueSlot entrySlot = queueSlots[queueSlots.Length - 1];
            if (entrySlot.IsOccupied)
            {
                return false;
            }

            NPCController npc = _internalWaitingList[0];
            if (!entrySlot.TryAssign(npc))
            {
                return false;
            }

            _internalWaitingList.RemoveAt(0);
            LogQueueEvent($"{npc.name} assigned to Queue08");
            npc.EnterVisibleQueue(entrySlot);
            return true;
        }

        private void ResolveUrinalFlowReferences()
        {
            if (urinalManager == null)
            {
                urinalManager = FindAnyObjectByType<UrinalManager>();
            }

            if (ticketManager == null)
            {
                ticketManager = FindAnyObjectByType<UrinalTicketManager>();
            }

            if (nyoiceApproachPoint == null)
            {
                GameObject point = GameObject.Find("GameStage/Queue/NyoiceApproachPoint");
                nyoiceApproachPoint = point != null ? point.transform : null;
            }

            if (lineCrossingTarget == null)
            {
                GameObject point = GameObject.Find("GameStage/NyoiceLine/CrossingTarget");
                lineCrossingTarget = point != null ? point.transform : null;
            }

            if (exitPoint == null)
            {
                GameObject point = GameObject.Find("GameStage/Exit/ExitPoint");
                exitPoint = point != null ? point.transform : null;
            }

            if (gameStateManager == null)
            {
                gameStateManager = FindAnyObjectByType<GameStateManager>();
            }

            SubscribeToTicketManager();
            SubscribeToGameState();
        }

        private void SubscribeToTicketManager()
        {
            if (ticketManager == null || _ticketEventSubscribed)
            {
                return;
            }

            ticketManager.TicketReleased += HandleTicketReleased;
            _ticketEventSubscribed = true;
        }

        private void UnsubscribeFromTicketManager()
        {
            if (ticketManager != null && _ticketEventSubscribed)
            {
                ticketManager.TicketReleased -= HandleTicketReleased;
            }

            _ticketEventSubscribed = false;
        }

        private void HandleTicketReleased()
        {
            if (IsProgressionBlocked)
            {
                LogGameOverBlock();
                return;
            }

            TryStartFrontWaitingNpc();
            CompactQueue();
        }

        private void SubscribeToGameState()
        {
            if (gameStateManager != null)
            {
                gameStateManager.GameOver -= HandleGameOver;
                gameStateManager.GameOver += HandleGameOver;
            }
        }

        private void UnsubscribeFromGameState()
        {
            if (gameStateManager != null)
            {
                gameStateManager.GameOver -= HandleGameOver;
            }
        }

        private void HandleGameOver()
        {
            LogGameOverBlock();
        }

        private void LogGameOverBlock()
        {
            if (!IsProgressionBlocked || _gameOverLogged)
            {
                return;
            }

            _gameOverLogged = true;
            LogQueueEvent("Queue progression blocked because game is over");
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

        private void LogQueueEvent(string message)
        {
            if (enableQueueDebugLogs)
            {
                Debug.Log(message, this);
            }
        }

        private int GetVisibleNpcCount()
        {
            var visibleNpcs = new HashSet<NPCController>();

            if (_decisionPointOccupant != null)
            {
                visibleNpcs.Add(_decisionPointOccupant);
            }

            if (queueSlots == null)
            {
                return visibleNpcs.Count;
            }

            foreach (QueueSlot slot in queueSlots)
            {
                if (slot != null && slot.Occupant != null)
                {
                    visibleNpcs.Add(slot.Occupant);
                }
            }

            return visibleNpcs.Count;
        }
    }
}
