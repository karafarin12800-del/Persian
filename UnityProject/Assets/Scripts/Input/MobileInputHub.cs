using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class MobileInputHub : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private float moveRadius = 120f;

        private int pointerId = -1;
        private Vector2 startScreen;

        private void Awake()
        {
            if (gameplayCamera == null) gameplayCamera = Camera.main;
        }

        private void Update()
        {
            if (player == null) return;

#if UNITY_EDITOR || UNITY_STANDALONE
            player.SetMoveInput(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")));
            return;
#endif

            if (Input.touchCount == 0)
            {
                player.SetMoveInput(Vector2.zero);
                return;
            }

            Touch touch = pointerId >= 0 ? FindTouch(pointerId) : Input.GetTouch(0);
            if (pointerId < 0)
            {
                pointerId = touch.fingerId;
                startScreen = touch.position;
            }

            Vector2 delta = touch.position - startScreen;
            Vector2 normalized = Vector2.ClampMagnitude(delta / moveRadius, 1f);
            player.SetMoveInput(normalized);

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                pointerId = -1;
                player.SetMoveInput(Vector2.zero);
            }
        }

        private static Touch FindTouch(int fingerId)
        {
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).fingerId == fingerId)
                    return Input.GetTouch(i);
            return Input.GetTouch(0);
        }
    }
}
