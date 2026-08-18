using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private float worldSize = 1056f;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (gameplayCamera != null)
            {
                gameplayCamera.orthographic = false;
                gameplayCamera.transform.position = new Vector3(0f, 14f, -14f);
                gameplayCamera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
                gameplayCamera.farClipPlane = 2000f;
            }

            CreateRuntimeGround();
        }

        private void CreateRuntimeGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "RuntimeGround";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = Vector3.one * (worldSize / 10f);

            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader != null)
                {
                    Material material = new Material(shader)
                    {
                        name = "RuntimeGroundMaterial"
                    };
                    material.color = new Color(0.16f, 0.20f, 0.15f, 1f);
                    renderer.sharedMaterial = material;
                }
            }
        }
    }
}
