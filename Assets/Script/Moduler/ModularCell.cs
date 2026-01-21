using UnityEngine;

public class ModularCell : MonoBehaviour
{
    public Vector3Int cellPosition;
    public Vector3 worldPosition;
    public bool collapsed;
    public ModularTile[] tileOptions;

    public void CreateCell( ModularTile[] tiles, Vector3Int cellPos)
    {
        collapsed = false;
        tileOptions = tiles;
        cellPosition = cellPos;
        worldPosition = transform.position;
    }

    public void RecreateCell(ModularTile[] tiles)
    {
        tileOptions = tiles;
    }

}
