using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class EnvironmentSpawner : MonoBehaviour
    {
        [SerializeField] private int buildings = 24;
        [SerializeField] private int trees = 40;
        [SerializeField] private float area = 520f;

        private void Start()
        {
            SpawnBuildings();
            SpawnTrees();
        }

        private void SpawnBuildings()
        {
            for (int i = 0; i < buildings; i++)
            {
                Vector3 p = RandomPoint();
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Building_" + i;
                b.transform.position = new Vector3(p.x, Random.Range(2.5f, 5f), p.z);
                b.transform.localScale = new Vector3(Random.Range(5f, 12f), Random.Range(5f, 10f), Random.Range(5f, 12f));
            }
        }

        private void SpawnTrees()
        {
            for (int i = 0; i < trees; i++)
            {
                Vector3 p = RandomPoint();
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "Tree_" + i;
                trunk.transform.position = new Vector3(p.x, 1.2f, p.z);
                trunk.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f);

                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "TreeCrown_" + i;
                crown.transform.position = new Vector3(p.x, 3f, p.z);
                crown.transform.localScale = Vector3.one * 2.5f;
            }
        }

        private Vector3 RandomPoint()
        {
            return new Vector3(Random.Range(-area, area), 0f, Random.Range(-area, area));
        }
    }
}
