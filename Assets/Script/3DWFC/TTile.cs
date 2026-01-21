using UnityEngine;

public class TTile : MonoBehaviour
{
    [Header("How likely this tile is to appear (1 = common, 0.1 = rare)")]
    [Range(0.01f, 1f)]
    public float spawnWeight = 1f;

    [Header("Tiles that can appear IN FRONT of this tile (Z+1)")]
    public TTile[] frontNeighbors;

    [Header("Tiles that can appear BEHIND this tile (Z-1)")]
    public TTile[] backNeighbors;

    [Header("Tiles that can appear to the LEFT of this tile (X-1)")]
    public TTile[] leftNeighbors;

    [Header("Tiles that can appear to the RIGHT of this tile (X+1)")]
    public TTile[] rightNeighbors;

    [Header("Tiles that can appear ABOVE this tile (Y+1)")]
    public TTile[] topNeighbors;

    [Header("Tiles that can appear BELOW this tile (Y-1)")]
    public TTile[] bottomNeighbors;

    [Header("Optional: Category for logic filtering (e.g., Ground, Mid, Top)")]
    public TileCategory category;

    [Header("Specific Type for Vertical Logic")]
    public TileType tileType;
}

[System.Flags]
public enum TileCategory
{
    None = 0,
    Ground = 1 << 0,
    Middle = 1 << 1,
    Top = 1 << 2,
}

public enum TileType
{
    Air,
    Door,
    Wall,
    Corner,
    Fence,
    Tower
}
