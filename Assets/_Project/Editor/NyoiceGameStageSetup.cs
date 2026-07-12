using System.IO;
using Nyoice.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nyoice.Editor
{
    /// <summary>
    /// Generates the Sprint 2 GameStage foundation in GameScene.
    /// </summary>
    public static class NyoiceGameStageSetup
    {
        private const string MenuPath = "Nyoice/Setup Game Stage";
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string GameStageName = "GameStage";
        private const int UrinalCount = 8;

        private const float UrinalStartX = -5.25f;
        private const float UrinalSpacing = 1.5f;
        private const float UrinalY = 3.25f;
        private const float StageBottomY = -4.75f;
        private const float NyoiceLineX = 6f;
        private const float NyoiceApproachX = 6.2f;
        private const float QueueStartX = 6.5f;
        private const float QueueSpacing = 0.45f;
        private const float QueueY = -3.75f;
        private const float SpawnPointX = 10.1f;

        [MenuItem(MenuPath)]
        public static void SetupGameStage()
        {
            if (!File.Exists(GameScenePath))
            {
                Warn("GameSceneが見つかりません。先に Nyoice > Setup Sprint 1 を実行してください。");
                return;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            if (GameObject.Find(GameStageName) != null)
            {
                Warn("GameStageは既に存在するため、生成を中止しました。");
                return;
            }

            GameObject gameStage = new GameObject(GameStageName);
            Transform urinals = CreateGroup("Urinals", gameStage.transform);
            Transform partitions = CreateGroup("Partitions", gameStage.transform);
            Transform entrance = CreateGroup("Entrance", gameStage.transform);
            Transform queue = CreateGroup("Queue", gameStage.transform);
            Transform nyoiceLine = CreateGroup("NyoiceLine", gameStage.transform);
            Transform exit = CreateGroup("Exit", gameStage.transform);
            Transform waypoints = CreateGroup("Waypoints", gameStage.transform);

            CreateUrinals(urinals);
            CreatePartitions(partitions);
            CreateEntrance(entrance);
            CreateQueue(queue);
            CreateNyoiceLine(nyoiceLine);
            CreateExit(exit);
            CreateWaypoints(waypoints);
            EnsureSceneCamera();
            EnsureSceneLight();

            EditorSceneManager.MarkSceneDirty(gameScene);
            EditorSceneManager.SaveScene(gameScene);
            Selection.activeGameObject = gameStage;

            Info("GameStageをGameSceneへ生成しました。");
        }

        private static void CreateUrinals(Transform parent)
        {
            for (int index = 0; index < UrinalCount; index++)
            {
                string number = (index + 1).ToString("00");
                float x = GetUrinalX(index);

                GameObject urinal = new GameObject($"Urinal{number}");
                urinal.transform.SetParent(parent, false);
                urinal.transform.position = new Vector3(x, UrinalY, 0f);

                GameObject body = CreateCube(
                    "Body",
                    urinal.transform,
                    Vector3.zero,
                    new Vector3(1f, 1.2f, 0.5f),
                    new Color(0.82f, 0.88f, 0.92f));
                body.GetComponent<BoxCollider>().isTrigger = false;

                CreateNumberLabel(urinal.transform, index + 1);
            }
        }

        private static void CreateNumberLabel(Transform parent, int number)
        {
            var label = new GameObject("Number", typeof(TextMesh));
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.26f);

            TextMesh text = label.GetComponent<TextMesh>();
            text.text = number.ToString();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 64;
            text.characterSize = 0.08f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.08f, 0.12f, 0.16f);
        }

        private static void CreatePartitions(Transform parent)
        {
            for (int index = 0; index < UrinalCount; index++)
            {
                float x = GetUrinalX(index) + (UrinalSpacing * 0.5f);
                CreateCube(
                    $"Partition{index + 1:00}",
                    parent,
                    new Vector3(x, UrinalY, 0f),
                    new Vector3(0.12f, 1.8f, 0.8f),
                    new Color(0.35f, 0.42f, 0.48f));
            }
        }

        private static void CreateEntrance(Transform parent)
        {
            CreateCube(
                "EntranceMarker",
                parent,
                new Vector3(10.4f, QueueY, 0f),
                new Vector3(0.25f, 2f, 0.25f),
                new Color(0.25f, 0.65f, 0.35f));

            CreatePoint("SpawnPoint", parent, new Vector3(SpawnPointX, QueueY, 0f), Color.green);
        }

        private static void CreateQueue(Transform parent)
        {
            CreatePoint(
                "NyoiceApproachPoint",
                parent,
                new Vector3(NyoiceApproachX, QueueY, 0f),
                new Color(1f, 0.35f, 0.15f));

            for (int index = 0; index < UrinalCount; index++)
            {
                float x = QueueStartX + (index * QueueSpacing);
                CreatePoint($"Queue{index + 1:00}", parent, new Vector3(x, QueueY, 0f), new Color(1f, 0.75f, 0.15f));
            }
        }

        private static void CreateNyoiceLine(Transform parent)
        {
            float height = UrinalY - StageBottomY;
            float centerY = StageBottomY + (height * 0.5f);

            GameObject line = CreateCube(
                "Line",
                parent,
                new Vector3(NyoiceLineX, centerY, 0f),
                new Vector3(0.08f, height, 0.08f),
                new Color(0.9f, 0.2f, 0.2f));

            line.GetComponent<BoxCollider>().isTrigger = true;
            line.AddComponent<NyoiceLine>();
        }

        private static void CreateExit(Transform parent)
        {
            CreateCube(
                "ExitMarker",
                parent,
                new Vector3(-6.5f, -3.75f, 0f),
                new Vector3(0.25f, 2f, 0.25f),
                new Color(0.3f, 0.55f, 0.9f));

            CreatePoint("ExitPoint", parent, new Vector3(-5.75f, -3.75f, 0f), Color.cyan);
        }

        private static void CreateWaypoints(Transform parent)
        {
            for (int index = 0; index < UrinalCount; index++)
            {
                string urinalName = $"Urinal{index + 1:00}";
                float x = GetUrinalX(index);
                Transform urinalWaypoints = CreateGroup(urinalName, parent);

                CreatePoint("MovePoint", urinalWaypoints, new Vector3(x, 1.35f, 0f), Color.yellow);
                CreatePoint("UsePoint", urinalWaypoints, new Vector3(x, 2.45f, 0f), Color.magenta);
                CreatePoint("ExitStartPoint", urinalWaypoints, new Vector3(x - 0.45f, 1.35f, 0f), Color.cyan);
            }
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void CreatePoint(string name, Transform parent, Vector3 position, Color color)
        {
            CreateCube(name, parent, position, new Vector3(0.18f, 0.18f, 0.18f), color);
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().material.color = color;
            return cube;
        }

        private static float GetUrinalX(int index)
        {
            return UrinalStartX + (index * UrinalSpacing);
        }

        private static void EnsureSceneCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.09f, 0.11f);
        }

        private static void EnsureSceneLight()
        {
            if (Object.FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        private static void Warn(string message)
        {
            Debug.LogWarning(message);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Nyoice Game Stage Setup", message, "OK");
            }
        }

        private static void Info(string message)
        {
            Debug.Log(message);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Nyoice Game Stage Setup", message, "OK");
            }
        }
    }
}
