using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 4-only camera occlusion fade. Only collidable objects inside the
/// StageFourMothershipMegabase hierarchy are faded, and only while they sit
/// between the active camera and the player.
/// </summary>
public sealed class StageFourOcclusionFade : MonoBehaviour
{
    private Transform player;
    private Camera targetCamera;
    private Transform worldRoot;
    private readonly HashSet<Renderer> currentlyOccluded = new HashSet<Renderer>();
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private readonly Dictionary<Renderer, Material[]> fadeMaterials = new Dictionary<Renderer, Material[]>();
    private readonly Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();
    private readonly HashSet<Renderer> fadeable = new HashSet<Renderer>();
    private readonly List<Renderer> cachedRenderers = new List<Renderer>();
    private readonly List<Renderer> toRestore = new List<Renderer>();
    private Shader fadeShader;
    private const float FadedAlpha = 0.2f;

    public void Configure(Transform playerTransform, Camera cameraToUse, Transform stageRoot)
    {
        RestoreAll();
        player = playerTransform;
        targetCamera = cameraToUse;
        worldRoot = stageRoot;
        fadeShader = Resources.Load<Shader>("PersiaWarAndroidFade");
        if (fadeShader == null || !fadeShader.isSupported)
            fadeShader = Shader.Find("PersiaWar/AndroidFade");
        if (fadeShader == null || !fadeShader.isSupported)
            fadeShader = Shader.Find("Sprites/Default");
        if (fadeShader == null || !fadeShader.isSupported)
        {
            fadeShader = null;
            Debug.LogError("PERSIA_STAGE4_OCCLUSION: no supported transparent shader found.");
            return;
        }
        CacheFadeableRenderers();
    }

    private void LateUpdate()
    {
        if (player == null || targetCamera == null || worldRoot == null) return;

        Vector3 target = player.position + Vector3.up * 1.05f;
        Vector3 origin = targetCamera.transform.position;
        Vector3 ray = target - origin;
        float distance = ray.magnitude;
        if (distance < 0.05f) return;

        var hits = Physics.RaycastAll(origin, ray / distance, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        var next = new HashSet<Renderer>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null) continue;
            Transform hitTransform = hitCollider.transform;
            if (hitTransform == player || hitTransform.IsChildOf(player)) continue;
            if (!hitTransform.IsChildOf(worldRoot)) continue;

            Renderer r = hitCollider.GetComponent<Renderer>();
            if (r == null) r = hitCollider.GetComponentInParent<Renderer>();
            if (r == null || !fadeable.Contains(r)) continue;
            next.Add(r);
        }

        foreach (Renderer r in currentlyOccluded)
            if (r != null && !next.Contains(r)) toRestore.Add(r);

        for (int i = 0; i < toRestore.Count; i++) SetFaded(toRestore[i], false);
        toRestore.Clear();

        foreach (Renderer r in next)
            if (r != null && !currentlyOccluded.Contains(r)) SetFaded(r, true);

        currentlyOccluded.Clear();
        foreach (Renderer r in next) currentlyOccluded.Add(r);
    }

    private void CacheFadeableRenderers()
    {
        fadeable.Clear();
        cachedRenderers.Clear();
        if (worldRoot == null) return;
        worldRoot.GetComponentsInChildren(true, cachedRenderers);
        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            Renderer r = cachedRenderers[i];
            if (r == null || r.GetComponent<Collider>() == null) continue;
            Material[] shared = r.sharedMaterials;
            if (shared == null || shared.Length == 0) continue;

            bool hasColor = false;
            for (int m = 0; m < shared.Length; m++)
                if (shared[m] != null && (shared[m].HasProperty("_Color") || shared[m].HasProperty("_BaseColor")))
                    hasColor = true;
            if (!hasColor) continue;

            fadeable.Add(r);
            if (!originalMaterials.ContainsKey(r))
            {
                originalMaterials[r] = shared;
                var colors = new Color[shared.Length];
                var clones = new Material[shared.Length];
                for (int m = 0; m < shared.Length; m++)
                {
                    Material source = shared[m];
                    colors[m] = source != null
                        ? (source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color)
                        : Color.white;

                    if (source == null) continue;
                    Material fade = new Material(fadeShader)
                    {
                        name = source.name + "_StageFourCameraFade",
                        renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
                    };

                    Texture texture = null;
                    if (source.HasProperty("_MainTex")) texture = source.GetTexture("_MainTex");
                    else if (source.HasProperty("_BaseMap")) texture = source.GetTexture("_BaseMap");
                    if (texture != null) fade.SetTexture("_MainTex", texture);

                    Color tint = colors[m];
                    tint.a = 1f;
                    fade.SetColor("_Color", tint);
                    fade.SetFloat("_FadeAlpha", 1f);
                    clones[m] = fade;
                }
                originalColors[r] = colors;
                fadeMaterials[r] = clones;
            }
        }
    }

    private void SetFaded(Renderer renderer, bool faded)
    {
        if (renderer == null) return;
        if (faded)
        {
            if (fadeMaterials.TryGetValue(renderer, out Material[] mats))
            {
                renderer.materials = mats;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    m.SetFloat("_FadeAlpha", FadedAlpha);
                }
            }
        }
        else if (originalMaterials.TryGetValue(renderer, out Material[] originals))
        {
            renderer.sharedMaterials = originals;
        }
    }

    private void RestoreAll()
    {
        foreach (Renderer r in currentlyOccluded)
            if (r != null) SetFaded(r, false);
        currentlyOccluded.Clear();
    }

    private void OnDisable()
    {
        RestoreAll();
    }

    private void OnDestroy()
    {
        RestoreAll();
        foreach (var pair in fadeMaterials)
            if (pair.Value != null)
                for (int i = 0; i < pair.Value.Length; i++)
                    if (pair.Value[i] != null) Destroy(pair.Value[i]);
        fadeMaterials.Clear();
        originalMaterials.Clear();
        originalColors.Clear();
    }
}
