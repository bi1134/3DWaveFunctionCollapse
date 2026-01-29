using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TerrainGenerator
{
    [System.Serializable]
    public class WFCSolver
    {
        [Header("Grid Settings (Managed by Builder)")]
        // Public for debug, but set by Builder
        public Vector3Int gridSize;
        public Vector3 cellAlignment;
        public WFCCell cellPrefab;
        public Grid unityGrid;
        public WFCBuilder builder;

        public int maxRetries = 5; 

        // Runtime State
        [Header("Runtime")]
        public List<WFCModule> allModules;
        
        // For the WFC algorithm - works with any IWFCCell implementation
        public List<IWFCCell> allCells = new List<IWFCCell>();
        public Dictionary<Vector3Int, IWFCCell> cellMap = new Dictionary<Vector3Int, IWFCCell>();
        public Queue<IWFCCell> initialQueue = new Queue<IWFCCell>();
        
        // Visual cells only (WFCCell with MonoBehaviour) - for cleanup and visual-specific operations
        public List<WFCCell> visualCells = new List<WFCCell>();

        // P1 FIX: Pooled collections to avoid per-call allocations
        private HashSet<WFCModule> _allowedPool = new HashSet<WFCModule>();
        private List<WFCModule> _keptModulesPool = new List<WFCModule>(64);

        // --- API FOR WFCBUILDER ---
        
        private MonoBehaviour context; // For Coroutines and Instantiation context
        private Transform container;

        // --- API FOR WFCBUILDER ---

        public void Initialize(Vector3Int size, List<WFCModule> modules, Grid grid, WFCCell prefab, Vector3 alignment, WFCBuilder b, Transform containerStr, HashSet<int> restrictedYLevels = null, HashSet<int> dualGridYLevels = null)
        {
            this.gridSize = size;
            this.allModules = modules;
            this.unityGrid = grid;
            this.cellPrefab = prefab;
            this.cellAlignment = alignment;
            this.builder = b;
            this.container = containerStr;

            // Clear all cell collections
            ClearCells();
            allCells.Clear();
            cellMap.Clear();
            
            // Create cells for each position
            for (int z = 0; z < gridSize.z; z++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    if (restrictedYLevels != null && !restrictedYLevels.Contains(y)) continue;
                    
                    bool isDual = dualGridYLevels != null && dualGridYLevels.Contains(y);

                    for (int x = 0; x < gridSize.x; x++)
                    {
                        Vector3Int cellPos = new Vector3Int(x, y, z);
                        
                        if (isDual)
                        {
                            // Dual Grid: Create lightweight WFCCellData (no GameObject)
                            WFCCellData cellData = new WFCCellData();
                            cellData.Initialize(allModules, cellPos, gridSize.y);
                            allCells.Add(cellData);
                            cellMap.Add(cellPos, cellData);
                        }
                        else
                        {
                            // Normal Grid: Create WFCCell with visual support
                            Vector3 worldPos = (unityGrid != null) ?
                               unityGrid.CellToWorld(cellPos) + Vector3.Scale(unityGrid.cellSize, cellAlignment) :
                               new Vector3(x, y, z); // Fallback

                            WFCCell c = Object.Instantiate(cellPrefab, worldPos, Quaternion.identity, container);
                            c.suppressVisuals = false;
                            c.Initialize(allModules, cellPos, gridSize.y);
                            allCells.Add(c);
                            cellMap.Add(cellPos, c);
                            visualCells.Add(c); // Track for cleanup and visual operations
                        }
                    }
                }
            }

            initialQueue.Clear();
            constraints.Clear();
        }


        private void ClearCells()
        {
             if (visualCells != null)
             {
                 foreach(var c in visualCells)
                 {
                     if (c != null)
                     {
                         if(Application.isPlaying) Object.Destroy(c.gameObject);
                         else Object.DestroyImmediate(c.gameObject);
                     }
                 }
                 visualCells.Clear();
             }
        }

        // Constraints Storage
        private Dictionary<Vector3Int, WFCModule> constraints = new Dictionary<Vector3Int, WFCModule>();

        public void ForceCollapse(Vector3Int pos, WFCModule module)
        {
            // Store for resets
            if (constraints.ContainsKey(pos)) constraints[pos] = module;
            else constraints.Add(pos, module);
            
            // Apply immediately
            ApplyConstraint(pos, module);
        }

        private void ApplyConstraint(Vector3Int pos, WFCModule module)
        {
            IWFCCell cell = GetCellAt(pos);
            if (cell != null && !cell.Collapsed)
            {
                // Strict check: Module MUST be in allModules (name check for verify)
                if (cell.PossibleModules.Any(m => m.name == module.name)) 
                {
                    cell.PossibleModules.Clear();
                    cell.PossibleModules.Add(module);
                    cell.Collapsed = true;
                    
                    // Spawn Visual only for WFCCell (not WFCCellData)
                    if (cell is WFCCell visualCell && !visualCell.suppressVisuals && module != null && module.gameObject != null)
                    {
                        visualCell.SpawnVisual(module.gameObject);
                    }
                    
                    if (!initialQueue.Contains(cell)) initialQueue.Enqueue(cell);
                    OnCellCollapsed?.Invoke(pos);
                }
            }
        }

        // Start Removed. Builder calls Initialize -> RunWFC.

        public IEnumerator RunWFC()
        {
            int currentRetries = 0;

            while (currentRetries <= maxRetries)
            {
                // Reset Grid
                ResetGridState();
                bool contradictionFound = false;

                // 2. Propagate Initial Constraints (from ForceCollapse)
                if (initialQueue.Count > 0)
                {
                    Propagate(initialQueue);
                }

                int steps = 0;
                // WFC Loop
                while (true)
                {
                    IWFCCell cellToCollapse = GetLowestEntropyCell();

                    if (cellToCollapse == null)
                    {
                        // Check if we actually finished or just ran out of options
                        if (allCells.Any(c => !c.Collapsed))
                        {
                            Debug.LogWarning($"Contradiction found! (Attempt {currentRetries + 1}/{maxRetries + 1})");
                            contradictionFound = true;
                            break; // Break inner loop to retry
                        }

                        // ResolveVariations now called via OnFinished event in WFCBuilder
                        OnFinished?.Invoke();
                        yield break; // Exit EVERYTHING
                    }

                    cellToCollapse.Collapse();
                    OnCellCollapsed?.Invoke(cellToCollapse.GridPosition);

                    Queue<IWFCCell> queue = new Queue<IWFCCell>();
                    queue.Enqueue(cellToCollapse);
                    Propagate(queue);

                    steps++;
                    if (steps % 50 == 0) yield return null; // Yield every 50 steps for speed + responsiveness
                }

                if (contradictionFound)
                {
                    currentRetries++;
                    yield return new WaitForSeconds(0.1f); // Brief pause before restart
                    continue; // Restart outer loop
                }
            }

            Debug.LogError("WFC Failed to generate a valid grid after max retries.");
        }

        public System.Func<Vector3Int, WFCCell> globalLookup; // Assigned by Builder - for visual cross-chunk lookups
        public System.Func<Vector3Int, IWFCCell> globalCellLookup; // Assigned by Builder - for algorithm/dual grid lookups (any cell type)
        public System.Func<Vector3Int, bool> globalChunkExists; // Assigned by Builder - checks if chunk is loaded (for edge handling)
        public float worldScale = 1.0f; // Assigned by Builder
        public Vector2Int chunkCoordinate; // Assigned by Builder
        public System.Action OnFinished;
        public System.Action<Vector3Int> OnCellCollapsed; // Dual Grid Hook

        public void RefreshVisualsOnEdge(Vector3Int direction)
        {
            // Iterate all cells on the specific edge and re-resolve visual
            // Direction: Left (-1,0,0), Right (1,0,0), etc.
            
            // Logic: simpler to iterate ALL border cells if direction is hard?
            // Let's do specific edge.
            
            int w = gridSize.x;
            int h = gridSize.y;
            int d = gridSize.z; // depth
            
            if (direction == Vector3Int.left)   for(int z=0; z<d; z++) for(int y=0; y<h; y++) ReResolve(new Vector3Int(0, y, z));
            if (direction == Vector3Int.right)  for(int z=0; z<d; z++) for(int y=0; y<h; y++) ReResolve(new Vector3Int(w-1, y, z));
            if (direction == Vector3Int.back)   for(int x=0; x<w; x++) for(int y=0; y<h; y++) ReResolve(new Vector3Int(x, y, 0));
            if (direction == Vector3Int.forward)for(int x=0; x<w; x++) for(int y=0; y<h; y++) ReResolve(new Vector3Int(x, y, d-1));
        }


        /// <summary>
        /// Resolves visual variations for all visual cells (WFCCell, not WFCCellData).
        /// Call this after all cells are collapsed to update visuals.
        /// </summary>
        public void ResolveVariations()
        {
            foreach (var cell in visualCells)
            {
                // We pass chunkCoordinate so Visualizer can calculate Global Grid Pos
                WFCVisualizer.ResolveVisualForCell(cell, GetVisualCellAt, gridSize, globalLookup, chunkCoordinate);
            }
        }
        
        private void ReResolve(Vector3Int pos)
        {
            WFCCell c = GetVisualCellAt(pos);
            if (c != null && c.Collapsed)
            {
                 WFCVisualizer.ResolveVisualForCell(c, GetVisualCellAt, gridSize, globalLookup, chunkCoordinate);
            }
        }

        private void UpdateVisualsAround(WFCCell centerCell)
        {
            WFCVisualizer.UpdateVisualsAround(centerCell, GetVisualCellAt, gridSize);
        }

        // Helper for algorithm - returns IWFCCell
        public IWFCCell GetCellAt(Vector3Int pos)
        {
            if (cellMap != null)
            {
                if (cellMap.TryGetValue(pos, out IWFCCell c)) return c;
                return null;
            }
            return allCells.FirstOrDefault(c => c.GridPosition == pos);
        }
        
        // Helper for visuals - returns WFCCell specifically
        public WFCCell GetVisualCellAt(Vector3Int pos)
        {
            if (cellMap != null && cellMap.TryGetValue(pos, out IWFCCell cell))
            {
                return cell as WFCCell;
            }
            return null;
        }

        private void ResetGridState()
        {
             initialQueue.Clear(); // Clear queue for fresh start
             
             foreach(var cell in allCells)
             {
                 cell.Collapsed = false;
                 // P1 FIX: Reuse list instead of allocating new
                 cell.PossibleModules.Clear();
                 cell.PossibleModules.AddRange(allModules);
             }
             
             // Re-Apply Constraints
             foreach(var kvp in constraints)
             {
                 ApplyConstraint(kvp.Key, kvp.Value);
             }
        }

        // Removed: ExecuteAllBlueprints, TopologicalSort, InitializeGrid, ApplyMapConstraints, ApplyBlueprints
        // These are now handled by WFCBuilder which configures the solver state.


        private IWFCCell GetLowestEntropyCell()
        {
            // P0 FIX: O(n) linear scan instead of O(n log n) LINQ sort
            IWFCCell best = null;
            int bestEntropy = int.MaxValue;
            int bestY = int.MaxValue;
            float bestRandom = float.MaxValue;

            for (int i = 0; i < allCells.Count; i++)
            {
                IWFCCell c = allCells[i];
                if (c.Collapsed || c.Entropy <= 0) continue;

                int y = c.GridPosition.y;
                int entropy = c.Entropy;

                // Priority: Lower Y first, then lower entropy, then random tiebreak
                bool isBetter = false;
                if (y < bestY)
                {
                    isBetter = true;
                }
                else if (y == bestY)
                {
                    if (entropy < bestEntropy)
                    {
                        isBetter = true;
                    }
                    else if (entropy == bestEntropy)
                    {
                        float r = Random.value;
                        if (r < bestRandom)
                        {
                            isBetter = true;
                            bestRandom = r;
                        }
                    }
                }

                if (isBetter)
                {
                    best = c;
                    bestEntropy = entropy;
                    bestY = y;
                    if (bestRandom == float.MaxValue) bestRandom = Random.value;
                }
            }

            return best;
        }

        private void Propagate(Queue<IWFCCell> queue)
        {
            while (queue.Count > 0)
            {
                IWFCCell current = queue.Dequeue();

                // Check all 6 neighbors
                CheckNeighbor(current, Vector3Int.right, queue);
                CheckNeighbor(current, Vector3Int.left, queue);
                CheckNeighbor(current, Vector3Int.up, queue);
                CheckNeighbor(current, Vector3Int.down, queue);
                CheckNeighbor(current, Vector3Int.forward, queue);
                CheckNeighbor(current, Vector3Int.back, queue);
            }
        }

        private void CheckNeighbor(IWFCCell current, Vector3Int dir, Queue<IWFCCell> queue)
        {
            Vector3Int nPos = current.GridPosition + dir;
            
            // P0 FIX: Use cellMap O(1) lookup instead of O(n) linear search
            if (!cellMap.TryGetValue(nPos, out IWFCCell neighbor)) return;
            if (neighbor == null || neighbor.Collapsed) return;

            // P1 FIX: Use pooled HashSet instead of allocating new
            _allowedPool.Clear();

            foreach (var mod in current.PossibleModules)
            {
                WFCModule[] neighbors = GetNeighbors(mod, dir);
                if (neighbors != null)
                {
                    foreach (var n in neighbors)
                    {
                        if (n != null) _allowedPool.Add(n);
                    }
                }
            }

            // Filter Neighbor's possibilities
            // P1 FIX: Use pooled List and reference-based Contains (faster than string comparison)
            _keptModulesPool.Clear();
            foreach (var nMod in neighbor.PossibleModules)
            {
                if (_allowedPool.Contains(nMod))
                {
                    _keptModulesPool.Add(nMod);
                }
            }

            if (neighbor.Constrain(_keptModulesPool))
                queue.Enqueue(neighbor);
        }

        private WFCModule[] GetNeighbors(WFCModule module, Vector3Int dir)
        {
            if (dir == Vector3Int.right) return module.rightNeighbors;
            if (dir == Vector3Int.left) return module.leftNeighbors;
            if (dir == Vector3Int.up) return module.topNeighbors;
            if (dir == Vector3Int.down) return module.bottomNeighbors;
            if (dir == Vector3Int.forward) return module.frontNeighbors;  // Z+
            if (dir == Vector3Int.back) return module.backNeighbors;      // Z-
            return new WFCModule[0];
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-Generate Neighbors")]
        public void GenerateNeighbors()
        {
            Debug.Log("Starting Auto-Generation of Neighbors based on Sockets...");

            // Clean lists first
            foreach (var mod in allModules)
            {
                mod.rightNeighbors = new WFCModule[0];
                mod.leftNeighbors = new WFCModule[0];
                mod.frontNeighbors = new WFCModule[0];
                mod.backNeighbors = new WFCModule[0];
                mod.topNeighbors = new WFCModule[0];
                mod.bottomNeighbors = new WFCModule[0];
            }

            // Matching
            foreach (var A in allModules)
            {
                List<WFCModule> right = new List<WFCModule>();
                List<WFCModule> left = new List<WFCModule>();
                List<WFCModule> front = new List<WFCModule>();
                List<WFCModule> back = new List<WFCModule>();
                List<WFCModule> top = new List<WFCModule>();
                List<WFCModule> bottom = new List<WFCModule>();

                foreach (var B in allModules)
                {
                    // standard: Socket must match. 
                    // convention: "0" means empty/air? Or just a connection.
                    // If we want "Symmetric" matching (A.right == B.left)

                    if (A.rightSocket == B.leftSocket) right.Add(B);
                    if (A.leftSocket == B.rightSocket) left.Add(B);

                    if (A.frontSocket == B.backSocket) front.Add(B);
                    if (A.backSocket == B.frontSocket) back.Add(B);

                    if (A.topSocket == B.bottomSocket) top.Add(B);
                    if (A.bottomSocket == B.topSocket) bottom.Add(B);
                }

                A.rightNeighbors = right.ToArray();
                A.leftNeighbors = left.ToArray();
                A.frontNeighbors = front.ToArray();
                A.backNeighbors = back.ToArray();
                A.topNeighbors = top.ToArray();
                A.bottomNeighbors = bottom.ToArray();

                UnityEditor.EditorUtility.SetDirty(A);
            }

            Debug.Log($"Auto-Generation Complete! Updated {allModules.Count} modules.");
        }
#endif
    }
}
