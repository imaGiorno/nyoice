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
            npc.name = $"NPC_{Time.frameCount}";
            queueManager.Enqueue(npc);
        }
    }
}
