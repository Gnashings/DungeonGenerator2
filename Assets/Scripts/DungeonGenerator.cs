using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BaseGraph;

[Serializable]
public class GridSettings
{
    [Min(1)] public int width = 50;
    [Min(1)] public int height = 50;

    //world-space conversion
    [Min(0.01f)] public float cellSize = 1f;
    public Vector2 origin = Vector2.zero;
}

//selectors for various rooms
public enum RoomType
{
    Normal,
    Start,
    End,
    Boss,
    Mob,
    Treasure
}

//room node information, not to be placed in BaseGraph due to context changes.
public class RoomNode
{
    public int Id;
    public Vector2Int Center;
    public RectInt Bounds;
    public int Depth = -1;
    public RoomType Type = RoomType.Normal;
}

public class DungeonGenerator : MonoBehaviour
{

    [Header("Grid")]
    [SerializeField] private GridSettings gridSettings = new GridSettings();
    public GridSettings GridSettings => gridSettings;
    public DungeonGrid DungeonGrid { get; private set; }

    private readonly List<RoomNode> rooms = new List<RoomNode>();
    public IReadOnlyList<RoomNode> Rooms => rooms;                           // exposed list for coloring only
    private Graph<RoomNode> roomGraph = new Graph<RoomNode>();

    private void Awake()
    {
        DungeonGrid = new DungeonGrid(gridSettings);
    }

    [Header("Rooms")]
    [SerializeField] private int roomCount = 10;
    [SerializeField] private Vector2Int roomSizeMin = new Vector2Int(5, 5);
    [SerializeField] private Vector2Int roomSizeMax = new Vector2Int(11, 11);

    [Header("Room Placement Rules")]
    [SerializeField] private bool allowRoomOverlap = true;
    [SerializeField] private int maxAttemptsPerRoom = 50;
    [SerializeField] private int overlapPadding = 0;

    [Header("Room Type Spawn Rules")]
    [SerializeField] int bossRoomCount = 1;
    [SerializeField] int mobRoomCount = 3;
    [SerializeField] int treasureRoomCount = 1;

    [SerializeField] int bossMinDepth = 3;
    [SerializeField] int mobMinDepth = 1;
    [SerializeField] int treasureMinDepth = 1;

    [Header("3D Prefabs")]
    [SerializeField] private Transform dungeonParent;
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private float wallHeight = 2f;

    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerInstance;
    [SerializeField] private float playerSpawnHeight = 1f;

    [Header("Dungeon Seeding")]
    [SerializeField] private bool useSeed = false;
    [SerializeField] private int seed = 12345;

    private void Start()
    {
        rooms.Clear();
        roomGraph = new Graph<RoomNode>();
        DungeonGrid.Clear(0);

        if (!useSeed)
        {
            seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        // Only instance of random for seed generation.
        System.Random rng = new System.Random(seed);

        for (int i = 0; i < roomCount; i++)
        {
            for (int attempt = 0; attempt < maxAttemptsPerRoom; attempt++)
            {
                int cx = rng.Next(0, DungeonGrid.Width);
                int cy = rng.Next(0, DungeonGrid.Height);

                int rw = rng.Next(roomSizeMin.x, roomSizeMax.x + 1);
                int rh = rng.Next(roomSizeMin.y, roomSizeMax.y + 1);

                // Used for overlap checking only
                GetRoomExtents(cx, cy, rw, rh, overlapPadding, out int checkX0, out int checkX1, out int checkY0, out int checkY1);
                // Used for actual carved room data
                GetRoomExtents(cx, cy, rw, rh, 0, out int roomX0, out int roomX1, out int roomY0, out int roomY1);

                if (!allowRoomOverlap && !CanPlaceRoom(checkX0, checkX1, checkY0, checkY1))
                    continue;

                CarveRoomCentered(cx, cy, rw, rh);

                RoomNode room = new RoomNode
                {
                    Id = rooms.Count,
                    Center = new Vector2Int(cx, cy),
                    Bounds = new RectInt(roomX0, roomY0, (roomX1 - roomX0) + 1, (roomY1 - roomY0) + 1),
                    Depth = -1,
                    Type = RoomType.Normal
                };

                rooms.Add(room);
                roomGraph.AddNode(room);

                break;
            }
        }

        if (rooms.Count >= 2)
        {
            ConnectRoomsMST();
            ComputeRoomDepths(rooms[0]);
            AssignRoomTypes(rng);
            SpawnPlayerAtStartRoom();
            SpawnDungeon3D();


            /*
            foreach (RoomNode room in rooms)
            {
                Debug.Log($"Room {room.Id} | Depth: {room.Depth} | Type: {room.Type} | Center: {room.Center}");
            }
            */
        }
    }

    //carves a room out from the center tile
    private void CarveRoomCentered(int cx, int cy, int w, int h)
    {
        int halfW = w / 2;
        int halfH = h / 2;

        int x0 = cx - halfW;
        int x1 = cx + halfW;
        int y0 = cy - halfH;
        int y1 = cy + halfH;

        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            {
                if (!DungeonGrid.InBounds(x, y)) continue;
                DungeonGrid.Set(x, y, 1);
            }
    }
    private void GetRoomExtents(int cx, int cy, int w, int h, int padding, out int x0, out int x1, out int y0, out int y1)
    {
        int halfW = w / 2;
        int halfH = h / 2;

        x0 = cx - halfW - padding;
        x1 = cx + halfW + padding;
        y0 = cy - halfH - padding;
        y1 = cy + halfH + padding;
    }

    private bool CanPlaceRoom(int x0, int x1, int y0, int y1)
    {
        // Reject if any part is out of bounds (keeps the room fully inside)
        if (!DungeonGrid.InBounds(x0, y0)) return false;
        if (!DungeonGrid.InBounds(x1, y1)) return false;

        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            {
                if (DungeonGrid.GetCell(x, y) != 0)
                    return false;
            }

        return true;
    }

    // Prim implimentation
    private void ConnectRoomsMST()
    {
        int n = rooms.Count;
        if (n < 2) return;

        bool[] inTree = new bool[n];
        int[] minCost = new int[n];
        int[] parent = new int[n];

        for (int i = 0; i < n; i++)
        {
            minCost[i] = int.MaxValue;
            parent[i] = -1;
        }

        // Start from room 0
        inTree[0] = true;

        // Initialize costs from node 0
        for (int i = 1; i < n; i++)
        {
            minCost[i] = DistSq(rooms[0].Center, rooms[i].Center);
            parent[i] = 0;
        }

        HashSet<(int a, int b)> carvedEdges = new HashSet<(int a, int b)>();

        // Add n-1 edges
        for (int step = 1; step < n; step++)
        {
            int next = -1;
            int best = int.MaxValue;

            // Pick the cheapest node not in the tree yet
            for (int i = 0; i < n; i++)
            {
                if (inTree[i]) continue;
                if (minCost[i] < best)
                {
                    best = minCost[i];
                    next = i;
                }
            }

            if (next == -1) break; // Safety

            // Add edge parent[next] -> next
            int a = parent[next];
            int b = next;

            int lo = Mathf.Min(a, b);
            int hi = Mathf.Max(a, b);

            if (!carvedEdges.Contains((lo, hi)))
            {
                carvedEdges.Add((lo, hi));

                roomGraph.AddEdge(rooms[a], rooms[b]);
                CarveCorridorL(rooms[a].Center, rooms[b].Center);
            }

            inTree[next] = true;

            // Update costs using the newly added node
            for (int j = 0; j < n; j++)
            {
                if (inTree[j]) continue;

                int cost = DistSq(rooms[next].Center, rooms[j].Center);
                if (cost < minCost[j])
                {
                    minCost[j] = cost;
                    parent[j] = next;
                }
            }
        }
    }

    private void ComputeRoomDepths(RoomNode startRoom)
    {
        foreach (RoomNode room in rooms)
        {
            room.Depth = -1;
            room.Type = RoomType.Normal;
        }

        if (startRoom == null) return;

        Queue<RoomNode> queue = new Queue<RoomNode>();

        startRoom.Depth = 0;
        startRoom.Type = RoomType.Start;
        queue.Enqueue(startRoom);

        while (queue.Count > 0)
        {
            RoomNode current = queue.Dequeue();

            foreach (RoomNode neighbor in roomGraph.GetNeighbors(current))
            {
                if (neighbor.Depth != -1) continue;

                neighbor.Depth = current.Depth + 1;
                queue.Enqueue(neighbor);
            }
        }
    }

    //Ensures all generations are created with a start, end, and speciality rooms for entity spawning.
    private void AssignRoomTypes(System.Random rng)
    {
        if (rooms.Count == 0) return;

        foreach (RoomNode room in rooms)
        {
            room.Type = RoomType.Normal;
        }

        RoomNode startRoom = rooms[0];
        startRoom.Type = RoomType.Start;

        RoomNode endRoom = GetDeepestRoomExcluding(startRoom);
        if (endRoom != null)
        {
            endRoom.Type = RoomType.End;
        }

        AssignMultipleRoomsOfType(
            rng,
            RoomType.Boss,
            bossRoomCount,
            bossMinDepth,
            startRoom,
            endRoom
        );

        AssignMultipleRoomsOfType(
            rng,
            RoomType.Treasure,
            treasureRoomCount,
            treasureMinDepth,
            startRoom,
            endRoom
        );

        AssignMultipleRoomsOfType(
            rng,
            RoomType.Mob,
            mobRoomCount,
            mobMinDepth,
            startRoom,
            endRoom
        );
    }
    private void AssignMultipleRoomsOfType(
    System.Random rng,
    RoomType type,
    int count,
    int minDepth,
    params RoomNode[] excludedRooms)
    {
        List<RoomNode> candidates = new List<RoomNode>();

        foreach (RoomNode room in rooms)
        {
            if (room.Depth < minDepth) continue;
            if (room.Type != RoomType.Normal) continue;
            if (IsExcluded(room, excludedRooms)) continue;

            candidates.Add(room);
        }

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int index = rng.Next(0, candidates.Count);
            RoomNode chosenRoom = candidates[index];

            chosenRoom.Type = type;
            candidates.RemoveAt(index);
        }
    }

    private RoomNode GetDeepestRoomExcluding(params RoomNode[] excludedRooms)
    {
        RoomNode deepest = null;

        foreach (RoomNode room in rooms)
        {
            if (IsExcluded(room, excludedRooms)) continue;

            if (deepest == null || room.Depth > deepest.Depth)
            {
                deepest = room;
            }
        }

        return deepest;
    }

    private RoomNode GetRandomRoomAtMinDepth(System.Random rng, int minDepth, params RoomNode[] excludedRooms)
    {
        List<RoomNode> candidates = new List<RoomNode>();

        foreach (RoomNode room in rooms)
        {
            if (room.Depth < minDepth) continue;
            if (IsExcluded(room, excludedRooms)) continue;

            candidates.Add(room);
        }

        if (candidates.Count == 0) return null;

        int index = rng.Next(0, candidates.Count);
        return candidates[index];
    }

    private bool IsExcluded(RoomNode room, params RoomNode[] excludedRooms)
    {
        foreach (RoomNode excluded in excludedRooms)
        {
            if (excluded == null) continue;
            if (room == excluded) return true;
        }

        return false;
    }

    private int DistSq(Vector2Int a, Vector2Int b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        return (dx * dx) + (dy * dy);
    }

    private void CarveCorridorL(Vector2Int from, Vector2Int to)
    {
        // Move in X
        int x = from.x;
        int y = from.y;

        int stepX = (to.x > x) ? 1 : -1;
        while (x != to.x)
        {
            if (DungeonGrid.InBounds(x, y)) DungeonGrid.Set(x, y, 1);
            x += stepX;
        }

        // Move in Y
        int stepY = (to.y > y) ? 1 : -1;
        while (y != to.y)
        {
            if (DungeonGrid.InBounds(x, y)) DungeonGrid.Set(x, y, 1);
            y += stepY;
        }

        // Ensure the destination tile is carved
        if (DungeonGrid.InBounds(x, y)) DungeonGrid.Set(x, y, 1);
    }

    private void SpawnDungeon3D()
    {
        if (dungeonParent != null)
        {
            for (int i = dungeonParent.childCount - 1; i >= 0; i--)
            {
                Destroy(dungeonParent.GetChild(i).gameObject);
            }
        }

        for (int x = 0; x < DungeonGrid.Width; x++)
            for (int y = 0; y < DungeonGrid.Height; y++)
            {
                if (DungeonGrid.GetCell(x, y) != 1) continue;

                Vector3 floorPos = GridToWorld(x, y);
                Instantiate(floorPrefab, floorPos, Quaternion.identity, dungeonParent);

                TrySpawnWall(x + 1, y, floorPos + new Vector3(DungeonGrid.CellSize / 2f, wallHeight / 2f, 0f), Quaternion.Euler(0f, 90f, 0f));
                TrySpawnWall(x - 1, y, floorPos + new Vector3(-DungeonGrid.CellSize / 2f, wallHeight / 2f, 0f), Quaternion.Euler(0f, 90f, 0f));
                TrySpawnWall(x, y + 1, floorPos + new Vector3(0f, wallHeight / 2f, DungeonGrid.CellSize / 2f), Quaternion.identity);
                TrySpawnWall(x, y - 1, floorPos + new Vector3(0f, wallHeight / 2f, -DungeonGrid.CellSize / 2f), Quaternion.identity);
            }
    }

    private void TrySpawnWall(int neighborX, int neighborY, Vector3 wallPosition, Quaternion rotation)
    {
        if (DungeonGrid.InBounds(neighborX, neighborY) && DungeonGrid.GetCell(neighborX, neighborY) == 1)
            return;

        Instantiate(wallPrefab, wallPosition, rotation, dungeonParent);
    }

    private Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(
            DungeonGrid.Origin.x + (x + 0.5f) * DungeonGrid.CellSize,
            0f,
            DungeonGrid.Origin.y + (y + 0.5f) * DungeonGrid.CellSize
        );
    }

    private void SpawnPlayerAtStartRoom()
    {
        RoomNode startRoom = null;

        foreach (RoomNode room in rooms)
        {
            if (room.Type == RoomType.Start)
            {
                startRoom = room;
                break;
            }
        }

        if (startRoom == null)
        {
            Debug.LogWarning("No start room found. Cannot spawn player.");
            return;
        }

        Vector3 spawnPosition = GridToWorld(startRoom.Center.x, startRoom.Center.y);
        spawnPosition.y = playerSpawnHeight;

        if (playerInstance != null)
        {
            playerInstance.position = spawnPosition;
            return;
        }

        if (playerPrefab != null)
        {
            GameObject player = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            playerInstance = player.transform;
        }
    }
}


public class DungeonGrid
{
    public int Width { get; }
    public int Height { get; }
    public float CellSize { get; }
    public Vector2 Origin { get; }

    private readonly int[,] cells;
    public int GetCell(int x, int y) => Get(x, y);

    public DungeonGrid(GridSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (settings.width < 1) throw new ArgumentOutOfRangeException(nameof(settings.width));
        if (settings.height < 1) throw new ArgumentOutOfRangeException(nameof(settings.height));
        if (settings.cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(settings.cellSize));

        Width = settings.width;
        Height = settings.height;
        CellSize = settings.cellSize;
        Origin = settings.origin;

        cells = new int[Width, Height];
    }

    public bool InBounds(int x, int y)
        => x >= 0 && x < Width && y >= 0 && y < Height;

    public int Get(int x, int y)
    {
        if (!InBounds(x, y)) throw new IndexOutOfRangeException($"({x},{y}) out of bounds.");
        return cells[x, y];
    }

    public void Set(int x, int y, int value)
    {
        if (!InBounds(x, y)) throw new IndexOutOfRangeException($"({x},{y}) out of bounds.");
        cells[x, y] = value;
    }

    public void Clear(int value = 0)
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                cells[x, y] = value;
    }
}
