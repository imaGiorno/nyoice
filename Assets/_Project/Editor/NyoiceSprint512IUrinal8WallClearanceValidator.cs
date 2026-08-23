using System;
using System.Collections.Generic;
using Nyoice.NPC;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512IUrinal8WallClearanceValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private const float RequiredClearance = 0.15f;

        [MenuItem("Nyoice/Validate Sprint5-12I Urinal8 Wall Clearance Fix")]
        public static void ValidateUrinal8WallClearanceFix()
        {
            NyoiceSprint512KWallRouteConsistencyValidator.ValidateWallRouteConsistencyFix();
            Debug.Log("Sprint5-12I Urinal8 Wall Clearance Fix validation passed.");
        }

        private static void ValidateAllUrinalRoutes(StageSnapshot snapshot)
        {
            Transform upper = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/WallUpper");
            Transform lower = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/WallLower");
            Transform crossing = RequiredTransform("GameStage/NyoiceLine/CrossingTarget");
            float openingTop = upper.position.y - (Mathf.Abs(upper.lossyScale.y) * 0.5f);
            float openingBottom = lower.position.y + (Mathf.Abs(lower.lossyScale.y) * 0.5f);
            float wallLeft = upper.position.x - (Mathf.Abs(upper.lossyScale.x) * 0.5f);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            Require(prefab != null && prefab.GetComponent<NPCController>() != null, "NPC prefab is missing.");
            Transform pixel = prefab.transform.Find("VisualRoot/PixelVisual");
            SpriteRenderer renderer = pixel?.GetComponent<SpriteRenderer>();
            Require(renderer != null && renderer.sprite != null, "NPC PixelVisual bounds are unavailable.");

            Bounds spriteBounds = renderer.sprite.bounds;
            float halfWidth = Mathf.Abs(spriteBounds.size.x * pixel.lossyScale.x) * 0.5f;
            float topOffset = pixel.localPosition.y +
                              (spriteBounds.center.y * pixel.localScale.y) +
                              (Mathf.Abs(spriteBounds.size.y * pixel.localScale.y) * 0.5f);
            float bottomOffset = pixel.localPosition.y +
                                 (spriteBounds.center.y * pixel.localScale.y) -
                                 (Mathf.Abs(spriteBounds.size.y * pixel.localScale.y) * 0.5f);
            float minimumClearance = float.PositiveInfinity;
            float limitingUpperClearance = float.PositiveInfinity;
            int closestUrinal = 0;

            for (int index = 1; index <= 8; index++)
            {
                Transform move = RequiredTransform($"GameStage/Waypoints/Urinal{index:00}/MovePoint");
                float t = Mathf.Clamp01((crossing.position.x - (wallLeft - halfWidth)) /
                                        (crossing.position.x - move.position.x));
                Vector3 closestCenter = Vector3.Lerp(crossing.position, move.position, t);
                float upperClearance = openingTop - (closestCenter.y + topOffset);
                float lowerClearance = (crossing.position.y + bottomOffset) - openingBottom;
                float clearance = Mathf.Min(upperClearance, lowerClearance);
                Require(upperClearance + 0.0001f >= RequiredClearance &&
                        lowerClearance + 0.0001f >= RequiredClearance,
                    $"Urinal{index:00} PixelVisual opening clearance is only {clearance:0.000} world units.");

                minimumClearance = Mathf.Min(minimumClearance, clearance);
                if (upperClearance < limitingUpperClearance)
                {
                    limitingUpperClearance = upperClearance;
                    closestUrinal = index;
                }

                Debug.Log($"Sprint5-12I Urinal{index:00} route {crossing.position} -> {move.position}, " +
                          $"closest center={closestCenter}, clearance={clearance:0.000}.");
            }

            Require(closestUrinal == 8, "Urinal08 is no longer the limiting WallVisual route.");
            Require(snapshot.UpperPosition == upper.position && snapshot.UpperScale == upper.localScale &&
                    snapshot.LowerPosition == lower.position && snapshot.LowerScale == lower.localScale,
                "WallVisual opening accumulated after repeated Setup.");
            Debug.Log($"Sprint5-12I minimum clearance={minimumClearance:0.000} at Urinal{closestUrinal:00}, " +
                      $"opening={openingBottom:0.000}..{openingTop:0.000}.");
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
                _positions["GameStage/NyoiceLine/CrossingTarget"] =
                    RequiredTransform("GameStage/NyoiceLine/CrossingTarget").position;
                for (int index = 1; index <= 8; index++)
                {
                    string root = $"GameStage/Waypoints/Urinal{index:00}";
                    _positions[root + "/MovePoint"] = RequiredTransform(root + "/MovePoint").position;
                    _positions[root + "/UsePoint"] = RequiredTransform(root + "/UsePoint").position;
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
                        $"NPC path coordinate changed: {pair.Key}.");
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
