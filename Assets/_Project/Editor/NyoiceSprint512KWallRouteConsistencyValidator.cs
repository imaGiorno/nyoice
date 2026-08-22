using System;
using System.Collections.Generic;
using Nyoice.Managers;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512KWallRouteConsistencyValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private const float ExpectedWallTop = 3.25f;
        private const float ExpectedWallBottom = -0.4f;
        private const float ExpectedTurnY = -2.7f;
        private const float RequiredClearance = 0.3f;

        [MenuItem("Nyoice/Validate Sprint5-12K Wall Route Consistency Fix")]
        public static void ValidateWallRouteConsistencyFix()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            StageSnapshot snapshot = new StageSnapshot();
            NyoiceGameStageSetup.SetupGameStage();

            ValidateWallAndRoutes(snapshot);
            snapshot.ValidateSetupStable();
            ValidateFixedCoordinates();
            ValidateFlowConfiguration();
            NyoiceSprint55AutoUrinalSelectionValidator.ValidateAutoUrinalSelection();
            NyoiceSprint512FUiWidthVisualRestoreValidator.ValidateUiWidthVisualRestore();
            Debug.Log("Sprint5-12K Wall Route Consistency Fix validation passed.");
        }

        private static void ValidateWallAndRoutes(StageSnapshot snapshot)
        {
            Transform root = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot");
            Transform wall = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/Wall");
            Transform crossing = RequiredTransform("GameStage/NyoiceLine/CrossingTarget");
            Transform approach = RequiredTransform("GameStage/Queue/NyoiceApproachPoint");
            Require(CountDirectChildren(root.parent, "WallVisualRoot") == 1 &&
                    CountDirectChildren(root, "Wall") == 1 && root.childCount == 1,
                "WallVisual must be one continuous, non-duplicated wall.");
            Require(root.GetComponentsInChildren<Collider>(true).Length == 0,
                "WallVisual must not add Colliders.");

            float wallTop = Edge(wall, true);
            float wallBottom = Edge(wall, false);
            float wallLength = Mathf.Abs(wall.lossyScale.y);
            Require(Mathf.Approximately(wallTop, ExpectedWallTop) &&
                    Mathf.Approximately(wallBottom, ExpectedWallBottom) && wallLength >= 3.5f,
                "WallVisual is not the natural pre-Sprint5-12G wall length.");
            Require(Mathf.Approximately(crossing.position.y, ExpectedTurnY) &&
                    Mathf.Approximately(approach.position.y, ExpectedTurnY) &&
                    crossing.position.y < wallBottom,
                "Approach/Crossing turn point is not below WallVisual.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            Transform pixel = prefab?.transform.Find("VisualRoot/PixelVisual");
            SpriteRenderer renderer = pixel?.GetComponent<SpriteRenderer>();
            Require(renderer != null && renderer.sprite != null, "NPC PixelVisual bounds are unavailable.");
            Bounds bounds = renderer.sprite.bounds;
            float halfWidth = Mathf.Abs(bounds.size.x * pixel.localScale.x) * 0.5f;
            float topOffset = pixel.localPosition.y + bounds.center.y * pixel.localScale.y +
                              Mathf.Abs(bounds.size.y * pixel.localScale.y) * 0.5f;
            float bottomOffset = pixel.localPosition.y + bounds.center.y * pixel.localScale.y -
                                 Mathf.Abs(bounds.size.y * pixel.localScale.y) * 0.5f;
            Require(crossing.position.y + topOffset < wallBottom,
                "PixelVisual has not fully passed the wall bottom before turning left.");

            float wallLeft = wall.position.x - Mathf.Abs(wall.lossyScale.x) * 0.5f;
            float minimumClearance = float.PositiveInfinity;
            int limitingUrinal = 0;
            for (int index = 1; index <= 8; index++)
            {
                Transform move = RequiredTransform($"GameStage/Waypoints/Urinal{index:00}/MovePoint");
                float clearCenterX = wallLeft - halfWidth;
                Require(move.position.x < clearCenterX,
                    $"Urinal{index:00} MovePoint does not clear WallVisual horizontally.");
                float clearance = wallBottom - (crossing.position.y + topOffset);
                Require(clearance + 0.0001f >= RequiredClearance,
                    $"Urinal{index:00} PixelVisual overlaps or shortcuts through WallVisual; clearance={clearance:0.000}.");
                if (clearance < minimumClearance)
                {
                    minimumClearance = clearance;
                    limitingUrinal = index;
                }
                Debug.Log($"Sprint5-12K Urinal{index:00}: {approach.position} -> {crossing.position} -> " +
                          $"{move.position}, wall clearance={clearance:0.000}.");
            }

            Require(limitingUrinal >= 1, "No urinal route was validated.");
            Require(snapshot.WallPosition == wall.position && snapshot.WallScale == wall.localScale,
                "WallVisual changed after repeated Setup.");
            Debug.Log($"Sprint5-12K WallVisual top={wallTop:0.000}, bottom={wallBottom:0.000}, " +
                      $"length={wallLength:0.000}; PixelVisual local Y={bottomOffset:0.000}..{topOffset:0.000}; " +
                      $"minimum clearance={minimumClearance:0.000} at Urinal{limitingUrinal:00}.");
        }

        private static void ValidateFixedCoordinates()
        {
            Require(Close(RequiredTransform("GameStage/Entrance/SpawnPoint").position, new Vector3(7f, 4.5f, 0f)),
                "SpawnPoint changed.");
            Require(Close(RequiredTransform("GameStage/Exit/ExitPoint").position, new Vector3(-5.75f, -2.5f, 0f)),
                "ExitPoint changed.");
            for (int index = 1; index <= 8; index++)
            {
                Require(Close(RequiredTransform($"GameStage/Queue/Queue{index:00}").position,
                        new Vector3(7f, -2.5f + ((index - 1) * 0.9f), 0f)),
                    $"Queue{index:00} changed.");
                float x = -6f + ((index - 1) * 1.5f);
                Require(Close(RequiredTransform($"GameStage/Waypoints/Urinal{index:00}/MovePoint").position,
                            new Vector3(x, 1.35f, 0f)) &&
                        Close(RequiredTransform($"GameStage/Waypoints/Urinal{index:00}/UsePoint").position,
                            new Vector3(x, 2.45f, 0f)),
                    $"Urinal{index:00} MovePoint/UsePoint changed.");
            }
        }

        private static void ValidateFlowConfiguration()
        {
            QueueManager queue = UnityEngine.Object.FindAnyObjectByType<QueueManager>();
            Require(queue != null && queue.HasInitialFlowReferences,
                "FIFO urinal flow is not configured.");
        }

        private static float Edge(Transform target, bool top) =>
            target.position.y + (top ? 1f : -1f) * Mathf.Abs(target.lossyScale.y) * 0.5f;

        private static int CountDirectChildren(Transform parent, string name)
        {
            int count = 0;
            for (int index = 0; index < parent.childCount; index++)
            {
                if (parent.GetChild(index).name == name) count++;
            }
            return count;
        }

        private static Transform RequiredTransform(string path)
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

        private sealed class StageSnapshot
        {
            private readonly Dictionary<string, Vector3> _positions = new Dictionary<string, Vector3>();
            private readonly Vector3 _linePosition, _lineScale, _colliderCenter, _colliderSize;
            private readonly bool _isTrigger;

            public StageSnapshot()
            {
                string[] paths =
                {
                    "GameStage/Entrance/SpawnPoint", "GameStage/Queue/DecisionPoint",
                    "GameStage/Queue/NyoiceApproachPoint", "GameStage/NyoiceLine/CrossingTarget",
                    "GameStage/Exit/ExitPoint"
                };
                foreach (string path in paths) _positions[path] = RequiredTransform(path).position;
                for (int index = 1; index <= 8; index++)
                {
                    _positions[$"GameStage/Queue/Queue{index:00}"] =
                        RequiredTransform($"GameStage/Queue/Queue{index:00}").position;
                    foreach (string point in new[] { "MovePoint", "UsePoint" })
                    {
                        string path = $"GameStage/Waypoints/Urinal{index:00}/{point}";
                        _positions[path] = RequiredTransform(path).position;
                    }
                }
                Transform line = RequiredTransform("GameStage/NyoiceLine/Line");
                BoxCollider collider = line.GetComponent<BoxCollider>();
                Require(collider != null, "Runtime NyoiceLine Collider is missing.");
                _linePosition = line.position;
                _lineScale = line.localScale;
                _colliderCenter = collider.center;
                _colliderSize = collider.size;
                _isTrigger = collider.isTrigger;
                Transform wall = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/Wall");
                WallPosition = wall.position;
                WallScale = wall.localScale;
            }

            public Vector3 WallPosition { get; }
            public Vector3 WallScale { get; }

            public void ValidateSetupStable()
            {
                foreach (KeyValuePair<string, Vector3> pair in _positions)
                {
                    Require(Close(RequiredTransform(pair.Key).position, pair.Value),
                        $"Setup changed coordinate on second run: {pair.Key}.");
                }
                Transform line = RequiredTransform("GameStage/NyoiceLine/Line");
                BoxCollider collider = line.GetComponent<BoxCollider>();
                Require(Close(line.position, _linePosition) && Close(line.localScale, _lineScale) &&
                        Close(collider.center, _colliderCenter) && Close(collider.size, _colliderSize) &&
                        collider.isTrigger == _isTrigger,
                    "Runtime NyoiceLine Collider changed.");
            }
        }
    }
}
