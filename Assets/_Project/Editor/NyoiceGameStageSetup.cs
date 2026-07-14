using System.IO;
using Nyoice.Core;
using Nyoice.Managers;
using Nyoice.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nyoice.Editor
{
    /// <summary>
    /// Creates and updates the Unity-only foundation used by the Nyoice game scene.
    /// </summary>
    public static class NyoiceGameStageSetup
    {
        private const string MenuPath = "Nyoice/Setup Game Stage";
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private const string GameStageName = "GameStage";

        private const int UrinalCount = 8;
        private const float UrinalStartX = -5.25f;
        private const float UrinalSpacing = 1.5f;
        private const float UrinalY = 3.25f;
        private const float StageBottomY = -4.75f;

        private const float NyoiceLineX = 6f;
        private const float NyoiceApproachX = 6.2f;
        private const float DecisionPointX = 6.5f;
        private const float QueueLaneX = 7f;
        private const float QueueStartY = -3.75f;
        private const float QueueSpacingY = 1f;
        private const float SpawnPointY = 4.5f;

        private static readonly Vector3 NpcVisualScale = new Vector3(0.12f, 0.45f, 0.3f);
        private static readonly Vector3 NpcVisualPosition = new Vector3(0f, 0.45f, 0f);

        [MenuItem(MenuPath)]
        public static void SetupGameStage()
        {
            if (!File.Exists(GameScenePath))
            {
                Warn("GameScene was not found. Run Nyoice/Setup Sprint 1 first.");
                return;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            NPCController npcPrefab = CreateOrUpdateNpcPrefab();
            GameObject existingStage = GameObject.Find(GameStageName);

            if (existingStage != null)
            {
                UpdateExistingQueueLayout(existingStage.transform);
                SaveSceneAndAssets(gameScene);
                Selection.activeGameObject = existingStage;
                Warn("NPC.prefab and the queue layout were updated. GameStage was not duplicated.");
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
            Transform spawnPoint = CreateEntrance(entrance);
            QueueSlot[] queueSlots = CreateQueue(queue, out Transform decisionPoint);
            CreateNyoiceLine(nyoiceLine);
            CreateExit(exit);
            CreateWaypoints(waypoints);
            CreateGameSystems(queueSlots, decisionPoint, spawnPoint, npcPrefab);
            EnsureSceneCamera();
            EnsureSceneLight();

            SaveSceneAndAssets(gameScene);
            Selection.activeGameObject = gameStage;
            Info("GameStage was created in GameScene.");
        }

        private static void CreateUrinals(Transform parent)
        {
            for (int index = 0; index < UrinalCount; index++)
            {
                int number = index + 1;
                float x = GetUrinalX(index);
                var urinal = new GameObject($"Urinal{number:00}");
                urinal.transform.SetParent(parent, false);
                urinal.transform.position = new Vector3(x, UrinalY, 0f);

                GameObject body = CreateCube(
                    "Body",
                    urinal.transform,
                    Vector3.zero,
                    new Vector3(1f, 1.2f, 0.5f),
                    new Color(0.82f, 0.88f, 0.92f));
                body.GetComponent<BoxCollider>().isTrigger = false;

                CreateNumberLabel(urinal.transform, number);
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

        private static Transform CreateEntrance(Transform parent)
        {
            CreateCube(
                "EntranceMarker",
                parent,
                new Vector3(QueueLaneX, 5f, 0f),
                new Vector3(0.25f, 2f, 0.25f),
                new Color(0.25f, 0.65f, 0.35f));

            GameObject spawnPoint = CreatePoint(
                "SpawnPoint",
                parent,
                new Vector3(QueueLaneX, SpawnPointY, 0f),
                Color.green);
            return spawnPoint.transform;
        }

        private static QueueSlot[] CreateQueue(Transform parent, out Transform decisionPoint)
        {
            GameObject decision = CreatePoint(
                "DecisionPoint",
                parent,
                new Vector3(DecisionPointX, QueueStartY, 0f),
                new Color(0.75f, 0.25f, 1f));
            decisionPoint = decision.transform;

            CreatePoint(
                "NyoiceApproachPoint",
                parent,
                new Vector3(NyoiceApproachX, QueueStartY, 0f),
                new Color(1f, 0.35f, 0.15f));

            var slots = new QueueSlot[UrinalCount];
            for (int index = 0; index < UrinalCount; index++)
            {
                GameObject slotObject = CreatePoint(
                    $"Queue{index + 1:00}",
                    parent,
                    GetQueuePosition(index),
                    new Color(1f, 0.75f, 0.15f));
                QueueSlot slot = slotObject.AddComponent<QueueSlot>();
                slot.Initialize(index + 1);
                EditorUtility.SetDirty(slot);
                slots[index] = slot;
            }

            return slots;
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
                float x = GetUrinalX(index);
                Transform urinalWaypoints = CreateGroup($"Urinal{index + 1:00}", parent);
                CreatePoint("MovePoint", urinalWaypoints, new Vector3(x, 1.35f, 0f), Color.yellow);
                CreatePoint("UsePoint", urinalWaypoints, new Vector3(x, 2.45f, 0f), Color.magenta);
                CreatePoint("ExitStartPoint", urinalWaypoints, new Vector3(x - 0.45f, 1.35f, 0f), Color.cyan);
            }
        }

        private static NPCController CreateOrUpdateNpcPrefab()
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            if (existingPrefab != null)
            {
                GameObject prefabContents = PrefabUtility.LoadPrefabContents(NpcPrefabPath);
                try
                {
                    ConfigureNpcPrefab(prefabContents);
                    PrefabUtility.SaveAsPrefabAsset(prefabContents, NpcPrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabContents);
                }

                GameObject updatedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
                return updatedPrefab.GetComponent<NPCController>();
            }

            string prefabDirectory = Path.GetDirectoryName(NpcPrefabPath);
            Directory.CreateDirectory(prefabDirectory);

            var npcRoot = new GameObject("NPC");
            ConfigureNpcPrefab(npcRoot);
            PrefabUtility.SaveAsPrefabAsset(npcRoot, NpcPrefabPath);
            Object.DestroyImmediate(npcRoot);

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            return prefabAsset.GetComponent<NPCController>();
        }

        private static void ConfigureNpcPrefab(GameObject npcRoot)
        {
            if (npcRoot.GetComponent<NPCMovement>() == null)
            {
                npcRoot.AddComponent<NPCMovement>();
            }

            if (npcRoot.GetComponent<NPCController>() == null)
            {
                npcRoot.AddComponent<NPCController>();
            }

            Transform visualTransform = npcRoot.transform.Find("Visual");
            if (visualTransform == null)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Visual";
                visual.transform.SetParent(npcRoot.transform, false);
                visual.GetComponent<Renderer>().material.color = new Color(0.35f, 0.7f, 0.95f);
                visualTransform = visual.transform;
            }

            visualTransform.localPosition = NpcVisualPosition;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = NpcVisualScale;
        }

        private static void CreateGameSystems(
            QueueSlot[] queueSlots,
            Transform decisionPoint,
            Transform spawnPoint,
            NPCController npcPrefab)
        {
            var gameSystems = new GameObject("GameSystems");

            var queueManagerObject = new GameObject("QueueManager", typeof(QueueManager));
            queueManagerObject.transform.SetParent(gameSystems.transform, false);
            QueueManager queueManager = queueManagerObject.GetComponent<QueueManager>();
            queueManager.Configure(queueSlots, decisionPoint);
            EditorUtility.SetDirty(queueManager);

            var spawnerObject = new GameObject("NPCSpawner", typeof(NPCSpawner));
            spawnerObject.transform.SetParent(gameSystems.transform, false);
            NPCSpawner spawner = spawnerObject.GetComponent<NPCSpawner>();
            spawner.Configure(npcPrefab, spawnPoint, queueManager);
            EditorUtility.SetDirty(spawner);
        }

        private static void UpdateExistingQueueLayout(Transform gameStage)
        {
            Transform queue = gameStage.Find("Queue");
            Transform entrance = gameStage.Find("Entrance");
            if (queue == null || entrance == null)
            {
                Debug.LogError("Existing GameStage is missing Queue or Entrance.");
                return;
            }

            SetExistingPointPosition(queue, "DecisionPoint", new Vector3(DecisionPointX, QueueStartY, 0f));
            SetExistingPointPosition(queue, "NyoiceApproachPoint", new Vector3(NyoiceApproachX, QueueStartY, 0f));

            for (int index = 0; index < UrinalCount; index++)
            {
                SetExistingPointPosition(queue, $"Queue{index + 1:00}", GetQueuePosition(index));
            }

            SetExistingPointPosition(entrance, "SpawnPoint", new Vector3(QueueLaneX, SpawnPointY, 0f));
            SetExistingPointPosition(entrance, "EntranceMarker", new Vector3(QueueLaneX, 5f, 0f));
        }

        private static void SetExistingPointPosition(Transform parent, string childName, Vector3 position)
        {
            Transform point = parent.Find(childName);
            if (point == null)
            {
                Debug.LogError($"Existing GameStage is missing {parent.name}/{childName}.");
                return;
            }

            point.position = position;
            EditorUtility.SetDirty(point);
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject CreatePoint(string name, Transform parent, Vector3 position, Color color)
        {
            return CreateCube(name, parent, position, new Vector3(0.18f, 0.18f, 0.18f), color);
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

        private static Vector3 GetQueuePosition(int index)
        {
            return new Vector3(QueueLaneX, QueueStartY + (index * QueueSpacingY), 0f);
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
            if (Object.FindAnyObjectByType<Light>() != null)
            {
                return;
            }

            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        private static void SaveSceneAndAssets(Scene scene)
        {
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
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

