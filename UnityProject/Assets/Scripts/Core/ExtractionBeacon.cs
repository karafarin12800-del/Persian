using UnityEngine;
using UnityEngine.Rendering;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Creates a distant blue extraction beam only after the final wave is cleared.
    /// The player must search the map and enter the beam to finish the mission.
    /// </summary>
    public sealed class ExtractionBeacon : MonoBehaviour
    {
        private static ExtractionBeacon instance;

        private Transform player;
        private Vector3 groundPosition;
        private const float ExtractionRadius = 3.2f;
        private const float MinimumPlayerDistance = 40f;
        private const float MaximumSearchDistance = 75f;
        private const float WorldLimit = 104f;
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

            // Keep the extraction point at least 40 world units (approximately metres)
            // away. Search farther rings and more directions so the player must explore.
            Vector3 farthestValidCandidate = origin;
            float farthestDistanceSqr = -1f;

            for (float distance = MinimumPlayerDistance;
                 distance <= MaximumSearchDistance;
                 distance += 5f)
            {
                for (int directionIndex = 0; directionIndex < 16; directionIndex++)
                {
                    Vector3 direction = Quaternion.Euler(
                        0f, directionIndex * 22.5f, 0f) * forward;
                    Vector3 candidate = origin + direction * distance;
                    candidate.x = Mathf.Clamp(candidate.x, -WorldLimit, WorldLimit);
                    candidate.z = Mathf.Clamp(candidate.z, -WorldLimit, WorldLimit);
                    candidate.y = 0f;

                    float actualDistanceSqr = HorizontalDistanceSqr(origin, candidate);
                    // Clamping near the edge of the map can shorten a candidate;
                    // reject it unless the actual distance is still at least 40.
                    if (actualDistanceSqr < MinimumPlayerDistance * MinimumPlayerDistance)
                        continue;

                    if (actualDistanceSqr > farthestDistanceSqr)
                    {
                        farthestDistanceSqr = actualDistanceSqr;
                        farthestValidCandidate = candidate;
                    }

                    if (IsClearGround(candidate, playerTransform))
                    {
                        Debug.Log("PERSIA_EXTRACTION: selected beacon distance " +
                                  Mathf.Sqrt(actualDistanceSqr).ToString("F1") + "m from player.");
                        return candidate;
                    }
                }
            }

            // If the usual search rings are obstructed, scan the rest of the map
            // before falling back. Never intentionally place the beacon within 40 units.
            for (float distance = MaximumSearchDistance + 5f;
                 distance <= WorldLimit * 1.8f;
                 distance += 5f)
            {
                for (int directionIndex = 0; directionIndex < 32; directionIndex++)
                {
                    Vector3 direction = Quaternion.Euler(
                        0f, directionIndex * 11.25f, 0f) * forward;
                    Vector3 candidate = origin + direction * distance;
                    candidate.x = Mathf.Clamp(candidate.x, -WorldLimit, WorldLimit);
                    candidate.z = Mathf.Clamp(candidate.z, -WorldLimit, WorldLimit);
                    candidate.y = 0f;

                    float actualDistanceSqr = HorizontalDistanceSqr(origin, candidate);
                    if (actualDistanceSqr < MinimumPlayerDistance * MinimumPlayerDistance)
                        continue;

                    if (actualDistanceSqr > farthestDistanceSqr)
                    {
                        farthestDistanceSqr = actualDistanceSqr;
                        farthestValidCandidate = candidate;
                    }

                    if (IsClearGround(candidate, playerTransform))
                    {
                        Debug.Log("PERSIA_EXTRACTION: selected distant fallback beacon " +
                                  Mathf.Sqrt(actualDistanceSqr).ToString("F1") + "m from player.");
                        return candidate;
                    }
                }
            }

            // Extremely crowded map fallback: distance is prioritized over perfect clearance.
            Debug.LogWarning("PERSIA_EXTRACTION: no clear beacon site found; using farthest valid site at " +
                             Mathf.Sqrt(Mathf.Max(0f, farthestDistanceSqr)).ToString("F1") + "m.");
            return farthestValidCandidate;
        }

        private static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
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
