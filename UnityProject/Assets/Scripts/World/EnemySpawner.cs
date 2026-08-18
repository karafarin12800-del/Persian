using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private int count = 8;
        [SerializeField] private float radius = 120f;
        [SerializeField] private float speed = 1.6f;

        private void Start()
        {
            for (int i = 0; i < count; i++)
            {
                var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                enemy.name = "Enemy_" + i;
                enemy.transform.position = new Vector3(Random.Range(-radius, radius), 1f, Random.Range(-radius, radius));
                enemy.AddComponent<EnemyChase>().Configure(player, speed);
            }
        }
    }

    public sealed class EnemyChase : MonoBehaviour
    {
        private Transform target;
        private float speed;

        public void Configure(Transform targetTransform, float moveSpeed)
        {
            target = targetTransform;
            speed = moveSpeed;
        }

        private void Update()
        {
            if (target == null) return;
            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1f) return;
            transform.position += direction.normalized * speed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}
