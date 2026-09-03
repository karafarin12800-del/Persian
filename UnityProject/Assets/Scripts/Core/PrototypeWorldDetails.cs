using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>Additional low-cost landmarks that make the prototype read like a complete town.</summary>
    public sealed class PrototypeWorldDetails : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BuildDetails()
        {
            if (GameObject.Find("PrototypeWorldDetails") != null) return;
            GameObject world = GameObject.Find("BattleRoyaleCity");
            if (world == null) return;

            GameObject root = new GameObject("PrototypeWorldDetails");
            root.transform.SetParent(world.transform, true);
            PrototypeWorldDetails builder = root.AddComponent<PrototypeWorldDetails>();
            builder.Build(root.transform);
        }

        private void Build(Transform root)
        {
            Material blue = RuntimeMaterialFactory.Create("PoliceBlue", new Color(0.10f, 0.34f, 0.58f));
            Material white = RuntimeMaterialFactory.Create("PoliceWhite", new Color(0.82f, 0.86f, 0.88f));
            Material red = RuntimeMaterialFactory.Create("BarnRed", new Color(0.64f, 0.12f, 0.08f));
            Material warehouse = RuntimeMaterialFactory.Create("WarehouseOrange", new Color(0.72f, 0.40f, 0.12f));
            Material concrete = RuntimeMaterialFactory.Create("Concrete", new Color(0.42f, 0.45f, 0.46f));
            Material grass = RuntimeMaterialFactory.Create("Park", new Color(0.24f, 0.56f, 0.18f));

            CreateBuilding(root, new Vector3(58f, 0f, 38f), new Vector3(16f, 5f, 12f), blue, white, "POLICE STATION");
            CreateBuilding(root, new Vector3(-54f, 0f, -52f), new Vector3(18f, 4.5f, 14f), warehouse, concrete, "WAREHOUSE");
            CreateBuilding(root, new Vector3(48f, 0f, 58f), new Vector3(13f, 5.8f, 12f), red, new Material(red), "FARM HOUSE");

            CreatePark(root, new Vector3(-46f, -0.01f, 36f), new Vector2(16f, 12f), grass);
            CreateParking(root, new Vector3(54f, -0.01f, -44f), new Vector2(18f, 12f), concrete);
            CreateCrates(root, new Vector3(20f, 0.4f, -52f));
        }

        private void CreateBuilding(Transform root, Vector3 position, Vector3 size, Material wall, Material trim, string label)
        {
            GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = label.Replace(' ', '_');
            building.transform.SetParent(root, true);
            building.transform.position = position + Vector3.up * (size.y * 0.5f);
            building.transform.localScale = size;
            building.GetComponent<Renderer>().sharedMaterial = wall;

            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = label + "Roof";
            roof.transform.SetParent(root, true);
            roof.transform.position = position + Vector3.up * (size.y + 0.2f);
            roof.transform.localScale = new Vector3(size.x + 0.4f, 0.35f, size.z + 0.4f);
            roof.GetComponent<Renderer>().sharedMaterial = trim;

            CreateBox(root, label + "Door", position + new Vector3(0f, 0.95f, -size.z * 0.51f), new Vector3(1.2f, 1.9f, 0.12f), trim, false);
            CreateBox(root, label + "Sign", position + new Vector3(0f, size.y + 1.25f, -size.z * 0.52f), new Vector3(Mathf.Min(5.5f, size.x * 0.45f), 0.55f, 0.12f), trim, false);
        }

        private void CreatePark(Transform root, Vector3 center, Vector2 size, Material grass)
        {
            CreateBox(root, "Park", center, new Vector3(size.x, 0.12f, size.y), grass, false);
            for (int i = 0; i < 6; i++)
            {
                float x = center.x + Random.Range(-size.x * 0.42f, size.x * 0.42f);
                float z = center.z + Random.Range(-size.y * 0.42f, size.y * 0.42f);
                CreateBox(root, "ParkBush", new Vector3(x, 0.65f, z), Vector3.one * Random.Range(0.8f, 1.5f), grass, false);
            }
        }

        private void CreateParking(Transform root, Vector3 center, Vector2 size, Material concrete)
        {
            CreateBox(root, "Parking", center, new Vector3(size.x, 0.08f, size.y), concrete, false);
            for (int i = 0; i < 4; i++)
            {
                float x = center.x - size.x * 0.30f + i * size.x * 0.20f;
                CreateBox(root, "ParkingLine", new Vector3(x, 0.06f, center.z), new Vector3(0.15f, 0.03f, size.y * 0.72f), RuntimeMaterialFactory.Create("ParkingLine" + i, new Color(0.86f, 0.82f, 0.64f)), false);
            }
        }

        private void CreateCrates(Transform root, Vector3 center)
        {
            Material crate = RuntimeMaterialFactory.Create("Crates", new Color(0.78f, 0.56f, 0.18f));
            for (int i = 0; i < 8; i++)
            {
                int row = i / 4;
                int col = i % 4;
                Vector3 p = center + new Vector3(col * 1.15f, row * 0.9f, (row % 2) * 1.1f);
                CreateBox(root, "Crate", p, Vector3.one * 1.0f, crate, true);
            }
        }

        private GameObject CreateBox(Transform root, string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(root, true);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            if (!collider)
            {
                Collider c = obj.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            return obj;
        }
    }
}
