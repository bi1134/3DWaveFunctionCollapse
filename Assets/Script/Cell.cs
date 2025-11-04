using UnityEngine;

public class Cell : MonoBehaviour
{
    public Vector3Int cellPosition;
    public bool collapsed;
    public Tile[] tileOptions;

    public void CreateCell(bool collapseState, Tile[] tiles, Vector3Int cellPos)
    {
        collapsed = collapseState;
        tileOptions = tiles;
        cellPosition = cellPos;
    }

    public void RecreateCell(Tile[] tiles)
    {
        tileOptions = tiles;
    }
}
