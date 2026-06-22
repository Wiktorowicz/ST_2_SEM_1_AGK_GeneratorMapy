using UnityEngine;
using System.Collections;

public class BiomeSpawner : MonoBehaviour
{
    ProceduralTerrainSmooth terrain;

    public int treeCount = 1200;
    public int rockCount = 800;
    public int bushCount = 800;

    void Awake()
    {
        terrain = GetComponent<ProceduralTerrainSmooth>();
    }

    IEnumerator Start()
    {
        yield return null;
        Generate();
    }

    void Generate()
    {
        float[] heights = terrain.GetHeights();
        int width = terrain.GetWidth();
        int depth = terrain.GetDepth();
        float hMul = terrain.GetHeightMultiplier();
        float waterLevel = terrain.waterLevel;

        Vector2 forestCenter = new Vector2(
            Random.Range(width * 0.2f, width * 0.8f),
            Random.Range(depth * 0.2f, depth * 0.8f)
        );

        float forestRadius = Random.Range(width * 0.2f, width * 0.45f);
        int localTreeCount = treeCount * 2;

        for (int i = 0; i < localTreeCount; i++)
        {
            int x = Random.Range(0, width);
            int z = Random.Range(0, depth);

            float dist = Vector2.Distance(new Vector2(x, z), forestCenter);
            float noise = Mathf.PerlinNoise(x * 0.05f, z * 0.05f);

            if (dist > forestRadius * (0.6f + noise * 0.7f)) continue;

            int idx = z * width + x;
            float y = heights[idx] * hMul;
            float normalized = heights[idx];

            if (y <= waterLevel + 1f) continue;
            if (normalized > 0.4f) continue;

            CreateTree(new Vector3(x, y, z));
        }

        for (int i = 0; i < rockCount; i++)
        {
            int x = Random.Range(0, width);
            int z = Random.Range(0, depth);

            int idx = z * width + x;
            float y = heights[idx] * hMul;
            float normalized = heights[idx];

            if (y <= waterLevel + 1f) continue;
            if (normalized < 0.45f || normalized > 0.75f) continue;

            CreateRock(new Vector3(x, y, z));
        }

        for (int i = 0; i < bushCount; i++)
        {
            int x = Random.Range(0, width);
            int z = Random.Range(0, depth);

            int idx = z * width + x;
            float y = heights[idx] * hMul;
            float normalized = heights[idx];

            if (y <= waterLevel + 1f) continue;
            if (normalized > 0.45f) continue;

            CreateBush(new Vector3(x, y, z));
        }
    }

    void CreateTree(Vector3 pos)
    {
        float scale = Random.Range(1.5f, 2.5f);

        GameObject treeParent = new GameObject("Tree");
        treeParent.transform.position = pos;
        treeParent.transform.parent = transform;

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "TreeTrunk";
        trunk.transform.position = pos + Vector3.up * (1f * scale);
        trunk.transform.localScale = new Vector3(0.4f, 1.5f, 0.4f) * scale;
        trunk.transform.parent = treeParent.transform;
        trunk.GetComponent<Renderer>().material.color = new Color(0.4f, 0.25f, 0.1f);

        GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaves.name = "TreeLeaves";
        leaves.transform.position = pos + Vector3.up * (2.5f * scale);
        leaves.transform.localScale = new Vector3(2f, 2f, 2f) * scale;
        leaves.transform.parent = treeParent.transform;
        leaves.GetComponent<Renderer>().material.color = Color.green;
    }

    void CreateRock(Vector3 pos)
    {
        float scale = Random.Range(2f, 4f);

        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Rock";
        rock.transform.position = pos + Vector3.up * scale * 0.5f;
        rock.transform.localScale = new Vector3(1.5f, 1f, 1.5f) * scale;
        rock.transform.parent = transform;
        rock.GetComponent<Renderer>().material.color = Color.gray;
    }

    void CreateBush(Vector3 pos)
    {
        int parts = Random.Range(4, 7);
        float baseScale = Random.Range(1.2f, 2f);

        GameObject parent = new GameObject("Bush");
        parent.transform.position = pos;
        parent.transform.parent = transform;

        for (int i = 0; i < parts; i++)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = "BushPart";

            float scale = baseScale * Random.Range(0.7f, 1.2f);

            Vector3 offset = new Vector3(
                Random.Range(-0.8f, 0.8f),
                Random.Range(0f, 0.6f),
                Random.Range(-0.8f, 0.8f)
            );

            part.transform.position = pos + offset;
            part.transform.localScale = Vector3.one * scale;
            part.transform.parent = parent.transform;

            Color color = new Color(
                Random.Range(0.05f, 0.15f),
                Random.Range(0.6f, 0.9f),
                Random.Range(0.05f, 0.15f)
            );

            part.GetComponent<Renderer>().material.color = color;
        }
    }
}