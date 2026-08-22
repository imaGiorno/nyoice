using System.IO;
using Nyoice.Audio;
using Nyoice.Core;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.Toilet;
using Nyoice.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nyoice.Editor
{
    public static class NyoiceGameStageSetup
    {
        private const string MenuPath = "Nyoice/Setup Game Stage";
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private const string BusinessSpriteDirectory = "Assets/_Project/Art/Pixel/NPC/Business";
        private const string BusinessSpriteHolderPath =
            BusinessSpriteDirectory + "/NPCBusinessSpriteHolder.asset";
        private const string UrinalSpriteHolderPath =
            "Assets/_Project/Art/Pixel/Urinals/UrinalSpriteHolder.asset";
        private const string MaterialsDirectory = "Assets/_Project/Materials";
        private const string AudioDirectory = "Assets/_Project/Audio";
        private const string AudioClipHolderPath = AudioDirectory + "/AudioClipHolder.asset";
        private const int UrinalCount = 8;

        private const float UrinalStartX = -6f;
        private const float UrinalSpacing = 1.5f;
        private const float UrinalY = 3.25f;
        private const float StageBottomY = -4.75f;
        private const float QueueLaneX = 7f;
        private const float QueueStartY = -2.5f;
        private const float QueueSpacingY = 0.9f;
        private const float DecisionPointX = 6.5f;
        private const float NyoiceApproachX = 6.2f;
        private const float NyoiceLineX = 6f;
        private const float CrossingTargetX = 4.8f;
        private const float WallVisualBottomY = -0.4f;
        private const float WallRouteTurnY = -2.7f;
        private const float SpawnPointY = 4.5f;
        private const float NpcMovementSpeed = 4f;

        private static readonly Vector3 UrinalBodyScale = new Vector3(1f, 1.2f, 0.5f);
        private static readonly Vector3 NpcVisualScale = new Vector3(0.12f, 0.45f, 0.3f);
        private static readonly Vector3 NpcVisualPosition = new Vector3(0f, 0.45f, 0f);

        [MenuItem(MenuPath)]
        public static void SetupGameStage()
        {
            NyoicePixelArtSetup.EnsureFolders();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
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
            GameObject gameStage = FindRootObject(gameScene, "GameStage");

            if (gameStage == null)
            {
                gameStage = CreateGameStage();
            }

            EnsureSceneCamera();
            EnsureSceneLight();
            ConfigureStageAndSystems(gameStage.transform, npcPrefab);
            MigrateSetupRendererMaterials(gameStage.transform);
            SaveSceneAndAssets(gameScene);
            Selection.activeGameObject = gameStage;
            ShowInfo("GameStage and Sprint 5-3A systems are ready.");
        }

        private static GameObject FindRootObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                {
                    return root;
                }
            }

            return null;
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
            NyoicePixelArtSetup.EnsureEnvironmentVisual(gameStage);
            Transform urinalRoot = GetOrCreateGroup("Urinals", gameStage);
            Transform partitionRoot = GetOrCreateGroup("Partitions", gameStage);
            Transform entranceRoot = GetOrCreateGroup("Entrance", gameStage);
            Transform queueRoot = GetOrCreateGroup("Queue", gameStage);
            Transform lineRoot = GetOrCreateGroup("NyoiceLine", gameStage);
            Transform exitRoot = GetOrCreateGroup("Exit", gameStage);
            Transform waypointRoot = GetOrCreateGroup("Waypoints", gameStage);

            EnsureQueueLayout(queueRoot);
            EnsurePartitionLayout(partitionRoot);
            RemoveBackgroundProps(gameStage);
            Transform spawnPoint = EnsureEntranceLayout(entranceRoot);
            Transform crossingTarget = EnsureNyoiceLine(lineRoot);
            Transform exitPoint = EnsureExitLayout(exitRoot);
            UrinalController[] urinals = EnsureUrinalControllers(urinalRoot, waypointRoot);
            QueueSlot[] queueSlots = EnsureQueueSlots(queueRoot);
            Transform decisionPoint = queueRoot.Find("DecisionPoint");
            Transform approachPoint = queueRoot.Find("NyoiceApproachPoint");

            EnsureStageVisualVisibility(gameStage);

            EnsureGameSystems(
                queueSlots,
                decisionPoint,
                approachPoint,
                crossingTarget,
                exitPoint,
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
                urinal.transform.localPosition = new Vector3(GetUrinalX(index), UrinalY, 0f);

                GameObject body = CreateCube(
                    "Body",
                    urinal.transform,
                    Vector3.zero,
                    UrinalBodyScale,
                    new Color(0.82f, 0.88f, 0.92f));
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
                body.transform.localScale = UrinalBodyScale;
                body.GetComponent<BoxCollider>().isTrigger = false;
                CreateNumberLabel(urinal.transform, index + 1);
            }
        }

        private static void CreateNumberLabel(Transform parent, int number)
        {
            var label = new GameObject("Number", typeof(TextMesh));
            label.transform.SetParent(parent, false);
            ConfigureNumberLabel(label.transform, number);
        }

        private static void ConfigureNumberLabel(Transform label, int number)
        {
            label.gameObject.SetActive(true);
            label.gameObject.layer = label.parent.gameObject.layer;
            label.localPosition = new Vector3(0f, 0f, -0.26f);
            label.localRotation = Quaternion.identity;
            label.localScale = Vector3.one;

            TextMesh text = label.GetComponent<TextMesh>();
            if (text == null)
            {
                text = label.gameObject.AddComponent<TextMesh>();
            }

            text.text = number.ToString();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 64;
            text.characterSize = 0.08f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.08f, 0.12f, 0.16f);
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = text.font.material;
            renderer.sortingOrder = 1;
            EditorUtility.SetDirty(text);
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

        private static void EnsurePartitionLayout(Transform parent)
        {
            for (int index = 0; index < UrinalCount; index++)
            {
                string partitionName = $"Partition{index + 1:00}";
                Transform partition = parent.Find(partitionName);
                if (partition == null)
                {
                    partition = CreateCube(
                        partitionName,
                        parent,
                        Vector3.zero,
                        new Vector3(0.12f, 1.8f, 0.8f),
                        new Color(0.35f, 0.42f, 0.48f)).transform;
                }

                partition.position = new Vector3(
                    GetUrinalX(index) + (UrinalSpacing * 0.5f),
                    UrinalY,
                    0f);
                EditorUtility.SetDirty(partition);
            }
        }

        private static void RemoveBackgroundProps(Transform gameStage)
        {
            Transform props = gameStage.Find("BackgroundProps");
            if (props != null)
            {
                Object.DestroyImmediate(props.gameObject);
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
                new Vector3(NyoiceApproachX, WallRouteTurnY, 0f),
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
                new Vector3(CrossingTargetX, WallRouteTurnY, 0f),
                new Color(0.95f, 0.5f, 0.15f));
        }

        private static void CreateExit(Transform parent)
        {
            CreateCube(
                "ExitMarker",
                parent,
                new Vector3(-6.5f, QueueStartY, 0f),
                new Vector3(0.25f, 2f, 0.25f),
                new Color(0.3f, 0.55f, 0.9f));
            CreatePoint("ExitPoint", parent, new Vector3(-5.75f, QueueStartY, 0f), Color.cyan);
        }

        private static Transform EnsureExitLayout(Transform parent)
        {
            Transform exitMarker = parent.Find("ExitMarker");
            if (exitMarker == null)
            {
                exitMarker = CreateCube(
                    "ExitMarker",
                    parent,
                    new Vector3(-6.5f, QueueStartY, 0f),
                    new Vector3(0.25f, 2f, 0.25f),
                    new Color(0.3f, 0.55f, 0.9f)).transform;
            }

            exitMarker.position = new Vector3(-6.5f, QueueStartY, 0f);
            return SetOrCreatePoint(
                parent,
                "ExitPoint",
                new Vector3(-5.75f, QueueStartY, 0f),
                Color.cyan);
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
                new Vector3(NyoiceApproachX, WallRouteTurnY, 0f),
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

            EnsureNyoiceLineWallVisual(lineRoot, lineTransform);

            return SetOrCreatePoint(
                lineRoot,
                "CrossingTarget",
                new Vector3(CrossingTargetX, WallRouteTurnY, 0f),
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
            UrinalSpriteHolder spriteHolder = EnsureUrinalSpriteHolder();
            for (int index = 0; index < UrinalCount; index++)
            {
                string urinalName = $"Urinal{index + 1:00}";
                Transform urinal = urinalRoot.Find(urinalName);
                if (urinal == null)
                {
                    urinal = new GameObject(urinalName).transform;
                    urinal.SetParent(urinalRoot, false);
                    urinal.localPosition = new Vector3(GetUrinalX(index), UrinalY, 0f);
                    CreateCube(
                        "Body",
                        urinal,
                        Vector3.zero,
                        UrinalBodyScale,
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
                        UrinalBodyScale,
                        new Color(0.82f, 0.88f, 0.92f)).transform;
                }

                urinal.SetParent(urinalRoot, false);
                urinal.localPosition = new Vector3(GetUrinalX(index), UrinalY, 0f);
                urinal.localRotation = Quaternion.identity;
                urinal.localScale = Vector3.one;

                body.SetParent(urinal, false);
                body.localPosition = Vector3.zero;
                body.localRotation = Quaternion.identity;
                body.localScale = UrinalBodyScale;

                Transform numberLabel = urinal.Find("Number");
                if (numberLabel == null)
                {
                    CreateNumberLabel(urinal, index + 1);
                }
                else
                {
                    ConfigureNumberLabel(numberLabel, index + 1);
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
                Transform exitStartPoint = SetOrCreatePoint(
                    waypointGroup,
                    "ExitStartPoint",
                    new Vector3(GetUrinalX(index) - 0.45f, 1.35f, 0f),
                    Color.cyan);

                Renderer visualRenderer = NyoicePixelArtSetup.EnsureUrinalVisuals(urinal, body);
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
                    exitStartPoint,
                    highlight,
                    visualRenderer != null ? visualRenderer : body.GetComponent<Renderer>());
                SpriteRenderer spriteRenderer = urinal
                    .Find("VisualRoot/PixelVisual")?.GetComponent<SpriteRenderer>();
                controller.ConfigureSpriteHolder(spriteHolder, spriteRenderer);
                ConfigureUrinalHighlightLayout(
                    urinal,
                    body,
                    highlight.transform,
                    NyoicePixelArtSetup.UsePixelVisuals);
                EditorUtility.SetDirty(controller);
                controllers[index] = controller;
            }

            return controllers;
        }

        private static UrinalSpriteHolder EnsureUrinalSpriteHolder()
        {
            UrinalSpriteHolder holder =
                AssetDatabase.LoadAssetAtPath<UrinalSpriteHolder>(UrinalSpriteHolderPath);
            if (holder != null)
            {
                return holder;
            }

            holder = ScriptableObject.CreateInstance<UrinalSpriteHolder>();
            AssetDatabase.CreateAsset(holder, UrinalSpriteHolderPath);
            return holder;
        }

        private static GameObject EnsureHighlight(Transform urinal, Transform body)
        {
            Transform highlight = urinal.Find("Highlight");
            if (highlight == null)
            {
                highlight = new GameObject("Highlight").transform;
                highlight.SetParent(urinal, false);
            }

            ConfigureUrinalHighlightLayout(
                urinal,
                body,
                highlight,
                NyoicePixelArtSetup.UsePixelVisuals);
            highlight.gameObject.SetActive(false);
            return highlight.gameObject;
        }

        public static void ConfigureUrinalHighlightLayout(
            Transform urinal,
            Transform body,
            Transform highlight,
            bool usePixelVisual)
        {
            const float pixelFrameScale = 1.055f;
            Vector3 center = body.localPosition;
            Quaternion rotation = body.localRotation;
            float frontZ = body.localPosition.z - ((body.localScale.z * 0.5f) + 0.08f);
            float outerWidth = body.localScale.x + 0.3f;
            float outerHeight = body.localScale.y + 0.3f;

            SpriteRenderer pixelRenderer = urinal.Find("VisualRoot/PixelVisual")?.GetComponent<SpriteRenderer>();
            if (pixelRenderer != null && pixelRenderer.sprite != null)
            {
                const float pixelHighlightYOffset = -0.065f;
                Vector2 spriteSize = pixelRenderer.sprite.bounds.size;
                Vector3 pixelScale = pixelRenderer.transform.localScale;
                outerWidth = spriteSize.x * Mathf.Abs(pixelScale.x) * pixelFrameScale;
                outerHeight = spriteSize.y * Mathf.Abs(pixelScale.y) * pixelFrameScale;
                center = pixelRenderer.transform.localPosition;
                center.y += pixelHighlightYOffset;
                rotation = pixelRenderer.transform.localRotation;
                frontZ = center.z - 0.08f;
            }

            highlight.localPosition = new Vector3(center.x, center.y, frontZ);
            highlight.localRotation = rotation;
            highlight.localScale = Vector3.one;

            const float borderThickness = 0.12f;
            const float borderDepth = 0.06f;
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
            SetRendererColor(renderer, color, "Unlit/Color");
            Collider collider = bar.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void EnsureGameSystems(
            QueueSlot[] queueSlots,
            Transform decisionPoint,
            Transform approachPoint,
            Transform crossingTarget,
            Transform exitPoint,
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
            GameStateManager gameStateManager = GetOrCreateChildComponent<GameStateManager>(
                gameSystems.transform,
                "GameStateManager");
            DiscomfortManager discomfortManager = GetOrCreateChildComponent<DiscomfortManager>(
                gameSystems.transform,
                "DiscomfortManager");
            ScoreManager scoreManager = GetOrCreateChildComponent<ScoreManager>(
                gameSystems.transform,
                "ScoreManager");
            AudioManager audioManager = GetOrCreateChildComponent<AudioManager>(
                gameSystems.transform,
                "AudioManager");

            AudioSource seSource = audioManager.GetComponent<AudioSource>();
            if (seSource == null)
            {
                seSource = audioManager.gameObject.AddComponent<AudioSource>();
            }

            seSource.playOnAwake = false;
            seSource.loop = false;
            seSource.spatialBlend = 0f;
            audioManager.Configure(seSource, EnsureAudioClipHolder());

            AudioSource audioSource = urinalManager.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = urinalManager.gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            ticketManager.Configure(UrinalCount);
            ticketManager.ConfigureGameState(gameStateManager);
            urinalManager.Configure(urinals, Camera.main, audioSource);
            urinalManager.ConfigureGameState(gameStateManager);
            queueManager.Configure(queueSlots, decisionPoint);
            queueManager.ConfigureUrinalFlow(urinalManager, ticketManager, approachPoint, crossingTarget);
            queueManager.ConfigureExitFlow(exitPoint);
            queueManager.ConfigureGameState(gameStateManager);
            queueManager.ConfigureScore(scoreManager);
            spawner.Configure(npcPrefab, spawnPoint, queueManager);
            spawner.ConfigureSpawnIntervalRange(
                NPCSpawner.DefaultMinimumSpawnInterval,
                NPCSpawner.DefaultMaximumSpawnInterval);
            spawner.ConfigureGameState(gameStateManager);
            discomfortManager.Configure(urinals, gameStateManager);
            scoreManager.Configure(discomfortManager, gameStateManager);
            EnsureDiscomfortUI(discomfortManager, gameStateManager);
            EnsureScoreUI(scoreManager);

            EditorUtility.SetDirty(queueManager);
            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(urinalManager);
            EditorUtility.SetDirty(ticketManager);
            EditorUtility.SetDirty(gameStateManager);
            EditorUtility.SetDirty(discomfortManager);
            EditorUtility.SetDirty(scoreManager);
            EditorUtility.SetDirty(audioManager);
            EditorUtility.SetDirty(seSource);
            EditorUtility.SetDirty(audioSource);
        }

        private static AudioClipHolder EnsureAudioClipHolder()
        {
            if (!AssetDatabase.IsValidFolder(AudioDirectory))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Audio");
            }

            AudioClipHolder holder = AssetDatabase.LoadAssetAtPath<AudioClipHolder>(AudioClipHolderPath);
            if (holder == null)
            {
                holder = ScriptableObject.CreateInstance<AudioClipHolder>();
                AssetDatabase.CreateAsset(holder, AudioClipHolderPath);
            }

            EditorUtility.SetDirty(holder);
            return holder;
        }

        private static void EnsureScoreUI(ScoreManager scoreManager)
        {
            GameObject uiRoot = GameObject.Find("UI");
            if (uiRoot == null)
            {
                uiRoot = new GameObject("UI");
            }

            Transform canvasTransform = uiRoot.transform.Find("DiscomfortCanvas");
            if (canvasTransform == null)
            {
                return;
            }

            Transform container = canvasTransform.Find("HUDRoot/HUDContainer");
            if (container == null)
            {
                return;
            }

            Transform statsPanel = container.Find("StatsPanel");
            Transform scoreGroup = statsPanel.Find("Score");
            Transform processedGroup = statsPanel.Find("ProcessedCount");
            Transform comboPanel = container.Find("ComboPanel");
            Text scoreLabel = EnsureUiText(
                scoreGroup,
                "ScoreLabel",
                new Vector2(0.08f, 0.55f),
                new Vector2(0.92f, 0.9f),
                26,
                TextAnchor.MiddleCenter);
            Text scoreText = EnsureUiText(
                scoreGroup,
                "ScoreText",
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.58f),
                38,
                TextAnchor.MiddleCenter);
            Text processedLabel = EnsureUiText(
                processedGroup,
                "ProcessedCountLabel",
                new Vector2(0.05f, 0.55f),
                new Vector2(0.95f, 0.9f),
                26,
                TextAnchor.MiddleCenter);
            Text processedCountText = EnsureUiText(
                processedGroup,
                "ProcessedCountText",
                new Vector2(0.05f, 0.08f),
                new Vector2(0.95f, 0.58f),
                38,
                TextAnchor.MiddleCenter);
            Text comboLabel = EnsureUiText(
                comboPanel,
                "ComboLabel",
                new Vector2(0.08f, 0.55f),
                new Vector2(0.92f, 0.9f),
                24,
                TextAnchor.MiddleCenter);
            Text comboText = EnsureUiText(
                comboPanel,
                "ComboText",
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.58f),
                35,
                TextAnchor.MiddleCenter);
            scoreLabel.text = "SCORE";
            processedLabel.text = "\u51e6\u7406\u4eba\u6570";
            comboLabel.text = "COMBO";
            EnsureTextOutline(scoreLabel);
            EnsureTextOutline(scoreText);
            EnsureTextOutline(processedLabel);
            EnsureTextOutline(processedCountText);
            EnsureTextOutline(comboLabel);
            EnsureTextOutline(comboText);

            ScoreUI scoreUI = GetOrAddComponent<ScoreUI>(canvasTransform.gameObject);
            scoreUI.Configure(scoreManager, scoreText, comboText, processedCountText, true);
            EditorUtility.SetDirty(scoreUI);
            EditorUtility.SetDirty(scoreLabel);
            EditorUtility.SetDirty(scoreText);
            EditorUtility.SetDirty(processedLabel);
            EditorUtility.SetDirty(processedCountText);
            EditorUtility.SetDirty(comboLabel);
            EditorUtility.SetDirty(comboText);
        }

        private static void EnsureDiscomfortUI(
            DiscomfortManager discomfortManager,
            GameStateManager gameStateManager)
        {
            GameObject uiRoot = GameObject.Find("UI");
            if (uiRoot == null)
            {
                uiRoot = new GameObject("UI");
            }

            Transform existingCanvas = uiRoot.transform.Find("DiscomfortCanvas");
            GameObject canvasObject;
            if (existingCanvas == null)
            {
                canvasObject = new GameObject(
                    "DiscomfortCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(uiRoot.transform, false);
            }
            else
            {
                canvasObject = existingCanvas.gameObject;
            }

            Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAddComponent<GraphicRaycaster>(canvasObject);

            RemoveDirectUiObject(canvasObject.transform, "HudPanel");
            RemoveDirectUiObject(canvasObject.transform, "DiscomfortText");
            RemoveDirectUiObject(canvasObject.transform, "ScoreText");
            RemoveDirectUiObject(canvasObject.transform, "ProcessedCountText");
            RemoveDirectUiObject(canvasObject.transform, "ComboText");
            Transform hudRoot = GetOrCreateUiObject(canvasObject.transform, "HUDRoot").transform;
            SetTopStretchRect(hudRoot.GetComponent<RectTransform>(), 10f, 118f);
            Transform hudContainer = GetOrCreateUiObject(hudRoot, "HUDContainer").transform;
            SetFixedRect(
                hudContainer.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(1220f, 118f),
                Vector2.zero,
                new Vector2(0.5f, 1f));

            Transform discomfortPanel = EnsureHudPanel(
                hudContainer,
                "DiscomfortPanel",
                0f,
                580f,
                118f);
            Transform statsPanel = EnsureHudPanel(
                hudContainer,
                "StatsPanel",
                604f,
                420f,
                118f);
            Transform comboPanel = EnsureHudPanel(
                hudContainer,
                "ComboPanel",
                1048f,
                172f,
                118f);
            EnsureStatsPanelStructure(statsPanel);

            Text discomfortLabel = EnsureUiText(
                discomfortPanel,
                "DiscomfortLabel",
                new Vector2(0.04f, 0.58f),
                new Vector2(0.32f, 0.92f),
                26,
                TextAnchor.MiddleLeft);
            discomfortLabel.text = "\u4e0d\u5feb\u5ea6";
            Text discomfortText = EnsureUiText(
                discomfortPanel,
                "DiscomfortText",
                new Vector2(0.68f, 0.58f),
                new Vector2(0.96f, 0.92f),
                32,
                TextAnchor.MiddleRight);
            discomfortText.text = "0 / 100";
            EnsureTextOutline(discomfortLabel);
            EnsureTextOutline(discomfortText);

            Image[] blockFills = EnsureDiscomfortBlockGauge(discomfortPanel);
            Slider discomfortSlider = EnsureLegacyDiscomfortSlider(canvasObject.transform, hudRoot);
            Text gameOverText = EnsureUiText(
                canvasObject.transform,
                "GameOverText",
                new Vector2(0.15f, 0.35f),
                new Vector2(0.85f, 0.65f),
                72,
                TextAnchor.MiddleCenter);
            gameOverText.text = "GAME OVER";
            gameOverText.color = new Color(1f, 0.25f, 0.2f);
            EnsureTextOutline(gameOverText);
            gameOverText.transform.SetAsLastSibling();
            gameOverText.gameObject.SetActive(gameStateManager.IsGameOver);

            DiscomfortUI ui = GetOrAddComponent<DiscomfortUI>(canvasObject);
            ui.Configure(
                discomfortManager,
                gameStateManager,
                discomfortText,
                discomfortSlider,
                gameOverText,
                blockFills,
                true);

            EditorUtility.SetDirty(canvasObject);
            EditorUtility.SetDirty(ui);
            EditorUtility.SetDirty(discomfortLabel);
            EditorUtility.SetDirty(discomfortText);
            EditorUtility.SetDirty(discomfortSlider);
            EditorUtility.SetDirty(gameOverText);

            EnsureLowerInfoRoot(canvasObject.transform);
            gameOverText.transform.SetAsLastSibling();
        }

        private static Slider EnsureLegacyDiscomfortSlider(Transform canvas, Transform hudRoot)
        {
            Transform existing = hudRoot.Find("LegacyDiscomfortSlider");
            if (existing == null)
            {
                existing = canvas.Find("DiscomfortSlider");
            }

            GameObject sliderObject = existing != null
                ? existing.gameObject
                : GetOrCreateUiObject(hudRoot, "LegacyDiscomfortSlider");
            sliderObject.name = "LegacyDiscomfortSlider";
            sliderObject.transform.SetParent(hudRoot, false);

            Image background = GetOrAddComponent<Image>(sliderObject);
            background.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            background.raycastTarget = false;

            Transform fillTransform = sliderObject.transform.Find("Fill");
            GameObject fillObject = fillTransform != null
                ? fillTransform.gameObject
                : GetOrCreateUiObject(sliderObject.transform, "Fill");
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            SetUiAnchors(fillRect, Vector2.zero, Vector2.one);
            Image fillImage = GetOrAddComponent<Image>(fillObject);
            fillImage.color = new Color(1f, 0.65f, 0.12f);
            fillImage.raycastTarget = false;

            Slider slider = GetOrAddComponent<Slider>(sliderObject);
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 0f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fillRect;
            slider.handleRect = null;
            slider.targetGraphic = background;
            slider.interactable = false;
            sliderObject.SetActive(false);
            return slider;
        }

        private static Transform EnsureHudPanel(
            Transform parent,
            string objectName,
            float left,
            float width,
            float height)
        {
            Transform panel = GetOrCreateUiObject(parent, objectName).transform;
            SetFixedRect(
                panel.GetComponent<RectTransform>(),
                Vector2.zero,
                new Vector2(width, height),
                new Vector2(left, 0f),
                Vector2.zero);
            Image background = GetOrAddComponent<Image>(panel.gameObject);
            background.color = new Color(0.04f, 0.06f, 0.08f, 0.86f);
            background.raycastTarget = false;
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = Image.Type.Sliced;
            return panel;
        }

        private static void EnsureStatsPanelStructure(Transform statsPanel)
        {
            Transform score = GetOrCreateUiObject(statsPanel, "Score").transform;
            SetUiAnchors(
                score.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(0.5f, 1f));
            Transform processed = GetOrCreateUiObject(statsPanel, "ProcessedCount").transform;
            SetUiAnchors(
                processed.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(1f, 1f));
            GameObject divider = GetOrCreateUiObject(statsPanel, "Divider");
            SetFixedRect(
                divider.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                new Vector2(2f, 82f),
                Vector2.zero,
                new Vector2(0.5f, 0.5f));
            Image dividerImage = GetOrAddComponent<Image>(divider);
            dividerImage.color = new Color(1f, 1f, 1f, 0.18f);
            dividerImage.raycastTarget = false;
        }

        private static Image[] EnsureDiscomfortBlockGauge(Transform discomfortPanel)
        {
            Transform gauge = GetOrCreateUiObject(discomfortPanel, "BlockGauge").transform;
            SetFixedRect(
                gauge.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(520f, 46f),
                new Vector2(30f, 13f),
                Vector2.zero);

            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var fills = new Image[10];
            const float blockWidth = 45.7f;
            const float gap = 7f;
            for (int index = 0; index < fills.Length; index++)
            {
                Transform block = GetOrCreateUiObject(gauge, $"Block{index + 1:00}").transform;
                SetFixedRect(
                    block.GetComponent<RectTransform>(),
                    new Vector2(0f, 0.5f),
                    new Vector2(blockWidth, 46f),
                    new Vector2(index * (blockWidth + gap), 0f),
                    new Vector2(0f, 0.5f));
                Image blockBackground = GetOrAddComponent<Image>(block.gameObject);
                blockBackground.sprite = uiSprite;
                blockBackground.type = Image.Type.Sliced;
                blockBackground.color = new Color(0.16f, 0.18f, 0.2f, 0.95f);
                blockBackground.raycastTarget = false;

                GameObject fillObject = GetOrCreateUiObject(block, "Fill");
                RectTransform fillRect = fillObject.GetComponent<RectTransform>();
                SetUiAnchors(fillRect, Vector2.zero, Vector2.one);
                fillRect.offsetMin = new Vector2(3f, 3f);
                fillRect.offsetMax = new Vector2(-3f, -3f);
                Image fill = GetOrAddComponent<Image>(fillObject);
                fill.sprite = uiSprite;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = 0f;
                fill.raycastTarget = false;
                fills[index] = fill;
            }

            return fills;
        }

        private static void EnsureLowerInfoRoot(Transform canvas)
        {
            Transform lowerRoot = GetOrCreateUiObject(canvas, "LowerInfoRoot").transform;
            SetFixedRect(
                lowerRoot.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(1220f, 140f),
                new Vector2(0f, 20f),
                new Vector2(0.5f, 0f));

            Transform rulePanel = EnsureLowerInfoPanel(lowerRoot, "RulePanel", 0f, 380f);
            Transform comboGuidePanel = EnsureLowerInfoPanel(lowerRoot, "ComboGuidePanel", 404f, 540f);
            Transform stageInfoPanel = EnsureLowerInfoPanel(lowerRoot, "StageInfoPanel", 968f, 252f);

            Text ruleText = EnsureUiText(
                rulePanel,
                "BodyText",
                new Vector2(0.05f, 0.08f),
                new Vector2(0.95f, 0.92f),
                22,
                TextAnchor.UpperLeft);
            ruleText.text =
                "RULE\n\u96a3\u306b\u4eba\u304c\u3044\u308b\u9593\u3001\u4e0d\u5feb\u5ea6\u304c\u5897\u52a0\n" +
                "\u4e0d\u5feb\u5ea6 100 \u3067 GameOver";

            Text comboGuideText = EnsureUiText(
                comboGuidePanel,
                "BodyText",
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f),
                21,
                TextAnchor.UpperLeft);
            comboGuideText.text =
                "5 COMBO   SCORE \u00d71.1\n" +
                "10 COMBO  SCORE \u00d71.2\n" +
                "20 COMBO  SCORE \u00d71.5\n" +
                "30 COMBO  SCORE \u00d72.0\n" +
                "50 COMBO  SCORE \u00d73.0";

            Text stageInfoText = EnsureUiText(
                stageInfoPanel,
                "BodyText",
                new Vector2(0.06f, 0.08f),
                new Vector2(0.94f, 0.92f),
                20,
                TextAnchor.UpperLeft);
            stageInfoText.text =
                "STAGE INFO\nLEVEL --\nSPAWN 1.0 - 5.0s\nPEE 2.0 - 10.0s";

            EditorUtility.SetDirty(lowerRoot);
            EditorUtility.SetDirty(ruleText);
            EditorUtility.SetDirty(comboGuideText);
            EditorUtility.SetDirty(stageInfoText);
        }

        private static Transform EnsureLowerInfoPanel(
            Transform parent,
            string objectName,
            float left,
            float width)
        {
            Transform panel = GetOrCreateUiObject(parent, objectName).transform;
            SetFixedRect(
                panel.GetComponent<RectTransform>(),
                Vector2.zero,
                new Vector2(width, 140f),
                new Vector2(left, 0f),
                Vector2.zero);
            Image background = GetOrAddComponent<Image>(panel.gameObject);
            background.color = new Color(0.035f, 0.05f, 0.065f, 0.68f);
            background.raycastTarget = false;
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = Image.Type.Sliced;
            return panel;
        }

        private static void SetTopStretchRect(RectTransform rect, float topInset, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -topInset);
            rect.sizeDelta = new Vector2(0f, height);
            rect.localScale = Vector3.one;
        }

        private static void SetFixedRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 size,
            Vector2 position,
            Vector2 pivot)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
        }

        private static void RemoveDirectUiObject(Transform parent, string objectName)
        {
            Transform child = null;
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform candidate = parent.GetChild(index);
                if (candidate.name == objectName)
                {
                    child = candidate;
                    break;
                }
            }

            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Text EnsureUiText(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject textObject = GetOrCreateUiObject(parent, objectName);
            SetUiAnchors(textObject.GetComponent<RectTransform>(), anchorMin, anchorMax);
            Text text = GetOrAddComponent<Text>(textObject);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void EnsureTextOutline(Text text)
        {
            Outline outline = GetOrAddComponent<Outline>(text.gameObject);
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }

        private static GameObject GetOrCreateUiObject(Transform parent, string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var uiObject = new GameObject(objectName, typeof(RectTransform));
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        private static void SetUiAnchors(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static T GetOrAddComponent<T>(GameObject target)
            where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
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
            NPCMovement npcMovement = npcRoot.GetComponent<NPCMovement>();
            if (npcMovement == null)
            {
                npcMovement = npcRoot.AddComponent<NPCMovement>();
            }

            npcMovement.ConfigureSpeed(NpcMovementSpeed);
            EditorUtility.SetDirty(npcMovement);

            NPCController npcController = npcRoot.GetComponent<NPCController>();
            if (npcController == null)
            {
                npcController = npcRoot.AddComponent<NPCController>();
            }

            npcController.ConfigureUrinationDurationRange(
                NPCController.DefaultMinimumUrinationDuration,
                NPCController.DefaultMaximumUrinationDuration);
            EditorUtility.SetDirty(npcController);

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
            }

            SetRendererColor(
                visual.GetComponent<Renderer>(),
                new Color(0.35f, 0.7f, 0.95f),
                "Standard");
            visual.localPosition = NpcVisualPosition;
            visual.localRotation = Quaternion.identity;
            visual.localScale = NpcVisualScale;

            NyoicePixelArtSetup.EnsureNpcVisuals(npcRoot);

            NPCBusinessSpriteHolder spriteHolder = EnsureBusinessSpriteHolder();
            SpriteRenderer spriteRenderer = npcRoot.transform
                .Find("VisualRoot/PixelVisual")?.GetComponent<SpriteRenderer>();
            npcController.ConfigureSpriteHolder(spriteHolder, spriteRenderer);
            EditorUtility.SetDirty(npcController);

            EnsureUrinationGauge(npcRoot, npcController);
        }

        private static NPCBusinessSpriteHolder EnsureBusinessSpriteHolder()
        {
            EnsureAssetFolder("Assets/_Project/Art/Pixel/NPC");
            EnsureAssetFolder(BusinessSpriteDirectory);

            NPCBusinessSpriteHolder holder =
                AssetDatabase.LoadAssetAtPath<NPCBusinessSpriteHolder>(BusinessSpriteHolderPath);
            if (holder != null)
            {
                return holder;
            }

            holder = ScriptableObject.CreateInstance<NPCBusinessSpriteHolder>();
            AssetDatabase.CreateAsset(holder, BusinessSpriteHolderPath);
            return holder;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void EnsureUrinationGauge(GameObject npcRoot, NPCController npcController)
        {
            Transform gaugeTransform = npcRoot.transform.Find("UrinationGauge");
            GameObject gaugeObject;
            if (gaugeTransform == null)
            {
                gaugeObject = new GameObject("UrinationGauge", typeof(RectTransform), typeof(Canvas));
                gaugeObject.transform.SetParent(npcRoot.transform, false);
            }
            else
            {
                gaugeObject = gaugeTransform.gameObject;
            }

            RectTransform gaugeRect = GetOrAddComponent<RectTransform>(gaugeObject);
            gaugeRect.localPosition = new Vector3(0.38f, 0.58f, -0.5f);
            gaugeRect.localRotation = Quaternion.identity;
            gaugeRect.localScale = Vector3.one * 0.01f;
            gaugeRect.sizeDelta = new Vector2(18f, 110f);

            Canvas canvas = GetOrAddComponent<Canvas>(gaugeObject);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = NyoicePixelArtSetup.GaugeOrder;

            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var fills = new Image[UrinationTimeGauge.SegmentCount];
            const float segmentWidth = 14f;
            const float segmentHeight = 18f;
            const float gap = 4f;
            float totalHeight = (segmentHeight * UrinationTimeGauge.SegmentCount) +
                                (gap * (UrinationTimeGauge.SegmentCount - 1));
            float startY = -totalHeight * 0.5f + segmentHeight * 0.5f;

            for (int index = 0; index < UrinationTimeGauge.SegmentCount; index++)
            {
                string cellName = $"Cell{index + 1:00}";
                Transform cellTransform = gaugeObject.transform.Find(cellName);
                GameObject cellObject = cellTransform != null
                    ? cellTransform.gameObject
                    : new GameObject(cellName, typeof(RectTransform), typeof(Image));
                cellObject.transform.SetParent(gaugeObject.transform, false);
                RectTransform cellRect = GetOrAddComponent<RectTransform>(cellObject);
                cellRect.anchorMin = cellRect.anchorMax = new Vector2(0.5f, 0.5f);
                cellRect.sizeDelta = new Vector2(segmentWidth, segmentHeight);
                cellRect.anchoredPosition = new Vector2(0f, startY + index * (segmentHeight + gap));
                Image background = GetOrAddComponent<Image>(cellObject);
                background.sprite = uiSprite;
                background.type = Image.Type.Sliced;
                background.color = new Color(0.08f, 0.12f, 0.18f, 0.9f);
                background.raycastTarget = false;

                Transform fillTransform = cellObject.transform.Find("Fill");
                GameObject fillObject = fillTransform != null
                    ? fillTransform.gameObject
                    : new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fillObject.transform.SetParent(cellObject.transform, false);
                RectTransform fillRect = GetOrAddComponent<RectTransform>(fillObject);
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.one * 2f;
                fillRect.offsetMax = Vector2.one * -2f;
                Image fill = GetOrAddComponent<Image>(fillObject);
                fill.sprite = uiSprite;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                fill.color = new Color(0.2f, 0.85f, 1f, 1f);
                fill.raycastTarget = false;
                fills[index] = fill;
            }

            UrinationTimeGauge gauge = GetOrAddComponent<UrinationTimeGauge>(gaugeObject);
            gauge.Configure(npcController, canvas, fills);
            EditorUtility.SetDirty(gauge);
            EditorUtility.SetDirty(canvas);
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
            SetRendererColor(cube.GetComponent<Renderer>(), color);
            return cube;
        }

        private static void EnsureStageVisualVisibility(Transform gameStage)
        {
            SetRenderersEnabled(gameStage.Find("PixelEnvironmentTest"), false);
            SetRenderersEnabled(gameStage.Find("Entrance/EntranceMarker"), true);
            SetRenderersEnabled(gameStage.Find("Entrance/SpawnPoint"), false);
            SetRenderersEnabled(gameStage.Find("Queue"), false);
            SetRenderersEnabled(gameStage.Find("NyoiceLine/Line"), false);
            SetRenderersEnabled(gameStage.Find("NyoiceLine/WallVisualRoot"), true);
            SetRenderersEnabled(gameStage.Find("NyoiceLine/CrossingTarget"), false);
            SetRenderersEnabled(gameStage.Find("Exit/ExitMarker"), true);
            SetRenderersEnabled(gameStage.Find("Exit/ExitPoint"), false);
            SetRenderersEnabled(gameStage.Find("Waypoints"), false);
            SetRenderersEnabled(gameStage.Find("Partitions"), true);
        }

        private static void EnsureNyoiceLineWallVisual(Transform lineRoot, Transform runtimeLine)
        {
            Transform legacyVisual = lineRoot.Find("WallVisual");
            if (legacyVisual != null)
            {
                Object.DestroyImmediate(legacyVisual.gameObject);
            }

            MeshFilter sourceFilter = runtimeLine.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = runtimeLine.GetComponent<MeshRenderer>();
            Transform visualRoot = lineRoot.Find("WallVisualRoot");
            if (visualRoot == null)
            {
                visualRoot = new GameObject("WallVisualRoot").transform;
                visualRoot.SetParent(lineRoot, false);
            }

            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
            EnsureWallVisualSegment(visualRoot, "Wall", WallVisualBottomY, UrinalY,
                sourceFilter, sourceRenderer);
            RemoveDirectChild(visualRoot, "WallUpper");
            RemoveDirectChild(visualRoot, "WallLower");

            sourceRenderer.enabled = false;
            EditorUtility.SetDirty(sourceRenderer);
            EditorUtility.SetDirty(visualRoot);
        }

        private static void EnsureWallVisualSegment(
            Transform root,
            string segmentName,
            float bottom,
            float top,
            MeshFilter sourceFilter,
            MeshRenderer sourceRenderer)
        {
            Transform segment = root.Find(segmentName);
            if (segment == null)
            {
                segment = new GameObject(segmentName, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                segment.SetParent(root, false);
            }

            MeshFilter filter = segment.GetComponent<MeshFilter>();
            MeshRenderer renderer = segment.GetComponent<MeshRenderer>();
            filter.sharedMesh = sourceFilter.sharedMesh;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.sortingOrder = sourceRenderer.sortingOrder;
            renderer.enabled = true;

            float height = top - bottom;
            segment.position = new Vector3(NyoiceLineX, bottom + (height * 0.5f), 0f);
            segment.rotation = Quaternion.identity;
            segment.localScale = new Vector3(0.08f, height, 0.08f);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(segment);
        }

        private static void RemoveDirectChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void SetRenderersEnabled(Transform root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = enabled;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void MigrateSetupRendererMaterials(Transform root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SpriteRenderer || renderer.GetComponent<TextMesh>() != null)
                {
                    continue;
                }

                Material current = renderer.sharedMaterial;
                if (current != null && current.shader != null)
                {
                    SetRendererColor(renderer, current.color, current.shader.name);
                }
            }
        }

        private static void SetRendererColor(
            Renderer renderer,
            Color color,
            string shaderName = null)
        {
            if (renderer == null)
            {
                return;
            }

            Shader shader = !string.IsNullOrEmpty(shaderName)
                ? Shader.Find(shaderName)
                : renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.shader
                    : null;
            if (shader == null)
            {
                return;
            }

            renderer.sharedMaterial = GetOrCreateSetupMaterial(shader, color);
        }

        private static Material GetOrCreateSetupMaterial(Shader shader, Color color)
        {
            Directory.CreateDirectory(MaterialsDirectory);
            Color32 color32 = color;
            string shaderToken = shader.name.Replace('/', '-').Replace(' ', '-');
            string colorToken = $"{color32.r:X2}{color32.g:X2}{color32.b:X2}{color32.a:X2}";
            string materialPath = $"{MaterialsDirectory}/Setup-{shaderToken}-{colorToken}.mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
            {
                return material;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsDirectory }))
            {
                string existingPath = AssetDatabase.GUIDToAssetPath(guid);
                Material existing = AssetDatabase.LoadAssetAtPath<Material>(existingPath);
                if (existing != null && existing.shader == shader &&
                    ((Color32)existing.color).Equals(color32))
                {
                    return existing;
                }
            }

            material = new Material(shader)
            {
                name = $"Setup {shader.name} {colorToken}",
                color = color
            };
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
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
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.orthographic = true;
                EditorUtility.SetDirty(camera);
                return;
            }

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            camera = cameraObject.GetComponent<Camera>();
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
