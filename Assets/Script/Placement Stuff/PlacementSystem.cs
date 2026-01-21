using System;
using System.Collections.Generic;
using UnityEngine;
using WFC_Sudoku; // Import WFC namespace

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] GameObject mouseInd;
    [SerializeField] GameObject cellInd;

    [SerializeField] private InputManager inputManager;

    [SerializeField] private Grid grid;

    [SerializeField] private ObjectDataBaseSO database;
    private int selectedObjectIndex = -1;

    [SerializeField] private GameObject gridVisual;
    
    [Header("WFC Integration")]
    [SerializeField] private WFCCell cellContainerPrefab;
    [Tooltip("Offset multiplier for spawning. (0.5, 0, 0.5) = Center X/Z, Ground Y.")]
    [SerializeField] private Vector3 cellAlignment = new Vector3(0.5f, 0f, 0.5f);
    
    // Simple counter for unique placement IDs
    private int placedObjectCount = 0;

    // Unified Grid Data (Single Source of Truth)
    private GridData gridData;

    private void Start()
    {
        StopPlacement();
        gridData = new GridData();
    }

   
    private void Update()
    {
        if(selectedObjectIndex < 0) return;

        Vector3 mousePosition = inputManager.GetPlacementPosition();
        Vector3Int cellPosition = grid.WorldToCell(mousePosition);

        mouseInd.transform.position = mousePosition;
        cellInd.transform.position = grid.CellToWorld(cellPosition);
        
    }

    public void StartPlacement(int currentID)
    {
        StopPlacement();
        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == currentID);

        //default is -1 meaning not found
        if (selectedObjectIndex < 0)
        {
            Debug.LogError("Object with ID " + currentID + " not found in database.");
            return;
        }

        gridVisual.SetActive(true);
        cellInd.SetActive(true);
        inputManager.OnClicked += PlaceObject;
        inputManager.OnExit += StopPlacement;
    }
    private void PlaceObject()
    {
        if(inputManager.isPointerOverUI() || selectedObjectIndex < 0)
        {
            return;
        }
        Vector3 mousePosition = inputManager.GetPlacementPosition();
        Vector3Int cellPosition = grid.WorldToCell(mousePosition);

        bool placementValidity = CheckPlacementValidity(cellPosition, selectedObjectIndex);
        if(placementValidity == false)
        {
            Debug.Log("Placement invalid due to grid data constraints.");
            return;
        }

        // Get the Module Prefab from Database
        GameObject modulePrefabObj = database.objectsData[selectedObjectIndex].Prefab;
        WFCModule modulePrefab = modulePrefabObj.GetComponent<WFCModule>();

        if (modulePrefab == null)
        {
            Debug.LogError("Selected object does not have a WFCModule component!");
            return;
        }

        // Spawn Container
        // Use user-defined alignment. default (0.5, 0, 0.5) puts it on floor, centered.
        Vector3 worldPos = grid.CellToWorld(cellPosition) + Vector3.Scale(grid.cellSize, cellAlignment);
        
        WFCCell newCell = Instantiate(cellContainerPrefab, worldPos, Quaternion.identity, transform);
        newCell.gridPosition = cellPosition;
        
        // Initialize Cell with ONLY the selected module (Collapsed State)
        List<WFCModule> singleList = new List<WFCModule> { modulePrefab };
        newCell.Initialize(singleList, cellPosition, 255); 
        newCell.Collapse();

        // Register to GRID DATA (Single Source of Truth)
        gridData.AddObjectAt(cellPosition, database.objectsData[selectedObjectIndex].Size, database.objectsData[selectedObjectIndex].ID, placedObjectCount, newCell);
        
        placedObjectCount++;

        // Trigger Auto-Variation Update
        WFCVisualizer.UpdateVisualsAround(newCell, GetPlacedCell, Vector3Int.zero); 
    }

    private bool CheckPlacementValidity(Vector3Int cellPosition, int selectedObjectIndex)
    {
        return gridData.CanPlaceObjectAt(cellPosition, database.objectsData[selectedObjectIndex].Size);
    }

    private WFCCell GetPlacedCell(Vector3Int pos)
    {
        return gridData.GetCellAt(pos);
    }

    private void StopPlacement()
    {
        selectedObjectIndex = -1;
        gridVisual.SetActive(false);
        cellInd.SetActive(false);
        inputManager.OnClicked -= PlaceObject;
        inputManager.OnExit -= StopPlacement;
    }

}
