using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class AmmoPickup : MonoBehaviour
    {
        [SerializeField] private int amount = 12;
        [SerializeField] private float rotateSpeed = 80f;
        public int Amount => amount;

        private void Update()
        {
            transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            PickupReceiver receiver = other.GetComponentInParent<PickupReceiver>();
            if (receiver != null)
            {
                receiver.AddAmmo(amount);
                Destroy(gameObject);
            }
        }
    }

    public sealed class PickupReceiver : MonoBehaviour
    {
        [SerializeField] private int ammo;
        public int Ammo => ammo;

        public void AddAmmo(int amount)
        {
            ammo = Mathf.Max(0, ammo + amount);
        }
    }
}
