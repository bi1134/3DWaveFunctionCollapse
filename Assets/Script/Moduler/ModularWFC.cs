using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ModularWFC : MonoBehaviour
{
    [SerializeField] private Grid grid;
    public int width, height, depth;
    public ModularTile[] foundationTiles; // y == 0 (ground layer only)
    public ModularTile[] midTiles;        // y in (1, height-2)
    public ModularTile[] topTiles;        // y == height-1 (top layer only)

    private ModularTile[] AllTiles => foundationTiles.Concat(midTiles).Concat(topTiles).ToArray();

    public ModularCell modularCell; //3D array of cells
    public List<ModularCell> gridComponents;
    private HashSet<Vector2Int> airColumns = new HashSet<Vector2Int>();

    private Dictionary<Vector3Int, ModularCell> cellLookup;

    private static readonly Vector3Int[] DirectionVectors = new Vector3Int[]
    {
        new Vector3Int(1, 0, 0),   // Right
        new Vector3Int(-1, 0, 0),  // Left
        new Vector3Int(0, 1, 0),   // Top
        new Vector3Int(0, -1, 0),  // Bottom
        new Vector3Int(0, 0, 1),   // Front
        new Vector3Int(0, 0, -1),  // Back
    };

    private void Awake()
    {
        gridComponents = new List<ModularCell>();
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        cellLookup = new Dictionary<Vector3Int, ModularCell>();

        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3Int cellPos = new Vector3Int(x, y, z);
                    Vector3 worldPos = grid.CellToWorld(cellPos);
                    // Place cell at world position
                    ModularCell newCell = Instantiate(modularCell, worldPos, Quaternion.identity, transform);

                    ModularTile[] layerTiles = y == 0 ? foundationTiles :
                           y == height - 1 ? topTiles :
                           midTiles;

                    newCell.CreateCell(layerTiles, cellPos);

                    gridComponents.Add(newCell);
                    cellLookup[cellPos] = newCell;
                }
            }
        }

        StartCoroutine(RunCollapse());
    }

    private IEnumerator RunCollapse()
    {
        for (int y = 0; y < height; y++) // Bottom-up
        {
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3Int pos = new Vector3Int(x, y, z);
                    Vector2Int columnKey = new Vector2Int(x, z);

                    if (airColumns.Contains(columnKey))
                    {
                        ModularCell airCell = cellLookup[pos];
                        if (!airCell.collapsed)
                        {
                            ModularTile air = AllTiles.FirstOrDefault(IsAirTile);
                            airCell.tileOptions = new ModularTile[] { air };
                            airCell.collapsed = true;
                            Instantiate(air.gameObject, airCell.worldPosition, air.transform.rotation, airCell.transform);
                        }
                        continue;
                    }

                    if (!cellLookup.TryGetValue(pos, out ModularCell cell) || cell.collapsed)
                        continue;

                    // --- Check tile BELOW ---
                    if (y > 0)
                    {
                        Vector3Int belowPos = new Vector3Int(x, y - 1, z);
                        if (cellLookup.TryGetValue(belowPos, out var belowCell) && belowCell.collapsed)
                        {
                            ModularTile belowTile = belowCell.tileOptions[0];
                            string supportSocket = GetSocketFromDirection(belowTile, Vector3Int.up);

                            if (IsAirTile(belowTile))
                            {
                                ModularTile air = AllTiles.FirstOrDefault(IsAirTile);
                                if (air != null)
                                {
                                    cell.tileOptions = new ModularTile[] { air };
                                    cell.collapsed = true;
                                    Instantiate(air.gameObject, cell.worldPosition, air.transform.rotation, cell.transform);
                                    airColumns.Add(columnKey); 
                                }
                                continue;
                            }

                            // Otherwise, filter valid options
                            cell.tileOptions = cell.tileOptions
                                .Where(t => SocketsMatch(supportSocket, GetSocketFromDirection(t, Vector3Int.down)))
                                .ToArray();
                        }
                    }

                    // No options left? Skip
                    if (cell.tileOptions.Length == 0)
                    {
                        Debug.LogWarning($"No valid tiles at {pos}. Skipping.");
                        continue;
                    }

                    // Collapse this tile
                    ModularTile chosen = cell.tileOptions[UnityEngine.Random.Range(0, cell.tileOptions.Length)];
                    cell.tileOptions = new ModularTile[] { chosen };
                    cell.collapsed = true;
                    Instantiate(chosen.gameObject, cell.worldPosition, chosen.transform.rotation, cell.transform);

                    PropagateFrom(cell);

                    yield return new WaitForSeconds(0.05f);
                }
            }
        }

    }



    private void PropagateFrom(ModularCell cell)
    {
        Vector3Int pos = cell.cellPosition;
        ModularTile tile = cell.tileOptions[0];

        foreach (Vector3Int dir in DirectionVectors)
        {
            Vector3Int neighborPos = pos + dir;
            if (!cellLookup.TryGetValue(neighborPos, out ModularCell neighbor) || neighbor.collapsed)
                continue;

            string mySocket = GetSocketFromDirection(tile, dir);

            List<ModularTile> validOptions = new List<ModularTile>();
            foreach (var option in neighbor.tileOptions)
            {
                string neighborSocket = GetSocketFromDirection(option, -dir);
                if (SocketsMatch(mySocket, neighborSocket))
                {
                    validOptions.Add(option);
                }
            }

            if (validOptions.Count != neighbor.tileOptions.Length)
            {
                neighbor.RecreateCell(validOptions.ToArray());
            }
        }
    }


    //check based in the rules if the sockets match
    private bool SocketsMatch(string socketA, string socketB)
    {
        if (socketA == "-1" || socketB == "-1") return false; //can only connect with air

        //symmetrical matching
        if (socketA.EndsWith("s") && socketB.EndsWith("s"))
        {
            return socketA.TrimEnd('s') == socketB.TrimEnd('s');
        }

        //asymmetrical matching
        if (socketA.EndsWith("f") && socketB.EndsWith("f"))
        {
            return socketA == socketB;
        }

        //vertical socketMatchs
        if(socketA.StartsWith("v") && socketB.StartsWith("v"))
        {
            var aSplit = socketA.Split('_');
            var bSplit = socketB.Split('_');
        
            return aSplit[0] == bSplit[0];
        }

        return false;
    }

    private bool IsAirTile(ModularTile tile)
    {
        return string.IsNullOrWhiteSpace(tile.top) || tile.top == "-1"
            && string.IsNullOrWhiteSpace(tile.bottom) || tile.bottom == "-1"
            && string.IsNullOrWhiteSpace(tile.left) || tile.left == "-1"
            && string.IsNullOrWhiteSpace(tile.right) || tile.right == "-1"
            && string.IsNullOrWhiteSpace(tile.front) || tile.front == "-1"
            && string.IsNullOrWhiteSpace(tile.back) || tile.back == "-1";
    }

    private string GetSocketFromDirection(ModularTile tile, Vector3Int dir)
    {
        if (dir == Vector3Int.right) return tile.right;
        if (dir == Vector3Int.left) return tile.left;
        if (dir == Vector3Int.up) return tile.top;
        if (dir == Vector3Int.down) return tile.bottom;
        if (dir == Vector3Int.forward) return tile.front;
        if (dir == Vector3Int.back) return tile.back;

        return "-1";
    }
}

