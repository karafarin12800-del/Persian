using UnityEngine;
using PersiaWar.Unity2D5D;

namespace PersiaWar.Unity2D5D.UI.Loadout
{
    public static class WeaponPreviewStudio
    {
        private const int Layer = 31;
        private static readonly RenderTexture[] Targets = new RenderTexture[6];
        private static readonly GameObject[] Roots = new GameObject[6];
        private static readonly Camera[] Cameras = new Camera[6];
        private static readonly float[] LastRender = new float[6];

        public static bool Draw(Rect rect, WeaponController.WeaponKind kind, bool selected)
        {
            if (GraphicsQualityProfile.CurrentTier == GraphicsQualityProfile.QualityTier.Low) return false;
            int i = WeaponCatalog.IndexOf(kind);
            Ensure(i, kind);
            if (Targets[i] == null) return false;
            if (selected && Time.unscaledTime - LastRender[i] > 0.15f)
            {
                Roots[i].transform.rotation = Quaternion.Euler(0f, Mathf.Sin(Time.unscaledTime * 0.55f) * 9f, 0f);
                Cameras[i].Render();
                LastRender[i] = Time.unscaledTime;
            }
            GUI.DrawTexture(rect, Targets[i], ScaleMode.ScaleToFit, true);
            return true;
        }

        private static void Ensure(int i, WeaponController.WeaponKind kind)
        {
            if (Targets[i] != null && Roots[i] != null && Cameras[i] != null) return;
            Vector3 origin = new Vector3(12000f + i * 10f, -12000f, 0f);
            GameObject root = new GameObject("PW_WeaponPreview_" + i);
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = origin;
            GameObject model = new GameObject("PreviewWeaponRoot");
            model.hideFlags = HideFlags.HideAndDontSave;
            model.transform.SetParent(root.transform, false);
            WeaponModelLibrary.Refresh(model.transform, kind);
            Roots[i] = root;
            GameObject lightGo = new GameObject("PW_WeaponPreviewLight_" + i);
            lightGo.hideFlags = HideFlags.HideAndDontSave;
            lightGo.layer = Layer;
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.color = new Color(1f, .88f, .70f);
            lightGo.transform.rotation = Quaternion.Euler(32f, 25f, 0f);
            lightGo.transform.SetParent(root.transform, false);
            GameObject cameraGo = new GameObject("PW_WeaponPreviewCamera_" + i);
            cameraGo.hideFlags = HideFlags.HideAndDontSave;
            cameraGo.layer = Layer;
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = .95f;
            camera.aspect = 4f / 3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.018f, .045f, .075f, 1f);
            camera.cullingMask = 1 << Layer;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 20f;
            cameraGo.transform.localPosition = new Vector3(0f, .35f, -2.8f);
            cameraGo.transform.LookAt(root.transform.position + new Vector3(0f, .1f, 0f));
            cameraGo.transform.SetParent(root.transform, true);
            SetLayer(root.transform);
            Cameras[i] = camera;
            RenderTexture target = new RenderTexture(256, 192, 16, RenderTextureFormat.ARGB32);
            target.name = "PW_WeaponPreviewRT_" + i;
            target.Create();
            Targets[i] = target;
            camera.targetTexture = target;
            camera.Render();
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