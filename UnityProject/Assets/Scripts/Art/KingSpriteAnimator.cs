using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class KingSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private int idleFramesPerDirection = 6;
        [SerializeField] private float frameRate = 8f;

        private int direction;
        private float frameTimer;
        private int frame;

        public void SetDirection(Vector2 worldDirection)
        {
            if (worldDirection.sqrMagnitude < 0.001f) return;

            if (Mathf.Abs(worldDirection.x) > Mathf.Abs(worldDirection.y))
                direction = worldDirection.x >= 0f ? 1 : 3;
            else
                direction = worldDirection.y >= 0f ? 2 : 0;
        }

        private void Update()
        {
            if (target == null || frames == null || frames.Length == 0) return;

            frameTimer += Time.deltaTime;
            if (frameTimer >= 1f / Mathf.Max(1f, frameRate))
            {
                frameTimer = 0f;
                frame = (frame + 1) % Mathf.Max(1, idleFramesPerDirection);
            }

            int index = direction * idleFramesPerDirection + frame;
            if (index >= 0 && index < frames.Length)
                target.sprite = frames[index];
        }
    }
}
