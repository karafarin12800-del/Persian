using System.Collections;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Lightweight visual pass inspired by the free SimplePoly-style city look.
    /// It keeps gameplay unchanged and uses shared runtime materials for Android.
    /// </summary>
    public sealed class StylizedCityUpgrade : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToEnvironment()
        {
            EnvironmentSpawner[] spawners = Object.FindObjectsByType<EnvironmentSpawner>(FindObjectsSortMode.None);
            foreach (EnvironmentSpawner spawner in spawners)
            {
                if (spawner != null && spawner.GetComponent<StylizedCityUpgrade>() == null)
                    spawner.gameObject.AddComponent<StylizedCityUpgrade>();
            }
        }

        private static readonly Color[] FacadePalette =
        {
            new Color(0.72f, 0.64f, 0.52f),
            new Color(0.62f, 0.68f, 0.70f),
            new Color(0.78f, 0.70f, 0.58f),
            new Color(0.55f, 0.63f, 0.66f)
        };

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            BuildPresentationPass();
        }

        private void BuildPresentationPass()
        {
            if (transform.Find("StylizedCityPresentation") != null)
                return;

            var root = new GameObject("StylizedCityPresentation").transform;
            root.SetParent(transform, false);

            AddBuildingAccents(root);
            AddStreetFurniture(root);
            AddRoadMarkings(root);
        }

        private static void AddBuildingAccents(Transform root)
        {
            GameObject[] buildings = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            int index = 0;
            foreach (GameObject go in buildings)
            {
                if (go == null || !go.name.StartsWith("Building_") || go.transform.parent != null)
                    continue;

                Renderer facade = FindRenderer(go.transform, "Facade");
                if (facade != null)
                    facade.sharedMaterial = RuntimeMaterialFactory.Create("CityFacade_" + index, FacadePalette[index % FacadePalette.Length]);

                float width = facade != null ? facade.transform.localScale.x : 14f;
                float depth = facade != null ? facade.transform.localScale.z : 14f;

                var awning = GameObject.CreatePrimitive(PrimitiveType.Cube);
                awning.name = "StorefrontAwning";
                awning.transform.SetParent(root, false);
                awning.transform.position = go.transform.position + new Vector3(0f, 2.5f, -depth * 0.56f);
                awning.transform.localScale = new Vector3(Mathf.Min(width * 0.55f, 6f), 0.18f, 1.25f);
                awning.GetComponent<Renderer>().sharedMaterial =
                    RuntimeMaterialFactory.Create("CityAccent", new Color(0.20f, 0.34f, 0.38f));

                if (++index >= 28)
                    break;
            }
        }

        private static void AddStreetFurniture(Transform root)
        {
            Material dark = RuntimeMaterialFactory.Create("StreetMetal", new Color(0.16f, 0.18f, 0.18f));
            Material warm = RuntimeMaterialFactory.Create("StreetLamp", new Color(0.90f, 0.76f, 0.45f));

            float[] positions = { -73f, -36.5f, 0f, 36.5f, 73f };
            int id = 0;
            foreach (float p in positions)
            {
                CreateLamp(root, new Vector3(p, 0f, -94f), dark, warm, id++);
                CreateLamp(root, new Vector3(p, 0f, 94f), dark, warm, id++);
                CreateLamp(root, new Vector3(-94f, 0f, p), dark, warm, id++);
                CreateLamp(root, new Vector3(94f, 0f, p), dark, warm, id++);
            }
        }

        private static void CreateLamp(Transform root, Vector3 position, Material pole, Material lamp, int id)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "StreetLamp_" + id;
            post.transform.SetParent(root, false);
            post.transform.position = position + Vector3.up * 2.0f;
            post.transform.localScale = new Vector3(0.16f, 2.0f, 0.16f);
            post.GetComponent<Renderer>().sharedMaterial = pole;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "LampHead";
            head.transform.SetParent(root, false);
            head.transform.position = position + Vector3.up * 4.05f;
            head.transform.localScale = Vector3.one * 0.38f;
            head.GetComponent<Renderer>().sharedMaterial = lamp;
        }

        private static void AddRoadMarkings(Transform root)
        {
            Material marking = RuntimeMaterialFactory.Create("RoadMarking", new Color(0.86f, 0.78f, 0.52f));
            float[] lines = { -73f, -36.5f, 0f, 36.5f, 73f };
            int id = 0;
            foreach (float p in lines)
            {
                for (int i = -4; i <= 4; i++)
                {
                    CreateMark(root, new Vector3(p, 0.04f, i * 20f),
                        new Vector3(0.22f, 0.02f, 6f), marking, id++);
                    CreateMark(root, new Vector3(i * 20f, 0.041f, p),
                        new Vector3(6f, 0.02f, 0.22f), marking, id++);
                }
            }
        }

        private static void CreateMark(Transform root, Vector3 position, Vector3 scale, Material material, int id)
        {
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = "RoadMark_" + id;
            mark.transform.SetParent(root, false);
            mark.transform.position = position;
            mark.transform.localScale = scale;
            mark.GetComponent<Renderer>().sharedMaterial = material;

            Collider collider = mark.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        private static Renderer FindRenderer(Transform root, string objectName)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                if (renderer != null && renderer.gameObject.name == objectName)
                    return renderer;
            return null;
        }
    }
}