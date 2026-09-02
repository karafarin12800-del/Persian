using UnityEngine;
using PersiaWar.Unity2D5D;

public sealed class PersiaWarRuntime : MonoBehaviour
{
    [SerializeField] private GameSession session;
    [SerializeField] private GameObject player;
    [SerializeField] private EnemySpawner enemySpawner;

    private void Awake()
    {
        if (session == null) session = FindFirstObjectByType<GameSession>();
        if (session == null)
        {
            GameObject obj = new GameObject("GameSession");
            session = obj.AddComponent<GameSession>();
        }
    }

    private void Update()
    {
        if (player == null)
        {
            PlayerController controller = FindFirstObjectByType<PlayerController>();
            if (controller != null) player = controller.gameObject;
        }

        if (player == null || session == null || session.IsFinished) return;

        TargetHealth health = player.GetComponent<TargetHealth>();
        if (health != null && health.CurrentHealth <= 0)
            session.EndMission(false);
    }
}
