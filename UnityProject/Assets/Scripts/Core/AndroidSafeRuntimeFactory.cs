using UnityEngine;
using UnityEngine.Rendering;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Runtime Android objects deliberately avoid texture-backed SpriteRenderer objects.
    /// </summary>
    internal static class AndroidSafeRuntimeFactory
    {
        private static Mesh markerMesh;

        private static Mesh MarkerMesh
        {
            get
            {
                if (markerMesh != null) return markerMesh;
                markerMesh = new Mesh { name = "AndroidSafeMarkerMesh" };
                markerMesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                    new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                    new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                    new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
                };
                markerMesh.triangles = new[]
                {
                    0,2,1, 0,3,2,
                    4,5,6, 4,6,7,
                    0,1,5, 0,5,4,
                    2,3,7, 2,7,6,
                    0,4,7, 0,7,6,
                    1,2,6, 1,6,5
                };
                markerMesh.RecalculateNormals();
                markerMesh.RecalculateBounds();
                return markerMesh;
            }
        }

        public static GameObject CreateMarker(string objectName, Vector3 position, Vector3 scale, Color tint, bool triggerCollider)
        {
            GameObject obj = new GameObject(objectName);
            obj.transform.position = position;
            obj.transform.localScale = scale;

            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = MarkerMesh;

            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = RuntimeMaterialFactory.Create("AndroidMarker", tint);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            if (triggerCollider)
            {
                SphereCollider collider = obj.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 0.7f;

                Rigidbody body = obj.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            return obj;
        }

        public static GameObject CreateProjectile(string objectName, Vector3 position, Quaternion rotation, Vector3 scale, Color tint)
        {
            GameObject obj = CreateMarker(objectName, position, scale, tint, true);
            obj.transform.rotation = rotation;
            return obj;
        }
    }
}
