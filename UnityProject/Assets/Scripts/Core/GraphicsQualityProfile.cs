using System.Collections;
using UnityEngine;
using PersiaWar.Unity2D5D;

/// <summary>
/// Conservative runtime quality tiers. Low intentionally preserves the old flat
/// Stage 4 material path; Medium is the default and High enables one short shadow
/// cascade. PW_GFX_TIER overrides the automatic device-capability choice (0/1/2).
/// </summary>
public sealed class GraphicsQualityProfile : MonoBehaviour
{
    public enum QualityTier { Low = 0, Medium = 1, High = 2 }

    private const string OverrideKey = "PW_GFX_TIER";
    private static GraphicsQualityProfile instance;
    private static bool tierChosen;
    private static QualityTier selectedTier = QualityTier.Medium;

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
        ChooseTierOnce();
        if (CurrentTier == QualityTier.Low)
        {
            camera.allowMSAA = false;
            return;
        }
        QualitySettings.antiAliasing = CurrentTier == QualityTier.High ? 4 : 2;
        camera.allowMSAA = true;
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
        int over = PlayerPrefs.GetInt(OverrideKey, -1);
        if (over >= 0 && over <= 2)
        {
            selectedTier = (QualityTier)over;
            tierChosen = true;
            return;
        }

        int gpuMemory = SystemInfo.graphicsMemorySize;
        int cpuCount = SystemInfo.processorCount;
        int systemMemory = SystemInfo.systemMemorySize;
        bool clearlyLow = (gpuMemory > 0 && gpuMemory < 1024) ||
                          (cpuCount > 0 && cpuCount <= 4) ||
                          (systemMemory > 0 && systemMemory < 2800);
        bool clearlyHigh = gpuMemory >= 4096 && cpuCount >= 8 && systemMemory >= 6000;
        selectedTier = clearlyLow ? QualityTier.Low :
                       (clearlyHigh ? QualityTier.High : QualityTier.Medium);
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
        ApplyAndroidResolutionBudget();
        StartCoroutine(ApplyAntiAliasingAfterPresentationLayer());
        StartCoroutine(DiscoverCharacterShadows());
    }

    private void ApplyAndroidResolutionBudget()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        int longestSide = CurrentTier == QualityTier.High ? 1920 : 1600;
        int currentLongest = Mathf.Max(Screen.width, Screen.height);
        if (currentLongest > longestSide && currentLongest > 0)
        {
            float scale = longestSide / (float)currentLongest;
            int width = Mathf.Max(720, Mathf.RoundToInt(Screen.width * scale));
            int height = Mathf.Max(480, Mathf.RoundToInt(Screen.height * scale));
            Screen.SetResolution(width, height, Screen.fullScreenMode);
            StartupCheckpoint.Set("AndroidRenderResolutionCapped_" + width + "x" + height);
        }
#endif
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
        if (CurrentTier == QualityTier.Low)
        {
            QualitySettings.antiAliasing = 0;
            for (int i = 0; i < cameras.Length; i++)
                if (cameras[i] != null) cameras[i].allowMSAA = false;
            yield break;
        }

        QualitySettings.antiAliasing = CurrentTier == QualityTier.High ? 4 : 2;
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null) cameras[i].allowMSAA = true;
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
            Color color = renderer.sharedMaterial.color;
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
