using UnityEngine;
using UnityEngine.Rendering;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Presentation-only layer for the existing 2.5D gameplay.
    /// It upgrades lighting, shadows, materials and camera rendering without
    /// changing movement, combat, targeting, pickups or game rules.
    /// </summary>
    public static class ThreeDPresentationLayer
    {
        private static bool configured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ConfigureScene()
        {
            if (configured)
                return;

            configured = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowDistance = 140f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.masterTextureLimit = 0;

            ConfigureWorldLighting();
            ConfigureMainCamera();
            UpgradeSceneRenderers();
        }

        private static void ConfigureWorldLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.34f, 0.40f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.23f, 0.25f);
            RenderSettings.ambientGroundColor = new Color(0.09f, 0.10f, 0.09f);
            RenderSettings.reflectionIntensity = 0.55f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.18f, 0.21f, 0.24f);
            RenderSettings.fogStartDistance = 95f;
            RenderSettings.fogEndDistance = 220f;

            Light sun = FindDirectionalLight();
            if (sun == null)
            {
                GameObject sunObject = new GameObject("PresentationSun");
                sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1.0f, 0.93f, 0.82f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.045f;
            sun.shadowNormalBias = 0.28f;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private static void ConfigureMainCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            camera.orthographic = false;
            camera.fieldOfView = Mathf.Clamp(camera.fieldOfView, 48f, 54f);
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 240f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.depthTextureMode |= DepthTextureMode.Depth;
        }

        private static void UpgradeSceneRenderers()
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.gameObject.name.StartsWith("Minimap"))
                    continue;

                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;

                Material material = renderer.sharedMaterial;
                if (material == null || material.shader == null)
                    continue;

                if (material.HasProperty("_Glossiness"))
                    material.SetFloat("_Glossiness", MaterialGlossiness(renderer.gameObject.name));

                if (material.HasProperty("_Metallic"))
                    material.SetFloat("_Metallic", MaterialMetallic(renderer.gameObject.name));

                if (material.HasProperty("_BumpScale"))
                    material.SetFloat("_BumpScale", 0.35f);
            }
        }

        private static float MaterialGlossiness(string objectName)
        {
            string name = objectName.ToLowerInvariant();
            if (name.Contains("road")) return 0.18f;
            if (name.Contains("roof")) return 0.28f;
            if (name.Contains("vehicle")) return 0.55f;
            if (name.Contains("window")) return 0.70f;
            if (name.Contains("door")) return 0.32f;
            if (name.Contains("tree")) return 0.05f;
            return 0.24f;
        }

        private static float MaterialMetallic(string objectName)
        {
            string name = objectName.ToLowerInvariant();
            if (name.Contains("vehicle")) return 0.45f;
            if (name.Contains("road")) return 0.02f;
            if (name.Contains("window")) return 0.08f;
            return 0.02f;
        }

        private static Light FindDirectionalLight()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                    return lights[i];
            }

            return null;
        }
    }
}
