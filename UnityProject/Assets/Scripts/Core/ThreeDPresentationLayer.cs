using UnityEngine;
using UnityEngine.Rendering;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Presentation-only layer for the existing 2.5D gameplay.
    /// Mobile uses a conservative rendering profile so the presentation layer
    /// cannot re-enable expensive/fragile features after GameBootstrap configures them.
    /// </summary>
    public static class ThreeDPresentationLayer
    {
        private static bool configured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ConfigureScene()
        {
            if (configured) return;
            configured = true;

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

#if UNITY_ANDROID
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowDistance = 0f;
            QualitySettings.shadowCascades = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
#else
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowDistance = 105f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
#endif
            QualitySettings.masterTextureLimit = 0;

            ConfigureWorldLighting();
            ConfigureMainCamera();
            UpgradeSceneRenderers();
        }

        private static void ConfigureWorldLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.82f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.64f, 0.78f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.42f, 0.25f);
            RenderSettings.reflectionIntensity = 0.60f;

            RenderSettings.fog = false;

            Light sun = FindDirectionalLight();
            if (sun == null)
            {
                GameObject sunObject = new GameObject("PresentationSun");
                sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.type = LightType.Directional;
            sun.intensity = 1.65f;
            sun.color = new Color(1f, 0.98f, 0.90f);

#if UNITY_ANDROID
            sun.shadows = LightShadows.None;
            sun.shadowStrength = 0f;
#else
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.58f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.22f;
#endif
            sun.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
        }

        private static void ConfigureMainCamera()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 240f;
            camera.allowHDR = false;

#if UNITY_ANDROID
            camera.allowMSAA = false;
#else
            camera.allowMSAA = true;
#endif
            camera.depthTextureMode = DepthTextureMode.None;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.48f, 0.72f, 0.90f, 1f);
        }

        private static void UpgradeSceneRenderers()
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.gameObject.name.StartsWith("Minimap"))
                    continue;

#if !UNITY_ANDROID
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
#endif

                Material material = renderer.sharedMaterial;
                if (material == null || material.shader == null) continue;

                if (material.HasProperty("_Glossiness"))
                    material.SetFloat("_Glossiness", MaterialGlossiness(renderer.gameObject.name));
                if (material.HasProperty("_Metallic"))
                    material.SetFloat("_Metallic", MaterialMetallic(renderer.gameObject.name));
            }
        }

        private static float MaterialGlossiness(string objectName)
        {
            string name = objectName.ToLowerInvariant();
            if (name.Contains("road")) return 0.08f;
            if (name.Contains("roof")) return 0.18f;
            if (name.Contains("vehicle")) return 0.32f;
            if (name.Contains("window")) return 0.45f;
            if (name.Contains("tree")) return 0.03f;
            return 0.16f;
        }

        private static float MaterialMetallic(string objectName)
        {
            string name = objectName.ToLowerInvariant();
            if (name.Contains("vehicle")) return 0.18f;
            if (name.Contains("road")) return 0.01f;
            return 0.01f;
        }

        private static Light FindDirectionalLight()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null && lights[i].type == LightType.Directional)
                    return lights[i];
            return null;
        }
    }
}
