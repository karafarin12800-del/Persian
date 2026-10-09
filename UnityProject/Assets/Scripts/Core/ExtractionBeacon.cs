using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Spawns a blue extraction beam after the final living enemy is defeated.
    /// The player must physically reach the beam before the mission is marked complete.
    /// </summary>
    public sealed class ExtractionBeacon : MonoBehaviour
    {
        private static ExtractionBeacon instance;
        private Transform player;
        private Vector3 groundPosition;
        private float extractionRadius = 2.8f;
        private bool activated;

        public static void NotifyEnemyDefeated(EnemyChase defeatedEnemy)
        {
            if (instance != null || defeatedEnemy == null)
                return;

            GameObject host = new GameObject("ExtractionBeaconController");
            instance = host.AddComponent<ExtractionBeacon>();
            instance.StartCoroutine(instance.CheckForLastEnemy(defeatedEnemy));
        }

        private IEnumerator CheckForLastEnemy(EnemyChase defeatedEnemy)
        {
            // Destroy() is deferred until the end of the frame. Wait briefly so the
            // dead enemy is no longer counted and any queued spawn can be observed.
            yield return new WaitForSeconds(0.25f);

            EnemyChase[] enemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyChase enemy = enemies[i];
                if (enemy == null || enemy == defeatedEnemy || !enemy.gameObject.activeInHierarchy)
                    continue;

                TargetHealth health = enemy.GetComponent<TargetHealth>();
                if (health == null || health.CurrentHealth > 0)
                    yield break;
            }

            PlayerController playerController = FindFirstObjectByType<PlayerController>();
            if (playerController == null || playerController.IsDefeated)
                yield break;

            player = playerController.transform;
            // Clear open area near the outer road, away from the planned building footprints.
            groundPosition = new Vector3(0f, 0f, 88f);
            BuildBeam();
            Debug.Log("PERSIA_EXTRACTION: final enemy defeated; blue extraction beam activated.");
        }

        private void BuildBeam()
        {
            CreateCylinder(
                "BlueExtractionBeamOuter",
                groundPosition + Vector3.up * 18f,
                new Vector3(5.4f, 18f, 5.4f),
                new Color(0.08f, 0.42f, 1f, 0.22f),
                true);

            CreateCylinder(
                "BlueExtractionBeamCore",
                groundPosition + Vector3.up * 18f,
                new Vector3(2.3f, 18f, 2.3f),
                new Color(0.20f, 0.72f, 1f, 0.24f),
                true);

            CreateCylinder(
                "BlueExtractionGroundRing",
                groundPosition + Vector3.up * 0.045f,
                new Vector3(6.6f, 0.045f, 6.6f),
                new Color(0.10f, 0.56f, 1f, 0.78f),
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

            Shader shader = transparent ? Shader.Find("Unlit/Transparent") : Shader.Find("PersiaWar/AndroidFlat");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                return;

            Material material = new Material(shader);
            material.name = objectName + "Material";
            material.color = color;
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (transparent)
            {
                material.renderQueue = (int)RenderQueue.Transparent;
                if (material.HasProperty("_SrcBlend"))
                    material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend"))
                    material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite"))
                    material.SetInt("_ZWrite", 0);
            }

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void Update()
        {
            if (activated || player == null || player.gameObject == null)
                return;

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null || controller.IsDefeated)
                return;

            Vector2 playerXZ = new Vector2(player.position.x, player.position.z);
            Vector2 beaconXZ = new Vector2(groundPosition.x, groundPosition.z);
            if ((playerXZ - beaconXZ).sqrMagnitude > extractionRadius * extractionRadius)
                return;

            activated = true;
            Debug.Log("PERSIA_EXTRACTION: player entered blue beam; mission complete.");
            if (GameSession.Instance != null)
                GameSession.Instance.EndMission(true);
            else
                Time.timeScale = 0f;
        }
    }
}
