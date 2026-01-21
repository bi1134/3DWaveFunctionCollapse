using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TWaveFunctionCollapse : MonoBehaviour
{
    [SerializeField] private Grid grid;

    public int width, height, depth; //x,y,z dimensions of the grid
    public TTile[] tileObjects; // list of all available tile prefabs
    public List<TCell> gridComponents; // all cell instances in the grid
    public TCell cellObj; // base cell prefab
    public TTile backupTile; // used if a cell fails to collapse

    private readonly List<TCell> layerBuffer = new();

    private void Awake()
    {
        gridComponents = new List<TCell>();
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3Int cellPos = new Vector3Int(x, y, z);
                    Vector3 worldPos = grid.CellToWorld(cellPos);

                    // Place cell at world position with spacing
                    TCell newCell = Instantiate(cellObj, worldPos, Quaternion.identity, transform);
                    newCell.CreateCell(false, tileObjects, cellPos); // initialize with all tile options

                    gridComponents.Add(newCell);
                }
            }
        }

        StartCoroutine(CheckEntropy());
    }

    /// <summary>
    /// Picks the cell with the lowest entropy (fewest options) and collapses it.
    /// </summary>
    private IEnumerator CheckEntropy()
    {
        Queue<TCell> queue = new Queue<TCell>();

        //enqueue all ground cells (y = 0)
        foreach (var cell in gridComponents.Where(c => c.cellPosition.y == 0))
            queue.Enqueue(cell);

        while (queue.Count > 0)
        {
            TCell currentCell = queue.Dequeue();

            //if collapsed, skip
            if (currentCell.collapsed)
                continue;

            CollapseCell(currentCell);

            //after collapsing, put neighbors into the queue
            foreach (var neighborCell in GetUncollapsedNeighbors(currentCell))
            {
                ApplyNeighborConstraints(neighborCell);
                if (!neighborCell.collapsed)
                    queue.Enqueue(neighborCell);
            }
            yield return null;
        }

    }

    private IEnumerable<TCell> GetUncollapsedNeighbors(TCell cell)
    {
        Vector3Int[] direction =
        {
            Vector3Int.right, Vector3Int.left,
            Vector3Int.forward, Vector3Int.back,
            Vector3Int.up, Vector3Int.down
        };

        foreach (var dir in direction)
        {
            var n = gridComponents.FirstOrDefault(c => c.cellPosition == cell.cellPosition + dir);
            if (n != null && !n.collapsed)
                yield return n;
        }
    }

    /// <summary>
    /// Chooses and collapses a tile for this cell.
    /// Applies vertical constraints based on the cell below.
    /// </summary>
    private void CollapseCell(TCell cell)
    {
        int cellY = cell.cellPosition.y;

        // 1. Vertical Logic: Check below for support
        if (cellY > 0)
        {
            TCell belowCell = gridComponents.First(c => c.cellPosition == cell.cellPosition + Vector3Int.down);

            if (belowCell != null && belowCell.collapsed)
            {
                TTile belowTile = belowCell.tileOptions[0];

                // HYBRID PERMISSIVE CHECK:
                // Allow if:
                // 1. It is explicitly in the 'topNeighbors' list (Socket match)
                // 2. OR It is a valid TileType pair (Logic match) AND the list is not exclusively preventing it (hard to know, so we trust Logic)

                // We trust "IsVerticalPairValid" to be the robust rule for structure.
                // We use inspector list for specific socket details if available, but we don't let it kill 'Air' or 'Top' valid placements easily.

                bool useInspectorList = belowTile.topNeighbors != null && belowTile.topNeighbors.Length > 0;

                cell.tileOptions = cell.tileOptions
                    .Where(t =>
                    {
                        bool typeValid = IsVerticalPairValid(belowTile, t);
                        bool listValid = !useInspectorList || belowTile.topNeighbors.Contains(t);

                        // If it's Air, always allow (unless type says no, but type says yes for air)
                        if (t.tileType == TileType.Air) return true;

                        // If it's a Top category item (Fence/Tower), prioritize Type validity to allow it to spawn
                        // even if user forgot to add it to the Wall's list.
                        if (t.tileType == TileType.Fence || t.tileType == TileType.Tower)
                            return typeValid;

                        // For others, strictly require Type Valid AND (List Valid OR List Empty)
                        return typeValid && listValid;
                    })
                    .ToArray();
            }
        }

        // 2. Filter by category based on height triggers (Layers) -> SIMPLIFIED
        // We now rely on WEIGHTS for selection preference, so we only filter invalid types.
        cell.tileOptions = cell.tileOptions
            .Where(tile =>
            {
                var type = tile.tileType;

                // SPECIAL RULE: Doors must be at Y=0 only
                if (type == TileType.Door && cellY != 0) return false;

                // SPECIAL RULE: Ground (Y=0) -> allow anything valid on ground
                if (cellY == 0) return true; 

                return true;
            }).ToArray();

        // 3. Selection Weight Bias (Structural Mass Logic)
        // Instead of hard filtering, we let the Weight System handle the preferences!

        if (cell.tileOptions.Length == 0)
        {
            // Debug.LogError($"[Collapse Failed] Cell {cell.cellPosition} has 0 options. Using Backup.");
            cell.tileOptions = new TTile[] { backupTile };
        }

        // 3. Collapse this cell with weight logic
        TTile chosenTile = GetWeightedTile(cell.tileOptions, cellY, height);
        
        // Final sanity check for logging
        if (cellY == height - 1 && (chosenTile.tileType == TileType.Fence || chosenTile.tileType == TileType.Tower))
             Debug.Log($"[Y={cellY}] Spawning Top Piece: {chosenTile.name}");
             
        cell.tileOptions = new TTile[] { chosenTile };
        cell.collapsed = true;

        Instantiate(chosenTile, cell.transform.position, Quaternion.identity, cell.transform);
    }


    /// <summary>
    /// Picks one tile based on "Structural Mass".
    /// Lower Y = Prefers Heavy (High spawnWeight).
    /// Higher Y = Prefers Light (Low spawnWeight).
    /// </summary>
    private TTile GetWeightedTile(TTile[] options, int y, int maxY)
    {
        List<(TTile tile, float score)> weightedTiles = new();
        
        // Normalized Height (0.0 to 1.0)
        float h = (float)y / (float)(maxY - 1);
        
        // Ideal Mass for this height.
        // Bottom (h=0) -> Wants 1.0 (Heavy)
        // Top (h=1) -> Wants 0.0 (Light)
        float idealMass = Mathf.Lerp(0.9f, 0.1f, h);

        foreach (var tile in options)
        {
            float mass = tile.spawnWeight; // Using spawnWeight as Physical Mass
            
            // Calculate detailed score
            // 1. Distance from ideal mass ( Closer is better )
            float dist = Mathf.Abs(mass - idealMass);
            float massScore = 1f / (dist + 0.1f); // Inverse distance scoring
            
            // 2. Special Top Boost (To ensure roofs)
            if (y == maxY - 1 && (tile.tileType == TileType.Fence || tile.tileType == TileType.Tower))
            {
                massScore *= 10f; // Artificial boost for Roof pieces at roof height
                // Note: Fence/Tower should have Low Mass (e.g. 0.1) which matches Ideal Mass (0.1) at top, so double win.
            }

            weightedTiles.Add((tile, massScore));
        }

        float totalScore = weightedTiles.Sum(t => t.score);
        float rand = Random.value * totalScore;

        foreach (var (tile, score) in weightedTiles)
        {
            rand -= score;
            if (rand <= 0f)
                return tile;
        }

        return weightedTiles.Last().tile; // fallback
    }

    private void ApplyNeighborConstraints(TCell cell)
    {
        List<TTile> allowed = new List<TTile>(tileObjects);

        Vector3Int p = cell.cellPosition;

        // ORDER: Bottom -> Left -> Right -> Front -> Back -> (Top Ignored)

        // 1. BOTTOM (Critical Foundation)
        TCell bottom = gridComponents.FirstOrDefault(c => c.cellPosition == p + Vector3Int.down);
        if (bottom != null && bottom.collapsed)
        {
             TTile bTile = bottom.tileOptions[0];
             bool useList = bTile.topNeighbors != null && bTile.topNeighbors.Length > 0;
             
             allowed = allowed.Where(t => 
             {
                 if (t.tileType == TileType.Air) return true;
                 if (t.tileType == TileType.Fence || t.tileType == TileType.Tower) return IsVerticalPairValid(bTile, t);
                 return IsVerticalPairValid(bTile, t) && (!useList || bTile.topNeighbors.Contains(t));
             }).ToList();
        }

        // TOP (IGNORED)
        // User Request: "no need for checking for top"


        // HORIZONTAL CHECKS (Left, Right, Front, Back)
        // Helper to check horizontal validity combining List + Type
        bool CheckH(TTile neighbor, TTile candidate, Vector3Int dir)
        {
            if (candidate.tileType == TileType.Air || neighbor.tileType == TileType.Air) return true;

            // Inspector List check
            bool listHas = false;
            // Note: Neighbor is to the Left of Candidate, so checking Neighbor's RIGHT list
            if (dir == Vector3Int.left) listHas = neighbor.rightNeighbors.Contains(candidate); 
            if (dir == Vector3Int.right) listHas = neighbor.leftNeighbors.Contains(candidate);
            if (dir == Vector3Int.forward) listHas = neighbor.backNeighbors.Contains(candidate);
            if (dir == Vector3Int.back) listHas = neighbor.frontNeighbors.Contains(candidate);

            bool inspectorDefined = false;
            if (dir == Vector3Int.left) inspectorDefined = neighbor.rightNeighbors != null && neighbor.rightNeighbors.Length > 0;
            if (dir == Vector3Int.right) inspectorDefined = neighbor.leftNeighbors != null && neighbor.leftNeighbors.Length > 0;
            if (dir == Vector3Int.forward) inspectorDefined = neighbor.backNeighbors != null && neighbor.backNeighbors.Length > 0;
            if (dir == Vector3Int.back) inspectorDefined = neighbor.frontNeighbors != null && neighbor.frontNeighbors.Length > 0;

            // Relaxed "Auto-Connect"
            bool typeValid = IsHorizontalPairValid(neighbor, candidate);

            if (inspectorDefined) return listHas; 
            return listHas || typeValid;
        }

        // 2. LEFT
        TCell left = gridComponents.FirstOrDefault(c => c.cellPosition == p + Vector3Int.left);
        if (left != null && left.collapsed)
            allowed = allowed.Where(t => CheckH(left.tileOptions[0], t, Vector3Int.left)).ToList();
            
        // 3. RIGHT
        TCell right = gridComponents.FirstOrDefault(c => c.cellPosition == p + Vector3Int.right);
        if (right != null && right.collapsed)
            allowed = allowed.Where(t => CheckH(right.tileOptions[0], t, Vector3Int.right)).ToList();

        // 4. FRONT
        TCell front = gridComponents.FirstOrDefault(c => c.cellPosition == p + Vector3Int.forward);
        if (front != null && front.collapsed)
            allowed = allowed.Where(t => CheckH(front.tileOptions[0], t, Vector3Int.forward)).ToList();

        // 5. BACK
        TCell back = gridComponents.FirstOrDefault(c => c.cellPosition == p + Vector3Int.back);
        if (back != null && back.collapsed)
            allowed = allowed.Where(t => CheckH(back.tileOptions[0], t, Vector3Int.back)).ToList();

        if (allowed.Count > 0)
            cell.tileOptions = allowed.ToArray();
    }

    /// <summary>
    /// Returns true if 'top' can be placed strictly above 'bottom'.
    /// </summary>
    private bool IsVerticalPairValid(TTile bottom, TTile top)
    {
        // Air is always valid
        if (bottom.tileType == TileType.Air || top.tileType == TileType.Air) return true;

        TileType b = bottom.tileType;
        TileType t = top.tileType;

        switch (b)
        {
            case TileType.Door:
                // Above Door: Wall or Corner
                return t == TileType.Wall || t == TileType.Corner;

            case TileType.Wall:
                // Above Wall: Wall, Corner, Fence, Tower
                return t == TileType.Wall || t == TileType.Corner || t == TileType.Fence || t == TileType.Tower;

            case TileType.Corner:
                // Above Corner: Corner, Wall, Fence, Tower (assuming corners work like walls for support)
                return t == TileType.Corner || t == TileType.Wall || t == TileType.Fence || t == TileType.Tower;

            case TileType.Fence:
                // Above Fence: Tower (Air handled above)
                return t == TileType.Tower;

            case TileType.Tower:
                // Above Tower: Only Air (handled at start), so nothing else typical
                return false;

            default:
                return true;
        }

        // Reverse check logic if needed? No, we check strictly bottom->top relation.
        // What about top constraints?
        // e.g. If Top is Fence, Bottom MUST be Wall or Corner.
        // The switch above covers "If Bottom is X, Top can be Y".
        // Let's add specific Top-down restrictions to be safe.

        /* 
         * Rules Refresher:
         * Doors -> Ground only (Handled in CollapseCell layer check)
         * Walls -> Stack on Walls, Doors.
         * Fences -> Must have Wall or Corner below.
         * Towers -> Must have Wall, Corner, Fence below.
         */
    }
    private bool IsHorizontalPairValid(TTile a, TTile b)
    {
        // Simple logic: 
        // Walls connect to Walls, Corners
        // Corners connect to Walls, Corners
        // Fences connect to Fences (maybe Walls?)
        // Doors?

        // Air always matches
        if (a.tileType == TileType.Air || b.tileType == TileType.Air) return true;

        if (a.tileType == TileType.Wall) return b.tileType == TileType.Wall || b.tileType == TileType.Corner || b.tileType == TileType.Door;
        if (a.tileType == TileType.Corner) return b.tileType == TileType.Wall || b.tileType == TileType.Corner;
        if (a.tileType == TileType.Door) return b.tileType == TileType.Wall; // Door typically next to wall
        if (a.tileType == TileType.Fence) return b.tileType == TileType.Fence || b.tileType == TileType.Tower;
        if (a.tileType == TileType.Tower) return b.tileType == TileType.Fence || b.tileType == TileType.Tower;

        return false;
    }
}
