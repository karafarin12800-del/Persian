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
            sun.shadows = !Application.isMobilePlatform &&
                          QualitySettings.shadows != ShadowQuality.Disable
                ? LightShadows.Hard
                : LightShadows.None;
            sun.shadowStrength = 0.65f;
            sun.renderMode = LightRenderMode.Auto;
            RenderSettings.sun = sun;

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
    }
}
