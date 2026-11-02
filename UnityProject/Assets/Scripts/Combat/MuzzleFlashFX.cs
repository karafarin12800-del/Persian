using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Pooled muzzle flash: one tiny glowing mesh per muzzle that is switched on for ~50 ms.
    /// No instantiate/destroy per shot. Call MuzzleFlashFX.Show(muzzleTransform, isAlien).
    /// </summary>
    public sealed class MuzzleFlashFX : MonoBehaviour
    {
        private const string ChildName = "PW_MuzzleFlash";
        private float hideAt;
        private Renderer flashRenderer;

        public static void Show(Transform muzzle, bool alien)
        {
            if (muzzle == null) return;
            Transform existing = muzzle.Find(ChildName);
            MuzzleFlashFX fx = null;
            if (existing != null) fx = existing.GetComponent<MuzzleFlashFX>();
            if (fx == null)
            {
                ModelKind kind = alien ? ModelKind.MuzzleFlashAlien : ModelKind.MuzzleFlash;
                GameObject go = ModelLibrary.Spawn(kind, muzzle, Vector3.zero, Quaternion.identity, 1f);
                if (go == null) return;
                go.name = ChildName;
                fx = go.AddComponent<MuzzleFlashFX>();
                fx.flashRenderer = go.GetComponent<Renderer>();
            }
            fx.Trigger();
        }

        private void Trigger()
        {
            if (flashRenderer == null) return;
            transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float s = Random.Range(0.8f, 1.25f);
            transform.localScale = new Vector3(s, s, s);
            flashRenderer.enabled = true;
            hideAt = Time.time + 0.05f;
            enabled = true;
        }

        private void Update()
        {
            if (Time.time < hideAt) return;
            if (flashRenderer != null) flashRenderer.enabled = false;
            enabled = false;
        }
    }
}
