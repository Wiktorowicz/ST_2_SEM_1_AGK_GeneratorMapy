using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VillageGenerator : MonoBehaviour
{
    [SerializeField] private ProceduralTerrainSmooth terrainGenerator;

    [Header("Village Size")]
    [SerializeField] private int minVillageSize = 100;
    [SerializeField] private int maxVillageSize = 220;

    private int villageSize;

    [SerializeField] private float buildingOffset = 6f;

    [Header("Village Count")]
    [SerializeField] private int minVillageCount = 2;
    [SerializeField] private int maxVillageCount = 5;

    [Header("Village Placement")]
    [SerializeField] private float minVillageHeightAboveWater = 2.4f;
    [SerializeField] private float minRoadHeightAboveWater = 2.2f;
    [SerializeField] private float maxVillageHeight = 18f;
    [SerializeField] private int flatCheckRadius = 12;
    [SerializeField] private float maxHeightDifference = 2.5f;
    [SerializeField] private float minDistanceBetweenVillages = 220f;
    [SerializeField] private float minDistanceFromOtherVillageRoads = 90f;

    [Header("Buildings")]
    [SerializeField] private float houseBlockRadius = 8f;
    [SerializeField] private float minDistanceFromRoad = 7f;
    [SerializeField] private float clearTreesRadius = 7f;

    [Header("Road")]
    [SerializeField] private Material roadMaterial;
    [SerializeField] private float roadWidth = 14f;
    [SerializeField] private float roadHeightOffset = 0.6f;
    [SerializeField] private float clearRoadObjectsRadius = 16f;

    [Header("Side Roads")]
    [SerializeField] private int sideRoadSpacing = 45;
    [SerializeField] private int minSideRoadLength = 20;
    [SerializeField] private float sideRoadChance = 0.45f;

    [Header("Village Connections")]
    /*[SerializeField]*/ private bool connectVillages = true;
    /*[SerializeField]*/ private float maxVillageConnectionDistance = 700f;
    /*[SerializeField]*/ private float connectionRoadStep = 4f;
    /*[SerializeField]*/ private float connectionMinHeightAboveWater = 1.0f;
    /*[SerializeField]*/ private float connectionMaxHeight = 24f;
    /*[SerializeField]*/ private float connectionSlopeLimit = 3.5f;
    /*[SerializeField]*/ private int maxPathSearchIterations = 20000;

    private List<Vector3> roadPoints = new List<Vector3>();
    private List<List<Vector3>> allRoads = new List<List<Vector3>>();
    private List<List<Vector3>> globalRoads = new List<List<Vector3>>();
    private List<Vector3> intersections = new List<Vector3>();

    private List<Vector2Int> villageCenters = new List<Vector2Int>();
    private List<Vector3> occupiedPositions = new List<Vector3>();

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);

        int villageCount = Random.Range(minVillageCount, maxVillageCount + 1);

        int generated = 0;
        int attempts = 0;

        while (generated < villageCount && attempts < villageCount * 25)
        {
            attempts++;

            if (GenerateVillage())
                generated++;
        }
    }

    bool GenerateVillage()
    {
        occupiedPositions.Clear();

        Vector2Int startPoint = FindFlatArea();

        if (startPoint.x < 0 || startPoint.y < 0)
            return false;

        if (IsTooCloseToOtherVillage(startPoint))
            return false;

        villageSize = Random.Range(minVillageSize, maxVillageSize + 1);

        GenerateRoadData(startPoint);

        if (roadPoints.Count < 15)
            return false;

        if (IsTooCloseToExistingRoads(allRoads, minDistanceFromOtherVillageRoads))
            return false;

        int possibleHouses = CountPossibleHouseSpots();

        if (possibleHouses < 6)
            return false;

        List<Vector3> connectionRoad = null;

        if (connectVillages)
            connectionRoad = TryCreateConnectionRoad();

        villageCenters.Add(startPoint);

        terrainGenerator.FlattenArea(startPoint.x, startPoint.y, 35);

        CreateAllRoadMeshes();

        if (connectionRoad != null && connectionRoad.Count > 2)
        {
            CreateRoadMesh(connectionRoad);
            globalRoads.Add(new List<Vector3>(connectionRoad));
        }

        GenerateBuildings();
        GenerateDecorations();

        foreach (var road in allRoads)
            globalRoads.Add(new List<Vector3>(road));

        return true;
    }

    List<Vector3> TryCreateConnectionRoad()
    {
        if (globalRoads.Count == 0 || roadPoints.Count < 2)
            return null;

        Vector3 newA = roadPoints[0];
        Vector3 newB = roadPoints[roadPoints.Count - 1];

        Vector3 bestStart = Vector3.zero;
        Vector3 bestTarget = Vector3.zero;

        float bestDistance = float.MaxValue;
        bool found = false;

        foreach (var oldRoad in globalRoads)
        {
            if (oldRoad.Count < 2)
                continue;

            Vector3 oldA = oldRoad[0];
            Vector3 oldB = oldRoad[oldRoad.Count - 1];

            CheckConnectionPair(newA, oldA, ref bestStart, ref bestTarget, ref bestDistance, ref found);
            CheckConnectionPair(newA, oldB, ref bestStart, ref bestTarget, ref bestDistance, ref found);
            CheckConnectionPair(newB, oldA, ref bestStart, ref bestTarget, ref bestDistance, ref found);
            CheckConnectionPair(newB, oldB, ref bestStart, ref bestTarget, ref bestDistance, ref found);
        }

        if (!found)
            return null;

        if (bestDistance > maxVillageConnectionDistance)
            return null;

        return FindConnectionPath(bestStart, bestTarget);
    }
    void CheckConnectionPair(
    Vector3 a,
    Vector3 b,
    ref Vector3 bestStart,
    ref Vector3 bestTarget,
    ref float bestDistance,
    ref bool found)
    {
        float dist = Vector2.Distance(
            new Vector2(a.x, a.z),
            new Vector2(b.x, b.z)
        );

        if (dist < bestDistance)
        {
            bestDistance = dist;
            bestStart = a;
            bestTarget = b;
            found = true;
        }
    }

    List<Vector3> FindConnectionPath(Vector3 startWorld, Vector3 endWorld)
    {
        Vector2Int start = WorldToConnectionCell(startWorld);
        Vector2Int end = WorldToConnectionCell(endWorld);

        List<Vector2Int> open = new List<Vector2Int>();
        HashSet<Vector2Int> closed = new HashSet<Vector2Int>();

        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> gCost = new Dictionary<Vector2Int, float>();

        open.Add(start);
        gCost[start] = 0f;

        Vector2Int[] dirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        int iterations = 0;

        while (open.Count > 0 && iterations < maxPathSearchIterations)
        {
            iterations++;

            Vector2Int current = GetLowestCostCell(open, gCost, end);

            if (Vector2Int.Distance(current, end) <= 1f)
                return BuildWorldPath(cameFrom, current);

            open.Remove(current);
            closed.Add(current);

            foreach (Vector2Int dir in dirs)
            {
                Vector2Int next = current + dir;

                if (closed.Contains(next))
                    continue;

                if (!IsValidConnectionCell(next))
                    continue;

                float moveCost = GetConnectionCellCost(current, next);

                if (moveCost >= 99999f)
                    continue;

                float newCost = gCost[current] + moveCost;

                if (!gCost.ContainsKey(next) || newCost < gCost[next])
                {
                    gCost[next] = newCost;
                    cameFrom[next] = current;

                    if (!open.Contains(next))
                        open.Add(next);
                }
            }
        }

        return null;
    }

    Vector2Int WorldToConnectionCell(Vector3 pos)
    {
        int x = Mathf.RoundToInt(pos.x / connectionRoadStep);
        int z = Mathf.RoundToInt(pos.z / connectionRoadStep);

        return new Vector2Int(x, z);
    }

    Vector3 ConnectionCellToWorld(Vector2Int cell)
    {
        float x = cell.x * connectionRoadStep;
        float z = cell.y * connectionRoadStep;
        float y = terrainGenerator.GetHeightWorld(x, z);

        return new Vector3(x, y + roadHeightOffset, z);
    }

    Vector2Int GetLowestCostCell(List<Vector2Int> open, Dictionary<Vector2Int, float> gCost, Vector2Int end)
    {
        Vector2Int best = open[0];
        float bestCost = float.MaxValue;

        foreach (Vector2Int cell in open)
        {
            float g = gCost.ContainsKey(cell) ? gCost[cell] : 99999f;
            float h = Vector2Int.Distance(cell, end);
            float total = g + h;

            if (total < bestCost)
            {
                bestCost = total;
                best = cell;
            }
        }

        return best;
    }

    List<Vector3> BuildWorldPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector3> path = new List<Vector3>();

        while (cameFrom.ContainsKey(current))
        {
            path.Add(ConnectionCellToWorld(current));
            current = cameFrom[current];
        }

        path.Add(ConnectionCellToWorld(current));
        path.Reverse();

        if (path.Count < 3)
            return null;

        return path;
    }

    bool IsValidConnectionCell(Vector2Int cell)
    {
        float x = cell.x * connectionRoadStep;
        float z = cell.y * connectionRoadStep;

        if (x < 10 || z < 10 ||
            x >= terrainGenerator.GetWidth() - 10 ||
            z >= terrainGenerator.GetDepth() - 10)
            return false;

        float h = terrainGenerator.GetHeightWorld(x, z);
        float water = terrainGenerator.GetWaterLevel();

        if (h < water + connectionMinHeightAboveWater)
            return false;

        if (h > connectionMaxHeight)
            return false;

        return true;
    }

    float GetConnectionCellCost(Vector2Int from, Vector2Int to)
    {
        Vector3 a = ConnectionCellToWorld(from);
        Vector3 b = ConnectionCellToWorld(to);

        float heightDiff = Mathf.Abs(a.y - b.y);

        if (heightDiff > connectionSlopeLimit)
            return 99999f;

        float cost = 1f;

        cost += heightDiff * 4f;

        float h = terrainGenerator.GetHeightWorld(b.x, b.z);
        float water = terrainGenerator.GetWaterLevel();

        if (h < water + 3f)
            cost += 10f;

        if (h > maxVillageHeight)
            cost += 8f;

        if (IsNearExistingRoadOnly(b, roadWidth * 0.5f))
            cost -= 0.4f;

        return Mathf.Max(0.1f, cost);
    }

    bool IsNearExistingRoadOnly(Vector3 pos, float radius)
    {
        foreach (var road in globalRoads)
        {
            for (int i = 0; i < road.Count - 1; i++)
            {
                Vector2 p = new Vector2(pos.x, pos.z);
                Vector2 a = new Vector2(road[i].x, road[i].z);
                Vector2 b = new Vector2(road[i + 1].x, road[i + 1].z);

                if (DistancePointToSegment(p, a, b) < radius)
                    return true;
            }
        }

        return false;
    }

    bool IsTooCloseToOtherVillage(Vector2Int point)
    {
        foreach (var center in villageCenters)
        {
            if (Vector2Int.Distance(center, point) < minDistanceBetweenVillages)
                return true;
        }

        return false;
    }

    bool IsTooCloseToExistingRoads(List<List<Vector3>> roadsToCheck, float radius)
    {
        foreach (var newRoad in roadsToCheck)
        {
            foreach (var point in newRoad)
            {
                foreach (var oldRoad in globalRoads)
                {
                    for (int i = 0; i < oldRoad.Count - 1; i++)
                    {
                        Vector2 p = new Vector2(point.x, point.z);
                        Vector2 a = new Vector2(oldRoad[i].x, oldRoad[i].z);
                        Vector2 b = new Vector2(oldRoad[i + 1].x, oldRoad[i + 1].z);

                        if (DistancePointToSegment(p, a, b) < radius)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    Vector2Int FindFlatArea()
    {
        List<Vector2Int> validPoints = new List<Vector2Int>();

        for (int i = 0; i < 7000; i++)
        {
            int x = Random.Range(25, terrainGenerator.GetWidth() - 25);
            int z = Random.Range(25, terrainGenerator.GetDepth() - 25);

            Vector2Int p = new Vector2Int(x, z);

            if (IsTooCloseToOtherVillage(p))
                continue;

            if (IsGoodVillageArea(x, z))
                validPoints.Add(p);
        }

        if (validPoints.Count > 0)
            return validPoints[Random.Range(0, validPoints.Count)];

        return new Vector2Int(-1, -1);
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

    bool IsGoodRoadHeight(float x, float z)
    {
        float water = terrainGenerator.GetWaterLevel();

        Vector3[] checks =
        {
            new Vector3(x, 0, z),
            new Vector3(x + roadWidth * 0.5f, 0, z),
            new Vector3(x - roadWidth * 0.5f, 0, z),
            new Vector3(x, 0, z + roadWidth * 0.5f),
            new Vector3(x, 0, z - roadWidth * 0.5f)
        };

        foreach (Vector3 p in checks)
        {
            if (p.x < 0 || p.z < 0 ||
                p.x >= terrainGenerator.GetWidth() ||
                p.z >= terrainGenerator.GetDepth())
                return false;

            float h = terrainGenerator.GetHeightWorld(p.x, p.z);

            if (h < water + minRoadHeightAboveWater)
                return false;

            if (h > maxVillageHeight)
                return false;
        }

        return true;
    }

    void GenerateRoadData(Vector2Int start)
    {
        roadPoints.Clear();
        allRoads.Clear();
        intersections.Clear();

        float x = start.x;
        float z = start.y;

        for (int i = 0; i < villageSize; i++)
        {
            if (x < 10 || z < 10 ||
                x > terrainGenerator.GetWidth() - 10 ||
                z > terrainGenerator.GetDepth() - 10)
                break;

            if (!IsGoodRoadHeight(x, z))
                break;

            float y = terrainGenerator.GetHeightWorld(x, z);

            Vector3 point = new Vector3(x, y + roadHeightOffset, z);
            roadPoints.Add(point);

            x += 2.5f;
            z += Random.Range(-0.3f, 0.3f);
        }

        if (roadPoints.Count > 2)
            allRoads.Add(new List<Vector3>(roadPoints));

        int lastSideRoadIndex = -999;

        for (int i = 0; i < roadPoints.Count - 1; i++)
        {
            Vector3 a = roadPoints[i];
            Vector3 b = roadPoints[i + 1];

            if (i - lastSideRoadIndex < sideRoadSpacing)
                continue;

            if (i % 4 != 0)
                continue;

            if (Random.value > sideRoadChance)
                continue;

            Vector3 dir = (b - a).normalized;

            if (GenerateSideRoadData(a, dir))
            {
                intersections.Add(a);
                lastSideRoadIndex = i;
            }
        }
    }

    bool GenerateSideRoadData(Vector3 startPoint, Vector3 mainDir)
    {
        List<Vector3> sidePoints = new List<Vector3>();

        Vector3 dir = Vector3.Cross(Vector3.up, mainDir).normalized;

        if (Random.value > 0.5f)
            dir *= -1f;

        float x = startPoint.x;
        float z = startPoint.z;

        int length = Random.Range(minSideRoadLength, minSideRoadLength + 20);

        for (int i = 0; i < length; i++)
        {
            if (x < 10 || z < 10 ||
                x > terrainGenerator.GetWidth() - 10 ||
                z > terrainGenerator.GetDepth() - 10)
                break;

            if (!IsGoodRoadHeight(x, z))
                break;

            float y = terrainGenerator.GetHeightWorld(x, z);

            Vector3 point = new Vector3(x, y + roadHeightOffset, z);
            sidePoints.Add(point);

            x += dir.x * 2.5f;
            z += dir.z * 2.5f;
        }

        if (sidePoints.Count >= minSideRoadLength)
        {
            allRoads.Add(sidePoints);
            return true;
        }

        return false;
    }

    int CountPossibleHouseSpots()
    {
        int count = 0;

        foreach (var road in allRoads)
        {
            for (int i = 1; i < road.Count - 1; i++)
            {
                if (i % 3 != 0)
                    continue;

                Vector3 point = road[i];

                if (IsNearIntersection(point, 12f))
                    continue;

                Vector3 dir = (road[i + 1] - road[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;

                Vector3 left = point + side * buildingOffset;
                Vector3 right = point - side * buildingOffset;

                left.y = terrainGenerator.GetHeightWorld(left.x, left.z);
                right.y = terrainGenerator.GetHeightWorld(right.x, right.z);

                if (CanPlaceHousePreview(left))
                    count++;

                if (CanPlaceHousePreview(right))
                    count++;
            }
        }

        return count;
    }

    bool CanPlaceHousePreview(Vector3 pos)
    {
        float h = terrainGenerator.GetHeightWorld(pos.x, pos.z);
        float water = terrainGenerator.GetWaterLevel();

        if (h < water + minRoadHeightAboveWater)
            return false;

        if (h > maxVillageHeight)
            return false;

        if (IsNearAnyRoad(pos, minDistanceFromRoad))
            return false;

        return true;
    }

    void CreateAllRoadMeshes()
    {
        foreach (var road in allRoads)
            CreateRoadMesh(road);
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

            float roadY = points[i].y + roadHeightOffset;

            left.y = roadY;
            right.y = roadY;

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

                if (IsNearIntersection(point, 12f))
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

        if (h < water + minRoadHeightAboveWater)
            return false;

        if (h > maxVillageHeight)
            return false;

        if (!IsAreaFree(pos, houseBlockRadius))
            return false;

        if (IsNearAnyRoad(pos, minDistanceFromRoad))
            return false;

        if (Random.value <= 0.05f)
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
            for (int i = 0; i < road.Count - 1; i++)
            {
                Vector2 p = new Vector2(pos.x, pos.z);
                Vector2 a = new Vector2(road[i].x, road[i].z);
                Vector2 b = new Vector2(road[i + 1].x, road[i + 1].z);

                float distance = DistancePointToSegment(p, a, b);

                if (distance < radius + roadWidth * 0.5f)
                    return true;
            }
        }

        foreach (var road in globalRoads)
        {
            for (int i = 0; i < road.Count - 1; i++)
            {
                Vector2 p = new Vector2(pos.x, pos.z);
                Vector2 a = new Vector2(road[i].x, road[i].z);
                Vector2 b = new Vector2(road[i + 1].x, road[i + 1].z);

                float distance = DistancePointToSegment(p, a, b);

                if (distance < radius + roadWidth * 0.5f)
                    return true;
            }
        }

        return false;
    }

    float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;

        if (ab.sqrMagnitude <= 0.001f)
            return Vector2.Distance(p, a);

        float t = Vector2.Dot(p - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);

        Vector2 closest = a + ab * t;

        return Vector2.Distance(p, closest);
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
        house.name = "House";

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
                   root.parent.name == "Rock" ||
                   root.parent.name == "House"))
            {
                root = root.parent;
            }

            string objectName = root.name;

            if (objectName.Contains("Tree") ||
                objectName.Contains("Bush") ||
                objectName.Contains("Rock") ||
                objectName.Contains("House"))
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