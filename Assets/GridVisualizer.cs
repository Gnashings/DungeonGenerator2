using UnityEngine;

public class GridVisualizer : MonoBehaviour
{
    private DungeonGenerator generator;
    [SerializeField] private bool drawCells = false;

    private void Awake()
    {
        generator = GetComponent<DungeonGenerator>();

        if (generator == null)
            Debug.LogError("GridVisualizer requires DungeonGenerator on the same object.");
    }

    private void OnDrawGizmos()
    {
        if (generator == null)
            generator = GetComponent<DungeonGenerator>();

        if (generator == null)
            return;

        GridSettings gridSettings = generator.GridSettings;
        if (gridSettings == null)
            return;

        int w = Mathf.Max(1, gridSettings.width);
        int h = Mathf.Max(1, gridSettings.height);
        float s = Mathf.Max(0.01f, gridSettings.cellSize);

        Vector3 origin = new Vector3(gridSettings.origin.x, 0f, gridSettings.origin.y);

        Gizmos.color = Color.gray;

        // Outline
        Vector3 bottomLeft  = origin;
        Vector3 bottomRight = origin + new Vector3(w * s, 0f, 0f);
        Vector3 topLeft     = origin + new Vector3(0f, 0f, h * s);
        Vector3 topRight    = origin + new Vector3(w * s, 0f, h * s);

        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);

        // Grid lines
        for (int x = 1; x < w; x++)
        {
            Vector3 a = origin + new Vector3(x * s, 0f, 0f);
            Vector3 b = origin + new Vector3(x * s, 0f, h * s);
            Gizmos.DrawLine(a, b);
        }

        for (int y = 1; y < h; y++)
        {
            Vector3 a = origin + new Vector3(0f, 0f, y * s);
            Vector3 b = origin + new Vector3(w * s, 0f, y * s);
            Gizmos.DrawLine(a, b);
        }

        if (!drawCells) return;

        if (generator.DungeonGrid == null) return;

        Gizmos.color = new Color(0f, 1f, 0f, 0.35f);

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (generator.DungeonGrid.GetCell(x, y) == 0) continue;

                Vector3 center = origin + new Vector3((x + 0.5f) * s, 0f, (y + 0.5f) * s);
                Gizmos.DrawCube(center, new Vector3(s, 0.01f, s));
            }
    }
}
