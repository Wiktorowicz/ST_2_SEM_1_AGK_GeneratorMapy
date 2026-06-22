using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ProceduralTerrainSmooth : MonoBehaviour {
    public int width = 256;
    public int depth = 256;

    private int iterations = 200;
    private float initialDisplacement = 1.2f;
    private float elevationExponent = 3.0f; 

    [Header("Skalowanie")]
    public float mnoznikWysokosci = 40f; 
    [Header("Tekstury i Woda")]
    public float waterLevel = 2.0f;
    public Material terrainMaterial;
    public Material waterMaterial;
    public Texture2D sandTexture;
    public Texture2D grassTexture;
    public Texture2D rockTexture;
    public Texture2D snowTexture;
    public float textureScale = 0.5f;

    private float[] heights;
    private Vector3[] vertices;

    void Start() => GenerateTerrain();

    public float[] GetHeights() => heights;
    public int GetWidth() => width;
    public int GetDepth() => depth;
    public float GetHeightMultiplier() => mnoznikWysokosci;

    void GenerateTerrain() {
        heights = new float[width * depth];
        float currentDisplacement = initialDisplacement;

        for (int i = 0; i < iterations; i++) {
            float lineX = Random.Range(0, width);
            float lineZ = Random.Range(0, depth);
            Vector2 dir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;

            for (int z = 0; z < depth; z++) {
                for (int x = 0; x < width; x++) {
                    int idx = z * width + x;
                    Vector2 pos = new Vector2(x - lineX, z - lineZ);

                    heights[idx] += (Vector2.Dot(pos, dir) > 0) ? currentDisplacement : -currentDisplacement;
                }
            }

            currentDisplacement *= 0.99f;
        }

        ApplyTerrainShaping();

        for (int z = 0; z < depth; z++) {
            for (int x = 0; x < width; x++) {
                int i = z * width + x;

                float largeNoise = Mathf.PerlinNoise(x * 0.005f, z * 0.005f);
                heights[i] += largeNoise * 0.5f;
            }
        }

        SmoothHeights(1);

        BuildMesh();
        ApplyTexturesToMaterial();
        CreateWater();
        SaveMaps();
    }

    void BuildMesh() {
        Mesh mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };

        vertices = new Vector3[width * depth];
        Color[] colors = new Color[vertices.Length];

        for (int z = 0; z < depth; z++) {
            for (int x = 0; x < width; x++) {
                int i = z * width + x;
                float y = heights[i] * mnoznikWysokosci;

                vertices[i] = new Vector3(x, y, z);
                colors[i] = CalculateWeights(y, heights[i]);
            }
        }

        int[] triangles = new int[(width - 1) * (depth - 1) * 6];
        int tri = 0;

        for (int z = 0; z < depth - 1; z++) {
            for (int x = 0; x < width - 1; x++) {
                int s = z * width + x;

                triangles[tri++] = s;
                triangles[tri++] = s + width;
                triangles[tri++] = s + 1;

                triangles[tri++] = s + 1;
                triangles[tri++] = s + width;
                triangles[tri++] = s + width + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    void ApplyTerrainShaping() {
        float minH = float.MaxValue, maxH = float.MinValue;

        foreach (float h in heights) {
            if (h < minH) minH = h;
            if (h > maxH) maxH = h;
        }

        for (int i = 0; i < heights.Length; i++) {
            float n = (heights[i] - minH) / (maxH - minH);
            heights[i] = Mathf.Pow(n, elevationExponent);
        }
    }

    void SmoothHeights(int iterations) {
        for (int it = 0; it < iterations; it++) {
            float[] newHeights = new float[heights.Length];

            for (int z = 1; z < depth - 1; z++) {
                for (int x = 1; x < width - 1; x++) {
                    float sum = 0f;

                    for (int dz = -1; dz <= 1; dz++) {
                        for (int dx = -1; dx <= 1; dx++) {
                            int idx = (z + dz) * width + (x + dx);
                            sum += heights[idx];
                        }
                    }

                    newHeights[z * width + x] = sum / 9f;
                }
            }

            heights = newHeights;
        }
    }

    Color CalculateWeights(float y, float h) {
        float sand = (y < waterLevel + 1.2f) ? 1f : 0f;
        float snow = (h > 0.75f) ? Mathf.Clamp01((y - 30f) / 10f) : 0f;
        float rock = (h > 0.45f) ? Mathf.Clamp01((y - 15f) / 10f) : 0f;
        float grass = Mathf.Clamp01(1f - (sand + rock + snow));

        float sum = sand + grass + rock + snow + 0.001f;
        return new Color(sand / sum, grass / sum, rock / sum, snow / sum);
    }

    void ApplyTexturesToMaterial() {
        if (!terrainMaterial) return;

        terrainMaterial.SetTexture("_SandTex", sandTexture);
        terrainMaterial.SetTexture("_GrassTex", grassTexture);
        terrainMaterial.SetTexture("_RockTex", rockTexture);
        terrainMaterial.SetTexture("_SnowTex", snowTexture);
        terrainMaterial.SetFloat("_Tiling", textureScale);

        GetComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
    }

    void CreateWater() {
        GameObject old = GameObject.Find("WaterSurface");
        if (old) DestroyImmediate(old);

        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "WaterSurface";
        water.transform.position = new Vector3(width / 2f, waterLevel, depth / 2f);
        water.transform.localScale = new Vector3(width / 10f, 1, depth / 10f);

        if (waterMaterial)
            water.GetComponent<MeshRenderer>().material = waterMaterial;
    }

    public float GetHeightWorld(float worldX, float worldZ) {
        int x = Mathf.Clamp(Mathf.RoundToInt(worldX), 0, width - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(worldZ), 0, depth - 1);

        return heights[z * width + x] * mnoznikWysokosci;
    }

    public float GetWaterLevel() => waterLevel;

    public void FlattenArea(int centerX, int centerZ, int radius) {
        float centerHeight = heights[centerZ * width + centerX];

        for (int z = -radius; z <= radius; z++) {
            for (int x = -radius; x <= radius; x++) {
                int px = centerX + x;
                int pz = centerZ + z;

                if (px < 0 || pz < 0 || px >= width || pz >= depth)
                    continue;

                float dist = Mathf.Sqrt(x * x + z * z);
                if (dist > radius) continue;

                float t = dist / radius;
                int idx = pz * width + px;

                heights[idx] = Mathf.Lerp(centerHeight, heights[idx], t);
            }
        }
        SmoothHeights(1);
        BuildMesh();
    }

    public float GetNormalizedHeight(float worldX, float worldZ) {
        int x = Mathf.Clamp(Mathf.RoundToInt(worldX), 0, width - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(worldZ), 0, depth - 1);

        return heights[z * width + x];
    }
    public Texture2D GenerateGrayscaleHeightMap()
    {
        Texture2D tex = new Texture2D(width, depth);

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = z * width + x;

                float h = Mathf.Clamp01(heights[i]);

                tex.SetPixel(x, z, new Color(h, h, h));
            }
        }

        tex.Apply();
        return tex;
    }
    public Texture2D GenerateColoredMap()
    {
        Texture2D tex = new Texture2D(width, depth);

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = z * width + x;

                float h = heights[i];
                float y = h * mnoznikWysokosci;

                Color color;

                if (y <= waterLevel)
                    color = Color.blue;
                else if (y < waterLevel + 1.2f)
                    color = new Color(0.8f, 0.7f, 0.4f);
                else if (h > 0.75f)
                    color = Color.white;
                else if (h > 0.45f)
                    color = Color.gray;
                else
                    color = Color.green;

                tex.SetPixel(x, z, color);
            }
        }

        tex.Apply();
        return tex;
    }
    public void SaveMaps()
    {
        Texture2D gray = GenerateGrayscaleHeightMap();
        Texture2D color = GenerateColoredMap();

        System.IO.File.WriteAllBytes(
            Application.dataPath + "/HeightMap_Gray.png",
            gray.EncodeToPNG());

        System.IO.File.WriteAllBytes(
            Application.dataPath + "/HeightMap_Color.png",
            color.EncodeToPNG());

        Debug.Log("Mapy zapisane.");
    }
}