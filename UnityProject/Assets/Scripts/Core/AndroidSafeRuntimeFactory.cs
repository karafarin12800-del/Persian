using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Small-object factory for the Android combat path. It reuses an imported Sprite
    /// and explicitly creates only the components required by the gameplay scripts.
    /// </summary>
    internal static class AndroidSafeRuntimeFactory
    {
        private static Sprite markerSprite;

        private static Sprite MarkerSprite
        {
            get
            {
                if (markerSprite == null)
                    markerSprite = Resources.Load<Sprite>("PersianCharacters/Enemy_03");
                if (markerSprite == null)
                    markerSprite = Resources.Load<Sprite>("PersianCharacters/Hero");
                return markerSprite;
            }
        }

        public static GameObject CreateMarker(
            string objectName,
            Vector3 position,
            Vector3 scale,
            Color tint,
            bool triggerCollider)
        {
            GameObject obj = new GameObject(objectName);
            obj.transform.position = position;
            obj.transform.localScale = scale;

            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = MarkerSprite;
            renderer.color = tint;
            renderer.sortingOrder = 15;

            if (triggerCollider)
            {
                SphereCollider collider = obj.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 0.7f;

                Rigidbody body = obj.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }

            return obj;
        }

        public static GameObject CreateProjectile(
            string objectName,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Color tint)
        {
            GameObject obj = CreateMarker(objectName, position, scale, tint, true);
            obj.transform.rotation = rotation;

            Rigidbody body = obj.GetComponent<Rigidbody>();
            if (body == null)
                body = obj.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            TrailRenderer trail = obj.AddComponent<TrailRenderer>();
            trail.time = 0.08f;
            trail.minVertexDistance = 0.03f;
            trail.startWidth = Mathf.Max(0.035f, scale.x * 0.55f);
            trail.endWidth = 0f;
            trail.material = RuntimeMaterialFactory.Create(objectName + "TrailMaterial", tint);
            trail.startColor = tint;
            trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return obj;
        }
    }
}
