using UnityEngine;
using PersiaWar.Unity2D5D;

namespace PersiaWar.Unity2D5D.UI.Loadout
{
    public static class HeroPortraitStudio
    {
        private const int Layer = 31;
        private static readonly RenderTexture[] Targets = new RenderTexture[6];
        private static readonly GameObject[] Roots = new GameObject[6];
        private static readonly Transform[] ModelRoots = new Transform[6];
        private static readonly Camera[] Cameras = new Camera[6];
        private static readonly float[] LastRender = new float[6];
        private static GameObject SharedLight;

        public static void Draw(Rect rect, int index, bool selected)
        {
            int i = Mathf.Clamp(index, 0, 5);
            Ensure(i);
            if (Targets[i] == null || ModelRoots[i] == null || Cameras[i] == null) return;

            // Rotate only the rendered hero. Camera and shared light remain fixed.
            if (selected && Time.unscaledTime - LastRender[i] > 0.15f)
            {
                ModelRoots[i].localRotation = Quaternion.Euler(
                    0f, Mathf.Sin(Time.unscaledTime * 0.45f) * 7f, 0f);
                Cameras[i].Render();
                LastRender[i] = Time.unscaledTime;
            }
            GUI.DrawTexture(rect, Targets[i], ScaleMode.ScaleToFit, true);
        }

        private static void Ensure(int i)
        {
            if (Targets[i] != null && Roots[i] != null && Cameras[i] != null && ModelRoots[i] != null)
                return;

            Vector3 origin = new Vector3(10000f + i * 12f, -10000f, 0f);
            GameObject root = new GameObject("PW_HeroPortrait_" + i);
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = origin;
            Roots[i] = root;

            GameObject model = new GameObject("HeroPortraitModel_" + i);
            model.hideFlags = HideFlags.HideAndDontSave;
            model.transform.SetParent(root.transform, false);
            ModelRoots[i] = model.transform;

            StylizedCharacterVisual visual = StylizedCharacterVisual.Attach(ModelRoots[i], true, 1);
            visual.ConfigurePortraitModel(i);
            SetLayer(ModelRoots[i]);
            EnsureSharedLight(origin);

            GameObject camGo = new GameObject("PW_PortraitCamera_" + i);
            camGo.hideFlags = HideFlags.HideAndDontSave;
            camGo.layer = Layer;
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = 1.45f;
            cam.aspect = 2f / 3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.018f, .045f, .075f, 1f);
            cam.cullingMask = 1 << Layer;
            cam.nearClipPlane = .05f;
            cam.farClipPlane = 20f;
            cam.transform.position = origin + new Vector3(0f, 1.4f, -4.4f);
            cam.transform.LookAt(origin + new Vector3(0f, 1.05f, 0f));
            Cameras[i] = cam;

            RenderTexture target = new RenderTexture(256, 384, 16, RenderTextureFormat.ARGB32);
            target.name = "PW_HeroPortraitRT_" + i;
            target.Create();
            Targets[i] = target;
            cam.targetTexture = target;
            cam.Render();
            LastRender[i] = Time.unscaledTime;
        }

        private static void EnsureSharedLight(Vector3 origin)
        {
            if (SharedLight != null) return;

            SharedLight = new GameObject("PW_PortraitSharedLight");
            SharedLight.hideFlags = HideFlags.HideAndDontSave;
            SharedLight.layer = Layer;
            SharedLight.transform.position = origin + new Vector3(-2f, 4f, -3f);
            SharedLight.transform.rotation = Quaternion.Euler(35f, 35f, 0f);

            Light light = SharedLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, .86f, .68f);
            light.cullingMask = 1 << Layer;
        }

        private static void SetLayer(Transform root)
        {
            root.gameObject.layer = Layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayer(root.GetChild(i));
        }

        public static void Close()
        {
            for (int i = 0; i < 6; i++)
            {
                if (Cameras[i] != null)
                {
                    Cameras[i].targetTexture = null;
                    Object.Destroy(Cameras[i].gameObject);
                }
                if (Targets[i] != null)
                {
                    Targets[i].Release();
                    Object.Destroy(Targets[i]);
                    Targets[i] = null;
                }
                if (Roots[i] != null) Object.Destroy(Roots[i]);
                Cameras[i] = null;
                Roots[i] = null;
                ModelRoots[i] = null;
                LastRender[i] = 0f;
            }
            if (SharedLight != null) Object.Destroy(SharedLight);
            SharedLight = null;
        }
    }
}