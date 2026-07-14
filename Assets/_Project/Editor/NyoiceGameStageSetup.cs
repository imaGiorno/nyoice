using System.IO;
using Nyoice.Core;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nyoice.Editor
{
    public static class NyoiceGameStageSetup
    {
        private const string MenuPath = "Nyoice/Setup Game Stage";
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private const int UrinalCount = 8;

        private const float UrinalStartX = -5.25f;
        private const float UrinalSpacing = 1.5f;
        private const float UrinalY = 3.25f;
        private const float StageBottomY = -4.75f;
        private const float QueueLaneX = 7f;
        private const float QueueStartY = -3.75f;
        private const float QueueSpacingY = 1f;
        private const float DecisionPointX = 6.5f;
        private const float NyoiceApproachX = 6.2f;
        private const float NyoiceLineX = 6f;
        private const float CrossingTargetX = 5.8f;
        private const float SpawnPointY = 4.5f;

        private static readonly Vector3 NpcVisualScale = new Vector3(0.12f, 0.45f, 0.3f);
        private static readonly Vector3 NpcVisualPosition = new Vector3(0f, 0.45f, 0f);

        [MenuItem(MenuPath)]
        public static void SetupGameStage()
        {
            if (!File.Exists(GameScenePath))
            {
                ShowWarning("GameScene was not found. Run Nyoice/Setup Sprint 1 first.");
                return;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            NPCController npcPrefab = CreateOrUpdateNpcPrefab();
            GameObject gameStage = GameObject.Find("GameStage");

            if (gameStage == null)
            {
                gameStage = CreateGameStage();
            }

            EnsureSceneCamera();
            EnsureSceneLight();
            ConfigureStageAndSystems(gameStage.transform, npcPrefab);
            SaveSceneAndAssets(gameScene);
            Selection.activeGameObject = gameStage;
            ShowInfo("GameStage and Sprint 4 systems are ready.");
        }

        private static GameObject CreateGameStage()
        {
            var gameStage = new GameObject("GameStage");
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
            return gameStage;
        }

        private static void ConfigureStageAndSystems(Transform gameStage, NPCController npcPrefab)
        {
            Transform urinalRoot = GetOrCreateGroup("Urinals", gameStage);
            Transform entranceRoot = GetOrCreateGroup("Entrance", gameStage);
            Transform queueRoot = GetOrCreateGroup("Queue", gameStage);
            Transform lineRoot = GetOrCreateGroup("NyoiceLine", gameStage);
            Transform waypointRoot = GetOrCreateGroup("Waypoints", gameStage);

            EnsureQueueLayout(queueRoot);
            Transform spawnPoint = EnsureEntranceLayout(entranceRoot);
            Transform crossingTarget = EnsureNyoiceLine(lineRoot);
            UrinalController[] urinals = EnsureUrinalControllers(urinalRoot, waypointRoot);
            QueueSlot[] queueSlots = EnsureQueueSlots(queueRoot);
            Transform decisionPoint = queueRoot.Find("DecisionPoint");
            Transform approachPoint = queueRoot.Find("NyoiceApproachPoint");

            EnsureGameSystems(
                queueSlots,
                decisionPoint,
                approachPoint,
                crossingTarget,
                spawnPoint,
                urinals,
                npcPrefab);
        }

        private static void CreateUrinals(Transform parent)
        {
            for (int index = 0; index < UrinalCount; index++)
            {
                var urinal = new GameObject($"Urinal{index + 1:00}");
                urinal.transform.SetParent(parent, false);
                urinal.transform.position = new Vector3(GetUrinalX(index), UrinalY, 0f);

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
                new Vector3(QueueLaneX, 5f, 0f),
                new Vector3(0.25f, 2f, 0.25f),
                new Color(0.25f, 0.65f, 0.35f));
            CreatePoint("SpawnPoint", parent, new Vector3(QueueLaneX, SpawnPointY, 0f), Color.green);
        }

        private static void CreateQueue(Transform parent)
        {
            CreatePoint(
                "DecisionPoint",
                parent,
                new Vector3(DecisionPointX, QueueStartY, 0f),
                new Color(0.75f, 0.25f, 1f));
            CreatePoint(
                "NyoiceApproachPoint",
                parent,
                new Vector3(NyoiceApproachX, QueueStartY, 0f),
                new Color(1f, 0.35f, 0.15f));

            for (int index = 0; index < UrinalCount; index++)
            {
                CreatePoint(
                    $"Queue{index + 1:00}",
                    parent,
                    GetQueuePosition(index),
                    new Color(1f, 0.75f, 0.15f));
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
            CreatePoint(
                "CrossingTarget",
                parent,
                new Vector3(CrossingTargetX, QueueStartY, 0f),
                new Color(0.95f, 0.5f, 0.15f));
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

        private static void EnsureQueueLayout(Transform queueRoot)
        {
            SetOrCreatePoint(
                queueRoot,
                "DecisionPoint",
                new Vector3(DecisionPointX, QueueStartY, 0f),
                new Color(0.75f, 0.25f, 1f));
            SetOrCreatePoint(
                queueRoot,
                "NyoiceApproachPoint",
                new Vector3(NyoiceApproachX, QueueStartY, 0f),
                new Color(1f, 0.35f, 0.15f));

            for (int index = 0; index < UrinalCount; index++)
            {
                SetOrCreatePoint(
                    queueRoot,
                    $"Queue{index + 1:00}",
                    GetQueuePosition(index),
                    new Color(1f, 0.75f, 0.15f));
            }
        }

        private static Transform EnsureEntranceLayout(Transform entranceRoot)
        {
            SetOrCreatePoint(
                entranceRoot,
                "EntranceMarker",
                new Vector3(QueueLaneX, 5f, 0f),
                new Color(0.25f, 0.65f, 0.35f));
            return SetOrCreatePoint(
                entranceRoot,
                "SpawnPoint",
                new Vector3(QueueLaneX, SpawnPointY, 0f),
                Color.green);
        }

        private static Transform EnsureNyoiceLine(Transform lineRoot)
        {
            Transform lineTransform = lineRoot.Find("Line");
            if (lineTransform == null)
            {
                float height = UrinalY - StageBottomY;
                float centerY = StageBottomY + (height * 0.5f);
                lineTransform = CreateCube(
                    "Line",
                    lineRoot,
                    new Vector3(NyoiceLineX, centerY, 0f),
                    new Vector3(0.08f, height, 0.08f),
                    new Color(0.9f, 0.2f, 0.2f)).transform;
            }

            BoxCollider lineCollider = lineTransform.GetComponent<BoxCollider>();
            if (lineCollider == null)
            {
                lineCollider = lineTransform.gameObject.AddComponent<BoxCollider>();
            }

            lineCollider.isTrigger = true;
            if (lineTransform.GetComponent<NyoiceLine>() == null)
            {
                lineTransform.gameObject.AddComponent<NyoiceLine>();
            }

            return SetOrCreatePoint(
                lineRoot,
                "CrossingTarget",
                new Vector3(CrossingTargetX, QueueStartY, 0f),
                new Color(0.95f, 0.5f, 0.15f));
        }

        private static QueueSlot[] EnsureQueueSlots(Transform queueRoot)
        {
            var slots = new QueueSlot[UrinalCount];
            for (int index = 0; index < UrinalCount; index++)
            {
                Transform slotTransform = queueRoot.Find($"Queue{index + 1:00}");
                QueueSlot slot = slotTransform.GetComponent<QueueSlot>();
                if (slot == null)
                {
                    slot = slotTransform.gameObject.AddComponent<QueueSlot>();
                }

                slot.Initialize(index + 1);
                EditorUtility.SetDirty(slot);
                slots[index] = slot;
            }

            return slots;
        }

        private static UrinalController[] EnsureUrinalControllers(
            Transform urinalRoot,
            Transform waypointRoot)
        {
            var controllers = new UrinalController[UrinalCount];
            for (int index = 0; index < UrinalCount; index++)
            {
                string urinalName = $"Urinal{index + 1:00}";
                Transform urinal = urinalRoot.Find(urinalName);
                if (urinal == null)
                {
                    urinal = new GameObject(urinalName).transform;
                    urinal.SetParent(urinalRoot, false);
                    urinal.position = new Vector3(GetUrinalX(index), UrinalY, 0f);
                    CreateCube(
                        "Body",
                        urinal,
                        Vector3.zero,
                        new Vector3(1f, 1.2f, 0.5f),
                        new Color(0.82f, 0.88f, 0.92f));
                    CreateNumberLabel(urinal, index + 1);
                }

                Transform body = urinal.Find("Body");
                if (body == null)
                {
                    body = CreateCube(
                        "Body",
                        urinal,
                        Vector3.zero,
                        new Vector3(1f, 1.2f, 0.5f),
                        new Color(0.82f, 0.88f, 0.92f)).transform;
                }

                BoxCollider bodyCollider = body.GetComponent<BoxCollider>();
                if (bodyCollider == null)
                {
                    bodyCollider = body.gameObject.AddComponent<BoxCollider>();
                }

                bodyCollider.isTrigger = false;

                Transform waypointGroup = waypointRoot.Find(urinalName);
                if (waypointGroup == null)
                {
                    waypointGroup = CreateGroup(urinalName, waypointRoot);
                }

                Transform movePoint = SetOrCreatePoint(
                    waypointGroup,
                    "MovePoint",
                    new Vector3(GetUrinalX(index), 1.35f, 0f),
                    Color.yellow);
                Transform usePoint = SetOrCreatePoint(
                    waypointGroup,
                    "UsePoint",
                    new Vector3(GetUrinalX(index), 2.45f, 0f),
                    Color.magenta);
                SetOrCreatePoint(
                    waypointGroup,
                    "ExitStartPoint",
                    new Vector3(GetUrinalX(index) - 0.45f, 1.35f, 0f),
                    Color.cyan);

                GameObject highlight = EnsureHighlight(urinal, body);
                UrinalController controller = urinal.GetComponent<UrinalController>();
                if (controller == null)
                {
                    controller = urinal.gameObject.AddComponent<UrinalController>();
                }

                controller.Configure(
                    index + 1,
                    movePoint,
                    usePoint,
                    highlight,
                    body.GetComponent<Renderer>());
                EditorUtility.SetDirty(controller);
                controllers[index] = controller;
            }

            return controllers;
        }

        private static GameObject EnsureHighlight(Transform urinal, Transform body)
        {
            Transform highlight = urinal.Find("Highlight");
            if (highlight == null)
            {
                highlight = new GameObject("Highlight").transform;
                highlight.SetParent(urinal, false);
            }

            RemovePrimitiveComponents(highlight.gameObject);
            float frontZ = body.localPosition.z - ((body.localScale.z * 0.5f) + 0.08f);
            highlight.localPosition = new Vector3(body.localPosition.x, body.localPosition.y, frontZ);
            highlight.localRotation = body.localRotation;
            highlight.localScale = Vector3.one;

            const float borderThickness = 0.12f;
            const float borderDepth = 0.06f;
            float outerWidth = body.localScale.x + 0.3f;
            float outerHeight = body.localScale.y + 0.3f;
            Color yellow = new Color(1f, 0.82f, 0.05f);

            EnsureHighlightBar(
                highlight,
                "Top",
                new Vector3(0f, (outerHeight - borderThickness) * 0.5f, 0f),
                new Vector3(outerWidth, borderThickness, borderDepth),
                yellow);
            EnsureHighlightBar(
                highlight,
                "Bottom",
                new Vector3(0f, -(outerHeight - borderThickness) * 0.5f, 0f),
                new Vector3(outerWidth, borderThickness, borderDepth),
                yellow);
            EnsureHighlightBar(
                highlight,
                "Left",
                new Vector3(-(outerWidth - borderThickness) * 0.5f, 0f, 0f),
                new Vector3(borderThickness, outerHeight, borderDepth),
                yellow);
            EnsureHighlightBar(
                highlight,
                "Right",
                new Vector3((outerWidth - borderThickness) * 0.5f, 0f, 0f),
                new Vector3(borderThickness, outerHeight, borderDepth),
                yellow);

            highlight.gameObject.SetActive(false);
            return highlight.gameObject;
        }

        private static void EnsureHighlightBar(
            Transform highlight,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            Transform bar = highlight.Find(name);
            if (bar == null)
            {
                bar = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                bar.name = name;
                bar.SetParent(highlight, false);
            }

            bar.localPosition = localPosition;
            bar.localRotation = Quaternion.identity;
            bar.localScale = localScale;
            Renderer renderer = bar.GetComponent<Renderer>();
            Shader shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                renderer.sharedMaterial = new Material(shader);
            }

            renderer.sharedMaterial.color = color;
            Collider collider = bar.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void RemovePrimitiveComponents(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                Object.DestroyImmediate(renderer);
            }

            MeshFilter meshFilter = target.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                Object.DestroyImmediate(meshFilter);
            }
        }

        private static void EnsureGameSystems(
            QueueSlot[] queueSlots,
            Transform decisionPoint,
            Transform approachPoint,
            Transform crossingTarget,
            Transform spawnPoint,
            UrinalController[] urinals,
            NPCController npcPrefab)
        {
            GameObject gameSystems = GameObject.Find("GameSystems");
            if (gameSystems == null)
            {
                gameSystems = new GameObject("GameSystems");
            }

            QueueManager queueManager = GetOrCreateChildComponent<QueueManager>(gameSystems.transform, "QueueManager");
            NPCSpawner spawner = GetOrCreateChildComponent<NPCSpawner>(gameSystems.transform, "NPCSpawner");
            UrinalManager urinalManager = GetOrCreateChildComponent<UrinalManager>(gameSystems.transform, "UrinalManager");
            UrinalTicketManager ticketManager = GetOrCreateChildComponent<UrinalTicketManager>(
                gameSystems.transform,
                "UrinalTicketManager");

            AudioSource audioSource = urinalManager.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = urinalManager.gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            ticketManager.Configure(UrinalCount);
            urinalManager.Configure(urinals, Camera.main, audioSource);
            queueManager.Configure(queueSlots, decisionPoint);
            queueManager.ConfigureUrinalFlow(urinalManager, ticketManager, approachPoint, crossingTarget);
            spawner.Configure(npcPrefab, spawnPoint, queueManager);

            EditorUtility.SetDirty(queueManager);
            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(urinalManager);
            EditorUtility.SetDirty(ticketManager);
            EditorUtility.SetDirty(audioSource);
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

                return AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath).GetComponent<NPCController>();
            }

            string directory = Path.GetDirectoryName(NpcPrefabPath);
            Directory.CreateDirectory(directory);
            var npcRoot = new GameObject("NPC");
            ConfigureNpcPrefab(npcRoot);
            PrefabUtility.SaveAsPrefabAsset(npcRoot, NpcPrefabPath);
            Object.DestroyImmediate(npcRoot);
            return AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath).GetComponent<NPCController>();
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

            Rigidbody body = npcRoot.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = npcRoot.AddComponent<Rigidbody>();
            }

            body.useGravity = false;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Transform visual = npcRoot.transform.Find("Visual");
            if (visual == null)
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
                visual.name = "Visual";
                visual.SetParent(npcRoot.transform, false);
                visual.GetComponent<Renderer>().material.color = new Color(0.35f, 0.7f, 0.95f);
            }

            visual.localPosition = NpcVisualPosition;
            visual.localRotation = Quaternion.identity;
            visual.localScale = NpcVisualScale;
        }

        private static T GetOrCreateChildComponent<T>(Transform parent, string childName)
            where T : Component
        {
            Transform child = parent.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(parent, false);
            }

            T component = child.GetComponent<T>();
            return component != null ? component : child.gameObject.AddComponent<T>();
        }

        private static Transform GetOrCreateGroup(string name, Transform parent)
        {
            Transform child = parent.Find(name);
            return child != null ? child : CreateGroup(name, parent);
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static Transform SetOrCreatePoint(
            Transform parent,
            string name,
            Vector3 position,
            Color color)
        {
            Transform point = parent.Find(name);
            if (point == null)
            {
                point = CreatePoint(name, parent, position, color).transform;
            }

            point.position = position;
            EditorUtility.SetDirty(point);
            return point;
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

        private static void ShowWarning(string message)
        {
            Debug.LogWarning(message);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Nyoice Game Stage Setup", message, "OK");
            }
        }

        private static void ShowInfo(string message)
        {
            Debug.Log(message);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Nyoice Game Stage Setup", message, "OK");
            }
        }
    }
}
