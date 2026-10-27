using System.Collections;
using System.Collections.Generic;
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
    private static readonly Dictionary<Renderer, Material[]> WorldOriginals = new Dictionary<Renderer, Material[]>();
    private static readonly Dictionary<int, Material> WorldLitCache = new Dictionary<int, Material>();
    private static Transform worldLookRoot;
    private static int worldLookStage = -1;
    private static Color worldLookPreviousAmbient;
    private static bool worldLookAmbientCaptured;
    private static readonly Dictionary<Light, Color> WorldOriginalLightColors = new Dictionary<Light, Color>();
    private static readonly Dictionary<Light, Quaternion> WorldOriginalLightRotations = new Dictionary<Light, Quaternion>();
    private static readonly Dictionary<Light, float> WorldOriginalLightIntensities = new Dictionary<Light, float>();

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

    public static bool IsEnhanced
    {
        get { return CurrentTier >= QualityTier.Medium; }
    }

    public static void ApplyToCamera(Camera camera)
    {
        if (camera == null) return;
        if (CurrentTier == QualityTier.Low)
        {
            camera.allowMSAA = false;
            return;
        }
        QualitySettings.antiAliasing = CurrentTier == QualityTier.High ? 4 : 2;
        camera.allowMSAA = true;
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
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        StartCoroutine(ApplyAntiAliasingAfterPresentationLayer());
        StartCoroutine(DiscoverCharacterShadows());
    }

    private IEnumerator ApplyAntiAliasingAfterPresentationLayer()
    {
        // ThreeDPresentationLayer configures cameras during startup. Wait one frame
        // so this tier is the last writer without editing that read-only component.
        yield return null;
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        if (CurrentTier == QualityTier.Low)
        {
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


    public static void ApplyWorldLook(Transform root, int stage)
    {
        if (root == null || stage < 1 || stage > 4) return;
        if (!IsEnhanced || stage == 4)
        {
            if (stage == 4)
            {
                Transform atmosphereRoot = root.Find("PW_WorldAtmosphereParticles");
                if (atmosphereRoot != null) Destroy(atmosphereRoot.gameObject);
            }
            RestoreWorldLook();
            return;
        }
        if (worldLookRoot == root && worldLookStage == stage) return;
        RestoreWorldLook();
        if (!worldLookAmbientCaptured) { worldLookPreviousAmbient = RenderSettings.ambientLight; worldLookAmbientCaptured = true; }
        Shader lit = Shader.Find("PersiaWar/MobileLit");
        if (lit == null) return;
        worldLookRoot = root;
        worldLookStage = stage;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterials == null) continue;
            Material[] originals = renderer.sharedMaterials;
            Material[] converted = new Material[originals.Length];
            bool changed = false;
            for (int m = 0; m < originals.Length; m++)
            {
                Material original = originals[m];
                if (original == null) { converted[m] = null; continue; }
                if (original.shader == lit) { converted[m] = original; continue; }
                int key = original.GetInstanceID();
                if (!WorldLitCache.TryGetValue(key, out Material cached) || cached == null)
                {
                    cached = new Material(original);
                    cached.shader = lit;
                    cached.enableInstancing = true;
                    if (original.HasProperty("_MainTex") && cached.HasProperty("_MainTex")) cached.mainTexture = original.mainTexture;
                    if (original.HasProperty("_Color") && cached.HasProperty("_Color")) cached.SetColor("_Color", original.GetColor("_Color"));
                    WorldLitCache[key] = cached;
                }
                converted[m] = cached;
                changed = true;
            }
            if (changed)
            {
                WorldOriginals[renderer] = originals;
                renderer.sharedMaterials = converted;
            }
        }
        RenderSettings.ambientLight = stage == 1 ? new Color(.53f,.56f,.61f) :
            stage == 2 ? new Color(.58f,.50f,.39f) : new Color(.28f,.42f,.58f);
        Light[] stageLights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < stageLights.Length; i++)
        {
            Light light = stageLights[i];
            if (light == null || light.type != LightType.Directional) continue;
            if (!WorldOriginalLightColors.ContainsKey(light))
            {
                WorldOriginalLightColors[light] = light.color;
                WorldOriginalLightRotations[light] = light.transform.rotation;
                WorldOriginalLightIntensities[light] = light.intensity;
            }
            light.color = stage == 1 ? new Color(1f,.91f,.78f) : stage == 2 ? new Color(1f,.78f,.55f) : new Color(.42f,.88f,1f);
            light.intensity = stage == 2 ? .9f : 1.1f;
            light.transform.rotation = stage == 1 ? Quaternion.Euler(48f, -32f, 0f)
                : stage == 2 ? Quaternion.Euler(34f, 24f, 0f) : Quaternion.Euler(22f, -18f, 0f);
        }
        ParticleSystem atmosphere = root.Find("PW_WorldAtmosphereParticles") != null
            ? root.Find("PW_WorldAtmosphereParticles").GetComponent<ParticleSystem>() : null;
        if (atmosphere == null)
        {
            GameObject go = new GameObject("PW_WorldAtmosphereParticles");
            go.transform.SetParent(root, false);
            atmosphere = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = atmosphere.main;
            main.loop = true; main.playOnAwake = false; main.maxParticles = 150;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .35f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .10f);
            ParticleSystem.ShapeModule shape = atmosphere.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(190f, 18f, 190f);
            ParticleSystem.EmissionModule emission = atmosphere.emission;
            emission.rateOverTime = 14f;
        }
        ParticleSystem.MainModule worldMain = atmosphere.main;
        worldMain.startColor = stage == 1 ? new Color(.90f,.83f,.65f,.28f) :
            stage == 2 ? new Color(.65f,.84f,.49f,.25f) : new Color(.24f,.88f,1f,.30f);
        atmosphere.Play(true);
        PickupItem[] worldPickups = root.GetComponentsInChildren<PickupItem>(true);
        for (int i = 0; i < worldPickups.Length; i++)
            if (worldPickups[i] != null)
                AddBlobShadow(worldPickups[i].transform, new Vector3(0f, -0.42f, 0f), new Vector2(.72f, .48f));
    }

    private static void RestoreWorldLook()
    {
        foreach (KeyValuePair<Renderer, Material[]> entry in WorldOriginals)
            if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
        WorldOriginals.Clear();
        foreach (KeyValuePair<Light, Color> entry in WorldOriginalLightColors)
            if (entry.Key != null) entry.Key.color = entry.Value;
        foreach (KeyValuePair<Light, Quaternion> entry in WorldOriginalLightRotations)
            if (entry.Key != null) entry.Key.transform.rotation = entry.Value;
        foreach (KeyValuePair<Light, float> entry in WorldOriginalLightIntensities)
            if (entry.Key != null) entry.Key.intensity = entry.Value;
        WorldOriginalLightColors.Clear();
        WorldOriginalLightRotations.Clear();
        WorldOriginalLightIntensities.Clear();
        if (worldLookAmbientCaptured)
        {
            RenderSettings.ambientLight = worldLookPreviousAmbient;
            worldLookAmbientCaptured = false;
        }
        worldLookRoot = null;
        worldLookStage = -1;
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
        int freeIndex = -1;
        for (int i = 0; i < profile.shadowTargets.Length; i++)
        {
            if (profile.shadowTargets[i] == target) return;
            if (freeIndex < 0 && (profile.shadowTargets[i] == null || profile.shadowObjects[i] == null))
                freeIndex = i;
        }
        if (freeIndex < 0) return;

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
        int index = freeIndex;
        profile.shadowCount = Mathf.Max(profile.shadowCount, index + 1);
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
