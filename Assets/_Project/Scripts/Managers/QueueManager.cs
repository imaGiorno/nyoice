using System.Collections.Generic;
using Nyoice.NPC;
using UnityEngine;

namespace Nyoice.Managers
{
    [DisallowMultipleComponent]
    public sealed class QueueManager : MonoBehaviour
    {
        private const int MaxVisibleNpcCount = 8;

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
        private bool enableQueueDebugLogs = true;

        private readonly List<NPCController> _internalWaitingList = new List<NPCController>();
        private NPCController _decisionPointOccupant;
        private NPCController _selectionZoneOccupant;
        private bool _hasLoggedInitializationError;
        private bool _ticketEventSubscribed;
        private bool _gameOverLogged;

        public IReadOnlyList<NPCController> InternalWaitingList => _internalWaitingList;
        public NPCController SelectionZoneOccupant => _selectionZoneOccupant;
        public bool IsSelectionZoneOccupied => _selectionZoneOccupant != null;
        public int VisibleNpcCount => GetVisibleNpcCount();
        public bool IsProgressionBlocked => gameStateManager != null && gameStateManager.IsGameOver;

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

        public void Enqueue(NPCController npc)
        {
            if (npc == null || IsProgressionBlocked)
            {
                LogGameOverBlock();
                return;
            }

            npc.ConfigureGameState(gameStateManager);
            npc.Initialize(this);
            npc.ConfigureUrinalFlow(urinalManager, ticketManager);
            npc.ConfigureExitFlow(exitPoint);
            npc.WaitInternally();
            _internalWaitingList.Add(npc);

            if (EnsureRuntimeReferences())
            {
                ResolveUrinalFlowReferences();
                CompactQueue();
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
            LogQueueEvent($"{npc.name} released SelectionZone");
            CompactQueue();
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
        }

        private bool TryStartFrontWaitingNpc()
        {
            NPCController npc = _decisionPointOccupant;
            if (npc == null || npc.State != NPCState.FrontWaiting)
            {
                return false;
            }

            if (_selectionZoneOccupant != null)
            {
                return false;
            }

            ResolveUrinalFlowReferences();
            if (urinalManager == null || ticketManager == null ||
                nyoiceApproachPoint == null || lineCrossingTarget == null)
            {
                return false;
            }

            if (!ticketManager.TryAcquireTicket(npc))
            {
                return false;
            }

            if (!TryEnterSelectionZone(npc))
            {
                ticketManager.ReleaseTicket(npc);
                return false;
            }

            npc.ConfigureUrinalFlow(urinalManager, ticketManager);
            _decisionPointOccupant = null;
            npc.BeginUrinalApproach(nyoiceApproachPoint.position, lineCrossingTarget.position);
            return true;
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

            if (_selectionZoneOccupant != null)
            {
                visibleNpcs.Add(_selectionZoneOccupant);
            }

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
