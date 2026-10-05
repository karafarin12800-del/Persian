using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Lightweight minimap for the 3D battlefield. It is independent from combat input.
    /// </summary>
    public sealed class AndroidMinimapOverlay : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private float worldSize = 192f;
        [SerializeField] private int textureSize = 192;
        [SerializeField] private float refreshInterval = 0.20f;

        private Camera mapCamera;
        private RenderTexture mapTexture;
        private Texture2D marker;
        private float nextRenderTime;

        public void Configure(Transform target)
        {
            player = target;
            Ensure();
        }

        private void Ensure()
        {
            if (mapCamera != null)
                return;

            GameObject cameraObject = new GameObject("3DMinimapCamera");
            mapCamera = cameraObject.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = worldSize * 0.5f;
            mapCamera.nearClipPlane = 0.1f;
            mapCamera.farClipPlane = 300f;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(0.07f, 0.10f, 0.08f, 1f);

            mapTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
            mapTexture.filterMode = FilterMode.Bilinear;
            mapCamera.targetTexture = mapTexture;
            // Render manually at a low refresh rate. The minimap should not pay for a
            // second full-city camera pass every frame on a mobile device.
            mapCamera.enabled = false;

            marker = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            marker.SetPixel(0, 0, Color.white);
            marker.Apply();
        }

        private void LateUpdate()
        {
            if (mapCamera == null || player == null)
                return;

            if (Time.unscaledTime < nextRenderTime)
                return;

            nextRenderTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            mapCamera.transform.position = player.position + Vector3.up * 120f;
            mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            mapCamera.Render();
        }

        private void OnGUI()
        {
            if (!Application.isMobilePlatform || mapTexture == null)
                return;

            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f);
            float size = 178f * scale;
            Rect rect = new Rect(Screen.width - size - 18f * scale, 18f * scale, size, size);

            GUI.DrawTexture(rect, mapTexture, ScaleMode.StretchToFill, false);
            Color old = GUI.color;
            GUI.color = new Color(0.95f, 0.72f, 0.18f, 0.95f);
            float markerSize = 10f * scale;
            GUI.DrawTexture(
                new Rect(rect.center.x - markerSize * 0.5f, rect.center.y - markerSize * 0.5f, markerSize, markerSize),
                marker);
            GUI.color = old;
            GUI.Box(rect, GUIContent.none);
        }

        private void OnDestroy()
        {
            if (mapTexture != null)
                mapTexture.Release();
            if (marker != null)
                Destroy(marker);
        }
    }
}
