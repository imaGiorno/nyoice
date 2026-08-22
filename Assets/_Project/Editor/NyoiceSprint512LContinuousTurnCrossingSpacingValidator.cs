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
    public static class NyoiceSprint512LContinuousTurnCrossingSpacingValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private static readonly MethodInfo ApproachReached = NpcMethod("HandleApproachPointReached");
        private static readonly MethodInfo CrossingReached = NpcMethod("HandleCrossingTargetReached");
        private static readonly FieldInfo ApproachOccupant = QueueField("_approachRouteOccupant");
        private static readonly FieldInfo SelectionRoutine = NpcField("_selectionWaitRoutine");

        [MenuItem("Nyoice/Validate Sprint5-12L Continuous Turn & Crossing Spacing Fix")]
        public static void ValidateContinuousTurnCrossingSpacingFix()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            StageSnapshot snapshot = new StageSnapshot();
            NyoiceGameStageSetup.SetupGameStage();
            snapshot.ValidateUnchanged();

            ValidateContinuousReservedRoute();
            ValidateCrossingSpacing(3);
            ValidateCrossingSpacing(5);
            NyoiceSprint512KWallRouteConsistencyValidator.ValidateWallRouteConsistencyFix();
            Debug.Log("Sprint5-12L Continuous Turn & Crossing Spacing Fix validation passed.");
        }

        private static void ValidateContinuousReservedRoute()
        {
            using (var fixture = new Fixture("Continuous"))
            {
                NPCController npc = fixture.CreateReservedNpc("NPC_001", 8);
                fixture.PlaceAtApproach(npc);
                ApproachReached.Invoke(npc, null);
                NPCMovement movement = npc.GetComponent<NPCMovement>();
                Require(npc.State == NPCState.CrossingLine && movement.IsMoving &&
                        Close(movement.TargetPosition, fixture.Crossing.position),
                    "Reserved NPC did not move continuously from ApproachPoint to CrossingTarget.");
                Require(SelectionRoutine.GetValue(npc) == null,
                    "Reserved NPC started a fixed selection wait in the wall route.");

                npc.transform.position = fixture.Crossing.position;
                CrossingReached.Invoke(npc, null);
                Vector3 horizontal = new Vector3(
                    npc.TargetUrinal.MovePoint.position.x, fixture.Crossing.position.y,
                    npc.TargetUrinal.MovePoint.position.z);
                Require(npc.State == NPCState.WalkingToUrinal && movement.IsMoving &&
                        Close(movement.TargetPosition, horizontal),
                    "CrossingTarget did not continue directly into horizontal movement.");
            }
        }

        private static void ValidateCrossingSpacing(int count)
        {
            using (var fixture = new Fixture($"Spacing{count}"))
            {
                var npcs = new List<NPCController>();
                for (int index = 0; index < count; index++)
                {
                    npcs.Add(fixture.CreateReservedNpc($"NPC_{index + 1:000}", 8 - index));
                }

                fixture.PlaceAtApproach(npcs[0]);
                ApproachReached.Invoke(npcs[0], null);
                Require(fixture.Queue.CrossingCorridorOccupant == npcs[0],
                    "First NPC did not enter Crossing Corridor.");

                for (int index = 1; index < count; index++)
                {
                    NPCController previous = npcs[index - 1];
                    NPCController follower = npcs[index];
                    fixture.PlaceAtApproach(follower);
                    ApproachReached.Invoke(follower, null);
                    Require(follower.State == NPCState.ApproachingLine &&
                            Close(follower.transform.position, fixture.Approach.position) &&
                            fixture.Queue.ApproachRouteOccupant == follower,
                        "Follower did not wait on the Approach side of the red wall.");
                    Require(follower.TargetUrinal != null && follower.TargetUrinal.ReservedBy == follower,
                        "Follower reservation changed while waiting for Crossing Corridor.");

                    previous.transform.position = fixture.Crossing.position +
                                                  (Vector3.left * fixture.Queue.CrossingMinimumCenterSpacing);
                    fixture.Queue.NotifyCrossingCorridorProgress(previous);
                    Require(fixture.Queue.CrossingCorridorOccupant == follower &&
                            follower.State == NPCState.CrossingLine,
                        "Follower was not released in FIFO order after minimum spacing.");
                    float spacing = Vector3.Distance(previous.transform.position, fixture.Crossing.position);
                    Require(spacing + 0.0001f >= fixture.Queue.CrossingMinimumCenterSpacing,
                        "Crossing Corridor released a follower below minimum spacing.");
                }

                for (int index = 1; index < npcs.Count; index++)
                {
                    Require(npcs[index - 1].name.CompareTo(npcs[index].name) < 0,
                        "Crossing Corridor FIFO order changed.");
                }
                Debug.Log($"Sprint5-12L {count} NPC spacing validation passed; " +
                          $"minimum center spacing={fixture.Queue.CrossingMinimumCenterSpacing:0.00}.");
            }
        }

        private static MethodInfo NpcMethod(string name) =>
            typeof(NPCController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingMethodException(typeof(NPCController).FullName, name);

        private static FieldInfo NpcField(string name) =>
            typeof(NPCController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingFieldException(typeof(NPCController).FullName, name);

        private static FieldInfo QueueField(string name) =>
            typeof(QueueManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingFieldException(typeof(QueueManager).FullName, name);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static bool Close(Vector3 a, Vector3 b) => Vector3.SqrMagnitude(a - b) < 0.000001f;

        private sealed class Fixture : IDisposable
        {
            private readonly UrinalTicketManager _tickets;
            private readonly UrinalController[] _urinals;

            public Fixture(string name)
            {
                Root = new GameObject($"Sprint512L_{name}");
                Root.SetActive(false);
                var slots = new QueueSlot[8];
                for (int index = 0; index < slots.Length; index++)
                {
                    Transform slot = Point(Root.transform, $"Queue{index + 1:00}",
                        new Vector3(7f, -2.5f + index * 0.9f, 0f));
                    slots[index] = slot.gameObject.AddComponent<QueueSlot>();
                    slots[index].Initialize(index + 1);
                }
                Transform decision = Point(Root.transform, "DecisionPoint", new Vector3(6.5f, -2.5f, 0f));
                Approach = Point(Root.transform, "NyoiceApproachPoint", new Vector3(6.2f, -2.7f, 0f));
                Crossing = Point(Root.transform, "CrossingTarget", new Vector3(5.8f, -2.7f, 0f));
                _urinals = CreateUrinals(Root.transform);
                var urinalManager = Add<UrinalManager>("UrinalManager");
                urinalManager.Configure(_urinals, null, null);
                _tickets = Add<UrinalTicketManager>("Tickets");
                _tickets.Configure(8);
                Queue = Add<QueueManager>("QueueManager");
                Queue.Configure(slots, decision);
                Queue.ConfigureUrinalFlow(urinalManager, _tickets, Approach, Crossing);
            }

            public GameObject Root { get; }
            public QueueManager Queue { get; }
            public Transform Approach { get; }
            public Transform Crossing { get; }

            public NPCController CreateReservedNpc(string name, int urinalNumber)
            {
                GameObject child = new GameObject(name, typeof(NPCMovement), typeof(NPCController));
                child.transform.SetParent(Root.transform, false);
                NPCController npc = child.GetComponent<NPCController>();
                npc.Initialize(Queue);
                npc.ConfigureUrinalFlow(null, _tickets);
                Require(_tickets.TryAcquireTicket(npc), $"{name} could not acquire a ticket.");
                UrinalController urinal = _urinals[urinalNumber - 1];
                Require(urinal.Reserve(npc) && npc.AcceptUrinalAssignment(urinal),
                    $"{name} could not preserve its reservation.");
                SetState(npc, NPCState.FrontWaiting);
                npc.BeginUrinalApproach(Approach.position, Crossing.position);
                return npc;
            }

            public void PlaceAtApproach(NPCController npc)
            {
                npc.transform.position = Approach.position;
                npc.GetComponent<NPCMovement>().Stop();
                ApproachOccupant.SetValue(Queue, npc);
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);

            private T Add<T>(string name) where T : Component
            {
                var child = new GameObject(name);
                child.transform.SetParent(Root.transform, false);
                return child.AddComponent<T>();
            }

            private static void SetState(NPCController npc, NPCState state)
            {
                MethodInfo method = typeof(NPCController).GetMethod(
                    "SetState", BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(npc, new object[] { state });
            }

            private static UrinalController[] CreateUrinals(Transform parent)
            {
                var result = new UrinalController[8];
                for (int index = 0; index < result.Length; index++)
                {
                    Transform root = Point(parent, $"Urinal{index + 1:00}", Vector3.zero);
                    Transform move = Point(root, "MovePoint", new Vector3(-6f + index * 1.5f, 1.35f, 0f));
                    Transform use = Point(root, "UsePoint", new Vector3(move.position.x, 2.45f, 0f));
                    Transform exit = Point(root, "ExitStartPoint", Vector3.zero);
                    result[index] = root.gameObject.AddComponent<UrinalController>();
                    result[index].Configure(index + 1, move, use, exit, null, null);
                }
                return result;
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
                    "GameStage/NyoiceLine/CrossingTarget", "GameStage/Queue/NyoiceApproachPoint",
                    "GameStage/Entrance/SpawnPoint", "GameStage/Exit/ExitPoint"
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

            private static Transform Required(string path)
            {
                Transform target = GameObject.Find(path)?.transform;
                Require(target != null, $"Required Transform is missing: {path}.");
                return target;
            }
        }
    }
}
