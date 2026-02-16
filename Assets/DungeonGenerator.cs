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
    private DungeonGrid grid;

    private void Awake()
    {
        grid = new DungeonGrid(gridSettings);
    }
    
    public int[,] mapSize = {{1, 2, 3, 4 ,5},{6, 7, 8, 9, 0}};

    void Start()
    {
        int rows = mapSize.GetLength(0);
        int cols = mapSize.GetLength(1);
        string templine = "";
    
        for (int x = 0; x < rows; x++)
        {
            for (int y = 0; y < cols; y++)
            {
                templine += mapSize[x,y].ToString();
            }

            Debug.Log(templine);
            templine = "";
        }
    }
}


public class DungeonGrid
{
    public int Width { get; }
    public int Height { get; }
    public float CellSize { get; }
    public Vector2 Origin { get; }

    // TODO: replace with official rooms
    private readonly int[,] cells;

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
}
