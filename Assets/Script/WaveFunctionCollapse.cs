using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// Wave Function Collapse system: spawns a grid of cells, each selecting a tile based on neighbor compatibility.
/// Each tile defines what tiles are valid in each direction (up, down, left, right).
/// </summary>
public class WaveFunctionCollapse : MonoBehaviour
{
    [SerializeField] private Grid grid;

    public int dimensions; // number of cells along one axis (grid is square)
    public Tile[] tileObjects; // list of all available tile prefabs
    public List<Cell> gridComponents; // all cell instances in the grid
    public Cell cellObj; // base cell prefab
    public Tile backupTile; // used if a cell fails to collapse

    private int iterationCount = 0;

    private void Awake()
    {
        gridComponents = new List<Cell>();
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        for (int y = 0; y < dimensions; y++)
        {
            for (int x = 0; x < dimensions; x++)
            {
                Vector3Int cellPos = new Vector3Int(x, 0, y);
                Vector3 worldPos = grid.CellToWorld(cellPos);

                // Place cell at world position with spacing
                Cell newCell = Instantiate(cellObj, worldPos, Quaternion.identity, transform);
                newCell.CreateCell(false, tileObjects, cellPos); // initialize with all tile options

                gridComponents.Add(newCell);
            }
        }

        StartCoroutine(CheckEntropy());
    }

    /// <summary>
    /// Picks the cell with the lowest entropy (fewest options) and collapses it.
    /// </summary>
    private IEnumerator CheckEntropy()
    {
        List<Cell> tempGrid = new List<Cell>(gridComponents);

        // Remove already collapsed cells
        tempGrid.RemoveAll(c => c.collapsed);

        if (tempGrid.Count == 0)
        {
            Debug.Log("Wave Function Collapse completed.");
            yield break;
        }

        // Sort by number of options (entropy)
        tempGrid.Sort((a, b) => a.tileOptions.Length - b.tileOptions.Length);

        // Only keep cells with the same (lowest) entropy
        tempGrid.RemoveAll(a => a.tileOptions.Length != tempGrid[0].tileOptions.Length);

        yield return new WaitForSeconds(0.125f); // small delay for visualization/debug

        CollapseCell(tempGrid);
    }

    /// <summary>
    /// Chooses one tile for the cell and instantiates it in the scene.
    /// </summary>
    void CollapseCell(List<Cell> tempGrid)
    {
        int randIndex = UnityEngine.Random.Range(0, tempGrid.Count);
        Cell cellToCollapse = tempGrid[randIndex];
        cellToCollapse.collapsed = true;

        try
        {
            // Pick a random tile from possible options
            Tile selectedTile = cellToCollapse.tileOptions[UnityEngine.Random.Range(0, cellToCollapse.tileOptions.Length)];
            cellToCollapse.tileOptions = new Tile[] { selectedTile };
        }
        catch
        {
            // Fallback if no options
            cellToCollapse.tileOptions = new Tile[] { backupTile };
        }

        Tile foundTile = cellToCollapse.tileOptions[0];

        // Instantiate the tile with its rotation
        Instantiate(foundTile, cellToCollapse.transform.position, foundTile.transform.rotation, cellToCollapse.transform);

        UpdateGeneration();
    }

    /// <summary>
    /// Filters all uncollapsed cells by eliminating tiles that are no longer valid based on neighbors.
    /// </summary>
    private void UpdateGeneration()
    {
        List<Cell> newGenerationCells = new List<Cell>(gridComponents);

        for (int y = 0; y < dimensions; y++)
        {
            for (int x = 0; x < dimensions; x++)
            {
                var index = x + (y * dimensions);

                if (gridComponents[index].collapsed)
                {
                    newGenerationCells[index] = gridComponents[index];
                }
                else
                {
                    List<Tile> options = new List<Tile>(tileObjects);

                    // --- UP: look at tile ABOVE (y + 1), check what it allows BELOW
                    if (y < dimensions - 1)
                    {
                        Cell above = gridComponents[x + (y + 1) * dimensions];
                        List<Tile> validOptions = new List<Tile>();

                        foreach (Tile possible in above.tileOptions)
                        {
                            var valid = possible.downNeighbors;
                            validOptions = validOptions.Concat(valid).ToList();
                        }

                        CheckValidity(options, validOptions);
                    }

                    // --- DOWN: look at tile BELOW (y - 1), check what it allows ABOVE
                    if (y > 0)
                    {
                        Cell below = gridComponents[x + (y - 1) * dimensions];
                        List<Tile> validOptions = new List<Tile>();

                        foreach (Tile possible in below.tileOptions)
                        {
                            var valid = possible.upNeighbors;
                            validOptions = validOptions.Concat(valid).ToList();
                        }

                        CheckValidity(options, validOptions);
                    }

                    // --- LEFT: look at tile LEFT (x - 1), check what it allows RIGHT
                    if (x > 0)
                    {
                        Cell left = gridComponents[(x - 1) + y * dimensions];
                        List<Tile> validOptions = new List<Tile>();

                        foreach (Tile possible in left.tileOptions)
                        {
                            var valid = possible.rightNeighbors;
                            validOptions = validOptions.Concat(valid).ToList();
                        }

                        CheckValidity(options, validOptions);
                    }

                    // --- RIGHT: look at tile RIGHT (x + 1), check what it allows LEFT
                    if (x < dimensions - 1)
                    {
                        Cell right = gridComponents[(x + 1) + y * dimensions];
                        List<Tile> validOptions = new List<Tile>();

                        foreach (Tile possible in right.tileOptions)
                        {
                            var valid = possible.leftNeighbors;
                            validOptions = validOptions.Concat(valid).ToList();
                        }

                        CheckValidity(options, validOptions);
                    }

                    // Apply updated options to this cell
                    newGenerationCells[index].RecreateCell(options.ToArray());
                }
            }
        }

        gridComponents = newGenerationCells;

        if (iterationCount < dimensions * dimensions)
        {
            StartCoroutine(CheckEntropy());
        }
    }


    /// <summary>
    /// Removes tile options that are not in the list of valid neighbors.
    /// </summary>
    private void CheckValidity(List<Tile> optionList, List<Tile> validOption)
    {
        for (int x = optionList.Count - 1; x >= 0; x--)
        {
            var element = optionList[x];
            if (!validOption.Contains(element))
            {
                optionList.RemoveAt(x);
            }
        }
    }
}
