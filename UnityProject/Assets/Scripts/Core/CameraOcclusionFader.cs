using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>Fades only named buildings and trees blocking the view to the player.</summary>
    public sealed class CameraOcclusionFader : MonoBehaviour
    {
        [SerializeField, Range(0.15f, 1f)] private float fadedAlpha = 0.32f;
        [SerializeField, Min(0.1f)] private float fadeSpeed = 6f;
        [SerializeField, Min(0.05f)] private float sphereRadius = 0.4f;
        [SerializeField] private float targetHeight = 1.05f;

        private Transform target;
        private Camera viewCamera;
        private readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
        private readonly HashSet<Renderer> blocked = new HashSet<Renderer>();
        private readonly List<Renderer> keys = new List<Renderer>(64);
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private sealed class Entry
        {
            public Renderer renderer;
            public MaterialPropertyBlock original;
            public Color[] colors;
            public float alpha = 1f;
        }

        public void SetTarget(Transform value)
        {
            target = value;
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            if (viewCamera == null) return;

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
                    Renderer r = col.GetComponent<Renderer>();
                    if (r == null) r = col.GetComponentInParent<Renderer>();
                    if (r != null && r.enabled && IsBuildingOrTree(r)) blocked.Add(r);
                }
            }

            foreach (Renderer r in blocked) EnsureEntry(r);
            keys.Clear();
            keys.AddRange(entries.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                Renderer r = keys[i];
                if (r == null) { entries.Remove(r); continue; }
                Entry e = entries[r];
                float targetAlpha = blocked.Contains(r) ? fadedAlpha : 1f;
                e.alpha = Mathf.MoveTowards(e.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
                Apply(e);
                if (targetAlpha >= 0.999f && e.alpha >= 0.999f)
                {
                    r.SetPropertyBlock(e.original);
                    entries.Remove(r);
                }
            }
        }

        private static bool IsBuildingOrTree(Renderer r)
        {
            Transform t = r.transform;
            while (t != null)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("tree") || n.Contains("building") || n.Contains("house") ||
                    n.Contains("roof") || n.Contains("wall") || n.Contains("foliage")) return true;
                t = t.parent;
            }
            return false;
        }

        private void EnsureEntry(Renderer r)
        {
            if (entries.ContainsKey(r)) return;
            Material[] mats = r.sharedMaterials;
            Color[] colors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                colors[i] = mats[i] != null && mats[i].HasProperty(ColorId) ? mats[i].GetColor(ColorId) : Color.white;
            Entry e = new Entry { renderer = r, colors = colors, original = new MaterialPropertyBlock() };
            r.GetPropertyBlock(e.original);
            entries.Add(r, e);
        }

        private void Apply(Entry e)
        {
            e.renderer.GetPropertyBlock(block);
            Material[] mats = e.renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;
                Color c = i < e.colors.Length ? e.colors[i] : Color.white;
                c.a *= e.alpha;
                if (m.HasProperty(ColorId)) block.SetColor(ColorId, c);
                if (m.HasProperty(BaseColorId)) block.SetColor(BaseColorId, c);
            }
            e.renderer.SetPropertyBlock(block);
        }
    }
}
