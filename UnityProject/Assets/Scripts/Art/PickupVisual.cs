using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>Cheap spin + bob for pickup models (no allocations per frame).</summary>
    public sealed class ModelSpinner : MonoBehaviour
    {
        public float spinDegreesPerSecond = 70f;
        public float bobHeight = 0.08f;
        public float bobSpeed = 2.2f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private float phase;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            phase = Random.value * 6.2831853f;
        }

        private void Update()
        {
            float t = Time.time;
            transform.localPosition = baseLocalPosition +
                new Vector3(0f, Mathf.Sin(t * bobSpeed + phase) * bobHeight, 0f);
            transform.localRotation = Quaternion.AngleAxis(t * spinDegreesPerSecond + phase * 57.29578f,
                Vector3.up) * baseLocalRotation;
        }
    }

    /// <summary>
    /// Gives an existing pickup object (Stage 1 loot, Stage 2 loot, enemy drops) the real
    /// shared model plus a coloured ground ring. The old primitive renderers are only
    /// disabled, so colliders / PickupItem logic keep working unchanged.
    /// </summary>
    public static class PickupVisual
    {
        private static readonly RaycastHit[] groundHits = new RaycastHit[8];

        public static Color32 RingColor(ModelKind kind)
        {
            switch (kind)
            {
                case ModelKind.Pistol:
                case ModelKind.AssaultRifle:
                case ModelKind.HeavyMachineGun:
                case ModelKind.Axe:
                    return new Color32(255, 200, 70, 255);
                case ModelKind.VoidRifle:
                case ModelKind.AlienSword:
                    return new Color32(60, 240, 215, 255);
                case ModelKind.ShieldPickup:
                    return new Color32(80, 170, 255, 255);
                case ModelKind.Grenade:
                    return new Color32(255, 120, 50, 255);
                case ModelKind.AmmoCrate:
                    return new Color32(255, 220, 90, 255);
                case ModelKind.Medkit:
                    return new Color32(255, 80, 80, 255);
                case ModelKind.QuickDash:
                    return new Color32(255, 70, 220, 255);
                default:
                    return new Color32(255, 255, 255, 255);
            }
        }

        /// <summary>
        /// kind None -> resolved from the pickup object's name. Returns the model object.
        /// </summary>
        public static GameObject Attach(Transform pickupRoot, ModelKind kind, bool hideOldVisuals)
        {
            if (pickupRoot == null) return null;
            if (kind == ModelKind.None) kind = ModelLibrary.KindFromName(pickupRoot.name);
            if (kind == ModelKind.None) return null;

            if (hideOldVisuals)
            {
                Renderer[] old = pickupRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < old.Length; i++)
                    if (old[i] != null) old[i].enabled = false;
            }

            bool isWeapon = ModelLibrary.IsWeapon(kind);
            Vector3 modelOffset = isWeapon ? pickupRoot.InverseTransformVector(Vector3.up * 0.62f) : Vector3.zero;
            GameObject model = ModelLibrary.Spawn(kind, pickupRoot, modelOffset, Quaternion.identity,
                ModelLibrary.PickupScale(kind));
            if (model == null) return null;
            ModelSpinner spinner = model.AddComponent<ModelSpinner>();
            spinner.spinDegreesPerSecond = isWeapon ? 55f : 80f;
            spinner.bobHeight = isWeapon ? 0.045f : 0.08f;

            // Ground ring: a separate tiny mesh coloured by pickup type.
            GameObject ring = new GameObject("PW_PickupRing");
            ring.transform.SetParent(pickupRoot, false);
            Vector3 world = pickupRoot.position;
            ring.transform.position = new Vector3(world.x, GroundY(world) + 0.03f, world.z);
            MeshFilter filter = ring.AddComponent<MeshFilter>();
            filter.sharedMesh = ModelLibrary.GetRingMesh(RingColor(kind));
            MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ModelLibrary.GetMaterial();
            ModelLibrary.ConfigureRenderer(renderer);
            PickupGroundRingFollower follower = ring.AddComponent<PickupGroundRingFollower>();
            follower.Configure(pickupRoot, GroundY(world));
            return model;
        }

        public static float GroundY(Vector3 worldPosition)
        {
            int count = Physics.RaycastNonAlloc(new Ray(worldPosition + Vector3.up * 0.6f, Vector3.down),
                groundHits, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float y = 0f;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider c = groundHits[i].collider;
                if (c == null || groundHits[i].normal.y < 0.5f) continue;
                if (c.GetComponentInParent<TargetHealth>() != null) continue;
                if (groundHits[i].distance < best)
                {
                    best = groundHits[i].distance;
                    y = groundHits[i].point.y;
                    found = true;
                }
            }
            return found ? y : 0f;
        }
    }
}


namespace PersiaWar.Unity2D5D
{
    /// <summary>Keeps the pickup locator on the ground while the weapon model floats above it.</summary>
    public sealed class PickupGroundRingFollower : MonoBehaviour
    {
        private Transform pickupRoot;
        private float groundY;

        public void Configure(Transform owner, float terrainHeight)
        {
            pickupRoot = owner;
            groundY = terrainHeight + 0.03f;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (pickupRoot == null) return;
            transform.position = new Vector3(pickupRoot.position.x, groundY, pickupRoot.position.z);
        }
    }
}
