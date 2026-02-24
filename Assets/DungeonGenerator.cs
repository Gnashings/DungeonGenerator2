using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class GridSettings
{
    [Min(1)] public int width = 50;
    [Min(1)] public int height = 50;

    //world-space conversion
    [Min(0.01f)] public float cellSize = 1f;
    public Vector2 origin = Vector2.zero;
}

public class DungeonGenerator : MonoBehaviour
{

    [Header("Grid")]
    [SerializeField] private GridSettings gridSettings = new GridSettings();
    public GridSettings GridSettings => gridSettings;
    public DungeonGrid DungeonGrid { get; private set; }

    private readonly List<Vector2Int> roomCenters = new List<Vector2Int>();

    private void Awake()
    {
        DungeonGrid = new DungeonGrid(gridSettings);
    }

    [Header("Rooms")]
    [SerializeField] private int roomCount = 10;
    [SerializeField] private Vector2Int roomSizeMin = new Vector2Int(5, 5);
    [SerializeField] private Vector2Int roomSizeMax = new Vector2Int(11, 11);

    [Header("Dungeon Seeding")]
    [SerializeField] private bool useSeed = false;
    [SerializeField] private int seed = 12345;

    private void Start()
    {
        roomCenters.Clear();
        DungeonGrid.Clear(0);

        // Only instance of random for seed generation.
        System.Random rng = useSeed ? new System.Random(seed) : new System.Random();

        if (!useSeed)
        {
            seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        for (int i = 0; i < roomCount; i++)
        {
            int cx = rng.Next(0, DungeonGrid.Width);
            int cy = rng.Next(0, DungeonGrid.Height);

            int rw = rng.Next(roomSizeMin.x, roomSizeMax.x + 1);
            int rh = rng.Next(roomSizeMin.y, roomSizeMax.y + 1);

            CarveRoomCentered(cx, cy, rw, rh);
            roomCenters.Add(new Vector2Int(cx, cy));
        }

        ConnectRoomsMST();
    }

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

    // Prim implimentation
    private void ConnectRoomsMST()
    {
        int n = roomCenters.Count;
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
            minCost[i] = DistSq(roomCenters[0], roomCenters[i]);
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
                CarveCorridorL(roomCenters[a], roomCenters[b]);
            }

            inTree[next] = true;

            // Update costs using the newly added node
            for (int j = 0; j < n; j++)
            {
                if (inTree[j]) continue;

                int cost = DistSq(roomCenters[next], roomCenters[j]);
                if (cost < minCost[j])
                {
                    minCost[j] = cost;
                    parent[j] = next;
                }
            }
        }
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
