using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Lightweight bridge for the optional SimplePoly City Asset Store package.
    /// The package is intentionally optional: if its prefabs are not imported,
    /// the existing Android city presentation remains the fallback.
    /// 
    /// Put selected SimplePoly prefabs under:
    /// Assets/Resources/SimplePolyCity/
    /// 
    /// Name prefabs with one of the supported roles:
    /// House_, Residential_, Commercial_, Industrial_, Road_, Landmark_, Prop_, Tree_
    /// </summary>
    public static class SimplePolyCityAdapter
    {
        private const string ResourceRoot = "SimplePolyCity";

        public static bool TryBuild(Transform worldRoot, float worldSize, int seed)
        {
            if (worldRoot == null)
                return false;

            GameObject[] prefabs = Resources.LoadAll<GameObject>(ResourceRoot);
            if (prefabs == null || prefabs.Length == 0)
            {
                StartupCheckpoint.Set("SimplePolyCityNotImported");
                return false;
            }

            List<GameObject> houses = new List<GameObject>();
            List<GameObject> commercials = new List<GameObject>();
            List<GameObject> industrials = new List<GameObject>();
            List<GameObject> roads = new List<GameObject>();
            List<GameObject> props = new List<GameObject>();
            List<GameObject> trees = new List<GameObject>();

            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null) continue;
                string n = prefab.name.ToLowerInvariant();

                if (n.Contains("road")) roads.Add(prefab);
                else if (n.Contains("tree") || n.Contains("bush") || n.Contains("vegetation")) trees.Add(prefab);
                else if (n.Contains("factory") || n.Contains("industrial") || n.Contains("warehouse")) industrials.Add(prefab);
                else if (n.Contains("supermarket") || n.Contains("bakery") || n.Contains("pizza") ||
                         n.Contains("shop") || n.Contains("commercial")) commercials.Add(prefab);
                else if (n.Contains("house") || n.Contains("residential") || n.Contains("building")) houses.Add(prefab);
                else props.Add(prefab);
            }

            if (houses.Count == 0 && commercials.Count == 0 && industrials.Count == 0)
            {
                StartupCheckpoint.Set("SimplePolyCityNoUsableBuildings");
                return false;
            }

            Random.State oldState = Random.state;
            Random.InitState(seed);

            Transform root = new GameObject("SimplePolyCity_Lite").transform;
            root.SetParent(worldRoot, false);

            // Small first-pass footprint: deliberately bounded for Android stability.
            const int buildingCount = 10;
            const float blockSpacing = 24f;
            const float roadOffset = 12f;

            for (int i = 0; i < buildingCount; i++)
            {
                List<GameObject> source =
                    i < 7 ? houses :
                    i < 9 ? commercials :
                    industrials;

                if (source.Count == 0)
                    source = houses.Count > 0 ? houses :
                             commercials.Count > 0 ? commercials : industrials;

                if (source.Count == 0) continue;

                GameObject prefab = source[Random.Range(0, source.Count)];
                float x = ((i % 5) - 2) * blockSpacing + Random.Range(-3f, 3f);
                float z = ((i / 5) - 1) * blockSpacing + Random.Range(-3f, 3f);
                if (Mathf.Abs(x) < roadOffset) x += x < 0f ? -roadOffset : roadOffset;
                if (Mathf.Abs(z) < roadOffset) z += z < 0f ? -roadOffset : roadOffset;

                GameObject instance = Object.Instantiate(prefab, new Vector3(x, 0f, z),
                    Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f), root);

                // Keep the first pass lightweight: no runtime scaling and no added colliders.
                instance.name = "SPC_" + prefab.name + "_" + i;
            }

            // Only a handful of trees/props; these are visual accents, not gameplay objects.
            AddVisuals(trees, root, 6, worldSize, seed + 11);
            AddVisuals(props, root, 4, worldSize, seed + 23);

            Random.state = oldState;
            StartupCheckpoint.Set("SimplePolyCityLiteBuilt");
            return true;
        }

        private static void AddVisuals(List<GameObject> source, Transform root, int count, float worldSize, int seed)
        {
            if (source == null || source.Count == 0) return;

            Random.State oldState = Random.state;
            Random.InitState(seed);
            float half = Mathf.Min(worldSize * 0.45f, 82f);

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = source[Random.Range(0, source.Count)];
                Vector3 pos = new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
                Object.Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), root);
            }

            Random.state = oldState;
        }
    }
}
