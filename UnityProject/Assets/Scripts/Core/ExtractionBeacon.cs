using UnityEngine;
using UnityEngine.Rendering;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Creates a nearby blue extraction beam only after the final wave is cleared.
    /// The player must enter the beam to finish the mission.
    /// </summary>
    public sealed class ExtractionBeacon : MonoBehaviour
    {
        private static ExtractionBeacon instance;

        private Transform player;
        private Vector3 groundPosition;
        private const float ExtractionRadius = 3.2f;
        private bool beamCreated;
        private bool completed;

        public static bool IsActive =>
            instance != null && instance.beamCreated && !instance.completed;

        public static Vector3 WorldPosition =>
            instance != null ? instance.groundPosition : Vector3.zero;

        public static void ActivateForVictory(PlayerController playerController)
        {
            if (playerController == null || playerController.IsDefeated)
            {
                if (GameSession.Instance != null)
                    GameSession.Instance.EndMission(false);
                return;
            }

            if (instance == null)
            {
                GameObject host = new GameObject("ExtractionBeaconController");
                instance = host.AddComponent<ExtractionBeacon>();
            }

            instance.ActivateBeam(playerController);
        }

        private void ActivateBeam(PlayerController playerController)
        {
            if (beamCreated || completed)
                return;

            player = playerController.transform;
            groundPosition = FindClearGroundNearPlayer(player);
            BuildBeam();
            beamCreated = true;

            Debug.Log("PERSIA_EXTRACTION: final wave cleared; blue extraction beam activated at " + groundPosition);
        }

        private static Vector3 FindClearGroundNearPlayer(Transform playerTransform)
        {
            Vector3 origin = playerTransform.position;
            origin.y = 0f;

            Vector3 forward = playerTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            // Search a short radius in front of the player first, then around them.
            // This avoids asking the player to cross the whole map to find extraction.
            for (float distance = 8f; distance <= 17f; distance += 3f)
            {
                for (int directionIndex = 0; directionIndex < 8; directionIndex++)
                {
                    Vector3 direction = Quaternion.Euler(0f, directionIndex * 45f, 0f) * forward;
                    Vector3 candidate = origin + direction * distance;
                    candidate.x = Mathf.Clamp(candidate.x, -88f, 88f);
                    candidate.z = Mathf.Clamp(candidate.z, -88f, 88f);
                    candidate.y = 0f;

                    if (IsClearGround(candidate, playerTransform))
                        return candidate;
                }
            }

            // The player controller's own movement remains available even if a
            // crowded scene leaves no completely clear candidate.
            Vector3 fallback = origin + forward * 7f;
            fallback.x = Mathf.Clamp(fallback.x, -88f, 88f);
            fallback.z = Mathf.Clamp(fallback.z, -88f, 88f);
            fallback.y = 0f;
            return fallback;
        }

        private static bool IsClearGround(Vector3 position, Transform playerTransform)
        {
            Collider[] overlaps = Physics.OverlapSphere(
                position + Vector3.up * 1.1f,
                2.25f,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null || collider.isTrigger)
                    continue;

                // Ignore the battlefield floor mesh, the player, and transient
                // actors/pickups. Solid building and vehicle colliders still count.
                if (collider is MeshCollider || collider is TerrainCollider)
                    continue;
                if (collider.transform == playerTransform ||
                    collider.transform.IsChildOf(playerTransform))
                    continue;
                if (collider.GetComponentInParent<PlayerController>() != null ||
                    collider.GetComponentInParent<EnemyChase>() != null ||
                    collider.GetComponentInParent<PickupItem>() != null)
                    continue;

                return false;
            }

            return true;
        }

        private void BuildBeam()
        {
            CreateCylinder(
                "BlueExtractionBeamOuter",
                groundPosition + Vector3.up * 18f,
                new Vector3(5.4f, 18f, 5.4f),
                new Color(0.08f, 0.42f, 1f, 0.27f),
                true);

            CreateCylinder(
                "BlueExtractionBeamCore",
                groundPosition + Vector3.up * 18f,
                new Vector3(2.3f, 18f, 2.3f),
                new Color(0.20f, 0.72f, 1f, 0.46f),
                true);

            CreateCylinder(
                "BlueExtractionGroundRing",
                groundPosition + Vector3.up * 0.045f,
                new Vector3(6.6f, 0.045f, 6.6f),
                new Color(0.10f, 0.56f, 1f, 0.96f),
                false);
        }

        private static void CreateCylinder(
            string objectName,
            Vector3 position,
            Vector3 scale,
            Color color,
            bool transparent)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = objectName;
            cylinder.transform.position = position;
            cylinder.transform.localScale = scale;

            Collider collider = cylinder.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = cylinder.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Shader shader = transparent
                ? (Resources.Load<Shader>("PersiaWarAndroidFade") ??
                   Shader.Find("PersiaWar/AndroidFade") ??
                   Shader.Find("Unlit/Transparent") ??
                   Shader.Find("Sprites/Default"))
                : (Resources.Load<Shader>("PersiaWarAndroidFlat") ??
                   Shader.Find("PersiaWar/AndroidFlat") ??
                   Shader.Find("Unlit/Color"));

            if (shader == null)
            {
                Debug.LogError("PERSIA_EXTRACTION: no supported shader for " + objectName);
                Destroy(cylinder);
                return;
            }

            Material material = new Material(shader);
            material.name = objectName + "Material";
            material.color = color;

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_FadeAlpha"))
                material.SetFloat("_FadeAlpha", 1f);

            if (transparent)
                material.renderQueue = (int)RenderQueue.Transparent;

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void Update()
        {
            if (!beamCreated || completed || player == null)
                return;

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null || controller.IsDefeated)
                return;

            if (GameSession.Instance != null && GameSession.Instance.IsFinished)
                return;

            Vector2 playerXZ = new Vector2(player.position.x, player.position.z);
            Vector2 beaconXZ = new Vector2(groundPosition.x, groundPosition.z);
            if ((playerXZ - beaconXZ).sqrMagnitude > ExtractionRadius * ExtractionRadius)
                return;

            completed = true;
            Debug.Log("PERSIA_EXTRACTION: player entered blue beam; mission complete.");
            if (GameSession.Instance != null)
                GameSession.Instance.EndMission(true);
            else
                Time.timeScale = 0f;
        }
    }
}
