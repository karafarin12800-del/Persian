using UnityEngine;
using PersiaWar.Unity2D5D;

namespace PersiaWar.Unity2D5D.UI.Loadout
{
    public static class HeroPortraitStudio
    {
        private const int Layer = 31;
        private static readonly RenderTexture[] Targets = new RenderTexture[6];
        private static readonly GameObject[] Roots = new GameObject[6];
        private static readonly Camera[] Cameras = new Camera[6];
        private static readonly float[] LastRender = new float[6];

        public static void Draw(Rect rect, int index, bool selected)
        {
            int i = Mathf.Clamp(index, 0, 5);
            Ensure(i);
            if (Targets[i] == null) return;
            if (selected && Time.unscaledTime - LastRender[i] > 0.15f)
            {
                Roots[i].transform.rotation = Quaternion.Euler(0f, Mathf.Sin(Time.unscaledTime * 0.45f) * 7f, 0f);
                Cameras[i].Render();
                LastRender[i] = Time.unscaledTime;
            }
            GUI.DrawTexture(rect, Targets[i], ScaleMode.ScaleToFit, true);
        }

        private static void Ensure(int i)
        {
            if (Targets[i] != null && Roots[i] != null && Cameras[i] != null) return;
            Vector3 origin = new Vector3(10000f + i * 12f, -10000f, 0f);
            GameObject root = new GameObject("PW_HeroPortrait_" + i);
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = origin;
            StylizedCharacterVisual visual = StylizedCharacterVisual.Attach(root.transform, true, 1);
            visual.ConfigurePortraitModel(i);
            SetLayer(root.transform);
            Roots[i] = root;

            GameObject lightGo = new GameObject("PW_PortraitLight_" + i);
            lightGo.hideFlags = HideFlags.HideAndDontSave;
            lightGo.layer = Layer;
            lightGo.transform.position = origin + new Vector3(-2f, 4f, -3f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, .86f, .68f);
            lightGo.transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            lightGo.transform.SetParent(root.transform, true);

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
            camGo.transform.position = origin + new Vector3(0f, 1.4f, -4.4f);
            camGo.transform.LookAt(origin + new Vector3(0f, 1.05f, 0f));
            camGo.transform.SetParent(root.transform, true);
            Cameras[i] = cam;
            RenderTexture target = new RenderTexture(256, 384, 16, RenderTextureFormat.ARGB32);
            target.name = "PW_HeroPortraitRT_" + i;
            target.Create();
            Targets[i] = target;
            cam.targetTexture = target;
            cam.Render();
            LastRender[i] = Time.unscaledTime;
        }

        private static void SetLayer(Transform root)
        {
            root.gameObject.layer = Layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i));
        }

        public static void Close()
        {
            for (int i = 0; i < 6; i++)
            {
                if (Cameras[i] != null) Cameras[i].targetTexture = null;
                if (Targets[i] != null) { Targets[i].Release(); Object.Destroy(Targets[i]); Targets[i] = null; }
                if (Roots[i] != null) Object.Destroy(Roots[i]);
                Cameras[i] = null; Roots[i] = null; LastRender[i] = 0f;
            }
        }
    }
}