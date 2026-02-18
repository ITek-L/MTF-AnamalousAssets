using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonGenerator2D : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap floorMap;
    public Tilemap wallMap;

    [Header("Tiles")]
    public TileBase floorTile;
    public TileBase wallTile;

    [Header("Map Bounds (tile coordinates)")]
    public int width = 220;
    public int height = 220;

    [Header("Rooms")]
    [Range(5, 80)] public int roomCount = 35;
    public Vector2Int roomMin = new Vector2Int(10, 8);
    public Vector2Int roomMax = new Vector2Int(22, 16);
    [Tooltip("Empty space (tiles) enforced between rooms.")]
    public int roomPadding = 3;

    [Header("Corridors")]
    [Tooltip("1 = 3 tiles wide, 2 = 5 tiles wide.")]
    public int corridorHalfWidth = 1;
    [Range(0f, 0.5f)] public float extraConnectionChance = 0.15f;

    [Header("Debug / Spawn")]
    public Vector2Int PlayerSpawnCell { get; private set; }

    private readonly HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
    private readonly List<RectInt> rooms = new List<RectInt>();

    void Start()
    {
        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (!floorMap || !wallMap || !floorTile || !wallTile)
        {
            Debug.LogError("DungeonGenerator2D: Assign Floor/Walls Tilemaps and Floor/Wall Tiles in the Inspector.");
            return;
        }

        floorMap.ClearAllTiles();
        wallMap.ClearAllTiles();
        floor.Clear();
        rooms.Clear();

        PlaceRoomsClean();
        SetPlayerSpawnFromFirstRoom();
        ConnectRoomsMST_EdgeDoors();
        PaintTiles();

        FindFirstObjectByType<PlayerSpawner>()?.Spawn();
    }

    // -------------------------
    // Room placement (clean)
    // -------------------------
    void PlaceRoomsClean()
    {
        int attempts = 0;
        int maxAttempts = roomCount * 60;

        int halfW = width / 2;
        int halfH = height / 2;

        while (rooms.Count < roomCount && attempts < maxAttempts)
        {
            attempts++;

            int w = Random.Range(roomMin.x, roomMax.x + 1);
            int h = Random.Range(roomMin.y, roomMax.y + 1);

            int x = Random.Range(-halfW, halfW - w);
            int y = Random.Range(-halfH, halfH - h);

            RectInt newRoom = new RectInt(x, y, w, h);

            // Enforce padding so rooms don't touch / smear corridors
            RectInt padded = new RectInt(
                newRoom.xMin - roomPadding,
                newRoom.yMin - roomPadding,
                newRoom.width + roomPadding * 2,
                newRoom.height + roomPadding * 2
            );

            bool overlaps = false;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (padded.Overlaps(rooms[i]))
                {
                    overlaps = true;
                    break;
                }
            }
            if (overlaps) continue;

            rooms.Add(newRoom);

            // Carve room floor
            for (int ix = newRoom.xMin; ix < newRoom.xMax; ix++)
            for (int iy = newRoom.yMin; iy < newRoom.yMax; iy++)
                floor.Add(new Vector2Int(ix, iy));
        }

        Debug.Log($"Rooms placed: {rooms.Count}/{roomCount} (attempts: {attempts})");
    }

    void SetPlayerSpawnFromFirstRoom()
    {
        if (rooms.Count == 0)
        {
            PlayerSpawnCell = Vector2Int.zero;
            return;
        }

        RectInt r = rooms[0];
        PlayerSpawnCell = new Vector2Int(r.x + r.width / 2, r.y + r.height / 2);
    }

    // -------------------------
    // Connections (MST + loops) using edge "door" points
    // -------------------------
    void ConnectRoomsMST_EdgeDoors()
    {
        if (rooms.Count < 2) return;

        // Room centers
        List<Vector2Int> centers = new List<Vector2Int>(rooms.Count);
        for (int i = 0; i < rooms.Count; i++)
        {
            RectInt r = rooms[i];
            centers.Add(new Vector2Int(r.x + r.width / 2, r.y + r.height / 2));
        }

        // All edges
        var edges = new List<Edge>();
        for (int i = 0; i < centers.Count; i++)
        for (int j = i + 1; j < centers.Count; j++)
        {
            float d = Vector2Int.Distance(centers[i], centers[j]);
            edges.Add(new Edge(i, j, d));
        }
        edges.Sort((a, b) => a.dist.CompareTo(b.dist));

        // Kruskal MST (Union-Find)
        int n = centers.Count;
        int[] parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;

        int Find(int x) => parent[x] == x ? x : (parent[x] = Find(parent[x]));
        void Union(int a, int b) => parent[Find(a)] = Find(b);

        var chosen = new List<(int a, int b)>();

        for (int i = 0; i < edges.Count && chosen.Count < n - 1; i++)
        {
            var e = edges[i];
            if (Find(e.a) == Find(e.b)) continue;
            Union(e.a, e.b);
            chosen.Add((e.a, e.b));
        }

        // Optional extra loops
        for (int i = 0; i < edges.Count; i++)
        {
            if (Random.value > extraConnectionChance) continue;

            var e = edges[i];
            bool already = false;
            for (int k = 0; k < chosen.Count; k++)
            {
                var c = chosen[k];
                if ((c.a == e.a && c.b == e.b) || (c.a == e.b && c.b == e.a))
                {
                    already = true;
                    break;
                }
            }
            if (!already) chosen.Add((e.a, e.b));
        }

        // Carve corridors from room-edge "doors" (facing each other)
        foreach (var (a, b) in chosen)
        {
            RectInt roomA = rooms[a];
            RectInt roomB = rooms[b];

            Vector2Int centerA = centers[a];
            Vector2Int centerB = centers[b];

            // Door points on edges facing the other room
            Vector2Int doorA = GetDoorPointFacing(roomA, centerB);
            Vector2Int doorB = GetDoorPointFacing(roomB, centerA);

            // Start corridor just outside each room
            Vector2Int start = StepOutsideRoom(roomA, doorA, centerB);
            Vector2Int end = StepOutsideRoom(roomB, doorB, centerA);

            // Ensure the "door" tiles are walkable
            floor.Add(doorA);
            floor.Add(doorB);

            CarveCorridorWide(start, end);
        }
    }

    // Picks a point on the room perimeter on the side facing the target.
    Vector2Int GetDoorPointFacing(RectInt room, Vector2Int target)
    {
        Vector2Int center = new Vector2Int(room.x + room.width / 2, room.y + room.height / 2);
        Vector2Int dir = target - center;

        // Pick which side to use (horizontal vs vertical) based on dominant axis
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
        {
            // Left or right edge
            int x = dir.x >= 0 ? (room.xMax - 1) : room.xMin;
            int y = Mathf.Clamp(target.y, room.yMin, room.yMax - 1);
            return new Vector2Int(x, y);
        }
        else
        {
            // Bottom or top edge
            int y = dir.y >= 0 ? (room.yMax - 1) : room.yMin;
            int x = Mathf.Clamp(target.x, room.xMin, room.xMax - 1);
            return new Vector2Int(x, y);
        }
    }

    // Steps one tile outward from the room along the direction toward "toward".
    Vector2Int StepOutsideRoom(RectInt room, Vector2Int edgePoint, Vector2Int toward)
    {
        Vector2Int dir = toward - edgePoint;
        if (dir == Vector2Int.zero) dir = Vector2Int.right;

        // Choose the primary axis direction
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            dir = new Vector2Int(dir.x >= 0 ? 1 : -1, 0);
        else
            dir = new Vector2Int(0, dir.y >= 0 ? 1 : -1);

        return edgePoint + dir;
    }

    struct Edge
    {
        public int a, b;
        public float dist;
        public Edge(int a, int b, float d) { this.a = a; this.b = b; dist = d; }
    }

    // -------------------------
    // Corridor carving (wide)
    // -------------------------
    void CarveCorridorWide(Vector2Int a, Vector2Int b)
    {
        bool horizFirst = Random.value < 0.5f;

        if (horizFirst)
        {
            CarveLineWide(a, new Vector2Int(b.x, a.y));
            CarveLineWide(new Vector2Int(b.x, a.y), b);
        }
        else
        {
            CarveLineWide(a, new Vector2Int(a.x, b.y));
            CarveLineWide(new Vector2Int(a.x, b.y), b);
        }
    }

    void CarveLineWide(Vector2Int from, Vector2Int to)
    {
        Vector2Int p = from;
        int dx = to.x == from.x ? 0 : (to.x > from.x ? 1 : -1);
        int dy = to.y == from.y ? 0 : (to.y > from.y ? 1 : -1);

        while (p != to)
        {
            CarveWideAt(p);
            p += new Vector2Int(dx, dy);
        }
        CarveWideAt(to);
    }

    void CarveWideAt(Vector2Int p)
    {
        for (int x = -corridorHalfWidth; x <= corridorHalfWidth; x++)
        for (int y = -corridorHalfWidth; y <= corridorHalfWidth; y++)
            floor.Add(new Vector2Int(p.x + x, p.y + y));
    }

    // -------------------------
    // Painting
    // -------------------------
    void PaintTiles()
    {
        foreach (var p in floor)
            floorMap.SetTile((Vector3Int)p, floorTile);

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        foreach (var p in floor)
        {
            foreach (var d in dirs)
            {
                var n = p + d;
                if (!floor.Contains(n))
                    wallMap.SetTile((Vector3Int)n, wallTile);
            }
        }
    }
}
