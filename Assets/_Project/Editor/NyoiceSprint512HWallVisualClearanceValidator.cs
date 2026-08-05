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
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            StageSnapshot snapshot = new StageSnapshot();
            NyoiceGameStageSetup.SetupGameStage();

            ValidateWallAndNpcBounds(snapshot);
            snapshot.ValidateUnchanged();
            NyoiceSprint512GWallLineLengthValidator.ValidateWallLineLengthFix();
            Debug.Log("Sprint5-12H Wall Visual Clearance Fix validation passed.");
        }

        private static void ValidateWallAndNpcBounds(StageSnapshot snapshot)
        {
            Transform wall = RequiredTransform("GameStage/NyoiceLine/WallVisual");
            Transform crossing = RequiredTransform("GameStage/NyoiceLine/CrossingTarget");
            Transform approach = RequiredTransform("GameStage/Queue/NyoiceApproachPoint");
            Require(CountDirectChildren(wall.parent, "WallVisual") == 1, "Exactly one WallVisual must exist.");

            float halfHeight = Mathf.Abs(wall.lossyScale.y) * 0.5f;
            float top = wall.position.y + halfHeight;
            float bottom = wall.position.y - halfHeight;
            Require(Mathf.Approximately(top, 3.25f), "WallVisual upper edge changed from Y=3.25.");
            Require(bottom > crossing.position.y, "WallVisual lower edge is not above CrossingTarget.");
            Require(Mathf.Approximately(wall.localScale.x, 0.08f), "WallVisual width changed.");

            float segment = crossing.position.x - approach.position.x;
            Require(!Mathf.Approximately(segment, 0f), "Approach-to-Crossing segment is invalid.");
            float t = (wall.position.x - approach.position.x) / segment;
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
            float pixelClearance = bottom - pixelTop;
            float legacyClearance = bottom - legacyTop;
            Require(pixelClearance >= RequiredClearance,
                $"PixelVisual clearance is only {pixelClearance:0.000} world units.");
            Require(legacyClearance >= RequiredClearance,
                $"LegacyVisual clearance is only {legacyClearance:0.000} world units.");
            Require(snapshot.WallPosition == wall.position && snapshot.WallScale == wall.localScale,
                "WallVisual position or length accumulated after repeated Setup.");

            Debug.Log(
                $"Sprint5-12H closest NPC center={closestCenter}, " +
                $"PixelBounds={pixelSize.x:0.000}x{pixelSize.y:0.000}, " +
                $"LegacyBounds={legacySize.x:0.000}x{legacySize.y:0.000}, " +
                $"clearance Pixel={pixelClearance:0.000}, Legacy={legacyClearance:0.000}.");
            Debug.Log($"Sprint5-12H WallVisual center={wall.position}, length={wall.lossyScale.y:0.000}, bottom={bottom:0.000}.");
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
                Transform wall = RequiredTransform("GameStage/NyoiceLine/WallVisual");
                WallPosition = wall.position;
                WallScale = wall.localScale;
            }

            public Vector3 WallPosition { get; }
            public Vector3 WallScale { get; }

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
