using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class OffscreenEnemySpawner : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private float spawnRadius = 18f;
        [SerializeField] private float minDistance = 11f;
        [SerializeField] private float interval = 4f;
        [SerializeField] private int maxAlive = 12;
        [SerializeField] private LayerMask blockingMask;

        private float nextSpawn;

        private void Update()
        {
            if (player == null || enemyPrefab == null || Time.time < nextSpawn)
                return;

            nextSpawn = Time.time + interval;
            if (FindObjectsOfType<EnemyChase>().Length >= maxAlive)
                return;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(minDistance, spawnRadius);
                Vector3 candidate = player.position + new Vector3(ring.x, 0f, ring.y);
                if (Physics.CheckSphere(candidate, 0.7f, blockingMask, QueryTriggerInteraction.Ignore))
                    continue;

                GameObject enemy = Instantiate(enemyPrefab, candidate, Quaternion.identity);
                EnemyChase chase = enemy.GetComponent<EnemyChase>();
                if (chase != null)
                    chase.SetTarget(player);
                break;
            }
        }
    }
}
