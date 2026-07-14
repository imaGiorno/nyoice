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

        [SerializeField, Min(0.1f)]
        private float spawnIntervalSeconds = 3f;

        private int _spawnedNpcCount;

        public void Configure(
            NPCController prefab,
            Transform spawnPointTransform,
            QueueManager manager)
        {
            npcPrefab = prefab;
            spawnPoint = spawnPointTransform;
            queueManager = manager;
        }

        private IEnumerator Start()
        {
            ResolveRuntimeReferences();
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
                SpawnNpc();
            }
        }

        private void SpawnNpc()
        {
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
        }
    }
}

