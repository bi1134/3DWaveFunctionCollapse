using System.Collections.Generic;
using UnityEngine;
using WFC_Sudoku;

public class GridData
{
    Dictionary<Vector3Int, PlacementData> placedObjects = new Dictionary<Vector3Int, PlacementData>(); 

    public void AddObjectAt(Vector3Int position, Vector2Int objectSize, int ID, int placedObjectIndex, WFCCell newCell)
    {
        List<Vector3Int> positionToOccupy = GetOccupiedPositions(position, objectSize);

        PlacementData placementData = new PlacementData(positionToOccupy, ID, placedObjectIndex, newCell);

        foreach (var pos in positionToOccupy)
        {
            if(placedObjects.ContainsKey(pos))
            {
                throw new System.Exception("Position already occupied");
            }

            placedObjects[pos] = placementData;
        }
    }

    private List<Vector3Int> GetOccupiedPositions(Vector3Int gridPosition, Vector2Int objectSize)
    {
        var occupiedPositions = new List<Vector3Int>(objectSize.x * objectSize.y);

        for (int x = 0; x < objectSize.x; x++)
        {
            for (int z = 0; z < objectSize.y; z++) // objectSize.y == depth
            {
                occupiedPositions.Add(gridPosition + new Vector3Int(x, 0, z));
            }
        }
        return occupiedPositions;
    }

    public bool CanPlaceObjectAt(Vector3Int gridPosition, Vector2Int objectSize)
    {
        var occupiedPositions = GetOccupiedPositions(gridPosition, objectSize);
        foreach (var pos in occupiedPositions)
        {
            //basicaly checking if the position is already occupied
            if (placedObjects.ContainsKey(pos))
            {
                return false;
            }
        }
        return true;
    }

    public WFCCell GetCellAt(Vector3Int gridPosition)
    {
        if (placedObjects.TryGetValue(gridPosition, out PlacementData data))
        {
            return data.storedCell;
        }
        return null;
    }
}

public class PlacementData
{
    public List<Vector3Int> occupiedPositions;

    public int ID { get; set; }

    public int PlacedObjectIndex { get; set; }
    
    public WFCCell storedCell;

    //constructor
    public PlacementData(List<Vector3Int> occupiedPositions, int id, int placedObjectIndex, WFCCell cell)
    {
        this.occupiedPositions = occupiedPositions;
        ID = id;
        PlacedObjectIndex = placedObjectIndex;
        storedCell = cell;
    }
}
