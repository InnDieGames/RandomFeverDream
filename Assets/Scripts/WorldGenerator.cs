using UnityEngine;

namespace WorldGen
{
    public class WorldGenerator : MonoBehaviour
    {
        [Header("Generation Settings")]
        [SerializeField] private GameObject cubePrefab;
        [SerializeField] private int width = 30;
        [SerializeField] private int depth = 30;
        [SerializeField] private float heightMultiplier = 10f;
        [SerializeField] private float noiseScale = 0.1f;
        [SerializeField] private float offsetX;
        [SerializeField] private float offsetZ;

        private void Awake()
        {
            offsetX = Random.Range(0f, 9999f);
            offsetZ = Random.Range(0f, 9999f);
        }

        private void Start()
        {
            GenerateWorld();
        }

        [ContextMenu("Regenerate World")]
        public void GenerateWorld()
        {
            ClearWorld();

            if (cubePrefab == null)
            {
                Debug.LogError("WorldGenerator: Cube Prefab is not assigned!");
                return;
            }

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    int height = Mathf.FloorToInt(CalculateHeight(x, z));
                    
                    // Fill from 0 up to height to give it a solid look
                    for (int y = 0; y <= height; y++)
                    {
                        Vector3 pos = new Vector3(x, y, z);
                        GameObject cube = Instantiate(cubePrefab, pos, Quaternion.identity, transform);
                        cube.name = $"Cube_{x}_{y}_{z}";
                    }
                }
            }
        }

        private float CalculateHeight(int x, int z)
        {
            float xCoord = x * noiseScale + offsetX;
            float zCoord = z * noiseScale + offsetZ;

            return Mathf.PerlinNoise(xCoord, zCoord) * heightMultiplier;
        }

        public void ClearWorld()
        {
            // Use a reverse loop so the index stays valid as you remove items
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}
