using System;
using UnityEngine;

/// <summary>
/// Runtime-only Stage 4 mothership detonation effects. All geometry, materials,
/// textures and particles are generated at runtime; no downloaded assets are used.
/// </summary>
public sealed class StageFourMothershipExplosionFX : MonoBehaviour
{
    private const float VisualLifetime = 2.55f;
    private static Texture2D radialTexture;
    private static Material additiveMaterial;
    private static Material particleAlphaMaterial;
    private static ParticleSystem emberSystem;
    private static ParticleSystem smokeSystem;
    private static GameObject particleRoot;
    private static Transform cachedWorldRoot;
    private static Renderer[] hullRenderers;
    private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
    private static readonly Vector3[] ChargeSurfacePoints =
    {
        new Vector3(10f, 39f, -95f),
        new Vector3(42f, 25f, -30f),
        new Vector3(0f, 46f, -8f),
        new Vector3(63f, 39f, 26f),
        new Vector3(0f, 46f, 49f),
        new Vector3(0f, 40f, 10f)
    };
    private static readonly Vector3[] SecondarySurfacePoints =
    {
        new Vector3(-42f, 31f, -44f), new Vector3(42f, 31f, -44f),
        new Vector3(-52f, 32f, -18f), new Vector3(52f, 32f, -18f),
        new Vector3(-44f, 42f, 4f), new Vector3(44f, 42f, 4f),
        new Vector3(-60f, 37f, 25f), new Vector3(60f, 37f, 25f),
        new Vector3(-28f, 43f, 48f), new Vector3(28f, 43f, 48f),
        new Vector3(0f, 43f, -74f), new Vector3(0f, 42f, 78f),
        new Vector3(-21f, 34f, 72f), new Vector3(21f, 34f, 72f)
    };

    private Camera targetCamera;
    private Transform worldRoot;
    private Transform protectedShuttle;
    private float radius;
    private float elapsed;
    private Transform flash;
    private Transform outerFire;
    private Transform middleFire;
    private Transform coreFire;
    private Renderer flashRenderer;
    private Renderer outerRenderer;
    private Renderer middleRenderer;
    private Renderer coreRenderer;
    private LineRenderer shockwave;
    private Transform[] debrisTransforms;
    private Vector3[] debrisStarts;
    private Vector3[] debrisVelocities;
    private Vector3[] debrisSpins;
    private Vector3[] debrisScales;
    private int debrisCount;

    public static Texture2D GetRadialTexture()
    {
        if (radialTexture != null) return radialTexture;
        try
        {
            const int size = 64;
            radialTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            radialTexture.name = "S4_ProceduralSoftRadial64";
            radialTexture.wrapMode = TextureWrapMode.Clamp;
            radialTexture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.15f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            radialTexture.SetPixels(pixels);
            radialTexture.Apply(false, true);
            return radialTexture;
        }
        catch (Exception)
        {
            radialTexture = null;
            return Texture2D.whiteTexture;
        }
    }

    public static Material GetSoftAdditiveMaterial()
    {
        if (additiveMaterial != null) return additiveMaterial;
        additiveMaterial = CreateSoftMaterial("PersiaWarAndroidAdditive", "S4_SharedSoftAdditive");
        return additiveMaterial;
    }

    public static Material GetSoftAlphaMaterial()
    {
        if (particleAlphaMaterial != null) return particleAlphaMaterial;
        particleAlphaMaterial = CreateSoftMaterial("PersiaWarAndroidParticleAlpha", "S4_SharedSoftAlpha");
        return particleAlphaMaterial;
    }

    private static Material CreateSoftMaterial(string resourceShaderName, string materialName)
    {
        Shader shader = null;
        try { shader = Resources.Load<Shader>(resourceShaderName); } catch (Exception) { }
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) return null;

        try
        {
            Material material = new Material(shader);
            material.name = materialName;
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", GetRadialTexture());
            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            material.renderQueue = 3000;
            return material;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static Vector3 ResolveHullPlacement(Transform root, Vector3 requestedPosition, string effectName, Camera camera)
    {
        EnsureHullCache(root);
        Vector3 resolved = requestedPosition;
        if (effectName != null && effectName.StartsWith("S4_MothershipChargeDetonation_", StringComparison.Ordinal))
        {
            int chargeNumber;
            if (int.TryParse(effectName.Substring("S4_MothershipChargeDetonation_".Length), out chargeNumber) &&
                chargeNumber >= 1 && chargeNumber <= ChargeSurfacePoints.Length)
                resolved = ChargeSurfacePoints[chargeNumber - 1];
        }
        else if (effectName != null && effectName.StartsWith("S4_MothershipSecondaryDetonation_", StringComparison.Ordinal))
        {
            int secondaryNumber;
            if (int.TryParse(effectName.Substring("S4_MothershipSecondaryDetonation_".Length), out secondaryNumber) &&
                secondaryNumber >= 1 && secondaryNumber <= SecondarySurfacePoints.Length)
                resolved = SecondarySurfacePoints[secondaryNumber - 1];
        }
        else if (effectName != null && effectName.StartsWith("S4_MothershipCoreDetonation", StringComparison.Ordinal))
        {
            resolved = new Vector3(0f, 40f, 10f);
        }

        Renderer best = FindNearestHullRenderer(resolved, true);
        if (best != null)
        {
            Bounds bounds = best.bounds;
            bool overFootprint = resolved.x >= bounds.min.x && resolved.x <= bounds.max.x &&
                                 resolved.z >= bounds.min.z && resolved.z <= bounds.max.z;
            if (overFootprint)
                resolved.y = Mathf.Max(resolved.y, bounds.max.y - 1.2f);
        }

        Vector3 towardCamera = camera != null
            ? camera.transform.position - resolved
            : new Vector3(0.35f, 0.35f, 0.85f);
        if (towardCamera.sqrMagnitude > 0.001f)
            resolved += towardCamera.normalized * 3f;
        return resolved;
    }

    public void Play(Transform sceneRoot, Camera camera, Transform shuttle, float blastRadius)
    {
        worldRoot = sceneRoot;
        targetCamera = camera;
        protectedShuttle = shuttle;
        radius = Mathf.Clamp(blastRadius, 3f, 60f);
        elapsed = 0f;
        EnsureHullCache(worldRoot);
        EnsureParticleSystems(worldRoot);
        flash = CreateSoftBillboard("S4_ExplosionWhiteYellowFlash", Vector3.up * (radius * 0.02f), Vector3.one);
        outerFire = CreateSoftBillboard("S4_ExplosionOuterFire", Vector3.up * (radius * 0.10f), Vector3.one);
        middleFire = CreateSoftBillboard("S4_ExplosionMiddleFire", Vector3.up * (radius * 0.06f), Vector3.one);
        coreFire = CreateSoftBillboard("S4_ExplosionCoreFire", Vector3.up * (radius * 0.03f), Vector3.one);
        flashRenderer = flash != null ? flash.GetComponent<Renderer>() : null;
        outerRenderer = outerFire != null ? outerFire.GetComponent<Renderer>() : null;
        middleRenderer = middleFire != null ? middleFire.GetComponent<Renderer>() : null;
        coreRenderer = coreFire != null ? coreFire.GetComponent<Renderer>() : null;
        BuildShockwave();
        BuildDebris();
        EmitExplosionParticles();
        PeelNearestHull();
        RuntimeGameAudio.PlayExplosion(radius);
        StageFourCameraShake.Trigger(targetCamera, Mathf.Clamp(radius * 0.018f, 0.08f, 1.25f));
    }

    private Transform CreateSoftBillboard(string objectName, Vector3 localPosition, Vector3 initialScale)
    {
        GameObject billboard = GameObject.CreatePrimitive(PrimitiveType.Quad);
        billboard.name = objectName;
        billboard.transform.SetParent(transform, false);
        billboard.transform.localPosition = localPosition;
        billboard.transform.localScale = initialScale;
        Collider collider = billboard.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = billboard.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = GetSoftAdditiveMaterial();
        return billboard.transform;
    }

    private void BuildShockwave()
    {
        shockwave = gameObject.AddComponent<LineRenderer>();
        shockwave.useWorldSpace = false;
        shockwave.loop = true;
        shockwave.positionCount = 48;
        shockwave.alignment = LineAlignment.TransformZ;
        shockwave.sharedMaterial = GetSoftAdditiveMaterial();
        shockwave.startColor = new Color(1f, 0.75f, 0.35f, 0.8f);
        shockwave.endColor = new Color(0.65f, 0.08f, 0.015f, 0f);
        shockwave.startWidth = 0.7f;
        shockwave.endWidth = 0.1f;
        Transform ring = shockwave.transform;
        ring.localPosition = new Vector3(0f, -Mathf.Max(0.3f, radius * 0.06f), 0f);
        ring.localScale = new Vector3(0.1f, 1f, 0.1f);
        for (int i = 0; i < shockwave.positionCount; i++)
        {
            float a = i * Mathf.PI * 2f / shockwave.positionCount;
            shockwave.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
        }
    }

    private void BuildDebris()
    {
        debrisCount = radius >= 50f ? 12 : (radius >= 15f ? 9 : (radius >= 7f ? 5 : 2));
        debrisTransforms = new Transform[debrisCount];
        debrisStarts = new Vector3[debrisCount];
        debrisVelocities = new Vector3[debrisCount];
        debrisSpins = new Vector3[debrisCount];
        debrisScales = new Vector3[debrisCount];
        Material hullMaterial = GetNearestHullMaterial(transform.position);
        for (int i = 0; i < debrisCount; i++)
        {
            GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "S4_MothershipDebrisChunk";
            chunk.transform.SetParent(transform, false);
            Vector3 direction = UnityEngine.Random.insideUnitSphere;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.up;
            direction.Normalize();
            Vector3 start = direction * UnityEngine.Random.Range(0.1f, Mathf.Min(12f, radius * 0.24f));
            chunk.transform.localPosition = start;
            Vector3 size = Vector3.one * UnityEngine.Random.Range(0.30f, Mathf.Max(0.55f, radius * 0.045f));
            chunk.transform.localScale = size;
            Collider collider = chunk.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = chunk.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = hullMaterial != null ? hullMaterial : GetSoftAdditiveMaterial();
                if (radius < 7f) SetTint(renderer, new Color(1f, 0.3f, 0.08f, 1f));
            }
            debrisTransforms[i] = chunk.transform;
            debrisStarts[i] = start;
            debrisVelocities[i] = direction * UnityEngine.Random.Range(7f, 21f) +
                                   Vector3.up * UnityEngine.Random.Range(2f, 9f);
            debrisSpins[i] = new Vector3(
                UnityEngine.Random.Range(-220f, 220f),
                UnityEngine.Random.Range(-220f, 220f),
                UnityEngine.Random.Range(-220f, 220f));
            debrisScales[i] = size;
        }
    }

    private void EmitExplosionParticles()
    {
        if (emberSystem == null || smokeSystem == null) return;
        int sparkCount = radius >= 50f ? 48 : (radius >= 15f ? 20 : 4);
        int smokeCount = radius >= 50f ? 60 : (radius >= 15f ? 28 : 5);
        Vector3 center = transform.position;
        for (int i = 0; i < sparkCount; i++)
        {
            Vector3 direction = UnityEngine.Random.insideUnitSphere;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.up;
            direction.Normalize();
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            emit.position = center + direction * UnityEngine.Random.Range(0.2f, radius * 0.28f);
            emit.velocity = direction * UnityEngine.Random.Range(7f, 22f) + Vector3.up * UnityEngine.Random.Range(1f, 7f);
            emit.startLifetime = UnityEngine.Random.Range(0.65f, 1.25f);
            emit.startSize = UnityEngine.Random.Range(0.35f, Mathf.Clamp(radius * 0.055f, 0.6f, 2.3f));
            emit.startColor = new Color(1f, UnityEngine.Random.Range(0.38f, 0.92f), 0.08f, 1f);
            emberSystem.Emit(emit, 1);
        }
        for (int i = 0; i < smokeCount; i++)
        {
            Vector3 direction = UnityEngine.Random.insideUnitSphere;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.up;
            direction.Normalize();
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            emit.position = center + direction * UnityEngine.Random.Range(0.1f, radius * 0.22f);
            emit.velocity = direction * UnityEngine.Random.Range(1f, 4f) + Vector3.up * UnityEngine.Random.Range(2f, 6f);
            emit.startLifetime = UnityEngine.Random.Range(1.25f, 2.05f);
            emit.startSize = UnityEngine.Random.Range(1.2f, Mathf.Clamp(radius * 0.11f, 2f, 6f));
            emit.startColor = new Color(0.12f, 0.14f, 0.17f, 0.68f);
            smokeSystem.Emit(emit, 1);
        }
    }

    private void PeelNearestHull()
    {
        if (hullRenderers == null || worldRoot == null) return;
        int peelCount = radius >= 50f ? 12 : (radius >= 15f ? 5 : 1);
        float maxDistance = radius * 1.85f + 8f;
        for (int pick = 0; pick < peelCount; pick++)
        {
            Renderer best = null;
            float bestDistance = maxDistance;
            for (int i = 0; i < hullRenderers.Length; i++)
            {
                Renderer candidate = hullRenderers[i];
                if (candidate == null || !candidate.enabled ||
                    !candidate.gameObject.name.StartsWith("S4_Mothership", StringComparison.Ordinal) ||
                    candidate.GetComponent<StageFourHullChunkFlight>() != null)
                    continue;
                if (protectedShuttle != null &&
                    (candidate.transform == protectedShuttle || candidate.transform.IsChildOf(protectedShuttle)))
                    continue;
                Vector3 closest = candidate.bounds.ClosestPoint(transform.position);
                float distance = Vector3.Distance(closest, transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            if (best == null) break;
            StageFourHullChunkFlight flight = best.gameObject.AddComponent<StageFourHullChunkFlight>();
            flight.Begin(worldRoot, transform.position, radius);
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float p = Mathf.Clamp01(elapsed / VisualLifetime);
        float flashProgress = Mathf.Clamp01(elapsed / 0.2f);
        SetBillboard(flash, flashRenderer, radius * (0.35f + flashProgress * 2.35f),
            Color.Lerp(new Color(1f, 0.98f, 0.78f, 0.98f), new Color(1f, 0.7f, 0.2f, 0f), flashProgress));
        SetBillboard(outerFire, outerRenderer, radius * (0.3f + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.72f)) * 2.8f),
            WithAlpha(FireRamp(Mathf.Clamp01(elapsed / 2.15f)), (1f - p) * 0.72f));
        SetBillboard(middleFire, middleRenderer, radius * (0.22f + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.55f)) * 2.05f),
            WithAlpha(FireRamp(Mathf.Clamp01((elapsed + 0.22f) / 1.85f)), Mathf.Clamp01(1f - elapsed / 1.95f) * 0.84f));
        SetBillboard(coreFire, coreRenderer, radius * (0.16f + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.38f)) * 1.35f),
            WithAlpha(FireRamp(Mathf.Clamp01((elapsed + 0.38f) / 1.55f)), Mathf.Clamp01(1f - elapsed / 1.45f) * 0.92f));

        if (shockwave != null)
        {
            float wave = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.25f));
            shockwave.transform.localScale = new Vector3(Mathf.Lerp(0.1f, radius * 3.8f, wave), 1f,
                Mathf.Lerp(0.1f, radius * 3.8f, wave));
            shockwave.widthMultiplier = Mathf.Lerp(0.65f, Mathf.Max(0.2f, radius * 0.035f), wave);
            float alpha = Mathf.Clamp01(1f - elapsed / 1.6f);
            shockwave.startColor = new Color(1f, 0.66f, 0.25f, alpha);
            shockwave.endColor = new Color(0.72f, 0.08f, 0.015f, 0f);
        }

        for (int i = 0; i < debrisCount; i++)
        {
            Transform chunk = debrisTransforms[i];
            if (chunk == null) continue;
            float fade = Mathf.Clamp01(elapsed / VisualLifetime);
            chunk.localPosition = debrisStarts[i] + debrisVelocities[i] * elapsed +
                                  Vector3.down * (4.5f * elapsed * elapsed);
            chunk.localRotation = chunk.localRotation * Quaternion.Euler(debrisSpins[i] * Time.deltaTime);
            chunk.localScale = Vector3.Lerp(debrisScales[i], Vector3.zero, fade);
        }
        if (targetCamera != null)
        {
            FaceCamera(flash);
            FaceCamera(outerFire);
            FaceCamera(middleFire);
            FaceCamera(coreFire);
        }
    }

    private static void SetBillboard(Transform target, Renderer renderer, float diameter, Color tint)
    {
        if (target == null) return;
        target.localScale = new Vector3(Mathf.Max(0.01f, diameter), Mathf.Max(0.01f, diameter), 1f);
        if (renderer != null) SetTint(renderer, tint);
    }

    private void FaceCamera(Transform target)
    {
        if (target == null || targetCamera == null) return;
        Vector3 direction = targetCamera.transform.position - target.position;
        if (direction.sqrMagnitude > 0.001f)
            target.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static Color FireRamp(float t)
    {
        if (t < 0.22f) return Color.Lerp(new Color(1f, 0.97f, 0.82f, 1f), new Color(1f, 0.91f, 0.27f, 1f), t / 0.22f);
        if (t < 0.52f) return Color.Lerp(new Color(1f, 0.91f, 0.27f, 1f), new Color(1f, 0.37f, 0.055f, 1f), (t - 0.22f) / 0.30f);
        return Color.Lerp(new Color(1f, 0.37f, 0.055f, 1f), new Color(0.28f, 0.025f, 0.012f, 1f), (t - 0.52f) / 0.48f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private static void SetTint(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        PropertyBlock.Clear();
        PropertyBlock.SetColor("_TintColor", color);
        PropertyBlock.SetColor("_Color", color);
        renderer.SetPropertyBlock(PropertyBlock);
    }

    private static void EnsureHullCache(Transform root)
    {
        if (root == null) return;
        if (cachedWorldRoot == root && hullRenderers != null) return;
        cachedWorldRoot = root;
        hullRenderers = root.GetComponentsInChildren<Renderer>(true);
    }

    private static bool IsHullSurfaceName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) ||
            !objectName.StartsWith("S4_Mothership", StringComparison.Ordinal))
            return false;
        return objectName.Contains("Egg") || objectName.Contains("Hull") ||
               objectName.Contains("TechCube") || objectName.Contains("Bridge") ||
               objectName.Contains("ProwArmor") || objectName.Contains("ProwTip") ||
               objectName.Contains("AftArmor") || objectName.Contains("SternTip") ||
               objectName.Contains("DorsalBayLip") || objectName.Contains("Keel");
    }

    private static Renderer FindNearestHullRenderer(Vector3 position, bool surfaceOnly)
    {
        if (hullRenderers == null) return null;
        Renderer best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < hullRenderers.Length; i++)
        {
            Renderer candidate = hullRenderers[i];
            if (candidate == null ||
                !candidate.gameObject.name.StartsWith("S4_Mothership", StringComparison.Ordinal) ||
                (surfaceOnly && !IsHullSurfaceName(candidate.gameObject.name)))
                continue;
            Vector3 closest = candidate.bounds.ClosestPoint(position);
            float score = Vector3.Distance(closest, position);
            if (candidate.bounds.Contains(position)) score = -0.1f;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    private static Material GetNearestHullMaterial(Vector3 position)
    {
        Renderer best = FindNearestHullRenderer(position, true);
        return best != null ? best.sharedMaterial : null;
    }

    private static void EnsureParticleSystems(Transform root)
    {
        if (root == null) return;
        if (particleRoot != null && cachedWorldRoot == root && emberSystem != null && smokeSystem != null)
            return;
        if (particleRoot != null) UnityEngine.Object.Destroy(particleRoot);
        particleRoot = new GameObject("S4_MothershipExplosionParticlePool");
        particleRoot.transform.SetParent(root, false);
        emberSystem = CreateParticleSystem(particleRoot.transform, "S4_ExplosionEmbers", GetSoftAdditiveMaterial(), 140);
        smokeSystem = CreateParticleSystem(particleRoot.transform, "S4_ExplosionSmoke", GetSoftAlphaMaterial(), 180);
    }

    private static ParticleSystem CreateParticleSystem(Transform parent, string objectName, Material material, int maxParticles)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 8f;
        main.startLifetime = 2f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.38f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.55f), new Keyframe(0.55f, 1.25f), new Keyframe(1f, 2.0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        if (objectName.Contains("Smoke"))
        {
            ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
            force.enabled = true;
            force.y = new ParticleSystem.MinMaxCurve(1.8f);
        }
        system.Play(true);
        return system;
    }

    public static void FinishEffects()
    {
        if (particleRoot != null) UnityEngine.Object.Destroy(particleRoot);
        particleRoot = null;
        emberSystem = null;
        smokeSystem = null;
        cachedWorldRoot = null;
        hullRenderers = null;
        if (additiveMaterial != null) UnityEngine.Object.Destroy(additiveMaterial);
        if (particleAlphaMaterial != null) UnityEngine.Object.Destroy(particleAlphaMaterial);
        additiveMaterial = null;
        particleAlphaMaterial = null;
        if (radialTexture != null && radialTexture != Texture2D.whiteTexture)
            UnityEngine.Object.Destroy(radialTexture);
        radialTexture = null;
    }

    public static void PlayFinalFlash(Camera camera)
    {
        if (camera == null) return;
        StageFourCameraShake shake = camera.GetComponent<StageFourCameraShake>();
        if (shake == null) shake = camera.gameObject.AddComponent<StageFourCameraShake>();
        shake.TriggerWhiteFlash(0.34f);
    }
}

/// <summary>
/// A detached piece of the mothership hull. Movement is transform-only, with no
/// Rigidbody or collision simulation, and every piece self-destructs after 2.5 s.
/// </summary>
public sealed class StageFourHullChunkFlight : MonoBehaviour
{
    private Transform worldRoot;
    private Vector3 startPosition;
    private Vector3 startScale;
    private Vector3 velocity;
    private Vector3 angularVelocity;
    private float elapsed;
    private const float Lifetime = 2.5f;

    public void Begin(Transform root, Vector3 blastCenter, float blastRadius)
    {
        worldRoot = root;
        transform.SetParent(worldRoot, true);
        startPosition = transform.position;
        startScale = transform.localScale;
        Vector3 direction = startPosition - blastCenter;
        if (direction.sqrMagnitude < 0.01f) direction = UnityEngine.Random.insideUnitSphere;
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.up;
        direction.Normalize();
        velocity = direction * UnityEngine.Random.Range(9f, 24f) + Vector3.up * UnityEngine.Random.Range(4f, 13f);
        angularVelocity = new Vector3(
            UnityEngine.Random.Range(-180f, 180f),
            UnityEngine.Random.Range(-180f, 180f),
            UnityEngine.Random.Range(-180f, 180f));
        elapsed = 0f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        transform.position = startPosition + velocity * elapsed + Vector3.down * (3.8f * elapsed * elapsed);
        transform.rotation = transform.rotation * Quaternion.Euler(angularVelocity * Time.deltaTime);
        transform.localScale = Vector3.Lerp(startScale, Vector3.zero, Mathf.Clamp01(elapsed / Lifetime));
        if (elapsed >= Lifetime) Destroy(gameObject);
    }
}

/// <summary>
/// Non-accumulating camera shake. The finale can continue assigning a base camera
/// transform every frame; this LateUpdate only adds a temporary offset afterward.
/// </summary>
public sealed class StageFourCameraShake : MonoBehaviour
{
    private Vector3 lastBasePosition;
    private Vector3 lastOffset;
    private bool haveLast;
    private float strength;
    private float flashRemaining;
    private float flashDuration;

    public static void Trigger(Camera camera, float intensity)
    {
        if (camera == null) return;
        StageFourCameraShake shake = camera.GetComponent<StageFourCameraShake>();
        if (shake == null) shake = camera.gameObject.AddComponent<StageFourCameraShake>();
        shake.strength = Mathf.Min(1.4f, shake.strength + Mathf.Max(0f, intensity));
    }

    public void TriggerWhiteFlash(float duration)
    {
        flashDuration = Mathf.Max(0.05f, duration);
        flashRemaining = flashDuration;
    }

    private void Update()
    {
        strength = Mathf.MoveTowards(strength, 0f, Time.deltaTime * 1.9f);
        if (flashRemaining > 0f) flashRemaining = Mathf.Max(0f, flashRemaining - Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (strength > 0.001f)
        {
            Vector3 current = transform.position;
            if (haveLast)
            {
                Vector3 expected = lastBasePosition + lastOffset;
                if ((current - expected).sqrMagnitude < 0.0025f)
                    current -= lastOffset;
            }
            float t = Time.time;
            Vector3 offset = new Vector3(
                Mathf.Sin(t * 27f) * strength * 0.34f,
                Mathf.Sin(t * 39f + 0.7f) * strength * 0.24f,
                Mathf.Cos(t * 23f + 0.4f) * strength * 0.18f);
            transform.position = current + offset;
            lastBasePosition = current;
            lastOffset = offset;
            haveLast = true;
        }
        else
        {
            lastOffset = Vector3.zero;
            lastBasePosition = transform.position;
            haveLast = true;
        }
    }

    private void OnGUI()
    {
        if (flashRemaining <= 0f || flashDuration <= 0f) return;
        Color previous = GUI.color;
        float alpha = 0.9f * Mathf.Clamp01(flashRemaining / flashDuration);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
