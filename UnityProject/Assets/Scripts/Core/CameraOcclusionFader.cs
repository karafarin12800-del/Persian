using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Smoothly fades only house/tree renderers between the gameplay camera and player.
    /// Uses a dedicated transparent shader because alpha changes on opaque materials
    /// are ignored by the GPU render state.
    /// </summary>
    public sealed class CameraOcclusionFader : MonoBehaviour
    {
        [SerializeField, Range(0.15f, 0.75f)] private float fadedAlpha = 0.34f;
        [SerializeField, Min(0.1f)] private float fadeSpeed = 5.5f;
        [SerializeField, Min(0.05f)] private float sphereRadius = 0.32f;
        [SerializeField] private float targetHeight = 1.05f;

        private Transform target;
        private Camera viewCamera;
        private Shader fadeShader;
        private readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
        private readonly HashSet<Renderer> blocked = new HashSet<Renderer>();
        private readonly List<Renderer> keys = new List<Renderer>(96);
        private readonly RaycastHit[] hits = new RaycastHit[96];
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int FadeAlphaId = Shader.PropertyToID("_FadeAlpha");

        private sealed class Entry
        {
            public Material[] originalMaterials;
            public Material[] fadeMaterials;
            public float alpha = 1f;
        }

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            fadeShader = Resources.Load<Shader>("PersiaWarAndroidFade");
            if (fadeShader == null) fadeShader = Shader.Find("PersiaWar/AndroidFade");
            if (fadeShader == null)
                Debug.LogError("PERSIA_OCCLUSION: transparent fade shader missing; buildings cannot fade.");
        }

        public void SetTarget(Transform value)
        {
            target = value;
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null || viewCamera == null || fadeShader == null) return;

            blocked.Clear();
            Vector3 from = viewCamera.transform.position;
            Vector3 to = target.position + Vector3.up * targetHeight;
            Vector3 delta = to - from;
            float distance = delta.magnitude;

            if (distance > 0.1f)
            {
                int count = Physics.SphereCastNonAlloc(from, sphereRadius, delta / distance,
                    hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider col = hits[i].collider;
                    if (col == null || col.transform == target || col.transform.IsChildOf(target)) continue;
                    AddBlockedRenderers(col);
                }
            }

            foreach (Renderer r in blocked) EnsureEntry(r);
            keys.Clear();
            keys.AddRange(entries.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                Renderer r = keys[i];
                if (r == null)
                {
                    entries.Remove(r);
                    continue;
                }

                Entry e = entries[r];
                float wanted = blocked.Contains(r) ? fadedAlpha : 1f;
                e.alpha = Mathf.MoveTowards(e.alpha, wanted, fadeSpeed * Time.deltaTime);
                ApplyFade(e, e.alpha);

                if (wanted >= 0.999f && e.alpha >= 0.999f)
                {
                    r.sharedMaterials = e.originalMaterials;
                    DestroyFadeMaterials(e.fadeMaterials);
                    entries.Remove(r);
                }
            }
        }

        private void AddBlockedRenderers(Collider col)
        {
            Renderer direct = col.GetComponent<Renderer>();
            if (direct != null && direct.enabled && IsBuildingOrTree(direct.transform))
            {
                blocked.Add(direct);
                return;
            }

            Transform root = FindBuildingOrTreeRoot(col.transform);
            if (root == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].enabled)
                    blocked.Add(renderers[i]);
        }

        private static Transform FindBuildingOrTreeRoot(Transform t)
        {
            Transform match = null;
            while (t != null)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("tree") || n.Contains("building") || n.Contains("house"))
                    match = t;
                t = t.parent;
            }
            return match;
        }

        private static bool IsBuildingOrTree(Transform t)
        {
            while (t != null)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("tree") || n.Contains("building") || n.Contains("house") ||
                    n.Contains("roof") || n.Contains("foliage"))
                    return true;
                t = t.parent;
            }
            return false;
        }

        private void EnsureEntry(Renderer r)
        {
            if (entries.ContainsKey(r)) return;
            Material[] originals = r.sharedMaterials;
            Material[] fades = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                Material source = originals[i];
                if (source == null) continue;
                Material copy = new Material(fadeShader)
                {
                    name = source.name + "_CameraFade",
                    renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
                };

                Texture texture = null;
                if (source.HasProperty(MainTexId)) texture = source.GetTexture(MainTexId);
                else if (source.HasProperty(BaseMapId)) texture = source.GetTexture(BaseMapId);
                if (texture != null) copy.SetTexture(MainTexId, texture);

                Color tint = Color.white;
                if (source.HasProperty(ColorId)) tint = source.GetColor(ColorId);
                else if (source.HasProperty(BaseColorId)) tint = source.GetColor(BaseColorId);
                tint.a = 1f;
                copy.SetColor(ColorId, tint);
                copy.SetFloat(FadeAlphaId, 1f);
                fades[i] = copy;
            }

            entries.Add(r, new Entry { originalMaterials = originals, fadeMaterials = fades });
            r.sharedMaterials = fades;
        }

        private static void ApplyFade(Entry entry, float alpha)
        {
            if (entry.fadeMaterials == null) return;
            for (int i = 0; i < entry.fadeMaterials.Length; i++)
            {
                Material m = entry.fadeMaterials[i];
                if (m != null) m.SetFloat(FadeAlphaId, alpha);
            }
        }

        private static void DestroyFadeMaterials(Material[] materials)
        {
            if (materials == null) return;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null) Destroy(materials[i]);
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<Renderer, Entry> pair in entries)
            {
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value.originalMaterials;
                DestroyFadeMaterials(pair.Value.fadeMaterials);
            }
            entries.Clear();
        }
    }
}
