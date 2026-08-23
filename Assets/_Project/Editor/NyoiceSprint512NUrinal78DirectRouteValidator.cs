using System;
using System.Reflection;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512NUrinal78DirectRouteValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private static readonly MethodInfo ApproachReached = NpcMethod("HandleApproachPointReached");
        private static readonly MethodInfo CrossingReached = NpcMethod("HandleCrossingTargetReached");
        private static readonly MethodInfo WallClearanceReached = NpcMethod("HandleWallClearanceReached");
        private static readonly FieldInfo SelectionRoutine = NpcField("_selectionWaitRoutine");

        [MenuItem("Nyoice/Validate Sprint5-12N Urinal7-8 Direct Route Correction")]
        public static void ValidateUrinal78DirectRouteCorrection()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            ValidateActualSceneTargets();
            NyoiceSprint512MHybridUrinalRoutingValidator.ValidateHybridUrinalRoutingFix();
            Debug.Log("Sprint5-12N Urinal7-8 Direct Route Correction validation passed.");
        }

        public static void ValidateActualSceneTargets()
        {
            Transform approach = Required("GameStage/Queue/NyoiceApproachPoint");
            Transform crossing = Required("GameStage/NyoiceLine/CrossingTarget");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            Require(prefab != null && prefab.GetComponent<NPCController>() != null,
                "NPC prefab is unavailable.");

            for (int number = 1; number <= 8; number++)
            {
                UrinalController urinal = FindSceneUrinal(number);
                Require(urinal != null && urinal.MovePoint != null && urinal.UsePoint != null,
                    $"Scene Urinal{number:00} route points are unavailable.");
                Require(urinal.UrinalNumber == number,
                    $"Scene Urinal{number:00} component number is {urinal.UrinalNumber}.");
                SpriteRenderer sceneSprite = urinal.transform.Find("VisualRoot/PixelVisual")
                    ?.GetComponent<SpriteRenderer>();
                Sprite originalSprite = sceneSprite != null ? sceneSprite.sprite : null;

                GameObject ticketObject = new GameObject($"Sprint512N_Tickets{number:00}");
                GameObject npcObject = null;
                try
                {
                    var tickets = ticketObject.AddComponent<UrinalTicketManager>();
                    tickets.Configure(1);
                    npcObject = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    Require(npcObject != null, "NPC prefab instantiation failed.");
                    npcObject.name = $"Sprint512N_NPC{number:00}";
                    NPCController npc = npcObject.GetComponent<NPCController>();
                    NPCMovement movement = npcObject.GetComponent<NPCMovement>();
                    npc.Initialize(null);
                    npc.ConfigureUrinalFlow(null, tickets);
                    Require(tickets.TryAcquireTicket(npc) && urinal.Reserve(npc) &&
                            npc.AcceptUrinalAssignment(urinal),
                        $"Scene Urinal{number:00} reservation fixture failed.");
                    SetState(npc, NPCState.FrontWaiting);
                    npc.BeginUrinalApproach(approach.position, crossing.position);
                    npc.transform.position = approach.position;
                    movement.Stop();
                    ApproachReached.Invoke(npc, null);
                    Require(npc.State == NPCState.CrossingLine &&
                            Close(movement.TargetPosition, crossing.position) &&
                            SelectionRoutine.GetValue(npc) == null,
                        $"Scene Urinal{number:00} did not continuously target CrossingTarget.");

                    npc.transform.position = crossing.position;
                    CrossingReached.Invoke(npc, null);
                    Vector3 horizontalTarget = new Vector3(
                        urinal.MovePoint.position.x, crossing.position.y, urinal.MovePoint.position.z);
                    Vector3 expectedTarget = number >= 7 ? horizontalTarget : urinal.MovePoint.position;
                    Require(Close(movement.TargetPosition, expectedTarget),
                        $"Scene Urinal{number:00} selected the wrong post-Crossing target " +
                        $"{movement.TargetPosition}; expected {expectedTarget}.");
                    if (number >= 7)
                    {
                        Require(Close(movement.TargetPosition, horizontalTarget) &&
                                !Close(movement.TargetPosition, urinal.MovePoint.position),
                            $"Scene Urinal{number:00} did not use the horizontal waypoint.");
                        WallClearanceReached.Invoke(npc, null);
                        Require(Close(movement.TargetPosition, urinal.MovePoint.position),
                            $"Scene Urinal{number:00} did not rise from horizontal target to MovePoint.");
                        Debug.Log($"Sprint5-12N scene Urinal{number:00} Target transition: " +
                                  $"{approach.position} -> {crossing.position} -> {horizontalTarget} -> " +
                                  $"{movement.TargetPosition}.");
                    }
                    else
                    {
                        Debug.Log($"Sprint5-12N scene Urinal{number:00} Target transition: " +
                                  $"{approach.position} -> {crossing.position} -> {movement.TargetPosition}.");
                    }
                    urinal.Release(npc);
                    if (sceneSprite != null) sceneSprite.sprite = originalSprite;
                }
                finally
                {
                    if (npcObject != null) UnityEngine.Object.DestroyImmediate(npcObject);
                    UnityEngine.Object.DestroyImmediate(ticketObject);
                }
            }
        }

        private static UrinalController FindSceneUrinal(int number)
        {
            foreach (UrinalController urinal in UnityEngine.Object.FindObjectsByType<UrinalController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (urinal.UrinalNumber == number &&
                    urinal.transform.parent != null && urinal.transform.parent.name == "Urinals")
                {
                    return urinal;
                }
            }
            return null;
        }

        private static Transform Required(string path)
        {
            Transform target = GameObject.Find(path)?.transform;
            Require(target != null, $"Required Transform is missing: {path}.");
            return target;
        }

        private static MethodInfo NpcMethod(string name) =>
            typeof(NPCController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingMethodException(typeof(NPCController).FullName, name);

        private static FieldInfo NpcField(string name) =>
            typeof(NPCController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingFieldException(typeof(NPCController).FullName, name);

        private static void SetState(NPCController npc, NPCState state)
        {
            MethodInfo method = typeof(NPCController).GetMethod(
                "SetState", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(npc, new object[] { state });
        }

        private static bool Close(Vector3 a, Vector3 b) => Vector3.SqrMagnitude(a - b) < 0.000001f;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
