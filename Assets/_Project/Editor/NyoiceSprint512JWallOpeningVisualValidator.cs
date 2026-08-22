using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512JWallOpeningVisualValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private const float RequiredClearance = 0.15f;

        [MenuItem("Nyoice/Validate Sprint5-12J Wall Opening Visual Fix")]
        public static void ValidateWallOpeningVisualFix()
        {
            NyoiceSprint512KWallRouteConsistencyValidator.ValidateWallRouteConsistencyFix();
            Debug.Log("Sprint5-12J Wall Opening Visual Fix validation passed.");
        }

        private static void ValidateOpening(StageSnapshot snapshot)
        {
            Transform lineRoot = RequiredTransform("GameStage/NyoiceLine");
            Transform root = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot");
            Transform upper = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/WallUpper");
            Transform lower = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/WallLower");
            Require(CountDirectChildren(lineRoot, "WallVisualRoot") == 1 &&
                    CountDirectChildren(lineRoot, "WallVisual") == 0,
                "WallVisualRoot was duplicated or legacy WallVisual remains.");
            Require(CountDirectChildren(root, "WallUpper") == 1 && CountDirectChildren(root, "WallLower") == 1,
                "Wall opening segments are duplicated or missing.");
            Require(root.GetComponentsInChildren<Collider>(true).Length == 0,
                "Wall visual objects must not have Colliders.");

            MeshRenderer runtimeRenderer = RequiredTransform("GameStage/NyoiceLine/Line").GetComponent<MeshRenderer>();
            MeshRenderer upperRenderer = upper.GetComponent<MeshRenderer>();
            MeshRenderer lowerRenderer = lower.GetComponent<MeshRenderer>();
            Require(runtimeRenderer != null && upperRenderer != null && lowerRenderer != null &&
                    upperRenderer.sharedMaterial == runtimeRenderer.sharedMaterial &&
                    lowerRenderer.sharedMaterial == runtimeRenderer.sharedMaterial &&
                    upperRenderer.sortingOrder == runtimeRenderer.sortingOrder &&
                    lowerRenderer.sortingOrder == runtimeRenderer.sortingOrder,
                "Wall segment color/material or Sorting Order changed.");
            Require(Mathf.Approximately(upper.lossyScale.x, 0.08f) &&
                    Mathf.Approximately(lower.lossyScale.x, 0.08f), "Wall segment width changed.");

            float upperTop = Edge(upper, true);
            float openingTop = Edge(upper, false);
            float openingBottom = Edge(lower, true);
            float lowerBottom = Edge(lower, false);
            Require(Mathf.Approximately(upperTop, 3.25f) && Mathf.Approximately(lowerBottom, -4.75f),
                "Wall visual endpoints changed.");
            Require(upper.lossyScale.y >= 1f, "WallUpper is too short to read as a wall.");
            Require(lower.lossyScale.y >= 1f, "WallLower is too short to read as a wall.");

            Transform crossing = RequiredTransform("GameStage/NyoiceLine/CrossingTarget");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            Transform pixel = prefab?.transform.Find("VisualRoot/PixelVisual");
            SpriteRenderer sprite = pixel?.GetComponent<SpriteRenderer>();
            Require(sprite != null && sprite.sprite != null, "NPC PixelVisual bounds are unavailable.");
            Bounds bounds = sprite.sprite.bounds;
            float halfWidth = Mathf.Abs(bounds.size.x * pixel.localScale.x) * 0.5f;
            float bottomOffset = pixel.localPosition.y + bounds.center.y * pixel.localScale.y -
                                 Mathf.Abs(bounds.size.y * pixel.localScale.y) * 0.5f;
            float topOffset = pixel.localPosition.y + bounds.center.y * pixel.localScale.y +
                              Mathf.Abs(bounds.size.y * pixel.localScale.y) * 0.5f;
            float wallLeft = upper.position.x - Mathf.Abs(upper.lossyScale.x) * 0.5f;
            float occupiedBottom = crossing.position.y + bottomOffset;
            float occupiedTop = float.NegativeInfinity;

            for (int index = 1; index <= 8; index++)
            {
                Transform move = RequiredTransform($"GameStage/Waypoints/Urinal{index:00}/MovePoint");
                float t = Mathf.Clamp01((crossing.position.x - (wallLeft - halfWidth)) /
                                        (crossing.position.x - move.position.x));
                Vector3 finalOverlappingCenter = Vector3.Lerp(crossing.position, move.position, t);
                occupiedTop = Mathf.Max(occupiedTop, finalOverlappingCenter.y + topOffset);
                Debug.Log($"Sprint5-12J Urinal{index:00} occupied Y=" +
                          $"{occupiedBottom:0.000}..{finalOverlappingCenter.y + topOffset:0.000}.");
            }

            float lowerClearance = occupiedBottom - openingBottom;
            float upperClearance = openingTop - occupiedTop;
            Require(lowerClearance + 0.0001f >= RequiredClearance &&
                    upperClearance + 0.0001f >= RequiredClearance,
                $"Wall opening clearance is only lower={lowerClearance:0.000}, upper={upperClearance:0.000}.");
            Require(snapshot.UpperPosition == upper.position && snapshot.UpperScale == upper.localScale &&
                    snapshot.LowerPosition == lower.position && snapshot.LowerScale == lower.localScale,
                "Wall opening accumulated after repeated Setup.");
            Debug.Log($"Sprint5-12J occupied union={occupiedBottom:0.000}..{occupiedTop:0.000}, " +
                      $"opening={openingBottom:0.000}..{openingTop:0.000}, " +
                      $"clearance lower={lowerClearance:0.000}, upper={upperClearance:0.000}.");
        }

        private static float Edge(Transform segment, bool top) =>
            segment.position.y + (top ? 1f : -1f) * Mathf.Abs(segment.lossyScale.y) * 0.5f;

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

        private sealed class StageSnapshot
        {
            private readonly Dictionary<string, Vector3> _positions = new Dictionary<string, Vector3>();
            private readonly Vector3 _linePosition, _lineScale, _colliderCenter, _colliderSize;
            private readonly bool _isTrigger;

            public StageSnapshot()
            {
                string[] fixedPaths =
                {
                    "GameStage/NyoiceLine/CrossingTarget", "GameStage/Queue/NyoiceApproachPoint",
                    "GameStage/Entrance/SpawnPoint", "GameStage/Exit/ExitPoint"
                };
                foreach (string path in fixedPaths) _positions[path] = RequiredTransform(path).position;
                for (int index = 1; index <= 8; index++)
                {
                    string waypoint = $"GameStage/Waypoints/Urinal{index:00}";
                    _positions[waypoint + "/MovePoint"] = RequiredTransform(waypoint + "/MovePoint").position;
                    _positions[waypoint + "/UsePoint"] = RequiredTransform(waypoint + "/UsePoint").position;
                    string slot = $"GameStage/Queue/Queue{index:00}";
                    _positions[slot] = RequiredTransform(slot).position;
                }

                Transform line = RequiredTransform("GameStage/NyoiceLine/Line");
                BoxCollider collider = line.GetComponent<BoxCollider>();
                Require(collider != null, "Runtime Line Trigger Collider is missing.");
                _linePosition = line.position;
                _lineScale = line.localScale;
                _colliderCenter = collider.center;
                _colliderSize = collider.size;
                _isTrigger = collider.isTrigger;
                Transform upper = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/WallUpper");
                Transform lower = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/WallLower");
                UpperPosition = upper.position;
                UpperScale = upper.localScale;
                LowerPosition = lower.position;
                LowerScale = lower.localScale;
            }

            public Vector3 UpperPosition { get; }
            public Vector3 UpperScale { get; }
            public Vector3 LowerPosition { get; }
            public Vector3 LowerScale { get; }

            public void ValidateUnchanged()
            {
                foreach (KeyValuePair<string, Vector3> pair in _positions)
                {
                    Require(Close(RequiredTransform(pair.Key).position, pair.Value),
                        $"Fixed path coordinate changed: {pair.Key}.");
                }
                Transform line = RequiredTransform("GameStage/NyoiceLine/Line");
                BoxCollider collider = line.GetComponent<BoxCollider>();
                Require(Close(line.position, _linePosition) && Close(line.localScale, _lineScale) &&
                        Close(collider.center, _colliderCenter) && Close(collider.size, _colliderSize) &&
                        collider.isTrigger == _isTrigger,
                    "Runtime Line Transform or Trigger Collider changed.");
            }

            private static bool Close(Vector3 a, Vector3 b) => Vector3.SqrMagnitude(a - b) < 0.000001f;
        }
    }
}
