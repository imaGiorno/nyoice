using Nyoice.NPC;
using UnityEngine;

namespace Nyoice.Managers
{
    [DisallowMultipleComponent]
    public sealed class NPCSpawner : MonoBehaviour
    {
        public const float DefaultMinimumSpawnInterval = 1f;
        public const float DefaultMaximumSpawnInterval = 5f;

        [SerializeField]
        private NPCController npcPrefab;

        [SerializeField]
        private Transform spawnPoint;

        [SerializeField]
        private QueueManager queueManager;

        [SerializeField]
        private GameStateManager gameStateManager;

        [SerializeField, Min(0.01f)]
        private float minimumSpawnIntervalSeconds = DefaultMinimumSpawnInterval;

        [SerializeField, Min(0.01f)]
        private float maximumSpawnIntervalSeconds = DefaultMaximumSpawnInterval;

        private int _spawnedNpcCount;
        private float _nextSpawnInterval;
        private float _remainingSpawnTime;
        private int _spawnScheduleVersion;
        private bool _hasScheduledSpawn;

        public bool IsSpawningBlocked => gameStateManager != null && gameStateManager.IsGameOver;
        public float MinimumSpawnInterval => minimumSpawnIntervalSeconds;
        public float MaximumSpawnInterval => maximumSpawnIntervalSeconds;
        public float NextSpawnInterval => _nextSpawnInterval;
        public float RemainingSpawnTime => _remainingSpawnTime;
        public int SpawnScheduleVersion => _spawnScheduleVersion;
        public bool HasScheduledSpawn => _hasScheduledSpawn;

        private void OnDestroy()
        {
            UnsubscribeFromGameState();
        }

        public void Configure(
            NPCController prefab,
            Transform spawnPointTransform,
            QueueManager manager)
        {
            npcPrefab = prefab;
            spawnPoint = spawnPointTransform;
            queueManager = manager;
        }

        public void ConfigureSpawnIntervalRange(float minimumSeconds, float maximumSeconds)
        {
            minimumSpawnIntervalSeconds = Mathf.Max(DefaultMinimumSpawnInterval, minimumSeconds);
            maximumSpawnIntervalSeconds = Mathf.Max(minimumSpawnIntervalSeconds, maximumSeconds);
        }

        public void ConfigureGameState(GameStateManager configuredGameStateManager)
        {
            UnsubscribeFromGameState();
            gameStateManager = configuredGameStateManager;
            SubscribeToGameState();

            if (IsSpawningBlocked)
            {
                HandleGameOver();
            }
        }

        private void Start()
        {
            ResolveRuntimeReferences();
            SubscribeToGameState();
            NormalizeSpawnIntervalRange();
            if (npcPrefab == null || spawnPoint == null || queueManager == null ||
                !queueManager.EnsureRuntimeReferences())
            {
                Debug.LogError("NPCSpawner could not initialize its prefab, SpawnPoint, or QueueManager.", this);
                enabled = false;
                return;
            }

            ScheduleNextSpawn();
        }

        private void Update()
        {
            AdvanceSpawnTimer(Time.deltaTime);
        }

        public void AdvanceSpawnTimer(float deltaTime)
        {
            if (IsSpawningBlocked || !_hasScheduledSpawn || deltaTime <= 0f)
            {
                return;
            }

            _remainingSpawnTime = Mathf.Max(0f, _remainingSpawnTime - deltaTime);
            if (_remainingSpawnTime <= 0f)
            {
                SpawnNpc();
            }
        }

        public void ScheduleNextSpawn()
        {
            if (IsSpawningBlocked)
            {
                return;
            }

            NormalizeSpawnIntervalRange();
            _nextSpawnInterval = Random.Range(minimumSpawnIntervalSeconds, maximumSpawnIntervalSeconds);
            _remainingSpawnTime = _nextSpawnInterval;
            _hasScheduledSpawn = true;
            _spawnScheduleVersion++;
        }

        private void SpawnNpc()
        {
            if (IsSpawningBlocked)
            {
                HandleGameOver();
                return;
            }

            if (npcPrefab == null || spawnPoint == null || queueManager == null)
            {
                Debug.LogError("NPCSpawner is missing its prefab, SpawnPoint, or QueueManager reference.", this);
                enabled = false;
                return;
            }

            NPCController npc = Instantiate(npcPrefab, spawnPoint.position, Quaternion.identity);
            _spawnedNpcCount++;
            npc.name = $"NPC_{_spawnedNpcCount:000}";
            queueManager.Enqueue(npc);
            ScheduleNextSpawn();
        }

        private void NormalizeSpawnIntervalRange()
        {
            minimumSpawnIntervalSeconds = Mathf.Max(DefaultMinimumSpawnInterval, minimumSpawnIntervalSeconds);
            maximumSpawnIntervalSeconds = Mathf.Max(minimumSpawnIntervalSeconds, maximumSpawnIntervalSeconds);
        }

        private void ResolveRuntimeReferences()
        {
            if (queueManager == null)
            {
                queueManager = FindAnyObjectByType<QueueManager>();
            }

            if (spawnPoint == null)
            {
                GameObject resolvedSpawnPoint = GameObject.Find("GameStage/Entrance/SpawnPoint");
                spawnPoint = resolvedSpawnPoint != null ? resolvedSpawnPoint.transform : null;
            }

            if (gameStateManager == null)
            {
                gameStateManager = FindAnyObjectByType<GameStateManager>();
            }
        }

        private void SubscribeToGameState()
        {
            if (gameStateManager != null)
            {
                gameStateManager.GameOver -= HandleGameOver;
                gameStateManager.GameOver += HandleGameOver;
            }
        }

        private void UnsubscribeFromGameState()
        {
            if (gameStateManager != null)
            {
                gameStateManager.GameOver -= HandleGameOver;
            }
        }

        private void HandleGameOver()
        {
            enabled = false;
            Debug.Log("NPCSpawner stopped because game is over", this);
        }
    }
}
