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
        private bool enableQueueDebugLogs = true;

        private readonly List<NPCController> _internalWaitingList = new List<NPCController>();
        private NPCController _decisionPointOccupant;
        private bool _hasLoggedInitializationError;
        private bool _ticketEventSubscribed;

        public IReadOnlyList<NPCController> InternalWaitingList => _internalWaitingList;

        private void Awake()
        {
            EnsureRuntimeReferences();
            ResolveUrinalFlowReferences();
        }

        private void OnDestroy()
        {
            UnsubscribeFromTicketManager();
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

        public void Enqueue(NPCController npc)
        {
            npc.Initialize(this);
            npc.ConfigureUrinalFlow(urinalManager, ticketManager);
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
            QueueSlot reachedSlot = npc.CurrentSlot;
            if (reachedSlot != null)
            {
                LogQueueEvent($"{npc.name} reached Queue{reachedSlot.QueueNumber:00}");
            }

            CompactQueue();
        }

        public void NotifyDecisionPointReached(NPCController npc)
        {
            LogQueueEvent($"{npc.name} reached DecisionPoint");
            TryStartFrontWaitingNpc();
            CompactQueue();
        }

        private void CompactQueue()
        {
            if (!HasValidQueueReferences())
            {
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

            SubscribeToTicketManager();
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
            TryStartFrontWaitingNpc();
            CompactQueue();
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
    }
}

