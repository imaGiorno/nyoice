using System;
using System.Reflection;
using Nyoice.Managers;
using Nyoice.NPC;
using Nyoice.Toilet;
using Nyoice.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.Editor
{
    public static class NyoiceSprint56ARandomTimingGaugeValidator
    {
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private static readonly MethodInfo SpawnNpcMethod = GetMethod(typeof(NPCSpawner), "SpawnNpc");

        [MenuItem("Nyoice/Validate Sprint5-6A Random Timing Gauge")]
        public static void ValidateRandomTimingGauge()
        {
            ValidateConfiguredSetupTwice();
            ValidateSpawnTiming();
            ValidateUrinationTimingAndGauge();
            NyoiceSprint55AutoUrinalSelectionValidator.ValidateAutoUrinalSelection();
            Debug.Log("Sprint5-6A Random Timing Gauge validation passed.");
        }

        private static void ValidateConfiguredSetupTwice()
        {
            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            Require(prefab != null, "NPC prefab is missing. Run Nyoice/Setup Sprint 1 and Setup Game Stage.");
            NPCController npc = prefab.GetComponent<NPCController>();
            Require(npc != null, "NPC prefab has no NPCController.");
            UrinationTimeGauge[] gauges = prefab.GetComponentsInChildren<UrinationTimeGauge>(true);
            Require(gauges.Length == 1, "NPC prefab must contain exactly one urination gauge after repeated setup.");
            Require(gauges[0].SegmentFills != null &&
                    gauges[0].SegmentFills.Length == UrinationTimeGauge.SegmentCount,
                "Configured NPC gauge does not contain five segment references.");
            Require(gauges[0].GetComponentsInChildren<Image>(true).Length == 10,
                "Configured NPC gauge must contain five backgrounds and five fills.");
            ValidateVerticalGaugeLayout(gauges[0]);
            Require(npc.MinimumUrinationDuration == NPCController.DefaultMinimumUrinationDuration &&
                    npc.MaximumUrinationDuration == NPCController.DefaultMaximumUrinationDuration,
                "NPC prefab urination range is not 2.0 to 10.0 seconds.");

            NPCSpawner sceneSpawner = UnityEngine.Object.FindAnyObjectByType<NPCSpawner>(FindObjectsInactive.Include);
            Require(sceneSpawner != null, "Configured GameScene has no NPCSpawner.");
            Require(sceneSpawner.MinimumSpawnInterval == NPCSpawner.DefaultMinimumSpawnInterval &&
                    sceneSpawner.MaximumSpawnInterval == NPCSpawner.DefaultMaximumSpawnInterval,
                "Configured GameScene spawn range is not 1.0 to 5.0 seconds.");
        }

        private static void ValidateSpawnTiming()
        {
            using (var fixture = new Fixture("SpawnTiming"))
            {
                NPCSpawner spawner = fixture.Root.AddComponent<NPCSpawner>();
                spawner.ConfigureSpawnIntervalRange(
                    NPCSpawner.DefaultMinimumSpawnInterval,
                    NPCSpawner.DefaultMaximumSpawnInterval);
                spawner.Configure(fixture.NpcPrefab, fixture.SpawnPoint, fixture.Queue);
                spawner.ConfigureGameState(fixture.GameState);

                Require(spawner.MinimumSpawnInterval == 1f && spawner.MaximumSpawnInterval == 5f,
                    "Default spawn range is not 1.0 to 5.0 seconds.");
                spawner.ScheduleNextSpawn();
                float scheduled = spawner.NextSpawnInterval;
                int version = spawner.SpawnScheduleVersion;
                Require(scheduled >= 1f && scheduled <= 5f, "Spawn interval is outside its range.");
                spawner.AdvanceSpawnTimer(0.25f);
                Require(spawner.NextSpawnInterval == scheduled && spawner.SpawnScheduleVersion == version,
                    "Spawn interval was redrawn before the next spawn.");

                SpawnNpcMethod.Invoke(spawner, null);
                Require(spawner.SpawnScheduleVersion == version + 1,
                    "A new interval was not selected immediately after spawning.");
                Require(spawner.NextSpawnInterval >= 1f && spawner.NextSpawnInterval <= 5f,
                    "Post-spawn interval is outside its range.");

                float remaining = spawner.RemainingSpawnTime;
                fixture.GameState.TriggerGameOver();
                spawner.AdvanceSpawnTimer(1f);
                Require(spawner.RemainingSpawnTime == remaining,
                    "Spawn timer advanced after GameOver.");

                spawner.ConfigureSpawnIntervalRange(-1f, -5f);
                Require(spawner.MinimumSpawnInterval > 0f &&
                        spawner.MaximumSpawnInterval >= spawner.MinimumSpawnInterval,
                    "Invalid spawn range was not normalized safely.");
            }
        }

        private static void ValidateUrinationTimingAndGauge()
        {
            using (var fixture = new Fixture("UrinationTiming"))
            {
                NPCController waitingNpc = fixture.CreateNpc("WaitingNPC");
                Require(waitingNpc.MinimumUrinationDuration == 2f && waitingNpc.MaximumUrinationDuration == 10f,
                    "Default urination range is not 2.0 to 10.0 seconds.");
                waitingNpc.Initialize(fixture.Queue);
                float assigned = waitingNpc.AssignedUrinationDuration;
                Require(assigned >= 2f && assigned <= 10f, "Assigned urination duration is outside its range.");
                waitingNpc.Initialize(fixture.Queue);
                Require(waitingNpc.AssignedUrinationDuration == assigned,
                    "The same NPC redrew its urination duration.");
                float waitingRemaining = waitingNpc.RemainingUrinationTime;
                waitingNpc.AdvanceUrinationTime(1f);
                Require(waitingNpc.RemainingUrinationTime == waitingRemaining,
                    "Remaining time decreased before urination started.");

                NPCController timedNpc = fixture.CreateNpc("TimedNPC");
                timedNpc.ConfigureUrinationDuration(3f);
                PrepareUsingUrinal(timedNpc, fixture.Urinals[0]);
                Require(timedNpc.BeginUrination(), "Could not begin validation urination.");
                Require(timedNpc.IsUrinationStarted && timedNpc.RemainingUrinationTime == 3f,
                    "Urination start did not initialize remaining time.");
                timedNpc.AdvanceUrinationTime(0.5f);
                Require(Mathf.Approximately(timedNpc.RemainingUrinationTime, 2.5f),
                    "Remaining time did not decrease after urination started.");

                UrinationTimeGauge gauge = CreateGauge(timedNpc);
                gauge.ApplyTime(3f);
                RequireFills(gauge, 1f, 0.5f, 0f, 0f, 0f);
                gauge.ApplyTime(10f);
                RequireFills(gauge, 1f, 1f, 1f, 1f, 1f);
                gauge.Refresh();
                Require(Mathf.Approximately(gauge.DisplayedTime, timedNpc.RemainingUrinationTime),
                    "Active gauge does not display remaining time.");
                RequireFills(gauge, 1f, 0.25f, 0f, 0f, 0f);

                timedNpc.AdvanceUrinationTime(10f);
                gauge.Refresh();
                Require(timedNpc.RemainingUrinationTime == 0f && !gauge.IsGaugeVisible,
                    "Completed urination did not reach zero and hide the gauge.");

                NPCController frozenNpc = fixture.CreateNpc("FrozenNPC");
                frozenNpc.ConfigureUrinationDuration(7f);
                PrepareUsingUrinal(frozenNpc, fixture.Urinals[1]);
                Require(frozenNpc.BeginUrination(), "Could not begin GameOver timing validation.");
                UrinationTimeGauge frozenGauge = CreateGauge(frozenNpc);
                frozenGauge.Refresh();
                float frozenRemaining = frozenNpc.RemainingUrinationTime;
                fixture.GameState.TriggerGameOver();
                frozenNpc.AdvanceUrinationTime(1f);
                frozenGauge.Refresh();
                Require(frozenNpc.RemainingUrinationTime == frozenRemaining,
                    "Remaining time decreased after GameOver.");
                Require(frozenGauge.DisplayedTime == frozenRemaining,
                    "Gauge display changed after GameOver.");
                Require(gauge != frozenGauge && gauge.SegmentFills != frozenGauge.SegmentFills,
                    "Multiple NPC gauges do not maintain independent state.");
            }
        }

        private static void PrepareUsingUrinal(NPCController npc, UrinalController urinal)
        {
            Require(urinal.Reserve(npc) && urinal.Occupy(npc), "Could not prepare occupied urinal.");
            SetAutoProperty(npc, "TargetUrinal", urinal);
            SetAutoProperty(npc, "State", NPCState.UsingUrinal);
        }

        private static UrinationTimeGauge CreateGauge(NPCController npc)
        {
            GameObject root = new GameObject("UrinationGauge", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(npc.transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var fills = new Image[UrinationTimeGauge.SegmentCount];
            for (int index = 0; index < fills.Length; index++)
            {
                GameObject cell = new GameObject($"Cell{index + 1:00}", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(root.transform, false);
                GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(cell.transform, false);
                fills[index] = fill.GetComponent<Image>();
                fills[index].type = Image.Type.Filled;
            }

            UrinationTimeGauge gauge = root.AddComponent<UrinationTimeGauge>();
            gauge.Configure(npc, canvas, fills);
            Require(npc.GetComponentsInChildren<UrinationTimeGauge>(true).Length == 1,
                "NPC does not contain exactly one gauge.");
            Require(fills.Length == 5, "Gauge does not contain five segments.");
            return gauge;
        }

        private static void RequireFills(UrinationTimeGauge gauge, params float[] expected)
        {
            Require(gauge.SegmentFills.Length == expected.Length, "Gauge segment count mismatch.");
            for (int index = 0; index < expected.Length; index++)
            {
                Require(Mathf.Approximately(gauge.SegmentFills[index].fillAmount, expected[index]),
                    $"Gauge segment {index + 1} expected {expected[index]:0.##} " +
                    $"but found {gauge.SegmentFills[index].fillAmount:0.##}.");
            }
        }

        private static void ValidateVerticalGaugeLayout(UrinationTimeGauge gauge)
        {
            RectTransform gaugeRect = gauge.GetComponent<RectTransform>();
            Require(gaugeRect != null && Mathf.Abs(gaugeRect.localPosition.x) >= 0.25f,
                "Configured gauge is not offset horizontally from the NPC center.");
            Require(gauge.GaugeCanvas != null && gauge.GaugeCanvas.sortingOrder > 1 &&
                    gaugeRect.localPosition.z < -0.26f,
                "Gauge canvas is not configured in front of the urinal number.");

            float previousY = float.NegativeInfinity;
            float expectedX = 0f;
            for (int index = 0; index < UrinationTimeGauge.SegmentCount; index++)
            {
                Transform cell = gauge.transform.Find($"Cell{index + 1:00}");
                Require(cell != null, $"Configured gauge is missing Cell{index + 1:00}.");
                RectTransform cellRect = cell.GetComponent<RectTransform>();
                Require(cellRect != null, $"Cell{index + 1:00} has no RectTransform.");
                Require(cellRect.anchoredPosition.y > previousY,
                    "Gauge cells are not ordered vertically from Cell01 bottom to Cell05 top.");
                Require(Mathf.Abs(cellRect.anchoredPosition.x - expectedX) <= 0.01f,
                    "Gauge cell X positions are not aligned.");
                Image fill = cell.Find("Fill")?.GetComponent<Image>();
                Require(fill != null && fill.type == Image.Type.Filled &&
                        fill.fillMethod == Image.FillMethod.Vertical &&
                        fill.fillOrigin == (int)Image.OriginVertical.Bottom,
                    $"Cell{index + 1:00} fill is not bottom-origin vertical fill.");
                previousY = cellRect.anchoredPosition.y;
            }
        }

        private static void SetAutoProperty<T>(object target, string propertyName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, $"Backing field for {propertyName} was not found.");
            field.SetValue(target, value);
        }

        private static MethodInfo GetMethod(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
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

        private sealed class Fixture : IDisposable
        {
            public Fixture(string name)
            {
                Root = new GameObject($"Sprint56A_{name}");
                Root.SetActive(false);
                GameState = Add<GameStateManager>("GameState");
                Urinals = CreateUrinals();
                UrinalManager urinalManager = Add<UrinalManager>("UrinalManager");
                urinalManager.Configure(Urinals, null, null);
                urinalManager.ConfigureGameState(GameState);
                UrinalTicketManager tickets = Add<UrinalTicketManager>("Tickets");
                tickets.Configure(8);
                tickets.ConfigureGameState(GameState);
                Transform queueRoot = Child(Root.transform, "Queue");
                QueueSlot[] slots = new QueueSlot[8];
                for (int index = 0; index < slots.Length; index++)
                {
                    slots[index] = Child(queueRoot, $"Queue{index + 1:00}").gameObject.AddComponent<QueueSlot>();
                    slots[index].Initialize(index + 1);
                }

                Transform decision = Child(queueRoot, "DecisionPoint");
                Transform approach = Child(queueRoot, "NyoiceApproachPoint");
                Transform crossing = Child(Root.transform, "CrossingTarget");
                Queue = Add<QueueManager>("QueueManager");
                Queue.Configure(slots, decision);
                Queue.ConfigureUrinalFlow(urinalManager, tickets, approach, crossing);
                Queue.ConfigureGameState(GameState);
                SpawnPoint = Child(Root.transform, "SpawnPoint");
                NpcPrefab = CreateNpc("NPCPrefab");
            }

            public GameObject Root { get; }
            public GameStateManager GameState { get; }
            public QueueManager Queue { get; }
            public UrinalController[] Urinals { get; }
            public Transform SpawnPoint { get; }
            public NPCController NpcPrefab { get; }

            public NPCController CreateNpc(string name)
            {
                GameObject child = new GameObject(name);
                child.transform.SetParent(Root.transform, false);
                child.AddComponent<NPCMovement>();
                NPCController npc = child.AddComponent<NPCController>();
                npc.ConfigureGameState(GameState);
                return npc;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                NPCController[] npcs = Resources.FindObjectsOfTypeAll<NPCController>();
                foreach (NPCController npc in npcs)
                {
                    if (npc != null && !EditorUtility.IsPersistent(npc) && npc.name == "NPC_001")
                    {
                        UnityEngine.Object.DestroyImmediate(npc.gameObject);
                    }
                }
            }

            private UrinalController[] CreateUrinals()
            {
                var result = new UrinalController[8];
                for (int index = 0; index < result.Length; index++)
                {
                    Transform root = Child(Root.transform, $"Urinal{index + 1:00}");
                    Transform move = Child(root, "MovePoint");
                    Transform use = Child(root, "UsePoint");
                    Transform exit = Child(root, "ExitStartPoint");
                    var urinal = root.gameObject.AddComponent<UrinalController>();
                    urinal.Configure(index + 1, move, use, exit, null, null);
                    result[index] = urinal;
                }

                return result;
            }

            private T Add<T>(string name) where T : Component
            {
                return Child(Root.transform, name).gameObject.AddComponent<T>();
            }

            private static Transform Child(Transform parent, string name)
            {
                var child = new GameObject(name);
                child.transform.SetParent(parent, false);
                return child.transform;
            }
        }
    }
}
