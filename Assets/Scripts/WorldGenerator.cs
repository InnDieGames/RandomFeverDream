using UnityEngine;
using System.Collections.Generic;

namespace WorldGen
{
    public class WorldGenerator : MonoBehaviour
    {
        [Header("Generation Settings")]
        [SerializeField] private GameObject cubePrefab;
        [SerializeField] private int renderDistance = 20;
        [SerializeField] private int unloadBuffer = 5;
        [SerializeField] private float heightMultiplier = 10f;
        [SerializeField] private float noiseScale = 0.1f;
        
        [Header("Target")]
        [SerializeField] private Transform playerTransform;

        private float offsetX;
        private float offsetZ;
        private Dictionary<Vector2Int, List<GameObject>> activeColumns = new();
        private float cleanupTimer = 0f;
        private float cleanupInterval = 0.5f;

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
            
            ClearWorld();
        }

        private void Update()
        {
            if (playerTransform == null) return;
            
            GenerateWorldNearPlayer();

            // Periodically clean up far columns
            cleanupTimer += Time.deltaTime;
            if (cleanupTimer >= cleanupInterval)
            {
                UnloadFarColumns();
                cleanupTimer = 0f;
            }
        }

        [ContextMenu("GenerateWorldNearPlayer")]
        private void GenerateWorldNearPlayerEditor()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.Find("Player");
                if (player != null) playerTransform = player.transform;
            }
            GenerateWorldNearPlayer();
        }
        
        private void GenerateWorldNearPlayer()
        {
            if (cubePrefab == null) return;
            
            Vector3 playerPos = playerTransform.position;
            int playerX = Mathf.FloorToInt(playerPos.x);
            int playerZ = Mathf.FloorToInt(playerPos.z);
            
            // Generate nearby columns
            for (int x = playerX - renderDistance; x <= playerX + renderDistance; x++)
            {
                for (int z = playerZ - renderDistance; z <= playerZ + renderDistance; z++)
                {
                    Vector2Int columnPos = new Vector2Int(x, z);
                    if (!activeColumns.ContainsKey(columnPos))
                    {
                        GenerateColumn(x, z);
                    }
                }
            }
        }

        private void GenerateColumn(int x, int z)
        {
            int height = Mathf.FloorToInt(CalculateHeight(x, z));
            List<GameObject> columnCubes = new List<GameObject>();
            
            for (int y = 0; y <= height; y++)
            {
                Vector3 pos = new Vector3(x, y, z);
                GameObject cube = Instantiate(cubePrefab, pos, Quaternion.identity, transform);
                cube.name = $"Cube_{x}_{y}_{z}";
                columnCubes.Add(cube);
            }

            activeColumns.Add(new Vector2Int(x, z), columnCubes);
        }

        private void UnloadFarColumns()
        {
            Vector3 playerPos = playerTransform.position;
            int playerX = Mathf.FloorToInt(playerPos.x);
            int playerZ = Mathf.FloorToInt(playerPos.z);
            
            float unloadDistanceSq = Mathf.Pow(renderDistance + unloadBuffer, 2);
            List<Vector2Int> toRemove = new List<Vector2Int>();

            foreach (var column in activeColumns)
            {
                float dx = column.Key.x - playerX;
                float dz = column.Key.y - playerZ;
                if (dx * dx + dz * dz > unloadDistanceSq)
                {
                    toRemove.Add(column.Key);
                }
            }

            foreach (var key in toRemove)
            {
                foreach (var cube in activeColumns[key])
                {
                    if (cube != null) Destroy(cube);
                }
                activeColumns.Remove(key);
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
            activeColumns.Clear();
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
