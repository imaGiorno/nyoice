using System.Collections;
using Nyoice.NPC;
using UnityEngine;

namespace Nyoice.Managers
{
    /// <summary>
    /// Spawns an NPC every three seconds and hands it to the queue.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NPCSpawner : MonoBehaviour
    {
        [SerializeField]
        private NPCController npcPrefab;

        [SerializeField]
        private Transform spawnPoint;

        [SerializeField]
        private QueueManager queueManager;

        [SerializeField]
        private GameStateManager gameStateManager;

        [SerializeField, Min(0.1f)]
        private float spawnIntervalSeconds = 3f;

        private int _spawnedNpcCount;

        public bool IsSpawningBlocked => gameStateManager != null && gameStateManager.IsGameOver;

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

        private IEnumerator Start()
        {
            ResolveRuntimeReferences();
            SubscribeToGameState();
            if (npcPrefab == null || spawnPoint == null || queueManager == null || !queueManager.EnsureRuntimeReferences())
            {
                Debug.LogError("NPCSpawner could not initialize its prefab, SpawnPoint, or QueueManager.", this);
                enabled = false;
                yield break;
            }

            var wait = new WaitForSeconds(spawnIntervalSeconds);

            while (true)
            {
                yield return wait;
                if (IsSpawningBlocked)
                {
                    yield break;
                }

                SpawnNpc();
            }
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
            StopAllCoroutines();
            enabled = false;
            Debug.Log("NPCSpawner stopped because game is over", this);
        }
    }
}
