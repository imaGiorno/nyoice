using System;
using System.Collections.Generic;
using System.Reflection;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint4Validator
    {
        private static readonly MethodInfo MovePointReachedMethod = GetNpcMethod("HandleMovePointReached");
        private static readonly MethodInfo UsePointReachedMethod = GetNpcMethod("HandleUsePointReached");
        private static readonly MethodInfo ApproachPointReachedMethod = GetNpcMethod(
            "HandleApproachPointReached");
        private static readonly MethodInfo CompleteSelectionWaitMethod = GetNpcMethod(
            "CompleteSelectionWait");
        private static readonly FieldInfo DecisionPointOccupantField = GetQueueManagerField(
            "_decisionPointOccupant");
        private static readonly FieldInfo InternalWaitingListField = GetQueueManagerField(
            "_internalWaitingList");

        [MenuItem("Nyoice/Validate Sprint4 Urinal Flow")]
        public static void ValidateSprint4UrinalFlow()
        {
            var root = new GameObject("Sprint4Validation");

            try
            {
                ValidateTicketLimits(root.transform);
                ValidateUrinalSelectionAndFlow(root.transform);
                ValidateSelectionZoneSerialization(root.transform);
                ValidateVisibleNpcLimit(root.transform);
                Debug.Log("Sprint 4 urinal flow validation passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateTicketLimits(Transform root)
        {
            UrinalTicketManager tickets = new GameObject("TicketValidation")
                .AddComponent<UrinalTicketManager>();
            tickets.transform.SetParent(root, false);
            tickets.Configure(8);

            Require(tickets.TotalTicketCount == 8, "Ticket total must start at eight.");
            Require(tickets.AvailableTicketCount == 8, "Eight tickets must initially be available.");

            var npcs = new List<NPCController>();
            for (int index = 0; index < 9; index++)
            {
                npcs.Add(CreateNpc(root, $"TicketNPC_{index + 1:000}"));
            }

            Require(tickets.TryAcquireTicket(npcs[0]), "The first ticket acquisition failed.");
            Require(!tickets.TryAcquireTicket(npcs[0]), "A single NPC acquired two tickets.");

            for (int index = 1; index < 8; index++)
            {
                Require(tickets.TryAcquireTicket(npcs[index]), $"Ticket acquisition {index + 1} failed.");
            }

            Require(tickets.UsedTicketCount == 8, "Eight tickets were not recorded as used.");
            Require(!tickets.TryAcquireTicket(npcs[8]), "The ninth ticket acquisition succeeded.");

            npcs[8].ConfigureUrinalFlow(null, tickets);
            npcs[8].HandleDecisionPointReached();
            npcs[8].BeginUrinalApproach(Vector3.zero, Vector3.left);
            Require(
                npcs[8].State == NPCState.FrontWaiting,
                "An NPC without a ticket advanced beyond FrontWaiting.");

            Require(tickets.ReleaseTicket(npcs[0]), "Ticket release failed.");
            Require(!tickets.ReleaseTicket(npcs[0]), "A ticket was released twice.");
        }

        private static void ValidateUrinalSelectionAndFlow(Transform root)
        {
            UrinalController[] urinals = CreateUrinals(root);
            UrinalManager manager = new GameObject("UrinalManagerValidation").AddComponent<UrinalManager>();
            manager.transform.SetParent(root, false);
            manager.Configure(urinals, null, null);

            NPCController selectionNpc = CreateNpc(root, "SelectionNPC");
            Require(manager.BeginSelection(selectionNpc), "Selection session did not start.");

            IReadOnlyList<UrinalController> available = manager.GetAvailableByPriority();
            Require(available.Count == 8, "The available urinal count is not eight.");
            for (int index = 0; index < available.Count; index++)
            {
                Require(
                    available[index].UrinalNumber == 8 - index,
                    "Automatic selection priority is not Urinal08 through Urinal01.");
            }

            Require(manager.SelectUrinal(urinals[2]), "Urinal03 selection failed.");
            Require(CountActiveHighlights(urinals) == 1, "More than one highlight is active.");

            NPCController reservationNpc = CreateNpc(root, "ReservationNPC");
            Require(manager.EndSelection(selectionNpc), "Initial selection session did not end.");
            Require(manager.BeginSelection(reservationNpc), "Reservation selection session did not start.");
            manager.SelectUrinal(urinals[7]);
            UrinalController reserved = manager.ConfirmSelection(reservationNpc);
            Require(reserved == urinals[7], "Urinal08 was not reserved.");
            Require(reserved.State == UrinalState.Reserved, "Reserved urinal state is incorrect.");
            Require(manager.GetAutomaticSelection() == urinals[6], "Reserved Urinal08 was selected again.");
            Require(manager.EndSelection(reservationNpc), "Reservation selection session did not end.");
            reserved.Release(reservationNpc);

            UrinalTicketManager flowTickets = new GameObject("FlowTicketValidation")
                .AddComponent<UrinalTicketManager>();
            flowTickets.transform.SetParent(root, false);
            flowTickets.Configure(8);

            NPCController flowNpc = CreateNpc(root, "NPC_001");
            flowNpc.Initialize(null);
            flowNpc.ConfigureUrinalFlow(manager, flowTickets);
            flowNpc.HandleDecisionPointReached();
            Require(flowTickets.TryAcquireTicket(flowNpc), "Flow NPC could not acquire a ticket.");
            Require(manager.BeginSelection(flowNpc), "Flow NPC selection session did not start.");

            flowNpc.BeginUrinalApproach(new Vector3(6.2f, -3.75f, 0f), new Vector3(5.8f, -3.75f, 0f));
            Require(flowNpc.State == NPCState.ApproachingLine, "NPC did not enter ApproachingLine.");
            Require(
                Mathf.Approximately(flowNpc.SelectionWaitSeconds, 2f),
                "Default urinal selection wait is not two seconds.");

            ApproachPointReachedMethod.Invoke(flowNpc, null);
            Require(flowNpc.State == NPCState.SelectingUrinal, "NPC did not enter SelectingUrinal.");

            manager.MoveSelection(-1);
            Require(
                manager.CurrentSelection == urinals[7],
                "Left or right selection did not initialize at Urinal08.");
            manager.MoveSelection(-1);
            Require(manager.CurrentSelection == urinals[6], "Arrow selection did not move to Urinal07.");
            Require(manager.SelectUrinal(urinals[4]), "Click-equivalent selection of Urinal05 failed.");
            Require(manager.CurrentSelection == urinals[4], "Urinal05 did not become the current selection.");
            Require(CountActiveHighlights(urinals) == 1, "Playable selection must show one highlight.");
            Require(urinals[4].Highlight.activeSelf, "Selected Highlight GameObject is not active.");
            Require(
                !Mathf.Approximately(
                    urinals[4].Highlight.transform.localPosition.z,
                    urinals[4].BodyRenderer.transform.localPosition.z),
                "Highlight is embedded at the same Z position as the urinal Body.");

            CompleteSelectionWaitMethod.Invoke(flowNpc, null);
            Require(flowNpc.State == NPCState.CrossingLine, "NPC did not cross after the selection wait.");
            flowNpc.HandleNyoiceLineCrossed();
            Require(flowNpc.TargetUrinal == urinals[4], "Line crossing did not retain selected Urinal05.");
            Require(urinals[4].State == UrinalState.Reserved, "Selected Urinal05 was not reserved.");
            Require(flowNpc.State == NPCState.WalkingToUrinal, "NPC did not start walking to the urinal.");

            MovePointReachedMethod.Invoke(flowNpc, null);
            UsePointReachedMethod.Invoke(flowNpc, null);
            Require(flowNpc.State == NPCState.UsingUrinal, "NPC did not enter UsingUrinal.");
            Require(urinals[4].State == UrinalState.Occupied, "Selected urinal did not become Occupied.");
            Require(manager.EndSelection(flowNpc), "Flow NPC selection session did not end.");

            NPCController automaticNpc = CreateNpc(root, "AutomaticSelectionNPC");
            Require(manager.BeginSelection(automaticNpc), "Automatic selection session did not start.");
            UrinalController automaticUrinal = manager.ConfirmSelection(automaticNpc);
            Require(automaticUrinal == urinals[7], "No-input selection did not reserve Urinal08.");
            Require(automaticUrinal.State == UrinalState.Reserved, "Automatic Urinal08 was not Reserved.");
            Require(manager.EndSelection(automaticNpc), "Automatic selection session did not end.");
            automaticUrinal.Release(automaticNpc);
        }

        private static void ValidateSelectionZoneSerialization(Transform root)
        {
            UrinalController[] urinals = CreateUrinals(root);
            UrinalManager urinalManager = new GameObject("SelectionZoneUrinalManager")
                .AddComponent<UrinalManager>();
            urinalManager.transform.SetParent(root, false);
            urinalManager.Configure(urinals, null, null);

            UrinalTicketManager tickets = new GameObject("SelectionZoneTickets")
                .AddComponent<UrinalTicketManager>();
            tickets.transform.SetParent(root, false);
            tickets.Configure(8);

            var queueManagerObject = new GameObject("SelectionZoneQueueManager");
            queueManagerObject.SetActive(false);
            queueManagerObject.transform.SetParent(root, false);
            QueueManager queueManager = queueManagerObject.AddComponent<QueueManager>();

            Transform approachPoint = CreatePoint(root, "SelectionZoneApproach", Vector3.right);
            Transform crossingTarget = CreatePoint(root, "SelectionZoneCrossing", Vector3.left);
            queueManager.ConfigureUrinalFlow(
                urinalManager,
                tickets,
                approachPoint,
                crossingTarget);

            NPCController firstNpc = CreateNpc(root, "SelectionZoneNPC_001");
            NPCController secondNpc = CreateNpc(root, "SelectionZoneNPC_002");

            firstNpc.ConfigureUrinalFlow(urinalManager, tickets);
            secondNpc.ConfigureUrinalFlow(urinalManager, tickets);
            Require(tickets.TryAcquireTicket(firstNpc), "First SelectionZone ticket acquisition failed.");
            Require(tickets.TryAcquireTicket(secondNpc), "Second SelectionZone ticket acquisition failed.");
            Require(queueManager.TryEnterSelectionZone(firstNpc), "First NPC could not occupy SelectionZone.");
            Require(
                !queueManager.TryEnterSelectionZone(secondNpc),
                "Two NPCs entered SelectionZone at the same time.");
            Require(
                queueManager.SelectionZoneOccupant == firstNpc &&
                urinalManager.ActiveSelectionNpc == firstNpc,
                "SelectionZone occupant changed while occupied.");

            firstNpc.HandleDecisionPointReached();
            firstNpc.BeginUrinalApproach(approachPoint.position, crossingTarget.position);
            ApproachPointReachedMethod.Invoke(firstNpc, null);
            Require(firstNpc.State == NPCState.SelectingUrinal, "SelectionZone NPC did not wait for selection.");
            Require(
                queueManager.SelectionZoneOccupant == firstNpc,
                "SelectionZone was released during the selection wait.");
            CompleteSelectionWaitMethod.Invoke(firstNpc, null);
            Require(firstNpc.State == NPCState.CrossingLine, "SelectionZone NPC did not finish its wait.");
            Require(
                queueManager.SelectionZoneOccupant == firstNpc,
                "SelectionZone was released before NyoiceLine confirmation.");
            Require(urinalManager.SelectUrinal(urinals[7]), "SelectionZone urinal selection failed.");
            Require(CountActiveHighlights(urinals) == 1, "SelectionZone must have one highlight.");
            Require(queueManager.NotifySelectionZoneCrossed(firstNpc), "First NPC did not release SelectionZone.");
            Require(CountActiveHighlights(urinals) == 0, "Highlight remained after SelectionZone release.");
            Require(queueManager.TryEnterSelectionZone(secondNpc), "Second NPC could not enter the released SelectionZone.");
            Require(
                queueManager.SelectionZoneOccupant == secondNpc &&
                urinalManager.ActiveSelectionNpc == secondNpc,
                "Second NPC did not become the SelectionZone occupant.");

            UnityEngine.Object.DestroyImmediate(queueManager.gameObject);
        }

        private static void ValidateVisibleNpcLimit(Transform root)
        {
            Transform validationRoot = new GameObject("VisibleNpcLimitValidation").transform;
            validationRoot.SetParent(root, false);

            QueueSlot[] slots = new QueueSlot[8];
            for (int index = 0; index < slots.Length; index++)
            {
                var slotObject = new GameObject($"VisibleQueue{index + 1:00}");
                slotObject.transform.SetParent(validationRoot, false);
                slots[index] = slotObject.AddComponent<QueueSlot>();
                slots[index].Initialize(index + 1);
            }

            Transform decisionPoint = CreatePoint(validationRoot, "VisibleDecisionPoint", Vector3.zero);
            Transform approachPoint = CreatePoint(validationRoot, "VisibleApproachPoint", Vector3.right);
            Transform crossingTarget = CreatePoint(validationRoot, "VisibleCrossingTarget", Vector3.left);

            UrinalController[] urinals = CreateUrinals(validationRoot);
            UrinalManager urinalManager = new GameObject("VisibleUrinalManager")
                .AddComponent<UrinalManager>();
            urinalManager.transform.SetParent(validationRoot, false);
            urinalManager.Configure(urinals, null, null);

            UrinalTicketManager tickets = new GameObject("VisibleTicketManager")
                .AddComponent<UrinalTicketManager>();
            tickets.transform.SetParent(validationRoot, false);
            tickets.Configure(8);

            var queueManagerObject = new GameObject("VisibleQueueManager");
            queueManagerObject.SetActive(false);
            queueManagerObject.transform.SetParent(validationRoot, false);
            QueueManager queueManager = queueManagerObject.AddComponent<QueueManager>();
            queueManager.Configure(slots, decisionPoint);
            queueManager.ConfigureUrinalFlow(
                urinalManager,
                tickets,
                approachPoint,
                crossingTarget);

            var queuedNpcs = new List<NPCController>();
            for (int index = 0; index < 6; index++)
            {
                NPCController npc = CreateNpc(validationRoot, $"VisibleQueueNPC_{index + 1:000}");
                Require(slots[index].TryAssign(npc), "Visible queue setup failed.");
                queuedNpcs.Add(npc);
            }

            NPCController frontNpc = CreateNpc(validationRoot, "VisibleFrontNPC");
            frontNpc.Initialize(queueManager);
            frontNpc.ConfigureUrinalFlow(urinalManager, tickets);
            DecisionPointOccupantField.SetValue(queueManager, frontNpc);

            NPCController eighthNpc = CreateNpc(validationRoot, "VisibleInternalNPC_008");
            NPCController ninthNpc = CreateNpc(validationRoot, "VisibleInternalNPC_009");
            AddInternalWaiter(queueManager, eighthNpc);
            AddInternalWaiter(queueManager, ninthNpc);

            frontNpc.HandleDecisionPointReached();

            Require(queueManager.IsSelectionZoneOccupied, "SelectionZone was not occupied.");
            Require(queueManager.VisibleNpcCount == 8, "Visible NPC count exceeded eight in SelectionZone flow.");
            Require(eighthNpc.IsPresentationVisible, "The available visible slot was not filled.");
            Require(!ninthNpc.IsPresentationVisible, "The ninth NPC became visible.");
            Require(queueManager.InternalWaitingList.Count == 1, "More than one internal NPC was admitted.");

            slots[0].Clear(queuedNpcs[0]);
            DecisionPointOccupantField.SetValue(queueManager, queuedNpcs[0]);
            Require(slots[6].TryAssign(frontNpc), "Duplicate-count validation setup failed.");
            Require(
                queueManager.VisibleNpcCount == 8,
                "The same NPC was counted more than once across visible locations.");
            slots[6].Clear(frontNpc);

            Require(queueManager.NotifySelectionZoneCrossed(frontNpc), "SelectionZone release failed.");
            Require(queueManager.VisibleNpcCount <= 8, "Visible NPC count exceeded eight after zone release.");
            Require(!ninthNpc.IsPresentationVisible, "The ninth NPC became visible after zone release.");
        }

        private static void AddInternalWaiter(QueueManager queueManager, NPCController npc)
        {
            npc.Initialize(queueManager);
            npc.WaitInternally();
            var internalWaitingList = (List<NPCController>)InternalWaitingListField.GetValue(queueManager);
            internalWaitingList.Add(npc);
        }

        private static UrinalController[] CreateUrinals(Transform root)
        {
            var urinals = new UrinalController[8];
            for (int index = 0; index < urinals.Length; index++)
            {
                var urinal = new GameObject($"Urinal{index + 1:00}");
                urinal.transform.SetParent(root, false);

                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(urinal.transform, false);

                var highlight = new GameObject("Highlight");
                highlight.transform.SetParent(urinal.transform, false);
                highlight.transform.localPosition = new Vector3(0f, 0f, -0.4f);
                highlight.SetActive(false);

                Transform movePoint = CreatePoint(urinal.transform, "MovePoint", new Vector3(index, 1f, 0f));
                Transform usePoint = CreatePoint(urinal.transform, "UsePoint", new Vector3(index, 2f, 0f));
                UrinalController controller = urinal.AddComponent<UrinalController>();
                controller.Configure(index + 1, movePoint, usePoint, highlight, body.GetComponent<Renderer>());
                urinals[index] = controller;
            }

            return urinals;
        }

        private static NPCController CreateNpc(Transform root, string name)
        {
            var npcObject = new GameObject(name);
            npcObject.transform.SetParent(root, false);
            npcObject.AddComponent<NPCMovement>();
            NPCController npc = npcObject.AddComponent<NPCController>();
            npc.Initialize(null);
            return npc;
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent, false);
            point.transform.position = position;
            return point.transform;
        }

        private static int CountActiveHighlights(IEnumerable<UrinalController> urinals)
        {
            int count = 0;
            foreach (UrinalController urinal in urinals)
            {
                if (urinal.IsSelected)
                {
                    count++;
                }
            }

            return count;
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

        private static FieldInfo GetQueueManagerField(string fieldName)
        {
            FieldInfo field = typeof(QueueManager).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(typeof(QueueManager).FullName, fieldName);
            }

            return field;
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
