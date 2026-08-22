using System;
using System.Reflection;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nyoice.Editor
{
    public static class NyoiceSprint54AInitialFlowValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private static readonly MethodInfo MovePointReachedMethod = GetNpcMethod("HandleMovePointReached");
        private static readonly MethodInfo UsePointReachedMethod = GetNpcMethod("HandleUsePointReached");
        private static readonly MethodInfo QueueSlotReachedMethod = GetNpcMethod("HandleQueueSlotReached");
        private static readonly MethodInfo ApproachPointReachedMethod = GetNpcMethod("HandleApproachPointReached");
        private static readonly MethodInfo CrossingTargetReachedMethod = GetNpcMethod("HandleCrossingTargetReached");
        private static readonly MethodInfo WallClearanceReachedMethod = GetNpcMethod("HandleWallClearanceReached");

        [MenuItem("Nyoice/Validate Sprint5-4A Initial Flow")]
        public static void ValidateInitialFlow()
        {
            ValidateConfiguredGameScene();

            var root = new GameObject("Sprint54AInitialFlowValidation");
            root.SetActive(false);

            try
            {
                Transform queueRoot = CreateChild(root.transform, "Queue");
                QueueSlot[] slots = CreateQueueSlots(queueRoot);
                Transform decisionPoint = CreatePoint(queueRoot, "DecisionPoint", Vector3.zero);
                Transform approachPoint = CreatePoint(queueRoot, "NyoiceApproachPoint", Vector3.left);
                Transform crossingTarget = CreatePoint(root.transform, "CrossingTarget", Vector3.left * 2f);
                Transform exitPoint = CreatePoint(root.transform, "ExitPoint", Vector3.right * 5f);

                GameStateManager gameState = Add<GameStateManager>(root.transform, "GameState");
                UrinalController[] urinals = CreateUrinals(root.transform);
                UrinalManager urinalManager = Add<UrinalManager>(root.transform, "UrinalManager");
                urinalManager.Configure(urinals, null, null);
                urinalManager.ConfigureGameState(gameState);
                UrinalTicketManager ticketManager = Add<UrinalTicketManager>(root.transform, "TicketManager");
                ticketManager.Configure(urinals.Length);
                ticketManager.ConfigureGameState(gameState);
                DiscomfortManager discomfort = Add<DiscomfortManager>(root.transform, "DiscomfortManager");
                discomfort.Configure(urinals, gameState);
                ScoreManager scoreManager = Add<ScoreManager>(root.transform, "ScoreManager");
                scoreManager.Configure(discomfort, gameState);

                QueueManager queueManager = Add<QueueManager>(root.transform, "QueueManager");
                queueManager.Configure(slots, decisionPoint);
                queueManager.ConfigureUrinalFlow(
                    urinalManager,
                    ticketManager,
                    approachPoint,
                    crossingTarget);
                queueManager.ConfigureExitFlow(exitPoint);
                queueManager.ConfigureGameState(gameState);
                queueManager.ConfigureScore(scoreManager);

                Require(queueManager.HasInitialFlowReferences, "Initial-flow references are incomplete.");
                Require(!scoreManager.IsComboTimingStarted, "Combo timing started before the first urinal use.");
                scoreManager.AdvanceTime(30f);
                Require(Mathf.Approximately(scoreManager.NoAdjacencyElapsed, 0f),
                    "Combo elapsed time advanced before the first urinal use.");
                Require(Mathf.Approximately(scoreManager.ComboMultiplier, 1f),
                    "Initial combo multiplier is not x1.0.");

                NPCController firstNpc = CreateNpc(root.transform, "NPC_001");
                queueManager.Enqueue(firstNpc);
                Require(queueManager.SelectionZoneOccupant == firstNpc,
                    "The first NPC cannot accept urinal selection at spawn.");
                Require(urinalManager.ActiveSelectionNpc == firstNpc,
                    "Urinal input was not assigned to the spawned NPC.");
                Require(firstNpc.State == NPCState.Queue && firstNpc.CanChangeUrinalAssignment,
                    "The spawned NPC cannot change its automatic assignment while preserving its queue state.");
                Require(firstNpc.TargetUrinal == urinals[7] && urinals[7].ReservedBy == firstNpc,
                    "Spawn or ticket acquisition did not automatically reserve Urinal08.");
                Require(!scoreManager.IsComboTimingStarted,
                    "Spawn or selection readiness started combo timing.");

                NPCController secondNpc = CreateNpc(root.transform, "NPC_002");
                queueManager.Enqueue(secondNpc);
                Require(queueManager.PendingNpcs.Count == 2 &&
                        queueManager.PendingNpcs[0] == firstNpc &&
                        queueManager.PendingNpcs[1] == secondNpc,
                    "Queue registration did not preserve FIFO order.");
                Require(queueManager.SelectionZoneOccupant == firstNpc,
                    "A newer NPC replaced the oldest selection candidate.");

                UrinalController selectedUrinal = urinals[7];
                Require(urinalManager.SelectUrinal(selectedUrinal), "Urinal08 could not remain selected.");
                Require(urinalManager.ConfirmActiveSelection(), "The automatic urinal assignment was not retained.");
                Require(firstNpc.TargetUrinal == selectedUrinal && selectedUrinal.ReservedBy == firstNpc,
                    "The spawn-time urinal selection was not reflected on the NPC.");
                Require(firstNpc.State == NPCState.Queue,
                    "Urinal assignment incorrectly removed the NPC from its queue route.");
                NPCMovement movement = firstNpc.GetComponent<NPCMovement>();
                Require(movement.IsMoving && movement.TargetPosition != selectedUrinal.MovePoint.position,
                    "Urinal assignment created a Spawn-to-MovePoint shortcut.");
                Require(queueManager.SelectionZoneOccupant == firstNpc,
                    "Selection control was released before the first NPC passed DecisionPoint.");
                NPCController thirdNpc = CreateNpc(root.transform, "NPC_003");
                queueManager.Enqueue(thirdNpc);
                Require(queueManager.SelectionZoneOccupant == firstNpc &&
                        queueManager.PendingNpcs[2] == thirdNpc,
                    "A newly spawned NPC overtook an older waiting NPC.");
                Require(!scoreManager.IsComboTimingStarted,
                    "Urinal reservation started combo timing before use began.");

                QueueSlotReachedMethod.Invoke(firstNpc, null);
                Require(firstNpc.CurrentSlot != null && firstNpc.CurrentSlot.QueueNumber == 7,
                    "The first NPC did not advance from Queue08 to Queue07.");
                Require(secondNpc.CurrentSlot != null && secondNpc.CurrentSlot.QueueNumber == 8,
                    "The second NPC was not admitted to the released Queue08 slot.");
                Require(firstNpc.CurrentSlot != secondNpc.CurrentSlot,
                    "Multiple NPCs were assigned to the same queue slot.");

                for (int expectedQueue = 6; expectedQueue >= 1; expectedQueue--)
                {
                    QueueSlotReachedMethod.Invoke(firstNpc, null);
                    Require(firstNpc.CurrentSlot != null && firstNpc.CurrentSlot.QueueNumber == expectedQueue,
                        $"The first NPC did not compact to Queue{expectedQueue:00}.");
                }

                QueueSlotReachedMethod.Invoke(firstNpc, null);
                Require(movement.TargetPosition == decisionPoint.position,
                    "The first NPC did not travel from Queue01 to DecisionPoint.");
                firstNpc.HandleDecisionPointReached();
                Require(firstNpc.State == NPCState.ApproachingLine &&
                        movement.TargetPosition == approachPoint.position &&
                        queueManager.ApproachRouteOccupant == firstNpc,
                    "The reserved NPC did not continue from DecisionPoint to ApproachPoint.");
                Require(queueManager.SelectionZoneOccupant == secondNpc,
                    "The oldest remaining NPC did not receive selection after DecisionPoint passage.");
                ApproachPointReachedMethod.Invoke(firstNpc, null);
                Require(firstNpc.State == NPCState.CrossingLine &&
                        movement.TargetPosition == crossingTarget.position &&
                        queueManager.ApproachRouteOccupant == null &&
                        queueManager.CrossingCorridorOccupant == firstNpc,
                    "The NPC did not continue directly from ApproachPoint to CrossingTarget.");
                CrossingTargetReachedMethod.Invoke(firstNpc, null);
                Vector3 wallClearanceTarget = selectedUrinal.UrinalNumber >= 7
                    ? new Vector3(selectedUrinal.MovePoint.position.x, crossingTarget.position.y,
                        selectedUrinal.MovePoint.position.z)
                    : selectedUrinal.MovePoint.position;
                Require(firstNpc.State == NPCState.WalkingToUrinal &&
                        movement.TargetPosition == wallClearanceTarget,
                    "The NPC did not follow its hybrid urinal route.");
                if (selectedUrinal.UrinalNumber >= 7)
                {
                    WallClearanceReachedMethod.Invoke(firstNpc, null);
                }
                Require(movement.TargetPosition == selectedUrinal.MovePoint.position,
                    "The NPC did not continue from wall clearance to MovePoint.");

                MovePointReachedMethod.Invoke(firstNpc, null);
                Require(movement.TargetPosition == selectedUrinal.UsePoint.position,
                    "The NPC did not continue from MovePoint to UsePoint.");
                UsePointReachedMethod.Invoke(firstNpc, null);
                Require(firstNpc.State == NPCState.UsingUrinal && selectedUrinal.IsOccupied,
                    "The NPC did not enter urinal use after reaching UsePoint.");
                Require(scoreManager.IsComboTimingStarted,
                    "The first urinal use did not start combo timing.");
                scoreManager.AdvanceTime(5f);
                Require(Mathf.Approximately(scoreManager.ComboMultiplier, 1.5f),
                    "Combo timing did not advance after the first urinal use.");

                scoreManager.ResetSession();
                Require(!scoreManager.IsComboTimingStarted &&
                        Mathf.Approximately(scoreManager.NoAdjacencyElapsed, 0f) &&
                        Mathf.Approximately(scoreManager.ComboMultiplier, 1f),
                    "ResetSession did not reset combo timing state.");

                Debug.Log("Sprint5-4A Initial Flow validation passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateConfiguredGameScene()
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) != null,
                "Configured GameScene does not exist. Run Nyoice/Setup Game Stage first.");
            Scene scene = SceneManager.GetActiveScene().path == GameScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            GameObject gameStage = FindRootObject(scene, "GameStage");
            Require(gameStage != null,
                "Configured GameScene has no GameStage. Run Nyoice/Setup Game Stage first.");
            Transform urinalRoot = gameStage.transform.Find("Urinals");
            Require(urinalRoot != null,
                "Configured GameScene has no GameStage/Urinals hierarchy.");
            UrinalController[] sceneUrinals = urinalRoot.GetComponentsInChildren<UrinalController>(true);
            Require(sceneUrinals.Length == 8, "Configured GameScene does not contain exactly eight urinals.");
            Array.Sort(sceneUrinals, (left, right) => left.UrinalNumber.CompareTo(right.UrinalNumber));
            for (int index = 0; index < sceneUrinals.Length; index++)
            {
                UrinalController urinal = sceneUrinals[index];
                int expectedNumber = index + 1;
                Require(urinal.UrinalNumber == expectedNumber,
                    $"Configured urinal number {expectedNumber} is missing or duplicated.");
                Transform labelTransform = urinal.transform.Find("Number");
                Require(labelTransform != null, $"Urinal{expectedNumber:00} has no number label.");
                Require(labelTransform.gameObject.activeInHierarchy,
                    $"Urinal{expectedNumber:00} number label is inactive.");
                TextMesh label = labelTransform.GetComponent<TextMesh>();
                Require(label != null && label.text == expectedNumber.ToString(),
                    $"Urinal{expectedNumber:00} label does not match UrinalNumber.");
                MeshRenderer labelRenderer = labelTransform.GetComponent<MeshRenderer>();
                Require(labelRenderer != null && labelRenderer.enabled && labelRenderer.sharedMaterial == label.font.material,
                    $"Urinal{expectedNumber:00} number label has an invalid renderer or font material.");
            }

            GameStateManager gameState = FindSceneComponent<GameStateManager>();
            DiscomfortManager discomfort = FindSceneComponent<DiscomfortManager>();
            NPCSpawner spawner = FindSceneComponent<NPCSpawner>();
            QueueManager queue = FindSceneComponent<QueueManager>();
            UrinalManager urinalManager = FindSceneComponent<UrinalManager>();
            ScoreManager score = FindSceneComponent<ScoreManager>();
            Require(GetPrivateField<GameStateManager>(discomfort, "gameStateManager") == gameState,
                "GameScene DiscomfortManager is not configured with its GameStateManager.");
            Require(GetPrivateField<UrinalController[]>(discomfort, "urinals").Length == 8,
                "GameScene DiscomfortManager is not configured with all urinals.");

            MethodInfo setDiscomfort = typeof(DiscomfortManager).GetMethod(
                "SetCurrentDiscomfort",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(setDiscomfort != null, "Discomfort maximum transition method was not found.");
            setDiscomfort.Invoke(discomfort, new object[] { discomfort.MaxDiscomfort - 0.00001f });
            setDiscomfort.Invoke(discomfort, new object[] { discomfort.MaxDiscomfort });

            Require(gameState.IsGameOver, "GameScene did not enter GameOver at maximum discomfort.");
            Require(spawner.IsSpawningBlocked, "GameScene NPC spawning continued after GameOver.");
            Require(queue.IsProgressionBlocked, "GameScene queue progression continued after GameOver.");
            Require(!urinalManager.IsInputEnabled, "GameScene urinal input remained enabled after GameOver.");
            Require(!score.NotifyNpcFinished(), "GameScene score progressed after GameOver.");

            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
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

        private static T FindSceneComponent<T>() where T : Component
        {
            T component = UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            Require(component != null, $"Configured GameScene has no {typeof(T).Name}.");
            return component;
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, $"{target.GetType().Name}.{fieldName} was not found.");
            return (T)field.GetValue(target);
        }

        private static QueueSlot[] CreateQueueSlots(Transform parent)
        {
            var slots = new QueueSlot[8];
            for (int index = 0; index < slots.Length; index++)
            {
                Transform child = CreatePoint(parent, $"Queue{index + 1:00}", Vector3.right * index);
                slots[index] = child.gameObject.AddComponent<QueueSlot>();
                slots[index].Initialize(index + 1);
            }

            return slots;
        }

        private static UrinalController[] CreateUrinals(Transform parent)
        {
            var urinals = new UrinalController[8];
            for (int index = 0; index < urinals.Length; index++)
            {
                Transform urinalRoot = CreateChild(parent, $"Urinal{index + 1:00}");
                Transform movePoint = CreatePoint(urinalRoot, "MovePoint", Vector3.right * (index + 1));
                Transform usePoint = CreatePoint(urinalRoot, "UsePoint", Vector3.right * (index + 1));
                Transform exitStartPoint = CreatePoint(urinalRoot, "ExitStartPoint", Vector3.up);
                var highlight = new GameObject("Highlight");
                highlight.transform.SetParent(urinalRoot, false);
                UrinalController urinal = urinalRoot.gameObject.AddComponent<UrinalController>();
                urinal.Configure(index + 1, movePoint, usePoint, exitStartPoint, highlight, null);
                urinals[index] = urinal;
            }

            return urinals;
        }

        private static NPCController CreateNpc(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<NPCMovement>();
            return child.AddComponent<NPCController>();
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 position)
        {
            Transform point = CreateChild(parent, name);
            point.position = position;
            return point;
        }

        private static T Add<T>(Transform parent, string name) where T : Component
        {
            return CreateChild(parent, name).gameObject.AddComponent<T>();
        }

        private static MethodInfo GetNpcMethod(string name)
        {
            MethodInfo method = typeof(NPCController).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(typeof(NPCController).FullName, name);
            }

            return method;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
