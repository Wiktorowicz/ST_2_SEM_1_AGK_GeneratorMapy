using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ProceduralTerrainSmooth : MonoBehaviour
{
    public int width = 256;
    public int depth = 256;

    private int iterations = 300;
    private float initialDisplacement = 2.0f;
    private float elevationExponent = 3.5f;
    private int smoothIterations = 4;

    [Header("Skalowanie")]
    public float mnoznikWysokosci = 50f;

    [Header("Tekstury i Woda")]
    public float waterLevel = 2.0f;
    public Material terrainMaterial;
    public Material waterMaterial;
    public Texture2D sandTexture;
    public Texture2D grassTexture;
    public Texture2D rockTexture;
    public Texture2D snowTexture;
    public float textureScale = 0.05f;

    private float[] heights;

    void Start() => GenerateTerrain();

    void GenerateTerrain()
    {
        heights = new float[width * depth];
        float currentDisplacement = initialDisplacement;

        for (int i = 0; i < iterations; i++)
        {
            float lineX = Random.Range(0, width);
            float lineZ = Random.Range(0, depth);
            Vector2 lineDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = z * width + x;
                    Vector2 pos = new Vector2(x - lineX, z - lineZ);
                    if (Vector2.Dot(pos, lineDir) > 0) heights[idx] += currentDisplacement;
                    else heights[idx] -= currentDisplacement;
                }
            }
            currentDisplacement *= 0.995f;
        }

        ApplyTerrainShaping();

        for (int s = 0; s < smoothIterations; s++) SmoothHeights();

        Mesh mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        Vector3[] vertices = new Vector3[width * depth];
        Color[] colors = new Color[vertices.Length];

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = z * width + x;
                float y = heights[i] * mnoznikWysokosci;

                float riverX = (width * 0.5f) + Mathf.Sin(z * 0.05f) * 20f;
                float distToRiver = Mathf.Abs(x - riverX);

                float rzekaPromien = 25f;

                if (distToRiver < rzekaPromien)
                {
                    float smoothRiver = Mathf.SmoothStep(rzekaPromien, 0f, distToRiver);
                    y -= smoothRiver * (waterLevel + 4f);
                }

                vertices[i] = new Vector3(x, y, z);
                colors[i] = CalculateWeights(y, distToRiver, heights[i]);
            }
        }

        int[] triangles = new int[(width - 1) * (depth - 1) * 6];
        int tri = 0;
        for (int z = 0; z < depth - 1; z++)
            for (int x = 0; x < width - 1; x++)
            {
                int s = z * width + x;
                triangles[tri++] = s; triangles[tri++] = s + width; triangles[tri++] = s + 1;
                triangles[tri++] = s + 1; triangles[tri++] = s + width; triangles[tri++] = s + width + 1;
            }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;

        ApplyTexturesToMaterial();
        CreateWater();
    }

    void ApplyTerrainShaping()
    {
        float minH = float.MaxValue, maxH = float.MinValue;
        foreach (float h in heights)
        {
            if (h < minH) minH = h;
            if (h > maxH) maxH = h;
        }
        for (int i = 0; i < heights.Length; i++)
        {
            float normalized = (heights[i] - minH) / (maxH - minH);
            heights[i] = Mathf.Pow(normalized, elevationExponent);
        }
    }

    void SmoothHeights()
    {
        float[] nh = new float[heights.Length];
        for (int z = 1; z < depth - 1; z++)
            for (int x = 1; x < width - 1; x++)
                nh[z * width + x] = (heights[(z - 1) * width + x] + heights[(z + 1) * width + x] +
                                     heights[z * width + x - 1] + heights[z * width + x + 1] + heights[z * width + x]) / 5f;
        heights = nh;
    }

    Color CalculateWeights(float y, float distToRiver, float h)
    {
        float sand = (y < waterLevel + 1.2f && distToRiver < 18f) ? 1f : 0f;

        float snow = (h > 0.75f) ? Mathf.Clamp01((y - 35f) / 10f) : 0f;
        float rock = (h > 0.45f) ? Mathf.Clamp01((y - 20f) / 10f) : 0f;
        float grass = Mathf.Clamp01(1f - (sand + rock + snow));

        float sum = sand + grass + rock + snow + 0.001f;
        return new Color(sand / sum, grass / sum, rock / sum, snow / sum);
    }

    void ApplyTexturesToMaterial()
    {
        if (!terrainMaterial) return;
        terrainMaterial.SetTexture("_SandTex", sandTexture);
        terrainMaterial.SetTexture("_GrassTex", grassTexture);
        terrainMaterial.SetTexture("_RockTex", rockTexture);
        terrainMaterial.SetTexture("_SnowTex", snowTexture);
        terrainMaterial.SetFloat("_Tiling", textureScale);
        GetComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
    }

    void CreateWater()
    {
        GameObject old = GameObject.Find("WaterSurface");
        if (old) DestroyImmediate(old);
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "WaterSurface";
        water.transform.position = new Vector3(width / 2f - 0.5f, waterLevel, depth / 2f - 0.5f);
        water.transform.localScale = new Vector3(width / 10f, 1, depth / 10f);
        if (waterMaterial) water.GetComponent<MeshRenderer>().material = waterMaterial;
    }
}