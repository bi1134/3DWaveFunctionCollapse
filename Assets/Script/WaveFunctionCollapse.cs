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
    public int dimentions; // number of cells along one axis (grid is square)
    public Tile[] tileObjects; // list of all available tile prefabs
    public List<Cell> gridComponents; // all cell instances in the grid
    public Cell cellObj; // base cell prefab
    public Tile backupTile; // used if a cell fails to collapse
    public int cellSize = 1; // spacing between cells

    private int iterationCount = 0;

    private void Awake()
    {
        gridComponents = new List<Cell>();
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        for (int y = 0; y < dimentions; y++)
        {
            for (int x = 0; x < dimentions; x++)
            {
                // Place cell at world position with spacing
                Vector3 position = new Vector3(x * cellSize, 0, y * cellSize);
                Cell newCell = Instantiate(cellObj, position, Quaternion.identity);
                newCell.CreateCell(false, tileObjects); // initialize with all tile options

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
        Instantiate(foundTile, cellToCollapse.transform.position, foundTile.transform.rotation);

        UpdateGeneration();
    }

    /// <summary>
    /// Filters all uncollapsed cells by eliminating tiles that are no longer valid based on neighbors.
    /// </summary>
    private void UpdateGeneration()
    {
        List<Cell> newGenerationCells = new List<Cell>(gridComponents);

        for (int y = 0; y < dimentions; y++)
        {
            for (int x = 0; x < dimentions; x++)
            {
                var index = x + (y * dimentions);

                if (gridComponents[index].collapsed)
                {
                    newGenerationCells[index] = gridComponents[index];
                }
                else
                {
                    // Start with all tile options
                    List<Tile> options = new List<Tile>(tileObjects);

                    // --- UP ---
                    // We are checking cell ABOVE (y - 1), so:
                    // Check what that cell allows BELOW (downNeighbors)
                    if (y < dimentions - 1)
                    {
                        Cell down = gridComponents[x + ((y + 1) * dimentions)];
                        List<Tile> validOptions = new List<Tile>();

                        foreach (Tile possibleOptions in down.tileOptions)
                        {
                            int validIndex = Array.FindIndex(tileObjects, obj => obj == possibleOptions);
                            Tile[] valid = tileObjects[validIndex].upNeighbors;
                            validOptions = validOptions.Concat(valid).ToList();
                        }

                        CheckValidity(options, validOptions);
                    }

                    // --- DOWN ---
                    // We are checking cell BELOW (y + 1), so:
                    // Check what that cell allows ABOVE (upNeighbors)
                    if (y > 0)
                    {
                        Cell up = gridComponents[x + ((y - 1) * dimentions)];
                        List<Tile> validOptions = new List<Tile>();

                        foreach (Tile possibleOptions in up.tileOptions)
                        {
                            int validIndex = Array.FindIndex(tileObjects, obj => obj == possibleOptions);
                            Tile[] valid = tileObjects[validIndex].downNeighbors;
                            validOptions = validOptions.Concat(valid).ToList();
                        }

                        CheckValidity(options, validOptions);
                    }

                    // --- LEFT ---
                    // We are checking cell to the RIGHT (x + 1), so:
                    // Check what that cell allows to the LEFT (rightNeighbors)
                    if (x > 0)
                    {
                        Cell left = gridComponents[(x - 1) + (y * dimentions)];
                        List<Tile> validOptions = new List<Tile>();

                        foreach (Tile neighborTile in left.tileOptions)
                        {
                            // Check what that tile allows to its right
                            Tile[] valid = neighborTile.rightNeighbors;
                            validOptions = validOptions.Concat(valid).ToList();
                        }

                        CheckValidity(options, validOptions);
                    }

                    // --- RIGHT ---
                    // We are checking cell to the LEFT (x - 1), so:
                    // Check what that cell allows to the RIGHT (leftNeighbors)
                    if (x < dimentions - 1)
                    {
                        Cell right = gridComponents[(x + 1) + (y * dimentions)];
                        List<Tile> validOptions = new List<Tile>();

                        foreach (Tile neighborTile in right.tileOptions)
                        {
                            // Check what that tile allows to its left
                            Tile[] valid = neighborTile.leftNeighbors;
                            validOptions = validOptions.Concat(valid).ToList();
                        }

                        CheckValidity(options, validOptions);
                    }

                    // Set new filtered options to this cell
                    Tile[] newTileList = options.ToArray();
                    newGenerationCells[index].RecreateCell(newTileList);
                }
            }
        }

        // Apply updated grid
        gridComponents = newGenerationCells;

        if (iterationCount < dimentions * dimentions)
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
