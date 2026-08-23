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
    public static class NyoiceSprint512MHybridUrinalRoutingValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private static readonly MethodInfo ApproachReached = NpcMethod("HandleApproachPointReached");
        private static readonly MethodInfo CrossingReached = NpcMethod("HandleCrossingTargetReached");
        private static readonly FieldInfo SelectionRoutine = NpcField("_selectionWaitRoutine");

        [MenuItem("Nyoice/Validate Sprint5-12M Hybrid Urinal Routing Fix")]
        public static void ValidateHybridUrinalRoutingFix()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            StageSnapshot snapshot = new StageSnapshot();
            NyoiceGameStageSetup.SetupGameStage();
            snapshot.ValidateUnchanged();

            ValidateHybridRoutesAndWallClearance();
            NyoiceSprint512LContinuousTurnCrossingSpacingValidator
                .ValidateContinuousTurnCrossingSpacingFix();
            Debug.Log("Sprint5-12M Hybrid Urinal Routing Fix validation passed.");
        }

        private static void ValidateHybridRoutesAndWallClearance()
        {
            Transform wall = Required("GameStage/NyoiceLine/WallVisualRoot/Wall");
            Transform crossing = Required("GameStage/NyoiceLine/CrossingTarget");
            Transform approach = Required("GameStage/Queue/NyoiceApproachPoint");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            Transform pixel = prefab?.transform.Find("VisualRoot/PixelVisual");
            SpriteRenderer renderer = pixel?.GetComponent<SpriteRenderer>();
            Require(renderer != null && renderer.sprite != null, "NPC PixelVisual bounds are unavailable.");
            Bounds bounds = renderer.sprite.bounds;
            float halfWidth = Mathf.Abs(bounds.size.x * pixel.localScale.x) * 0.5f;
            float topOffset = pixel.localPosition.y + bounds.center.y * pixel.localScale.y +
                              Mathf.Abs(bounds.size.y * pixel.localScale.y) * 0.5f;
            float wallLeft = wall.position.x - Mathf.Abs(wall.lossyScale.x) * 0.5f;
            float wallBottom = wall.position.y - Mathf.Abs(wall.lossyScale.y) * 0.5f;
            float crossingHorizontalClearance = wallLeft - (crossing.position.x + halfWidth);
            float crossingVerticalClearance = wallBottom - (crossing.position.y + topOffset);
            Require(crossingHorizontalClearance > 0f && crossingVerticalClearance >= 0.3f,
                "CrossingTarget does not clear WallVisual for hybrid routing.");

            for (int number = 1; number <= 8; number++)
            {
                Transform move = Required($"GameStage/Waypoints/Urinal{number:00}/MovePoint");
                using (var fixture = new RouteFixture(number, approach.position, crossing.position, move.position))
                {
                    NPCController npc = fixture.Npc;
                    ApproachReached.Invoke(npc, null);
                    NPCMovement movement = npc.GetComponent<NPCMovement>();
                    Require(npc.State == NPCState.CrossingLine &&
                            Close(movement.TargetPosition, crossing.position) &&
                            SelectionRoutine.GetValue(npc) == null,
                        $"Urinal{number:00} added a fixed wait before CrossingTarget.");
                    npc.transform.position = crossing.position;
                    CrossingReached.Invoke(npc, null);

                    Vector3 expected = number >= 7
                        ? new Vector3(move.position.x, crossing.position.y, move.position.z)
                        : move.position;
                    Require(npc.State == NPCState.WalkingToUrinal && movement.IsMoving &&
                            Close(movement.TargetPosition, expected),
                        number >= 7
                            ? $"Urinal{number:00} did not use the wall-bottom horizontal route."
                            : $"Urinal{number:00} did not move directly from CrossingTarget to MovePoint.");

                    if (number <= 6)
                    {
                        Require(move.position.x + halfWidth < wallLeft,
                            $"Urinal{number:00} MovePoint PixelVisual overlaps WallVisual.");
                    }
                    Debug.Log($"Sprint5-12M Urinal{number:00}: Crossing={crossing.position}, " +
                              $"target={expected}, direct={number <= 6}.");
                }
            }

            Debug.Log($"Sprint5-12M hybrid clearance: horizontal={crossingHorizontalClearance:0.000}, " +
                      $"vertical={crossingVerticalClearance:0.000}.");
        }

        private static MethodInfo NpcMethod(string name) =>
            typeof(NPCController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingMethodException(typeof(NPCController).FullName, name);

        private static FieldInfo NpcField(string name) =>
            typeof(NPCController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingFieldException(typeof(NPCController).FullName, name);

        private static Transform Required(string path)
        {
            Transform target = GameObject.Find(path)?.transform;
            Require(target != null, $"Required Transform is missing: {path}.");
            return target;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static bool Close(Vector3 a, Vector3 b) => Vector3.SqrMagnitude(a - b) < 0.000001f;

        private sealed class RouteFixture : IDisposable
        {
            private readonly GameObject _root;

            public RouteFixture(int number, Vector3 approach, Vector3 crossing, Vector3 move)
            {
                _root = new GameObject($"Sprint512M_Urinal{number:00}");
                _root.SetActive(false);
                var ticketObject = new GameObject("Tickets");
                ticketObject.transform.SetParent(_root.transform, false);
                var tickets = ticketObject.AddComponent<UrinalTicketManager>();
                tickets.Configure(1);

                Transform urinalRoot = Point(_root.transform, $"Urinal{number:00}", Vector3.zero);
                Transform movePoint = Point(urinalRoot, "MovePoint", move);
                Transform usePoint = Point(urinalRoot, "UsePoint", move + Vector3.up);
                Transform exitPoint = Point(urinalRoot, "ExitStartPoint", move + Vector3.left);
                var urinal = urinalRoot.gameObject.AddComponent<UrinalController>();
                urinal.Configure(number, movePoint, usePoint, exitPoint, null, null);

                var npcObject = new GameObject("NPC", typeof(NPCMovement), typeof(NPCController));
                npcObject.transform.SetParent(_root.transform, false);
                Npc = npcObject.GetComponent<NPCController>();
                Npc.Initialize(null);
                Npc.ConfigureUrinalFlow(null, tickets);
                Require(tickets.TryAcquireTicket(Npc) && urinal.Reserve(Npc) &&
                        Npc.AcceptUrinalAssignment(urinal),
                    $"Urinal{number:00} route fixture reservation failed.");
                SetState(Npc, NPCState.FrontWaiting);
                Npc.BeginUrinalApproach(approach, crossing);
                Npc.transform.position = approach;
                Npc.GetComponent<NPCMovement>().Stop();
            }

            public NPCController Npc { get; }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(_root);

            private static void SetState(NPCController npc, NPCState state)
            {
                MethodInfo method = typeof(NPCController).GetMethod(
                    "SetState", BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(npc, new object[] { state });
            }

            private static Transform Point(Transform parent, string name, Vector3 position)
            {
                var point = new GameObject(name).transform;
                point.SetParent(parent, false);
                point.position = position;
                return point;
            }
        }

        private sealed class StageSnapshot
        {
            private readonly Dictionary<string, Vector3> _positions = new Dictionary<string, Vector3>();
            private readonly Vector3 _linePosition, _lineScale, _center, _size;
            private readonly bool _trigger;

            public StageSnapshot()
            {
                string[] fixedPaths =
                {
                    "GameStage/Entrance/SpawnPoint", "GameStage/Queue/NyoiceApproachPoint",
                    "GameStage/NyoiceLine/CrossingTarget", "GameStage/Exit/ExitPoint"
                };
                foreach (string path in fixedPaths) _positions[path] = Required(path).position;
                for (int index = 1; index <= 8; index++)
                {
                    foreach (string point in new[] { "MovePoint", "UsePoint" })
                    {
                        string path = $"GameStage/Waypoints/Urinal{index:00}/{point}";
                        _positions[path] = Required(path).position;
                    }
                }
                Transform line = Required("GameStage/NyoiceLine/Line");
                BoxCollider collider = line.GetComponent<BoxCollider>();
                Require(collider != null, "Runtime Line Collider is missing.");
                _linePosition = line.position;
                _lineScale = line.localScale;
                _center = collider.center;
                _size = collider.size;
                _trigger = collider.isTrigger;
            }

            public void ValidateUnchanged()
            {
                foreach (KeyValuePair<string, Vector3> pair in _positions)
                {
                    Require(Close(Required(pair.Key).position, pair.Value),
                        $"Coordinate changed after repeated Setup: {pair.Key}.");
                }
                Transform line = Required("GameStage/NyoiceLine/Line");
                BoxCollider collider = line.GetComponent<BoxCollider>();
                Require(Close(line.position, _linePosition) && Close(line.localScale, _lineScale) &&
                        Close(collider.center, _center) && Close(collider.size, _size) &&
                        collider.isTrigger == _trigger,
                    "Runtime Line Collider changed after repeated Setup.");
            }
        }
    }
}
