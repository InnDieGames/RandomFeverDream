using UnityEngine;
using System.Collections.Generic;

namespace WorldGen
{
    public class WorldGenerator : MonoBehaviour
    {
        [Header("Generation Settings")]
        [SerializeField] private GameObject cubePrefab;
        [SerializeField] private int renderDistance = 20;
        [SerializeField] private float heightMultiplier = 10f;
        [SerializeField] private float noiseScale = 0.1f;
        
        [Header("Target")]
        [SerializeField] private Transform playerTransform;

        private float offsetX;
        private float offsetZ;
        private HashSet<Vector2Int> generatedColumns = new HashSet<Vector2Int>();

        private void Awake()
        {
            offsetX = Random.Range(0f, 9999f);
            offsetZ = Random.Range(0f, 9999f);
        }

        private void Start()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.Find("Player");
                if (player != null) playerTransform = player.transform;
            }
            
            // Clear any editor-generated world objects
            ClearWorld();
        }

        private void Update()
        {
            if (playerTransform == null) return;

            Vector3 playerPos = playerTransform.position;
            int playerX = Mathf.FloorToInt(playerPos.x);
            int playerZ = Mathf.FloorToInt(playerPos.z);
            
            for (int x = playerX - renderDistance; x <= playerX + renderDistance; x++)
            {
                for (int z = playerZ - renderDistance; z <= playerZ + renderDistance; z++)
                {
                    Vector2Int columnPos = new Vector2Int(x, z);
                    if (!generatedColumns.Contains(columnPos))
                    {
                        GenerateColumn(x, z);
                        generatedColumns.Add(columnPos);
                    }
                }
            }
        }

        private void GenerateColumn(int x, int z)
        {
            if (cubePrefab == null) return;

            int height = Mathf.FloorToInt(CalculateHeight(x, z));
            
            for (int y = 0; y <= height; y++)
            {
                Vector3 pos = new Vector3(x, y, z);
                GameObject cube = Instantiate(cubePrefab, pos, Quaternion.identity, transform);
                cube.name = $"Cube_{x}_{y}_{z}";
            }
        }

        private float CalculateHeight(int x, int z)
        {
            float xCoord = x * noiseScale + offsetX;
            float zCoord = z * noiseScale + offsetZ;

            return Mathf.PerlinNoise(xCoord, zCoord) * heightMultiplier;
        }

        [ContextMenu("Clear World")]
        public void ClearWorld()
        {
            generatedColumns.Clear();
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
