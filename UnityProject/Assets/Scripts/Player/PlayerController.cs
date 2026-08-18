using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;

        private Vector3 input;

        public void SetMoveInput(Vector2 value)
        {
            input = new Vector3(value.x, 0f, value.y);
            if (input.sqrMagnitude > 1f)
                input.Normalize();
        }

        private void Update()
        {
            transform.position += input * moveSpeed * Time.deltaTime;
        }
    }
}
