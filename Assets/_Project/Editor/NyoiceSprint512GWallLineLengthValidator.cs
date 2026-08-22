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
            NyoiceSprint512KWallRouteConsistencyValidator.ValidateWallRouteConsistencyFix();
            Debug.Log("Sprint5-12G Wall Line Length Fix validation passed.");
        }

        private static void ValidateWallVisual(StageSnapshot snapshot)
        {
            Transform lineRoot = GameObject.Find("GameStage/NyoiceLine")?.transform;
            Require(lineRoot != null, "NyoiceLine root is missing.");
            Transform wallRoot = null;
            int count = 0;
            for (int index = 0; index < lineRoot.childCount; index++)
            {
                if (lineRoot.GetChild(index).name != "WallVisualRoot") continue;
                wallRoot = lineRoot.GetChild(index);
                count++;
            }

            Require(count == 1 && wallRoot != null, "Exactly one WallVisualRoot must exist.");
            Transform upper = wallRoot.Find("WallUpper");
            Transform lower = wallRoot.Find("WallLower");
            Require(upper != null && lower != null, "WallVisual opening segments are missing.");
            Require(upper.GetComponent<MeshRenderer>()?.enabled == true &&
                    lower.GetComponent<MeshRenderer>()?.enabled == true,
                "WallVisual segment Renderer is missing or disabled.");
            Require(upper.GetComponent<Collider>() == null && lower.GetComponent<Collider>() == null,
                "WallVisual segments must not add Colliders.");

            Transform crossing = lineRoot.Find("CrossingTarget");
            Require(crossing != null && Mathf.Approximately(crossing.position.y, ExpectedTurnY),
                "CrossingTarget position changed.");
            float upperTop = upper.position.y + (Mathf.Abs(upper.lossyScale.y) * 0.5f);
            float lowerBottom = lower.position.y - (Mathf.Abs(lower.lossyScale.y) * 0.5f);
            Require(Mathf.Approximately(upperTop, 3.25f) && Mathf.Approximately(lowerBottom, -4.75f),
                "WallVisual no longer spans the original wall endpoints.");
            Require(snapshot.WallUpperPosition == upper.position && snapshot.WallUpperScale == upper.localScale &&
                    snapshot.WallLowerPosition == lower.position && snapshot.WallLowerScale == lower.localScale,
                "WallVisual opening accumulated after repeated Setup.");
            Debug.Log($"Sprint5-12G WallUpper={upper.position}/{upper.lossyScale.y:0.000}, " +
                      $"WallLower={lower.position}/{lower.lossyScale.y:0.000}.");
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
                Transform upper = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/WallUpper");
                Transform lower = RequiredTransform("GameStage/NyoiceLine/WallVisualRoot/WallLower");
                WallUpperPosition = upper.position;
                WallUpperScale = upper.localScale;
                WallLowerPosition = lower.position;
                WallLowerScale = lower.localScale;
            }

            public Vector3 WallUpperPosition { get; }
            public Vector3 WallUpperScale { get; }
            public Vector3 WallLowerPosition { get; }
            public Vector3 WallLowerScale { get; }

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
