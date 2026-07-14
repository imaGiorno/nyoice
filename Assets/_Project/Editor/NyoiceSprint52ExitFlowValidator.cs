using System;
using System.Linq;
using System.Reflection;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint52ExitFlowValidator
    {
        private static readonly MethodInfo ApproachPointReachedMethod = GetNpcMethod(
            "HandleApproachPointReached");
        private static readonly MethodInfo CompleteSelectionWaitMethod = GetNpcMethod(
            "CompleteSelectionWait");
        private static readonly MethodInfo MovePointReachedMethod = GetNpcMethod(
            "HandleMovePointReached");
        private static readonly MethodInfo UsePointReachedMethod = GetNpcMethod(
            "HandleUsePointReached");
        private static readonly MethodInfo CompleteUrinationMethod = GetNpcMethod(
            "CompleteUrination");
        private static readonly MethodInfo ExitStartPointReachedMethod = GetNpcMethod(
            "HandleExitStartPointReached");
        private static readonly MethodInfo ExitPointReachedMethod = GetNpcMethod(
            "HandleExitPointReached");

        [MenuItem("Nyoice/Validate Sprint5-2 Exit Flow")]
        public static void ValidateExitFlow()
        {
            var root = new GameObject("Sprint52ExitFlowValidation");
            root.SetActive(false);

            try
            {
                RunValidation(root.transform);
                Debug.Log("Sprint 5-2 exit flow validation passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void RunValidation(Transform root)
        {
            Transform movePoint = CreatePoint(root, "MovePoint", new Vector3(1f, 1f, 0f));
            Transform usePoint = CreatePoint(root, "UsePoint", new Vector3(1f, 2f, 0f));
            Transform exitStartPoint = CreatePoint(
                root,
                "ExitStartPoint",
                new Vector3(0.5f, 1f, 0f));
            Transform exitPoint = CreatePoint(root, "ExitPoint", new Vector3(-5.75f, -3.75f, 0f));
            Transform decisionPoint = CreatePoint(root, "DecisionPoint", new Vector3(6.5f, -3.75f, 0f));
            Transform approachPoint = CreatePoint(
                root,
                "NyoiceApproachPoint",
                new Vector3(6.2f, -3.75f, 0f));
            Transform crossingTarget = CreatePoint(
                root,
                "CrossingTarget",
                new Vector3(5.8f, -3.75f, 0f));

            QueueSlot[] queueSlots = CreateQueueSlots(root);
            UrinalController urinal = CreateUrinal(
                root,
                movePoint,
                usePoint,
                exitStartPoint);
            UrinalManager urinalManager = CreateChildComponent<UrinalManager>(root, "UrinalManager");
            urinalManager.Configure(new[] { urinal }, null, null);

            UrinalTicketManager ticketManager = CreateChildComponent<UrinalTicketManager>(
                root,
                "UrinalTicketManager");
            Require(ticketManager.TotalTicketCount == 8, "Ticket initial value is not eight.");
            ticketManager.Configure(1);

            int ticketReleasedEventCount = 0;
            int ticketsAvailableDuringReleaseEvent = 0;
            ticketManager.TicketReleased += () =>
            {
                ticketReleasedEventCount++;
                ticketsAvailableDuringReleaseEvent = ticketManager.AvailableTicketCount;
            };

            QueueManager queueManager = CreateChildComponent<QueueManager>(root, "QueueManager");
            queueManager.Configure(queueSlots, decisionPoint);
            queueManager.ConfigureUrinalFlow(
                urinalManager,
                ticketManager,
                approachPoint,
                crossingTarget);
            queueManager.ConfigureExitFlow(exitPoint);

            NPCController departingNpc = CreateNpc(root, "NPC_001", queueManager, urinalManager, ticketManager, exitPoint);
            Require(ticketManager.TryAcquireTicket(departingNpc), "Departing NPC could not acquire a Ticket.");
            departingNpc.HandleDecisionPointReached();
            Require(queueManager.TryEnterSelectionZone(departingNpc), "Departing NPC could not enter SelectionZone.");
            Require(urinalManager.SelectUrinal(urinal), "Urinal08 selection failed.");

            departingNpc.BeginUrinalApproach(approachPoint.position, crossingTarget.position);
            ApproachPointReachedMethod.Invoke(departingNpc, null);
            CompleteSelectionWaitMethod.Invoke(departingNpc, null);
            departingNpc.HandleNyoiceLineCrossed();
            Require(departingNpc.State == NPCState.WalkingToUrinal, "NPC did not start walking to Urinal08.");

            MovePointReachedMethod.Invoke(departingNpc, null);
            Require(
                departingNpc.GetComponent<NPCMovement>().TargetPosition == usePoint.position,
                "NPC did not target UsePoint after MovePoint.");
            UsePointReachedMethod.Invoke(departingNpc, null);
            Require(departingNpc.State == NPCState.UsingUrinal, "UsePoint did not enter UsingUrinal.");
            Require(urinal.IsOccupied, "Urinal08 did not become Occupied.");

            NPCController nextNpc = CreateNpc(root, "NPC_002", queueManager, urinalManager, ticketManager, exitPoint);
            SetPrivateField(queueManager, "_decisionPointOccupant", nextNpc);
            nextNpc.HandleDecisionPointReached();
            Require(nextNpc.State == NPCState.FrontWaiting, "Next NPC did not wait at DecisionPoint.");
            Require(!ticketManager.HasTicket(nextNpc), "Next NPC acquired a Ticket before release.");

            bool completed = (bool)CompleteUrinationMethod.Invoke(departingNpc, null);
            Require(completed, "Urination completion failed.");
            Require(departingNpc.State == NPCState.Leaving, "ReadyToLeave did not advance to Leaving.");
            Require(departingNpc.IsLeavingStarted, "Leaving guard was not set.");
            Require(urinal.IsAvailable, "Urinal was not released when leaving began.");
            Require(urinal.ReservedBy == null, "Released urinal still has a user.");
            Require(!ticketManager.HasTicket(departingNpc), "Departing NPC still holds its Ticket.");
            Require(ticketReleasedEventCount == 1, "TicketReleased did not fire exactly once.");
            Require(ticketsAvailableDuringReleaseEvent == 1, "Ticket availability did not increase on release.");
            Require(queueManager.SelectionZoneOccupant == nextNpc, "Next NPC did not enter SelectionZone.");
            Require(ticketManager.HasTicket(nextNpc), "Next NPC did not acquire the returned Ticket.");
            Require(nextNpc.State == NPCState.ApproachingLine, "Next NPC did not approach NyoiceLine.");
            Require(urinalManager.GetAutomaticSelection() == urinal, "Released urinal is not selectable.");

            NPCMovement departingMovement = departingNpc.GetComponent<NPCMovement>();
            Require(departingMovement.IsMoving, "Leaving NPC did not start moving.");
            Require(
                departingMovement.TargetPosition == exitStartPoint.position,
                "Leaving NPC did not target ExitStartPoint first.");
            Require(!departingNpc.BeginLeaving(), "BeginLeaving ran more than once.");
            Require(!urinal.Release(departingNpc), "Urinal accepted a duplicate Release.");
            Require(!ticketManager.ReleaseTicket(departingNpc), "Ticket accepted a duplicate Release.");
            Require(ticketReleasedEventCount == 1, "Duplicate release fired TicketReleased.");

            ExitStartPointReachedMethod.Invoke(departingNpc, null);
            Require(departingNpc.IsMovingToExitPoint, "NPC did not advance from ExitStartPoint.");
            Require(
                departingMovement.TargetPosition == exitPoint.position,
                "NPC did not target ExitPoint after ExitStartPoint.");
            ExitStartPointReachedMethod.Invoke(departingNpc, null);
            Require(
                departingMovement.TargetPosition == exitPoint.position,
                "Repeated ExitStartPoint notification changed the destination.");

            ExitPointReachedMethod.Invoke(departingNpc, null);
            Require(departingNpc.State == NPCState.Finished, "ExitPoint did not enter Finished.");
            Require(departingNpc.IsDestroyScheduled, "Finished NPC was not marked for destruction.");
            ExitPointReachedMethod.Invoke(departingNpc, null);
            Require(departingNpc.State == NPCState.Finished, "Repeated ExitPoint notification changed state.");

            ValidateVisibleLimit(root, queueManager, queueSlots, urinalManager, ticketManager, exitPoint);
        }

        private static void ValidateVisibleLimit(
            Transform root,
            QueueManager queueManager,
            QueueSlot[] queueSlots,
            UrinalManager urinalManager,
            UrinalTicketManager ticketManager,
            Transform exitPoint)
        {
            for (int index = 0; index < 7; index++)
            {
                NPCController queuedNpc = CreateNpc(
                    root,
                    $"VisibleNPC_{index + 1:00}",
                    queueManager,
                    urinalManager,
                    ticketManager,
                    exitPoint);
                Require(queueSlots[index].TryAssign(queuedNpc), "QueueSlot accepted more than one NPC.");
                queuedNpc.EnterVisibleQueue(queueSlots[index]);
            }

            Require(queueManager.VisibleNpcCount == 8, "Visible NPC count is not capped at eight.");

            NPCController hiddenNpc = CreateNpc(
                root,
                "NPC_009",
                queueManager,
                urinalManager,
                ticketManager,
                exitPoint);
            queueManager.Enqueue(hiddenNpc);
            Require(queueManager.VisibleNpcCount == 8, "Visible NPC count exceeded eight.");
            Require(!hiddenNpc.IsPresentationVisible, "Internal waiter is visible.");
            Require(queueManager.InternalWaitingList.Contains(hiddenNpc), "Hidden NPC is not waiting internally.");
        }

        private static UrinalController CreateUrinal(
            Transform root,
            Transform movePoint,
            Transform usePoint,
            Transform exitStartPoint)
        {
            var urinalObject = new GameObject("Urinal08");
            urinalObject.transform.SetParent(root, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(urinalObject.transform, false);
            var highlight = new GameObject("Highlight");
            highlight.transform.SetParent(urinalObject.transform, false);
            highlight.SetActive(false);

            UrinalController urinal = urinalObject.AddComponent<UrinalController>();
            urinal.Configure(
                8,
                movePoint,
                usePoint,
                exitStartPoint,
                highlight,
                body.GetComponent<Renderer>());
            return urinal;
        }

        private static QueueSlot[] CreateQueueSlots(Transform root)
        {
            var slots = new QueueSlot[8];
            for (int index = 0; index < slots.Length; index++)
            {
                QueueSlot slot = CreateChildComponent<QueueSlot>(root, $"Queue{index + 1:00}");
                slot.Initialize(index + 1);
                slots[index] = slot;
            }

            return slots;
        }

        private static NPCController CreateNpc(
            Transform root,
            string objectName,
            QueueManager queueManager,
            UrinalManager urinalManager,
            UrinalTicketManager ticketManager,
            Transform exitPoint)
        {
            var npcObject = new GameObject(objectName);
            npcObject.transform.SetParent(root, false);
            npcObject.AddComponent<NPCMovement>();
            NPCController npc = npcObject.AddComponent<NPCController>();
            npc.Initialize(queueManager);
            npc.ConfigureUrinalFlow(urinalManager, ticketManager);
            npc.ConfigureExitFlow(exitPoint);
            npc.ConfigureUrinationDuration(0.1f);
            return npc;
        }

        private static T CreateChildComponent<T>(Transform root, string objectName)
            where T : Component
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(root, false);
            return child.AddComponent<T>();
        }

        private static Transform CreatePoint(Transform parent, string objectName, Vector3 position)
        {
            var point = new GameObject(objectName);
            point.transform.SetParent(parent, false);
            point.transform.position = position;
            return point.transform;
        }

        private static MethodInfo GetNpcMethod(string methodName)
        {
            MethodInfo method = typeof(NPCController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(typeof(NPCController).FullName, methodName);
            }

            return method;
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
