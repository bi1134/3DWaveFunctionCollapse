using UnityEngine;

public class TCell : MonoBehaviour
{
    public Vector3Int cellPosition;
    public bool collapsed;
    public TTile[] tileOptions;

    public void CreateCell(bool collapseState, TTile[] tiles, Vector3Int cellPos)
    {
        collapsed = collapseState;
        tileOptions = tiles;
        cellPosition = cellPos;
    }

    public void RecreateCell(TTile[] tiles)
    {
        tileOptions = tiles;
    }

}
