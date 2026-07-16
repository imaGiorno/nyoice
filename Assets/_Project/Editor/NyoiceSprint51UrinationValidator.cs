using System;
using System.Reflection;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint51UrinationValidator
    {
        private static readonly MethodInfo ApproachPointReachedMethod = GetNpcMethod(
            "HandleApproachPointReached");
        private static readonly MethodInfo CompleteSelectionWaitMethod = GetNpcMethod(
            "CompleteSelectionWait");
        private static readonly MethodInfo UsePointReachedMethod = GetNpcMethod(
            "HandleUsePointReached");
        private static readonly MethodInfo CompleteUrinationMethod = GetNpcMethod(
            "CompleteUrination");

        [MenuItem("Nyoice/Validate Sprint5-1 Urination Timer")]
        public static void ValidateUrinationTimer()
        {
            var root = new GameObject("Sprint51UrinationValidation");

            try
            {
                RunValidation(root.transform);
                Debug.Log("Sprint 5-1 urination timer validation passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void RunValidation(Transform root)
        {
            Transform movePoint = CreatePoint(root, "MovePoint", new Vector3(0f, 1f, 0f));
            Transform usePoint = CreatePoint(root, "UsePoint", new Vector3(0f, 2f, 0f));

            var urinalObject = new GameObject("Urinal08");
            urinalObject.transform.SetParent(root, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(urinalObject.transform, false);
            var highlight = new GameObject("Highlight");
            highlight.transform.SetParent(urinalObject.transform, false);
            highlight.SetActive(false);

            UrinalController urinal = urinalObject.AddComponent<UrinalController>();
            urinal.Configure(8, movePoint, usePoint, highlight, body.GetComponent<Renderer>());

            UrinalManager urinalManager = new GameObject("UrinalManager")
                .AddComponent<UrinalManager>();
            urinalManager.transform.SetParent(root, false);
            urinalManager.Configure(new[] { urinal }, null, null);

            UrinalTicketManager ticketManager = new GameObject("UrinalTicketManager")
                .AddComponent<UrinalTicketManager>();
            ticketManager.transform.SetParent(root, false);
            ticketManager.Configure(8);

            var npcObject = new GameObject("NPC_001");
            npcObject.transform.SetParent(root, false);
            npcObject.AddComponent<NPCMovement>();
            NPCController npc = npcObject.AddComponent<NPCController>();
            npc.Initialize(null);
            npc.ConfigureUrinalFlow(urinalManager, ticketManager);

            Require(
                Mathf.Approximately(npc.UrinationDurationSeconds, 6f),
                "Default urination duration is not six seconds.");
            npc.ConfigureUrinationDuration(0.1f);

            Require(ticketManager.TryAcquireTicket(npc), "NPC could not acquire its ticket.");
            npc.HandleDecisionPointReached();
            Require(urinalManager.BeginSelection(npc), "Urinal selection session did not start.");
            Require(urinalManager.SelectUrinal(urinal), "Urinal08 selection failed.");

            npc.BeginUrinalApproach(Vector3.right, Vector3.left);
            ApproachPointReachedMethod.Invoke(npc, null);
            CompleteSelectionWaitMethod.Invoke(npc, null);
            npc.HandleNyoiceLineCrossed();
            Require(npc.State == NPCState.WalkingToUrinal, "NPC did not start walking to Urinal08.");

            npcObject.GetComponent<NPCMovement>().Stop();
            npc.transform.position = usePoint.position;
            Vector3 usePosition = npc.transform.position;
            UsePointReachedMethod.Invoke(npc, null);

            Require(npc.State == NPCState.UsingUrinal, "UsePoint did not enter UsingUrinal.");
            Require(urinal.IsOccupied, "Urinal08 did not become Occupied.");
            Require(urinal.CurrentUser == npc, "Occupied urinal user is incorrect.");
            Require(ticketManager.HasTicket(npc), "Ticket was returned when urination started.");
            Require(npc.IsUrinationTimerStarted, "Urination timer did not start.");
            Require(!npc.IsUrinationComplete, "Urination completed before its duration elapsed.");
            Require(
                npc.UrinationElapsed < npc.UrinationDurationSeconds,
                "Urination elapsed time reached its duration before completion.");
            Require(npc.State == NPCState.UsingUrinal, "NPC left UsingUrinal before completion.");
            Require(!npc.BeginUrination(), "Urination timer started twice.");

            bool completed = (bool)CompleteUrinationMethod.Invoke(npc, null);
            Require(completed, "Urination completion failed.");
            Require(npc.State == NPCState.ReadyToLeave, "NPC did not enter ReadyToLeave.");
            Require(npc.IsUrinationComplete, "Urination completion flag was not set.");
            Require(
                Mathf.Approximately(npc.UrinationElapsed, 0.1f),
                "Urination elapsed time was not recorded.");
            Require(npc.transform.position == usePosition, "ReadyToLeave NPC moved away from UsePoint.");
            Require(urinal.IsOccupied, "ReadyToLeave released its urinal.");
            Require(urinal.CurrentUser == npc, "ReadyToLeave changed the occupied urinal user.");
            Require(ticketManager.HasTicket(npc), "ReadyToLeave returned its ticket.");
            Require(!npc.BeginUrination(), "ReadyToLeave restarted urination.");
            Require(
                !(bool)CompleteUrinationMethod.Invoke(npc, null),
                "Urination completed more than once.");
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name);
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

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
