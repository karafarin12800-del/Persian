using UnityEngine;
using UnityEngine.Rendering;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Applies a low-cost lighting profile to the existing battlefield.
    /// It deliberately adds no renderers, materials, or per-frame work.
    /// </summary>
    public static class EnvironmentLightingProfile
    {
        private const string SunObjectName = "PersiaWarEnvironmentSun";
        private const string FillObjectName = "PersiaWarEnvironmentFill";
        private const string RimObjectName = "PersiaWarEnvironmentRim";

        public static void Apply(Transform owner, bool warmVillage)
        {
            if (owner == null)
                return;

            Transform sunTransform = owner.Find(SunObjectName);
            if (sunTransform == null)
            {
                GameObject sunObject = new GameObject(SunObjectName);
                sunTransform = sunObject.transform;
                sunTransform.SetParent(owner, false);
            }

            Light sun = sunTransform.GetComponent<Light>();
            if (sun == null)
                sun = sunTransform.gameObject.AddComponent<Light>();

            sun.type = LightType.Directional;
            sunTransform.rotation = Quaternion.Euler(48f, -32f, 0f);
            sun.color = warmVillage
                ? new Color(1f, 0.84f, 0.68f)
                : new Color(1f, 0.94f, 0.82f);
            sun.intensity = warmVillage ? 1.0f : 1.1f;
            sun.shadows = QualitySettings.shadows == ShadowQuality.All
                ? LightShadows.Soft
                : (QualitySettings.shadows == ShadowQuality.HardOnly ? LightShadows.Hard : LightShadows.None);
            sun.shadowStrength = QualitySettings.shadows == ShadowQuality.All ? 0.82f : 0.68f;
            sun.renderMode = LightRenderMode.ForcePixel;

            // Keep one shadow-casting key light; the two additional lights are
            // shadowless fill/rim sources and are restored immediately below.
            Light[] sceneLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < sceneLights.Length; i++)
            {
                Light other = sceneLights[i];
                if (other != null && other != sun && other.type == LightType.Directional)
                    other.enabled = false;
            }
            RenderSettings.sun = sun;
            AddFillLights(owner, warmVillage);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = warmVillage
                ? new Color(0.46f, 0.52f, 0.62f)
                : new Color(0.52f, 0.62f, 0.76f);
            RenderSettings.ambientEquatorColor = warmVillage
                ? new Color(0.48f, 0.40f, 0.31f)
                : new Color(0.48f, 0.50f, 0.48f);
            RenderSettings.ambientGroundColor = warmVillage
                ? new Color(0.22f, 0.19f, 0.16f)
                : new Color(0.20f, 0.23f, 0.20f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.55f;
        }
        public static void AddFillLights(Transform owner, bool warmVillage)
        {
            Light fill = EnsureDirectional(owner, FillObjectName);
            fill.transform.rotation = Quaternion.Euler(22f, 138f, 0f);
            fill.color = warmVillage ? new Color(0.55f, 0.72f, 1f) : new Color(0.50f, 0.70f, 1f);
            fill.intensity = 0.46f;
            fill.shadows = LightShadows.None;
            fill.renderMode = LightRenderMode.ForcePixel;
            fill.enabled = true;

            Light rim = EnsureDirectional(owner, RimObjectName);
            rim.transform.rotation = Quaternion.Euler(16f, 32f, 0f);
            rim.color = warmVillage ? new Color(1f, 0.72f, 0.48f) : new Color(0.72f, 0.84f, 1f);
            rim.intensity = 0.30f;
            rim.shadows = LightShadows.None;
            rim.renderMode = LightRenderMode.ForcePixel;
            rim.enabled = true;
        }

        private static Light EnsureDirectional(Transform owner, string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                go = new GameObject(objectName);
                if (owner != null)
                    go.transform.SetParent(owner, false);
            }

            Light light = go.GetComponent<Light>();
            if (light == null)
                light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = ~0;
            return light;
        }
    }
}
