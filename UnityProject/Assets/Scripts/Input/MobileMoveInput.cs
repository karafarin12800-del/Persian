using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class MobileMoveInput : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private float deadZone = 0.12f;

        private void Update()
        {
            Vector2 value = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (value.magnitude < deadZone) value = Vector2.zero;
            if (value.sqrMagnitude > 1f) value.Normalize();
            if (player != null) player.SetMoveInput(value);
        }
    }
}
