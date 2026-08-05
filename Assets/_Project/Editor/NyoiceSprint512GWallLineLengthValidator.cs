using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512GWallLineLengthValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const float ExpectedTurnY = -2.5f;

        [MenuItem("Nyoice/Validate Sprint5-12G Wall Line Length Fix")]
        public static void ValidateWallLineLengthFix()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            StageSnapshot snapshot = new StageSnapshot();
            NyoiceGameStageSetup.SetupGameStage();

            ValidateWallVisual(snapshot);
            snapshot.ValidateUnchanged();
            NyoiceSprint512FUiWidthVisualRestoreValidator.ValidateUiWidthVisualRestore();
            Debug.Log("Sprint5-12G Wall Line Length Fix validation passed.");
        }

        private static void ValidateWallVisual(StageSnapshot snapshot)
        {
            Transform lineRoot = GameObject.Find("GameStage/NyoiceLine")?.transform;
            Require(lineRoot != null, "NyoiceLine root is missing.");
            Transform wallVisual = null;
            int count = 0;
            for (int index = 0; index < lineRoot.childCount; index++)
            {
                if (lineRoot.GetChild(index).name != "WallVisual") continue;
                wallVisual = lineRoot.GetChild(index);
                count++;
            }

            Require(count == 1 && wallVisual != null, "Exactly one red WallVisual must exist.");
            MeshRenderer renderer = wallVisual.GetComponent<MeshRenderer>();
            Require(renderer != null && renderer.enabled, "WallVisual Renderer is missing or disabled.");
            Require(wallVisual.GetComponent<Collider>() == null, "WallVisual must not add a Collider.");

            Transform crossing = lineRoot.Find("CrossingTarget");
            Require(crossing != null && Mathf.Approximately(crossing.position.y, ExpectedTurnY),
                "CrossingTarget position changed.");
            float lowerEnd = wallVisual.position.y - (Mathf.Abs(wallVisual.lossyScale.y) * 0.5f);
            float gap = lowerEnd - crossing.position.y;
            Require(lowerEnd >= crossing.position.y - 0.001f,
                "WallVisual extends below the NPC turning point.");
            Require(gap <= 2.2f,
                $"WallVisual leaves an excessive {gap:0.000}-unit clearance above the turning point.");
            Require(snapshot.WallVisualPosition == wallVisual.position && snapshot.WallVisualScale == wallVisual.localScale,
                "WallVisual position or length accumulated after repeated Setup.");
            Debug.Log($"Sprint5-12G WallVisual center={wallVisual.position}, length={wallVisual.lossyScale.y:0.000}, lowerEnd={lowerEnd:0.000}.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class StageSnapshot
        {
            private readonly Dictionary<string, Vector3> _positions = new Dictionary<string, Vector3>();
            private readonly Vector3 _linePosition;
            private readonly Vector3 _lineScale;
            private readonly Vector3 _colliderCenter;
            private readonly Vector3 _colliderSize;
            private readonly bool _colliderTrigger;

            public StageSnapshot()
            {
                string[] paths =
                {
                    "GameStage/Entrance/SpawnPoint",
                    "GameStage/Queue/DecisionPoint",
                    "GameStage/Queue/NyoiceApproachPoint",
                    "GameStage/NyoiceLine/CrossingTarget",
                    "GameStage/Exit/ExitPoint"
                };
                foreach (string path in paths) _positions[path] = RequiredTransform(path).position;
                for (int index = 1; index <= 8; index++)
                {
                    string path = $"GameStage/Queue/Queue{index:00}";
                    _positions[path] = RequiredTransform(path).position;
                }

                Transform line = RequiredTransform("GameStage/NyoiceLine/Line");
                BoxCollider collider = line.GetComponent<BoxCollider>();
                Require(collider != null, "Runtime NyoiceLine collider is missing.");
                _linePosition = line.position;
                _lineScale = line.localScale;
                _colliderCenter = collider.center;
                _colliderSize = collider.size;
                _colliderTrigger = collider.isTrigger;
                Transform visual = RequiredTransform("GameStage/NyoiceLine/WallVisual");
                WallVisualPosition = visual.position;
                WallVisualScale = visual.localScale;
            }

            public Vector3 WallVisualPosition { get; }
            public Vector3 WallVisualScale { get; }

            public void ValidateUnchanged()
            {
                foreach (KeyValuePair<string, Vector3> pair in _positions)
                {
                    Require(Close(RequiredTransform(pair.Key).position, pair.Value),
                        $"Runtime path coordinate changed: {pair.Key}.");
                }

                Transform line = RequiredTransform("GameStage/NyoiceLine/Line");
                BoxCollider collider = line.GetComponent<BoxCollider>();
                Require(Close(line.position, _linePosition) && Close(line.localScale, _lineScale) &&
                        Close(collider.center, _colliderCenter) && Close(collider.size, _colliderSize) &&
                        collider.isTrigger == _colliderTrigger,
                    "Runtime NyoiceLine transform or Collider changed.");
            }

            private static Transform RequiredTransform(string path)
            {
                Transform target = GameObject.Find(path)?.transform;
                Require(target != null, $"Required Transform is missing: {path}.");
                return target;
            }

            private static bool Close(Vector3 a, Vector3 b) => Vector3.SqrMagnitude(a - b) < 0.000001f;
        }
    }
}
