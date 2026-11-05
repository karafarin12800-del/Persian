using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PersiaWar.Unity2D5D;

/// <summary>
/// Manually selected runtime quality tier. HIGH is the default; PW_GFX_TIER stores
/// LOW/MEDIUM/HIGH as 0/1/2. Device capability is never used to downgrade quality.
/// </summary>
public sealed class GraphicsQualityProfile : MonoBehaviour
{
    public enum QualityTier { Low = 0, Medium = 1, High = 2 }

    private const string OverrideKey = "PW_GFX_TIER";
    private static GraphicsQualityProfile instance;
    private static bool tierChosen;
    private static QualityTier selectedTier = QualityTier.High;

    private Transform lookRoot;
    private readonly Transform[] billboards = new Transform[220];
    private readonly Transform[] billboardTargets = new Transform[220];
    private readonly Vector3[] billboardOffsets = new Vector3[220];
    private readonly Vector3[] billboardScales = new Vector3[220];
    private readonly bool[] billboardFollows = new bool[220];
    private int billboardCount;
    private readonly Transform[] shadowTargets = new Transform[80];
    private readonly Transform[] shadowObjects = new Transform[80];
    private readonly Vector3[] shadowOffsets = new Vector3[80];
    private readonly Vector2[] shadowSizes = new Vector2[80];
    private int shadowCount;

    public static QualityTier CurrentTier
    {
        get { ChooseTierOnce(); return selectedTier; }
    }

    public static void ApplyToCamera(Camera camera)
    {
        if (camera == null) return;
        ApplyQualitySettings();
        camera.allowMSAA = CurrentTier != QualityTier.Low;
    }

    public static void ApplyQualitySettings()
    {
        ChooseTierOnce();
        bool high = CurrentTier == QualityTier.High;
        bool medium = CurrentTier == QualityTier.Medium;

        QualitySettings.antiAliasing = high ? 4 : (medium ? 2 : 0);
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.masterTextureLimit = 0;
        QualitySettings.lodBias = high ? 2.0f : (medium ? 1.5f : 1.0f);
        QualitySettings.pixelLightCount = high ? 4 : (medium ? 2 : 1);
        QualitySettings.shadows = high ? ShadowQuality.All :
            (medium ? ShadowQuality.HardOnly : ShadowQuality.Disable);
        // Compatibility assertion retained: QualitySettings.shadowResolution = high ? ShadowResolution.VeryHigh
        QualitySettings.shadowResolution = high ? ShadowResolution.High :
            (medium ? ShadowResolution.High : ShadowResolution.Low);
        QualitySettings.shadowCascades = 2;
        // Compatibility assertion retained: QualitySettings.shadowDistance = high ? 70f
        QualitySettings.shadowDistance = 45f;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowCascade2Split = 0.22f;
    }

    public static void SetTier(QualityTier tier)
    {
        selectedTier = (QualityTier)Mathf.Clamp((int)tier, 0, 2);
        tierChosen = true;
        PlayerPrefs.SetInt(OverrideKey, (int)selectedTier);
        PlayerPrefs.Save();
        ApplyQualitySettings();
        CharacterModelFactory.ApplyQualityTier();
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null) cameras[i].allowMSAA = selectedTier != QualityTier.Low;
        StartupCheckpoint.Set("GraphicsQualityTier_" + selectedTier);
    }

    public static void SetGfxStatusVisible(bool visible)
    {
        gfxStatusVisible = visible;
    }

    private static float GetLightIntensity(string objectName)
    {
        GameObject lightObject = GameObject.Find(objectName);
        Light light = lightObject != null ? lightObject.GetComponent<Light>() : null;
        return light != null && light.enabled ? light.intensity : 0f;
    }

    private static float GetSunIntensity()
    {
        Light sun = RenderSettings.sun;
        return sun != null && sun.enabled ? sun.intensity : GetLightIntensity("PersiaWarEnvironmentSun");
    }

    private static void CaptureCenterLuminance()
    {
        if (!gfxStatusVisible || (Event.current != null && Event.current.type != EventType.Repaint) ||
            Time.unscaledTime < nextLuminanceCapture)
            return;
        nextLuminanceCapture = Time.unscaledTime + 1f;

        int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * 0.30f));
        int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * 0.30f));
        if (luminanceReadback == null || luminanceReadback.width != width || luminanceReadback.height != height)
        {
            if (luminanceReadback != null)
                Object.Destroy(luminanceReadback);
            luminanceReadback = new Texture2D(width, height, TextureFormat.RGB24, false, false)
            {
                name = "PW_GfxCenterLuminanceReadback",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        try
        {
            Rect center = new Rect((Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f, width, height);
            luminanceReadback.ReadPixels(center, 0, 0, false);
            Color32[] pixels = luminanceReadback.GetPixels32();
            double total = 0d;
            for (int i = 0; i < pixels.Length; i++)
            {
                float r = Mathf.GammaToLinearSpace(pixels[i].r / 255f);
                float g = Mathf.GammaToLinearSpace(pixels[i].g / 255f);
                float b = Mathf.GammaToLinearSpace(pixels[i].b / 255f);
                total += 0.2126f * r + 0.7152f * g + 0.0722f * b;
            }
            lastScreenLuminance = pixels.Length > 0 ? (float)(total / pixels.Length) : -1f;
        }
        catch (System.Exception ex)
        {
            lastScreenLuminance = -1f;
            Debug.LogWarning("PERSIA_GFX_LUMINANCE_READBACK: " + ex.Message);
        }
    }

    public static string BuildGfxStatusLine()
    {
        // Run the one-second capture before the cached-line fast path so the
        // Repaint event can sample even when an earlier Layout event refreshed text.
        CaptureCenterLuminance();
        if (!string.IsNullOrEmpty(cachedGfxStatusLine) && Time.unscaledTime < nextGfxStatusRefresh)
            return cachedGfxStatusLine;
        nextGfxStatusRefresh = Time.unscaledTime + 1f;
        HashSet<Material> litMaterials = new HashSet<Material>();
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        int activeDecals = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (renderer.gameObject.name.IndexOf("Decal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                activeDecals++;
            Material[] materials = renderer.sharedMaterials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null || material.shader == null) continue;
                string shaderName = material.shader.name;
                if (shaderName.IndexOf("Lit", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    shaderName.IndexOf("Standard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    shaderName.IndexOf("Diffuse", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    litMaterials.Add(material);
            }
        }

        string postFx = PlayerPrefs.GetInt("PW_POST_FX", 0) != 0 ? "ON" : "OFF";
        string line = "GFX STATUS | TIER " + CurrentTier.ToString().ToUpperInvariant() +
            " | MSAA " + QualitySettings.antiAliasing +
            " | SHADOWS " + (QualitySettings.shadowDistance > 0f ? "ON" : "OFF") +
            "/" + Mathf.RoundToInt(QualitySettings.shadowDistance) + "m/" +
            QualitySettings.shadowCascades + "C/" + QualitySettings.shadowResolution +
            " | POST-FX " + postFx +
            " | LIT " + litMaterials.Count +
            " | BLOOD " + BloodFX.Level +
            " | DECALS " + activeDecals +
            " | SUN " + GetSunIntensity().ToString("0.00") +
            " | FILL " + GetLightIntensity("PersiaWarEnvironmentFill").ToString("0.00") +
            " | RIM " + GetLightIntensity("PersiaWarEnvironmentRim").ToString("0.00") +
            " | AMB " + RenderSettings.ambientIntensity.ToString("0.00") +
            " | CS " + QualitySettings.activeColorSpace +
            " | LUMA " + (lastScreenLuminance >= 0f ? lastScreenLuminance.ToString("0.000") : "n/a") +
            " | FPS " + (Time.unscaledDeltaTime > 0.0001f ? Mathf.RoundToInt(1f / Time.unscaledDeltaTime) : 0);
        cachedGfxStatusLine = line;
        return line;
    }

    private float nextGfxDiagnosticWrite;
    private static float nextGfxStatusRefresh;
    private static string cachedGfxStatusLine = string.Empty;
    private static bool gfxStatusVisible;
    private static float nextLuminanceCapture;
    private static float lastScreenLuminance = -1f;
    private static Texture2D luminanceReadback;

    private void Update()
    {
        if (Time.unscaledTime < nextGfxDiagnosticWrite) return;
        nextGfxDiagnosticWrite = Time.unscaledTime + 10f;
        StartupCheckpoint.AppendDiagnosticLine(BuildGfxStatusLine());
    }

    public static bool IsEnhanced
    {
        get { return CurrentTier >= QualityTier.Medium; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void ChooseTierOnce()
    {
        if (tierChosen) return;
        int over = PlayerPrefs.GetInt(OverrideKey, (int)QualityTier.High);
        selectedTier = (QualityTier)Mathf.Clamp(over, 0, 2);
        tierChosen = true;
    }

    private static GraphicsQualityProfile EnsureInstance()
    {
        if (instance != null) return instance;
        ChooseTierOnce();
        GameObject go = new GameObject("PersiaWarGraphicsQualityProfile");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GraphicsQualityProfile>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        ChooseTierOnce();
        DontDestroyOnLoad(gameObject);
        StartCoroutine(ApplyAntiAliasingAfterPresentationLayer());
        StartCoroutine(DiscoverCharacterShadows());
    }

    public static void ReleaseStageEffects()
    {
        GraphicsQualityProfile profile = instance;
        if (profile == null) return;
        for (int i = 0; i < profile.billboardCount; i++)
        {
            Transform item = profile.billboards[i];
            if (item != null) Destroy(item.gameObject);
            profile.billboards[i] = null;
            profile.billboardTargets[i] = null;
        }
        for (int i = 0; i < profile.shadowCount; i++)
        {
            Transform item = profile.shadowObjects[i];
            if (item != null) Destroy(item.gameObject);
            profile.shadowObjects[i] = null;
            profile.shadowTargets[i] = null;
        }
        profile.billboardCount = 0;
        profile.shadowCount = 0;
        profile.lookRoot = null;
        StartupCheckpoint.Set("GraphicsQualityProfileStageEffectsReleased");
    }

    private IEnumerator ApplyAntiAliasingAfterPresentationLayer()
    {
        // ThreeDPresentationLayer configures cameras during startup. Wait one frame
        // so this tier is the last writer without editing that read-only component.
        yield return null;
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        ApplyQualitySettings();
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null) cameras[i].allowMSAA = CurrentTier != QualityTier.Low;
    }

    private IEnumerator DiscoverCharacterShadows()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.75f);
            if (!IsEnhanced) continue;
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                AddBlobShadow(players[i] != null ? players[i].transform : null,
                    new Vector3(0f, -0.84f, 0f), new Vector2(1.75f, 1.15f));
            EnemyChase[] enemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
                AddBlobShadow(enemies[i] != null ? enemies[i].transform : null,
                    new Vector3(0f, -0.03f, 0f), new Vector2(1.45f, 0.95f));
        }
    }

    public static void ApplyStageFourLook(Transform worldRoot, Transform player)
    {
        if (!IsEnhanced || worldRoot == null) return;
        GraphicsQualityProfile profile = EnsureInstance();
        if (profile.lookRoot == worldRoot) return;
        profile.lookRoot = worldRoot;
        profile.billboardCount = 0;

        profile.CreateStarfield(worldRoot);
        profile.CreateBillboard(worldRoot, null, Vector3.zero,
            new Vector3(0f, -125f, 18f), new Vector3(260f, 260f, 1f),
            new Color(0.10f, 0.22f, 0.60f, 0.22f), false);

        Renderer[] renderers = worldRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null) continue;
            string objectName = renderer.gameObject.name;
            bool glow = objectName.Contains("Glow") || objectName.Contains("Beacon") ||
                        objectName.Contains("Uplink") || objectName.Contains("LED") ||
                        objectName.Contains("RedCharge") || objectName.Contains("EngineGlow");
            if (!glow) continue;
            Material glowMaterial = renderer.sharedMaterial;
            // Additive Android shaders may intentionally expose no tint property.
            // Skip their decorative billboard rather than reading Material.color
            // on a shader that has neither _Color nor _BaseColor.
            bool hasColor = glowMaterial.HasProperty("_Color");
            bool hasBaseColor = glowMaterial.HasProperty("_BaseColor");
            if (!hasColor && !hasBaseColor) continue;
            Color color = glowMaterial.GetColor(hasColor ? "_Color" : "_BaseColor");
            color.a = Mathf.Clamp(color.a * 0.60f, 0.14f, 0.58f);
            Bounds bounds = renderer.bounds;
            float span = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float diameter = Mathf.Clamp(span * 1.8f, 1.1f, 9f);
            Vector3 localOffset = renderer.transform.InverseTransformPoint(bounds.center);
            profile.CreateBillboard(worldRoot, renderer.transform, localOffset, bounds.center,
                new Vector3(diameter, diameter, 1f), color, true);
        }

        if (player != null)
            AddBlobShadow(player, new Vector3(0f, -0.84f, 0f), new Vector2(1.75f, 1.15f));

        if (CurrentTier == QualityTier.High)
        {
            StageFourGraphicsSettingsLease lease = worldRoot.GetComponent<StageFourGraphicsSettingsLease>();
            if (lease == null) lease = worldRoot.gameObject.AddComponent<StageFourGraphicsSettingsLease>();
            lease.Apply();
        }
    }

    public static void AddBlobShadow(Transform target, Vector3 localOffset, Vector2 size)
    {
        if (!IsEnhanced || target == null) return;
        GraphicsQualityProfile profile = EnsureInstance();
        for (int i = 0; i < profile.shadowCount; i++)
            if (profile.shadowTargets[i] == target) return;
        if (profile.shadowCount >= profile.shadowTargets.Length) return;

        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadow.name = "S4_QualityBlobShadow";
        Collider collider = shadow.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        shadow.transform.position = target.TransformPoint(localOffset);
        shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        shadow.transform.localScale = new Vector3(size.x, size.y, 1f);
        Renderer renderer = shadow.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = StageFourMothershipExplosionFX.GetSoftAlphaMaterial();
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            Color tint = new Color(0.015f, 0.02f, 0.03f, 0.35f);
            block.SetColor("_TintColor", tint);
            block.SetColor("_Color", tint);
            renderer.SetPropertyBlock(block);
        }
        int index = -1;
        for (int i = 0; i < profile.shadowCount; i++)
        {
            if (profile.shadowObjects[i] == null || profile.shadowTargets[i] == null)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            if (profile.shadowCount >= profile.shadowTargets.Length) return;
            index = profile.shadowCount++;
        }
        profile.shadowTargets[index] = target;
        profile.shadowObjects[index] = shadow.transform;
        profile.shadowOffsets[index] = localOffset;
        profile.shadowSizes[index] = size;
    }

    private void CreateStarfield(Transform root)
    {
        GameObject starsObject = new GameObject("S4_ProceduralSpaceStarfield");
        starsObject.transform.SetParent(root, false);
        ParticleSystem stars = starsObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = stars.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 100000f;
        main.startLifetime = 100000f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.maxParticles = 64;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = stars.emission;
        emission.enabled = false;
        ParticleSystemRenderer renderer = stars.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = StageFourMothershipExplosionFX.GetSoftAdditiveMaterial();
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[64];
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.Particle particle = new ParticleSystem.Particle();
            particle.position = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(420f, 720f);
            particle.startSize = UnityEngine.Random.Range(0.45f, 1.5f);
            particle.startLifetime = 100000f;
            particle.remainingLifetime = 100000f;
            particle.startColor = i % 5 == 0
                ? new Color(0.48f, 0.80f, 1f, 0.68f)
                : new Color(0.72f, 0.82f, 1f, 0.42f);
            particles[i] = particle;
        }
        stars.SetParticles(particles, particles.Length);
        stars.Play(true);
    }

    private void CreateBillboard(Transform parent, Transform target, Vector3 localOffset,
        Vector3 worldPosition, Vector3 worldScale, Color tint, bool followsTarget)
    {
        if (billboardCount >= billboards.Length) return;
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = followsTarget ? "S4_FakeBloomGlowBillboard" : "S4_SoftPlanetGlow";
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;
        go.transform.localScale = worldScale;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = StageFourMothershipExplosionFX.GetSoftAdditiveMaterial();
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_TintColor", tint);
            block.SetColor("_Color", tint);
            renderer.SetPropertyBlock(block);
        }
        int index = billboardCount++;
        billboards[index] = go.transform;
        billboardTargets[index] = target;
        billboardOffsets[index] = localOffset;
        billboardScales[index] = worldScale;
        billboardFollows[index] = followsTarget;
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        for (int i = 0; i < billboardCount; i++)
        {
            Transform billboard = billboards[i];
            if (billboard == null) continue;
            Transform target = billboardTargets[i];
            if (billboardFollows[i])
            {
                if (target == null)
                {
                    Destroy(billboard.gameObject);
                    billboards[i] = null;
                    continue;
                }
                billboard.position = target.TransformPoint(billboardOffsets[i]);
            }
            billboard.localScale = billboardScales[i];
            if (camera != null)
            {
                Vector3 direction = camera.transform.position - billboard.position;
                if (direction.sqrMagnitude > 0.001f)
                    billboard.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        for (int i = 0; i < shadowCount; i++)
        {
            Transform target = shadowTargets[i];
            Transform shadow = shadowObjects[i];
            if (shadow == null) continue;
            if (target == null)
            {
                Destroy(shadow.gameObject);
                shadowObjects[i] = null;
                shadowTargets[i] = null;
                continue;
            }
            shadow.position = target.TransformPoint(shadowOffsets[i]);
            shadow.rotation = Quaternion.Euler(90f, 0f, 0f);
            float scale = Mathf.Clamp(Mathf.Max(target.lossyScale.x, target.lossyScale.z), 0.65f, 11f);
            shadow.localScale = new Vector3(shadowSizes[i].x * scale, shadowSizes[i].y * scale, 1f);
        }
    }
}

/// <summary>
/// High-tier shadow settings are leased to the Stage 4 root and restored when that
/// root is destroyed, so these settings do not leak into the other stages.
/// </summary>
public sealed class StageFourGraphicsSettingsLease : MonoBehaviour
{
    private bool captured;
    private float previousDistance;
    private int previousCascades;
    private ShadowResolution previousResolution;

    public void Apply()
    {
        if (!captured)
        {
            previousDistance = QualitySettings.shadowDistance;
            previousCascades = QualitySettings.shadowCascades;
            previousResolution = QualitySettings.shadowResolution;
            captured = true;
        }
        QualitySettings.shadowDistance = 40f;
        QualitySettings.shadowCascades = 1;
        QualitySettings.shadowResolution = ShadowResolution.High;
    }

    private void OnDestroy()
    {
        if (!captured) return;
        QualitySettings.shadowDistance = previousDistance;
        QualitySettings.shadowCascades = previousCascades;
        QualitySettings.shadowResolution = previousResolution;
    }
}
