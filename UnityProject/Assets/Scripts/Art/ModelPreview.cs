using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Renders the REAL shared models (the same meshes used in-game) into small
    /// RenderTextures so the store / menus can draw them with GUI.DrawTexture.
    /// Cheap by design: only previews requested in the last 0.25 s are rendered, at
    /// ~20 fps, 192x192, and idle previews are released after 2.5 s.
    /// The preview rig lives far below the playfield on layer 31 and uses its own
    /// orthographic camera, so it can never appear in gameplay cameras.
    /// </summary>
    public sealed class ModelPreview : MonoBehaviour
    {
        private const int PreviewLayer = 31;
        private const float RenderInterval = 0.05f;
        private const float VisibleWindow = 0.25f;
        private const float ReleaseAfter = 2.5f;

        private sealed class Entry
        {
            public ModelKind kind;
            public RenderTexture texture;
            public GameObject pivot;
            public float radius;
            public float lastRequest;
            public float lastRender;
            public float angle;
        }

        private sealed class CharacterEntry
        {
            public int heroIndex;
            public RenderTexture texture;
            public GameObject pivot;
            public float lastRequest;
            public float lastRender;
            public float angle;
        }

        private static ModelPreview instance;

        private readonly List<Entry> entries = new List<Entry>(8);
        private readonly List<CharacterEntry> characterEntries = new List<CharacterEntry>(6);
        private Camera previewCamera;
        private Transform rig;

        public static Texture Get(ModelKind kind, int size)
        {
            if (kind == ModelKind.None) return null;
            ModelPreview preview = Ensure();
            if (preview == null) return null;
            Entry entry = preview.FindOrCreate(kind, Mathf.Clamp(size, 64, 512));
            if (entry == null) return null;
            entry.lastRequest = Time.unscaledTime;
            return entry.texture;
        }

        /// <summary>IMGUI helper: call from OnGUI; draws only on Repaint.</summary>
        public static void Draw(Rect rect, ModelKind kind)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            Texture texture = Get(kind, 192);
            if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        }

        /// <summary>Draws the real mobile 3D hero rig inside a character selection card.</summary>
        public static void DrawHero(Rect rect, int heroIndex)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            ModelPreview preview = Ensure();
            if (preview == null) return;
            CharacterEntry entry = preview.FindOrCreateHero(Mathf.Clamp(heroIndex, 0, 5));
            if (entry == null) return;
            entry.lastRequest = Time.unscaledTime;
            if (entry.texture != null)
                GUI.DrawTexture(rect, entry.texture, ScaleMode.ScaleToFit, true);
        }

        public static void ReleaseAll()
        {
            if (instance == null) return;
            instance.ReleaseEverything();
        }

        private static ModelPreview Ensure()
        {
            if (instance != null) return instance;
            GameObject go = new GameObject("PW_ModelPreview");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ModelPreview>();
            instance.BuildRig();
            return instance;
        }

        private void BuildRig()
        {
            GameObject rigObject = new GameObject("PW_PreviewRig");
            rigObject.transform.SetParent(transform, false);
            rigObject.transform.position = new Vector3(0f, -4000f, 0f);
            rig = rigObject.transform;

            GameObject cameraObject = new GameObject("PW_PreviewCamera");
            cameraObject.transform.SetParent(rig, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.orthographic = true;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 40f;
            previewCamera.allowMSAA = false;
            previewCamera.allowHDR = false;
            previewCamera.depth = -100f;

            GameObject keyLightObject = new GameObject("PW_PreviewKeyLight");
            keyLightObject.transform.SetParent(rig, false);
            keyLightObject.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.18f;
            keyLight.color = new Color(1f, 0.94f, 0.82f);
            keyLight.shadows = LightShadows.None;
            keyLight.cullingMask = 1 << PreviewLayer;

            GameObject fillLightObject = new GameObject("PW_PreviewFillLight");
            fillLightObject.transform.SetParent(rig, false);
            fillLightObject.transform.rotation = Quaternion.Euler(18f, 145f, 0f);
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.42f;
            fillLight.color = new Color(0.48f, 0.68f, 1f);
            fillLight.shadows = LightShadows.None;
            fillLight.cullingMask = 1 << PreviewLayer;
        }

        private Entry FindOrCreate(ModelKind kind, int size)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry existing = entries[i];
                if (existing.kind == kind && existing.texture != null && existing.pivot != null)
                    return existing;
            }

            Mesh mesh = ModelLibrary.GetMesh(kind);
            if (mesh == null || rig == null) return null;

            Entry entry = new Entry();
            entry.kind = kind;
            entry.texture = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32);
            entry.texture.antiAliasing = 1;
            entry.texture.Create();
            entry.radius = Mathf.Max(0.15f, mesh.bounds.extents.magnitude);

            entry.pivot = new GameObject("PW_PreviewPivot_" + kind);
            entry.pivot.transform.SetParent(rig, false);
            entry.pivot.transform.localPosition = new Vector3(entries.Count * 6f, 0f, 20f);
            GameObject model = ModelLibrary.Spawn(kind, entry.pivot.transform,
                -mesh.bounds.center, Quaternion.identity, 1f);
            SetLayerRecursive(entry.pivot, PreviewLayer);
            if (model != null) model.transform.localPosition = -mesh.bounds.center;

            entry.lastRequest = Time.unscaledTime;
            entry.lastRender = 0f;
            entries.Add(entry);
            return entry;
        }

        private CharacterEntry FindOrCreateHero(int heroIndex)
        {
            for (int i = 0; i < characterEntries.Count; i++)
            {
                CharacterEntry existing = characterEntries[i];
                if (existing.heroIndex == heroIndex && existing.texture != null && existing.pivot != null)
                    return existing;
            }
            if (rig == null || previewCamera == null) return null;

            CharacterEntry entry = new CharacterEntry();
            entry.heroIndex = heroIndex;
            entry.texture = new RenderTexture(160, 220, 16, RenderTextureFormat.ARGB32);
            entry.texture.antiAliasing = 1;
            entry.texture.Create();

            entry.pivot = new GameObject("PW_PreviewHeroPivot_" + heroIndex);
            entry.pivot.transform.SetParent(rig, false);
            int slot = entries.Count + characterEntries.Count;
            entry.pivot.transform.localPosition = new Vector3(slot * 6f, 0f, 22f);

            GameObject owner = new GameObject("PW_PreviewHeroOwner_" + heroIndex);
            owner.transform.SetParent(entry.pivot.transform, false);
            StylizedCharacterVisual visual = StylizedCharacterVisual.Attach(owner.transform, true, 1);
            visual.ConfigurePlayerHeroPreview(heroIndex, previewCamera);

            SetLayerRecursive(entry.pivot, PreviewLayer);
            entry.lastRequest = Time.unscaledTime;
            entry.lastRender = 0f;
            characterEntries.Add(entry);
            return entry;
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
        }

        private void LateUpdate()
        {
            if (previewCamera == null) return;
            float now = Time.unscaledTime;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry entry = entries[i];
                if (entry.texture == null || entry.pivot == null || now - entry.lastRequest > ReleaseAfter)
                {
                    Release(entry);
                    entries.RemoveAt(i);
                    continue;
                }
                if (now - entry.lastRequest > VisibleWindow) continue;
                if (now - entry.lastRender < RenderInterval) continue;

                float dt = entry.lastRender <= 0f ? 0f : Mathf.Min(now - entry.lastRender, 0.2f);
                entry.angle += dt * 38f;
                entry.lastRender = now;
                RenderEntry(entry);
            }

            for (int i = characterEntries.Count - 1; i >= 0; i--)
            {
                CharacterEntry entry = characterEntries[i];
                if (entry.texture == null || entry.pivot == null || now - entry.lastRequest > ReleaseAfter)
                {
                    Release(entry);
                    characterEntries.RemoveAt(i);
                    continue;
                }
                if (now - entry.lastRequest > VisibleWindow || now - entry.lastRender < RenderInterval)
                    continue;

                float dt = entry.lastRender <= 0f ? 0f : Mathf.Min(now - entry.lastRender, 0.2f);
                entry.angle += dt * 24f;
                entry.lastRender = now;
                RenderHeroEntry(entry);
            }
        }

        private void RenderHeroEntry(CharacterEntry entry)
        {
            Transform pivot = entry.pivot.transform;
            pivot.localRotation = Quaternion.Euler(0f, entry.angle, 0f);
            Transform cameraTransform = previewCamera.transform;
            cameraTransform.position = pivot.position + new Vector3(0.75f, 1.68f, -5.4f);
            cameraTransform.LookAt(pivot.position + Vector3.up * 0.91f);
            previewCamera.orthographicSize = 1.48f;
            previewCamera.targetTexture = entry.texture;
            previewCamera.Render();
            previewCamera.targetTexture = null;
        }

        private void RenderEntry(Entry entry)
        {
            Transform pivot = entry.pivot.transform;
            pivot.localRotation = Quaternion.Euler(0f, entry.angle, 0f);

            Vector3 direction = new Vector3(0.75f, 0.60f, -0.85f).normalized;
            Transform cameraTransform = previewCamera.transform;
            cameraTransform.position = pivot.position + direction * 12f;
            cameraTransform.LookAt(pivot.position);
            previewCamera.orthographicSize = entry.radius * 1.08f;
            previewCamera.targetTexture = entry.texture;
            previewCamera.Render();
            previewCamera.targetTexture = null;
        }

        private static void Release(CharacterEntry entry)
        {
            if (entry == null) return;
            if (entry.texture != null)
            {
                entry.texture.Release();
                Destroy(entry.texture);
                entry.texture = null;
            }
            if (entry.pivot != null) Destroy(entry.pivot);
            entry.pivot = null;
        }

        private static void Release(Entry entry)
        {
            if (entry == null) return;
            if (entry.texture != null)
            {
                entry.texture.Release();
                Destroy(entry.texture);
                entry.texture = null;
            }
            if (entry.pivot != null) Destroy(entry.pivot);
            entry.pivot = null;
        }

        private void ReleaseEverything()
        {
            for (int i = 0; i < entries.Count; i++) Release(entries[i]);
            entries.Clear();
            for (int i = 0; i < characterEntries.Count; i++) Release(characterEntries[i]);
            characterEntries.Clear();
        }

        private void OnDestroy()
        {
            ReleaseEverything();
            if (instance == this) instance = null;
        }
    }
}
