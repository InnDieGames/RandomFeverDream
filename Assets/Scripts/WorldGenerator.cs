using UnityEngine;
using System.Collections.Generic;

namespace WorldGen
{
    public class WorldGenerator : MonoBehaviour
    {
        [System.Serializable]
        public struct BiomeConfig
        {
            public string name;
            [Range(0f, 1f)] public float threshold; // The biomeNoise value threshold
            public float heightMultiplier;
            public float noiseScale;
            
            [Header("Biome Colors")]
            public Color colorStart; // Color at the bottom (y = 0)
            public Color colorEnd;   // Color at the top (y = height)
        }

        [Header("Generation Settings")]
        [SerializeField] private GameObject cubePrefab;
        [SerializeField] private int renderDistance = 20;
        [SerializeField] private int unloadBuffer = 5;
        
        [Header("Target")]
        [SerializeField] private Transform playerTransform;

        [Header("Biome & Seed Settings")]
        [SerializeField] private int seed = 1337;
        [SerializeField] private float biomeNoiseScale = 0.015f;
        [SerializeField] private int biomeCount = 4;
        [SerializeField] private List<BiomeConfig> biomes = new();

        [Header("Color Jitter (Local Randomness)")]
        [SerializeField] [Range(0f, 0.1f)] private float hueJitter = 0.02f;
        [SerializeField] [Range(0f, 0.2f)] private float satJitter = 0.05f;
        [SerializeField] [Range(0f, 0.2f)] private float valJitter = 0.08f;

        private float offsetX;
        private float offsetZ;
        private float biomeOffsetX;
        private float biomeOffsetZ;
        private bool offsetsInitialized = false;
        private int currentInitializedSeed;

        private Dictionary<Vector2Int, List<GameObject>> activeColumns = new();
        private float cleanupTimer = 0f;
        private float cleanupInterval = 0.5f;

        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        private void Awake()
        {
            EnsureOffsetsInitialized();
        }

        private void Start()
        {
            EnsureOffsetsInitialized();
            
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
        public void GenerateWorldNearPlayerEditor()
        {
            EnsureOffsetsInitialized();
            if (playerTransform == null)
            {
                GameObject player = GameObject.Find("Player");
                if (player != null) playerTransform = player.transform;
            }
            GenerateWorldNearPlayer();
        }
        
        private void EnsureOffsetsInitialized()
        {
            if (offsetsInitialized && currentInitializedSeed == seed) return;
            
            System.Random prng = new System.Random(seed);
            offsetX = (float)(prng.NextDouble() * 100000f);
            offsetZ = (float)(prng.NextDouble() * 100000f);
            biomeOffsetX = (float)(prng.NextDouble() * 100000f);
            biomeOffsetZ = (float)(prng.NextDouble() * 100000f);
            
            offsetsInitialized = true;
            currentInitializedSeed = seed;
            
            GenerateBiomesFromSeed(prng);
        }

        private void GenerateBiomesFromSeed(System.Random prng)
        {
            EnsureDefaultBiomes();
            
            for (int i = 0; i < biomeCount; i++)
            {
                float threshold = (float)i / biomeCount;
                
                // Get a name from list or generic
                string bName = $"Biome {i}";
                
                // Deterministic height settings for this biome
                float minHeightMult = 2f + i * 2.5f;
                float maxHeightMult = 6f + i * 4.5f;
                float heightMultiplierVal = Mathf.Lerp(minHeightMult, maxHeightMult, (float)prng.NextDouble());
                float noiseScaleVal = Mathf.Lerp(0.02f, 0.08f, (float)prng.NextDouble());
                
                // Beautiful seed-based HSV colors
                float h1 = (float)prng.NextDouble();
                // Second hue is complementary or analogous
                float hueOffset = (float)(prng.NextDouble() * 0.3f - 0.15f);
                float h2 = Mathf.Repeat(h1 + hueOffset, 1f);
                
                float s1 = Mathf.Lerp(0.5f, 0.9f, (float)prng.NextDouble());
                float s2 = Mathf.Lerp(0.5f, 0.9f, (float)prng.NextDouble());
                
                float v1 = Mathf.Lerp(0.4f, 0.8f, (float)prng.NextDouble());
                float v2 = Mathf.Lerp(0.5f, 0.9f, (float)prng.NextDouble());
                
                Color colorStart = Color.HSVToRGB(h1, s1, v1);
                Color colorEnd = Color.HSVToRGB(h2, s2, v2);
                
                biomes.Add(new BiomeConfig
                {
                    name = bName,
                    threshold = threshold,
                    heightMultiplier = heightMultiplierVal,
                    noiseScale = noiseScaleVal,
                    colorStart = colorStart,
                    colorEnd = colorEnd
                });
            }
        }

        [ContextMenu("DefaultBiomes")]
        private void EnsureDefaultBiomes()
        {
            if (biomes == null || biomes.Count == 0)
            {
                biomes = new List<BiomeConfig>
                {
                    new BiomeConfig
                    {
                        name = "Ocean",
                        threshold = 0.0f,
                        heightMultiplier = 2f,
                        noiseScale = 0.03f,
                        colorStart = new Color(0.0f, 0.1f, 0.4f), // Deep blue
                        colorEnd = new Color(0.1f, 0.5f, 0.8f)    // Light blue/cyan
                    },
                    new BiomeConfig
                    {
                        name = "Forest",
                        threshold = 0.3f,
                        heightMultiplier = 7f,
                        noiseScale = 0.06f,
                        colorStart = new Color(0.1f, 0.3f, 0.1f), // Deep forest green
                        colorEnd = new Color(0.4f, 0.7f, 0.2f)    // Bright grass green
                    },
                    new BiomeConfig
                    {
                        name = "Desert",
                        threshold = 0.6f,
                        heightMultiplier = 5f,
                        noiseScale = 0.04f,
                        colorStart = new Color(0.6f, 0.4f, 0.2f), // Sand brown
                        colorEnd = new Color(0.9f, 0.8f, 0.5f)    // Pale desert gold
                    },
                    new BiomeConfig
                    {
                        name = "Peaks",
                        threshold = 0.8f,
                        heightMultiplier = 15f,
                        noiseScale = 0.08f,
                        colorStart = new Color(0.2f, 0.2f, 0.3f), // Dark slate
                        colorEnd = new Color(0.95f, 0.95f, 1.0f)  // Snow white
                    }
                };
            }
        }

        private float CalculateBiomeNoise(int x, int z)
        {
            EnsureOffsetsInitialized();
            float xCoord = x * biomeNoiseScale + biomeOffsetX;
            float zCoord = z * biomeNoiseScale + biomeOffsetZ;
            return Mathf.PerlinNoise(xCoord, zCoord);
        }

        private void EvaluateBiome(float biomeNoiseValue, 
            out float heightMultiplierOut, out float noiseScaleOut, 
            out Color colorStartOut, out Color colorEndOut)
        {
            EnsureOffsetsInitialized();
            
            int lowerIndex = 0;
            for (int i = 0; i < biomes.Count; i++)
            {
                if (biomeNoiseValue >= biomes[i].threshold)
                {
                    lowerIndex = i;
                }
            }
            
            int upperIndex = lowerIndex + 1;
            if (upperIndex >= biomes.Count)
            {
                BiomeConfig b = biomes[lowerIndex];
                heightMultiplierOut = b.heightMultiplier;
                noiseScaleOut = b.noiseScale;
                colorStartOut = b.colorStart;
                colorEndOut = b.colorEnd;
                return;
            }
            
            BiomeConfig b1 = biomes[lowerIndex];
            BiomeConfig b2 = biomes[upperIndex];
            
            float t = (biomeNoiseValue - b1.threshold) / (b2.threshold - b1.threshold);
            t = Mathf.SmoothStep(0f, 1f, t);
            
            heightMultiplierOut = Mathf.Lerp(b1.heightMultiplier, b2.heightMultiplier, t);
            noiseScaleOut = Mathf.Lerp(b1.noiseScale, b2.noiseScale, t);
            colorStartOut = Color.Lerp(b1.colorStart, b2.colorStart, t);
            colorEndOut = Color.Lerp(b1.colorEnd, b2.colorEnd, t);
        }

        private float GetDeterministicValue(int x, int y, int z, int channel)
        {
            unchecked
            {
                int hash = seed;
                hash = hash * 16777619 ^ x;
                hash = hash * 16777619 ^ y;
                hash = hash * 16777619 ^ z;
                hash = hash * 16777619 ^ channel;
                return (float)(hash & 0x7FFFFFFF) / int.MaxValue;
            }
        }
        
        private void GenerateWorldNearPlayer()
        {
            if (cubePrefab == null) return;
            
            Vector3 targetPos = playerTransform != null ? playerTransform.position : Vector3.zero;
            int playerX = Mathf.FloorToInt(targetPos.x);
            int playerZ = Mathf.FloorToInt(targetPos.z);
            
            EnsureOffsetsInitialized();
            
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
            EnsureOffsetsInitialized();
            
            float biomeVal = CalculateBiomeNoise(x, z);
            float heightMultiplierVal, noiseScaleVal;
            Color colorStartVal, colorEndVal;
            
            EvaluateBiome(biomeVal, out heightMultiplierVal, out noiseScaleVal, out colorStartVal, out colorEndVal);
            
            int height = Mathf.FloorToInt(CalculateHeight(x, z, noiseScaleVal, heightMultiplierVal));
            List<GameObject> columnCubes = new List<GameObject>();
            
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            
            for (int y = 0; y <= height; y++)
            {
                Vector3 pos = new Vector3(x, y, z);
                GameObject cube = Instantiate(cubePrefab, pos, Quaternion.identity, transform);
                cube.name = $"Cube_{x}_{y}_{z}";
                
                // Calculate height factor (0 at bottom, 1 at peak of this specific column)
                float heightFactor = height > 0 ? (float)y / height : 0f;
                
                // Base gradient color for this height
                Color baseColor = Color.Lerp(colorStartVal, colorEndVal, heightFactor);
                
                // Apply deterministic color jitter in HSV space for beautiful, natural tile variance
                Color.RGBToHSV(baseColor, out float h, out float s, out float v);
                
                float hRand = GetDeterministicValue(x, y, z, 0) * 2f - 1f;
                float sRand = GetDeterministicValue(x, y, z, 1) * 2f - 1f;
                float vRand = GetDeterministicValue(x, y, z, 2) * 2f - 1f;
                
                h = Mathf.Repeat(h + hRand * hueJitter, 1f);
                s = Mathf.Clamp01(s + sRand * satJitter);
                v = Mathf.Clamp01(v + vRand * valJitter);
                
                Color finalColor = Color.HSVToRGB(h, s, v);
                
                // Apply color using MaterialPropertyBlock
                Renderer rend = cube.GetComponent<Renderer>();
                if (rend != null)
                {
                    propertyBlock.Clear();
                    propertyBlock.SetColor(BaseColorProp, finalColor);
                    propertyBlock.SetColor(ColorProp, finalColor);
                    rend.SetPropertyBlock(propertyBlock);
                }
                
                columnCubes.Add(cube);
            }

            activeColumns.Add(new Vector2Int(x, z), columnCubes);
        }

        private void UnloadFarColumns()
        {
            Vector3 targetPos = playerTransform != null ? playerTransform.position : Vector3.zero;
            int playerX = Mathf.FloorToInt(targetPos.x);
            int playerZ = Mathf.FloorToInt(targetPos.z);
            
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
                    if (cube != null)
                    {
                        if (Application.isPlaying)
                            Destroy(cube);
                        else
                            DestroyImmediate(cube);
                    }
                }
                activeColumns.Remove(key);
            }
        }

        private float CalculateHeight(int x, int z, float noiseScaleValue, float heightMultiplierValue)
        {
            EnsureOffsetsInitialized();
            float xCoord = x * noiseScaleValue + offsetX;
            float zCoord = z * noiseScaleValue + offsetZ;
            return Mathf.PerlinNoise(xCoord, zCoord) * heightMultiplierValue;
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
