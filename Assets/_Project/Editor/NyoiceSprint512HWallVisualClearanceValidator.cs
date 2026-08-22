using System;
using System.Collections.Generic;
using Nyoice.NPC;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512HWallVisualClearanceValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private const float RequiredClearance = 0.15f;

        [MenuItem("Nyoice/Validate Sprint5-12H Wall Visual Clearance Fix")]
        public static void ValidateWallVisualClearanceFix()
        {
            NyoiceSprint512KWallRouteConsistencyValidator.ValidateWallRouteConsistencyFix();
            Debug.Log("Sprint5-12H Wall Visual Clearance Fix validation passed.");
        }

        private static void ValidateWallAndNpcBounds(StageSnapshot snapshot)
        {
            Transform root = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot");
            Transform upper = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/WallUpper");
            Transform lower = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/WallLower");
            Transform crossing = RequiredTransform("GameStage/NyoiceLine/CrossingTarget");
            Transform approach = RequiredTransform("GameStage/Queue/NyoiceApproachPoint");
            Require(CountDirectChildren(root.parent, "WallVisualRoot") == 1,
                "Exactly one WallVisualRoot must exist.");

            float top = upper.position.y + (Mathf.Abs(upper.lossyScale.y) * 0.5f);
            float openingTop = upper.position.y - (Mathf.Abs(upper.lossyScale.y) * 0.5f);
            float openingBottom = lower.position.y + (Mathf.Abs(lower.lossyScale.y) * 0.5f);
            Require(Mathf.Approximately(top, 3.25f), "WallVisual upper edge changed from Y=3.25.");
            Require(openingTop > crossing.position.y && openingBottom < crossing.position.y,
                "WallVisual opening does not contain CrossingTarget.");
            Require(Mathf.Approximately(upper.localScale.x, 0.08f) &&
                    Mathf.Approximately(lower.localScale.x, 0.08f), "WallVisual width changed.");

            float segment = crossing.position.x - approach.position.x;
            Require(!Mathf.Approximately(segment, 0f), "Approach-to-Crossing segment is invalid.");
            float t = (upper.position.x - approach.position.x) / segment;
            Vector3 closestCenter = Vector3.Lerp(approach.position, crossing.position, t);
            Require(t >= 0f && t <= 1f, "NPC route does not cross WallVisual X.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            Require(prefab != null && prefab.GetComponent<NPCController>() != null, "NPC prefab is missing.");
            Transform pixel = prefab.transform.Find("VisualRoot/PixelVisual");
            Transform legacy = prefab.transform.Find("VisualRoot/LegacyVisual");
            SpriteRenderer pixelRenderer = pixel?.GetComponent<SpriteRenderer>();
            MeshFilter legacyFilter = legacy?.GetComponent<MeshFilter>();
            Require(pixelRenderer != null && pixelRenderer.sprite != null && legacyFilter != null && legacyFilter.sharedMesh != null,
                "NPC PixelVisual or LegacyVisual bounds are unavailable.");

            Vector2 pixelSize = Vector2.Scale(pixelRenderer.sprite.bounds.size,
                new Vector2(Mathf.Abs(pixel.localScale.x), Mathf.Abs(pixel.localScale.y)));
            Bounds legacyMeshBounds = legacyFilter.sharedMesh.bounds;
            Vector2 legacySize = Vector2.Scale(legacyMeshBounds.size,
                new Vector2(Mathf.Abs(legacy.localScale.x), Mathf.Abs(legacy.localScale.y)));
            float pixelTop = closestCenter.y + pixel.localPosition.y +
                             (pixelRenderer.sprite.bounds.center.y * pixel.localScale.y) + (pixelSize.y * 0.5f);
            float legacyTop = closestCenter.y + legacy.localPosition.y +
                              (legacyMeshBounds.center.y * legacy.localScale.y) + (legacySize.y * 0.5f);
            float pixelClearance = openingTop - pixelTop;
            float legacyClearance = openingTop - legacyTop;
            Require(pixelClearance >= RequiredClearance,
                $"PixelVisual clearance is only {pixelClearance:0.000} world units.");
            Require(legacyClearance >= RequiredClearance,
                $"LegacyVisual clearance is only {legacyClearance:0.000} world units.");
            Require(snapshot.UpperPosition == upper.position && snapshot.UpperScale == upper.localScale &&
                    snapshot.LowerPosition == lower.position && snapshot.LowerScale == lower.localScale,
                "WallVisual opening accumulated after repeated Setup.");

            Debug.Log(
                $"Sprint5-12H closest NPC center={closestCenter}, " +
                $"PixelBounds={pixelSize.x:0.000}x{pixelSize.y:0.000}, " +
                $"LegacyBounds={legacySize.x:0.000}x{legacySize.y:0.000}, " +
                $"clearance Pixel={pixelClearance:0.000}, Legacy={legacyClearance:0.000}.");
            Debug.Log($"Sprint5-12H Wall opening={openingBottom:0.000}..{openingTop:0.000}.");
        }

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
            private readonly Dictionary<string, Vector3> _pathPositions = new Dictionary<string, Vector3>();
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
                foreach (string path in paths) _pathPositions[path] = RequiredTransform(path).position;
                for (int index = 1; index <= 8; index++)
                {
                    string path = $"GameStage/Queue/Queue{index:00}";
                    _pathPositions[path] = RequiredTransform(path).position;
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
                foreach (KeyValuePair<string, Vector3> pair in _pathPositions)
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
