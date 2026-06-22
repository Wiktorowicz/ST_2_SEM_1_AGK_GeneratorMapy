using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VillageGenerator : MonoBehaviour
{
    [SerializeField] private ProceduralTerrainSmooth terrainGenerator;

    [SerializeField] private int villageSize = 30;
    [SerializeField] private float buildingOffset = 6f;

    [Header("Village Placement")]
    [SerializeField] private float minVillageHeightAboveWater = 1.2f;
    [SerializeField] private float maxVillageHeight = 18f;
    [SerializeField] private int flatCheckRadius = 12;
    [SerializeField] private float maxHeightDifference = 2.5f;

    [Header("Buildings")]
    [SerializeField] private float houseBlockRadius = 8f;
    [SerializeField] private float minDistanceFromRoad = 5f;
    [SerializeField] private float clearTreesRadius = 7f;

    [Header("Road")]
    [SerializeField] private Material roadMaterial;
    [SerializeField] private float roadWidth = 4f;
    [SerializeField] private float clearRoadObjectsRadius = 5f;

    private List<Vector3> roadPoints = new List<Vector3>();
    private List<List<Vector3>> allRoads = new List<List<Vector3>>();
    private List<Vector3> intersections = new List<Vector3>();

    private List<Vector2Int> villageCenters = new List<Vector2Int>();
    private List<Vector3> occupiedPositions = new List<Vector3>();

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);

        int villageCount = Random.Range(2, 6);

        for (int i = 0; i < villageCount; i++)
        {
            GenerateVillage();
        }
    }

    void GenerateVillage()
    {
        occupiedPositions.Clear();

        Vector2Int startPoint = FindFlatArea();

        foreach (var center in villageCenters)
        {
            if (Vector2Int.Distance(center, startPoint) < 50f)
                return;
        }

        villageCenters.Add(startPoint);

        villageSize = Random.Range(100, 220);

        terrainGenerator.FlattenArea(startPoint.x, startPoint.y, 35);

        GenerateRoad(startPoint);
        GenerateBuildings();
        GenerateDecorations();
    }

    Vector2Int FindFlatArea()
    {
        List<Vector2Int> validPoints = new List<Vector2Int>();

        for (int i = 0; i < 4000; i++)
        {
            int x = Random.Range(20, terrainGenerator.GetWidth() - 20);
            int z = Random.Range(20, terrainGenerator.GetDepth() - 20);

            if (IsGoodVillageArea(x, z))
                validPoints.Add(new Vector2Int(x, z));
        }

        if (validPoints.Count > 0)
            return validPoints[Random.Range(0, validPoints.Count)];

        return new Vector2Int(50, 50);
    }

    bool IsGoodVillageArea(int x, int z)
    {
        float h = terrainGenerator.GetHeightWorld(x, z);
        float water = terrainGenerator.GetWaterLevel();

        if (h < water + minVillageHeightAboveWater)
            return false;

        if (h > maxVillageHeight)
            return false;

        if (!IsFlat(x, z))
            return false;

        return true;
    }

    bool IsFlat(int x, int z)
    {
        float baseHeight = terrainGenerator.GetHeightWorld(x, z);
        float water = terrainGenerator.GetWaterLevel();

        if (baseHeight < water + minVillageHeightAboveWater)
            return false;

        if (baseHeight > maxVillageHeight)
            return false;

        for (int dx = -flatCheckRadius; dx <= flatCheckRadius; dx += 2)
        {
            for (int dz = -flatCheckRadius; dz <= flatCheckRadius; dz += 2)
            {
                int px = x + dx;
                int pz = z + dz;

                if (px < 0 || pz < 0 ||
                    px >= terrainGenerator.GetWidth() ||
                    pz >= terrainGenerator.GetDepth())
                    return false;

                float h = terrainGenerator.GetHeightWorld(px, pz);

                if (h < water + minVillageHeightAboveWater)
                    return false;

                if (h > maxVillageHeight)
                    return false;

                if (Mathf.Abs(h - baseHeight) > maxHeightDifference)
                    return false;
            }
        }

        return true;
    }

    bool IsAboveWater(float x, float z)
    {
        float h = terrainGenerator.GetHeightWorld(x, z);
        return h > terrainGenerator.GetWaterLevel() + 0.2f;
    }

    void GenerateRoad(Vector2Int start)
    {
        roadPoints.Clear();
        allRoads.Clear();
        intersections.Clear();

        float x = start.x;
        float z = start.y;

        for (int i = 0; i < villageSize; i++)
        {
            if (x < 5 || z < 5 ||
                x > terrainGenerator.GetWidth() - 5 ||
                z > terrainGenerator.GetDepth() - 5)
                break;

            float y = terrainGenerator.GetHeightWorld(x, z);
            float waterLevel = terrainGenerator.GetWaterLevel();

            if (y <= waterLevel + 0.3f)
                break;

            if (y > maxVillageHeight)
                break;

            Vector3 point = new Vector3(x, y + 0.05f, z);
            roadPoints.Add(point);

            x += 2.5f;
            z += Random.Range(-0.3f, 0.3f);
        }

        allRoads.Add(new List<Vector3>(roadPoints));

        CreateRoadMesh(roadPoints);

        for (int i = 0; i < roadPoints.Count - 1; i++)
        {
            Vector3 a = roadPoints[i];
            Vector3 b = roadPoints[i + 1];

            if (i % 8 == 0 && Random.value > 0.5f)
            {
                Vector3 dir = (b - a).normalized;

                intersections.Add(a);
                GenerateSideRoad(a, dir);
            }
        }
    }

    void GenerateSideRoad(Vector3 startPoint, Vector3 mainDir)
    {
        List<Vector3> sidePoints = new List<Vector3>();

        Vector3 dir = Vector3.Cross(Vector3.up, mainDir).normalized;

        if (Random.value > 0.5f)
            dir *= -1f;

        float x = startPoint.x;
        float z = startPoint.z;

        int length = Random.Range(10, 35);

        for (int i = 0; i < length; i++)
        {
            if (x < 5 || z < 5 ||
                x > terrainGenerator.GetWidth() - 5 ||
                z > terrainGenerator.GetDepth() - 5)
                break;

            float y = terrainGenerator.GetHeightWorld(x, z);
            float waterLevel = terrainGenerator.GetWaterLevel();

            if (y <= waterLevel + 0.3f)
                break;

            if (y > maxVillageHeight)
                break;

            Vector3 point = new Vector3(x, y + 0.05f, z);
            sidePoints.Add(point);

            x += dir.x * 2.5f;
            z += dir.z * 2.5f;
        }

        if (sidePoints.Count > 2)
            allRoads.Add(sidePoints);

        CreateRoadMesh(sidePoints);
    }

    void CreateRoadMesh(List<Vector3> points)
    {
        if (points.Count < 2)
            return;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 dir;

            if (i == 0)
                dir = (points[i + 1] - points[i]).normalized;
            else if (i == points.Count - 1)
                dir = (points[i] - points[i - 1]).normalized;
            else
                dir = (points[i + 1] - points[i - 1]).normalized;

            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * (roadWidth / 2f);

            Vector3 left = points[i] + side;
            Vector3 right = points[i] - side;

            left.y = terrainGenerator.GetHeightWorld(left.x, left.z) + 0.1f;
            right.y = terrainGenerator.GetHeightWorld(right.x, right.z) + 0.1f;

            vertices.Add(left);
            vertices.Add(right);

            ClearObjectsAround(points[i], clearRoadObjectsRadius);
            occupiedPositions.Add(points[i]);
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            int index = i * 2;

            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);

            triangles.Add(index + 2);
            triangles.Add(index + 1);
            triangles.Add(index + 3);
        }

        GameObject road = new GameObject("RoadMesh");

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        MeshFilter mf = road.AddComponent<MeshFilter>();
        MeshRenderer mr = road.AddComponent<MeshRenderer>();

        mf.mesh = mesh;

        if (roadMaterial != null)
            mr.material = roadMaterial;
        else
        {
            mr.material = new Material(Shader.Find("Standard"));
            mr.material.color = new Color(0.2f, 0.2f, 0.2f);
        }
    }

    void GenerateBuildings()
    {
        foreach (var road in allRoads)
        {
            for (int i = 1; i < road.Count - 1; i++)
            {
                if (i % 3 != 0)
                    continue;

                Vector3 point = road[i];

                if (IsNearIntersection(point, 10f))
                    continue;

                Vector3 dir = (road[i + 1] - road[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;

                Vector3 left = point + side * (buildingOffset + Random.Range(-2f, 3f));
                Vector3 right = point - side * (buildingOffset + Random.Range(-2f, 3f));

                left += new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                right += new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));

                left.y = terrainGenerator.GetHeightWorld(left.x, left.z);
                right.y = terrainGenerator.GetHeightWorld(right.x, right.z);

                if (CanPlaceHouse(left))
                    SpawnHouse(left);

                if (CanPlaceHouse(right))
                    SpawnHouse(right);
            }
        }
    }

    bool CanPlaceHouse(Vector3 pos)
    {
        float h = terrainGenerator.GetHeightWorld(pos.x, pos.z);
        float water = terrainGenerator.GetWaterLevel();

        if (h < water + 0.5f)
            return false;

        if (h > maxVillageHeight)
            return false;

        if (!IsAreaFree(pos, houseBlockRadius))
            return false;

        if (IsNearAnyRoad(pos, minDistanceFromRoad))
            return false;

        if (Random.value <= 0.25f)
            return false;

        return true;
    }

    bool IsNearIntersection(Vector3 pos, float radius)
    {
        foreach (var intersection in intersections)
        {
            if (Vector3.Distance(pos, intersection) < radius)
                return true;
        }

        return false;
    }

    bool IsNearAnyRoad(Vector3 pos, float radius)
    {
        foreach (var road in allRoads)
        {
            foreach (var p in road)
            {
                if (Vector3.Distance(pos, p) < radius)
                    return true;
            }
        }

        return false;
    }

    bool IsAreaFree(Vector3 pos, float radius)
    {
        foreach (var p in occupiedPositions)
        {
            if (Vector3.Distance(p, pos) < radius)
                return false;
        }

        return true;
    }

    void SpawnHouse(Vector3 pos)
    {
        ClearObjectsAround(pos, clearTreesRadius);

        occupiedPositions.Add(pos);

        GameObject house = GameObject.CreatePrimitive(PrimitiveType.Cube);

        float height = Random.Range(3f, 6f);

        house.transform.position = pos + Vector3.up * (height / 2f);
        house.transform.localScale = new Vector3(4f, height, 4f);

        house.GetComponent<Renderer>().material.color = new Color(
            Random.Range(0.6f, 0.9f),
            Random.Range(0.6f, 0.9f),
            Random.Range(0.6f, 0.9f)
        );
    }

    void ClearObjectsAround(Vector3 pos, float radius)
    {
        Collider[] colliders = Physics.OverlapSphere(pos, radius);

        foreach (Collider col in colliders)
        {
            Transform root = col.transform;

            while (root.parent != null &&
                  (root.parent.name == "Tree" ||
                   root.parent.name == "Bush" ||
                   root.parent.name == "Rock"))
            {
                root = root.parent;
            }

            string objectName = root.name;

            if (objectName.Contains("Tree") ||
                objectName.Contains("Bush") ||
                objectName.Contains("Rock"))
            {
                Destroy(root.gameObject);
            }
        }
    }

    void GenerateDecorations()
    {
        foreach (var road in allRoads)
        {
            for (int i = 1; i < road.Count - 1; i++)
            {
                if (i % 5 != 0)
                    continue;

                Vector3 point = road[i];

                if (IsNearIntersection(point, 8f))
                    continue;

                Vector3 dir = (road[i + 1] - road[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;

                Vector3 left = point + side * 5f;
                Vector3 right = point - side * 5f;

                left.y = terrainGenerator.GetHeightWorld(left.x, left.z);
                right.y = terrainGenerator.GetHeightWorld(right.x, right.z);

                if (IsAboveWater(left.x, left.z) && IsAreaFree(left, 2f))
                    SpawnLamp(left);

                if (IsAboveWater(right.x, right.z) && IsAreaFree(right, 2f))
                    SpawnLamp(right);
            }
        }
    }

    void SpawnLamp(Vector3 pos)
    {
        occupiedPositions.Add(pos);

        float y = terrainGenerator.GetHeightWorld(pos.x, pos.z);

        GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lamp.transform.position = new Vector3(pos.x, y + 1, pos.z);
        lamp.transform.localScale = new Vector3(0.2f, 2f, 0.2f);

        lamp.GetComponent<Renderer>().material.color = new Color(0.1f, 0.1f, 0.1f);
    }
}