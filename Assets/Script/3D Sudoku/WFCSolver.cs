using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WFC_Sudoku
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
        public List<WFCCell> cells = new List<WFCCell>();
        public Dictionary<Vector3Int, WFCCell> cellMap = new Dictionary<Vector3Int, WFCCell>(); // Optimization
        public Queue<WFCCell> initialQueue = new Queue<WFCCell>();

        // --- API FOR WFCBUILDER ---
        
        private MonoBehaviour context; // For Coroutines and Instantiation context
        private Transform container;

        // --- API FOR WFCBUILDER ---

        public void Initialize(Vector3Int size, List<WFCModule> modules, Grid grid, WFCCell prefab, Vector3 alignment, WFCBuilder b, Transform containerStr, HashSet<int> restrictedYLevels = null)
        {
            this.gridSize = size;
            this.allModules = modules;
            this.unityGrid = grid;
            this.cellPrefab = prefab;
            this.cellAlignment = alignment;
            this.builder = b;
            this.container = containerStr;

            int targetCount = gridSize.x * gridSize.y * gridSize.z; // Max possible
            if (restrictedYLevels != null)
            {
                // If strict, calculate exact count
                targetCount = gridSize.x * gridSize.z * restrictedYLevels.Count;
            }

            if (cells.Count == targetCount && cells.All(c => c != null))
            {
                // Reuse
                // Debug.Log("Reusing existing WFC Cells...");
                for (int i = 0; i < cells.Count; i++)
                {
                    WFCCell c = cells[i];
                    // Reset State
                    c.Initialize(allModules, c.gridPosition, gridSize.y); // visual cleanup happens here now

                    // Ensure Parent (Migrate if needed)
                    if (c.transform.parent != container) c.transform.SetParent(container);

                    if (!cellMap.ContainsKey(c.gridPosition)) cellMap.Add(c.gridPosition, c);
                }
            }
            else
            {
                // Create New
                // Debug.Log($"Spawning {targetCount} WFC Cells...");
                ClearCells(); // Helper to destroy old if mismatch
                cells = new List<WFCCell>(targetCount);
                cellMap.Clear();

                for (int z = 0; z < gridSize.z; z++)
                {
                    for (int y = 0; y < gridSize.y; y++)
                    {
                        if (restrictedYLevels != null && !restrictedYLevels.Contains(y)) continue;

                        for (int x = 0; x < gridSize.x; x++)
                        {
                            Vector3Int cellPos = new Vector3Int(x, y, z);
                            Vector3 worldPos = (unityGrid != null) ?
                               unityGrid.CellToWorld(cellPos) + Vector3.Scale(unityGrid.cellSize, cellAlignment) :
                               new Vector3(x, y, z); // Fallback

                            // Instantiate using Object.Instantiate since we are not a MonoBehaviour
                            WFCCell c = Object.Instantiate(cellPrefab, worldPos, Quaternion.identity, container);
                            c.Initialize(allModules, cellPos, gridSize.y);
                            cells.Add(c);
                            cellMap.Add(cellPos, c);
                        }
                    }
                }
            }

            initialQueue.Clear();
            constraints.Clear();
        }


        private void ClearCells()
        {
             if (cells != null)
             {
                 foreach(var c in cells)
                 {
                     if (c != null)
                     {
                         if(Application.isPlaying) Object.Destroy(c.gameObject);
                         else Object.DestroyImmediate(c.gameObject);
                     }
                 }
                 cells.Clear();
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
            WFCCell cell = GetCellAt(pos);
            if (cell != null && !cell.collapsed)
            {
                // Strict check: Module MUST be in allModules (name check for verify)
                if (cell.possibleModules.Any(m => m.name == module.name)) 
                {
                    cell.possibleModules.Clear();
                    cell.possibleModules.Add(module);
                    cell.collapsed = true;
                    if (!initialQueue.Contains(cell)) initialQueue.Enqueue(cell);
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
                // InitializeGrid(); // Called by Builder before Start
                // But wait, for Retries we need to Re-Initialize!
                // We need to keep a copy of 'modules' and 'size' to re-init?
                // Or we just assume cells are reset?
                // Re-instantiating cells every retry is expensive.
                // Better: Reset existing cells.
                
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
                    WFCCell cellToCollapse = GetLowestEntropyCell();

                    if (cellToCollapse == null)
                    {
                        // Check if we actually finished or just ran out of options
                        if (cells.Any(c => !c.collapsed))
                        {
                            Debug.LogWarning($"Contradiction found! (Attempt {currentRetries + 1}/{maxRetries + 1})");
                            contradictionFound = true;
                            break; // Break inner loop to retry
                        }

                        ResolveVariations();
                        OnFinished?.Invoke();
                        yield break; // Exit EVERYTHING
                    }

                    cellToCollapse.Collapse();

                    Queue<WFCCell> queue = new Queue<WFCCell>();
                    queue.Enqueue(cellToCollapse);
                    Propagate(queue);

                    // --- REAL TIME VISUAL UPDATE ---
                    // Optional: Only update every N steps to save perf
                    // UpdateVisualsAround(cellToCollapse); 

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

        public System.Func<Vector3Int, WFCCell> globalLookup; // Assigned by Builder
        public float worldScale = 1.0f; // Assigned by Builder
        public Vector2Int chunkCoordinate; // Assigned by Builder
        public System.Action OnFinished;

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


        private void ResolveVariations()
        {
            foreach (var cell in cells)
            {
                // We pass chunkCoordinate so Visualizer can calculate Global Grid Pos
                WFCVisualizer.ResolveVisualForCell(cell, GetCellAt, gridSize, globalLookup, chunkCoordinate);
            }
        }
        
        private void ReResolve(Vector3Int pos)
        {
            WFCCell c = GetCellAt(pos);
            if (c != null && c.collapsed)
            {
                 WFCVisualizer.ResolveVisualForCell(c, GetCellAt, gridSize, globalLookup, chunkCoordinate);
            }
        }

        private void UpdateVisualsAround(WFCCell centerCell)
        {
            WFCVisualizer.UpdateVisualsAround(centerCell, GetCellAt, gridSize);
        }

        // Helper for Visualizer callback
        public WFCCell GetCellAt(Vector3Int pos)
        {
            if (cellMap != null)
            {
                if (cellMap.TryGetValue(pos, out WFCCell c)) return c;
                return null;
            }
            return cells.FirstOrDefault(c => c.gridPosition == pos);
        }

        private void ResetGridState()
        {
             initialQueue.Clear(); // Clear queue for fresh start
             
             foreach(var cell in cells)
             {
                 cell.collapsed = false;
                 cell.possibleModules = new List<WFCModule>(allModules);
             }
             
             // Re-Apply Constraints
             foreach(var kvp in constraints)
             {
                 ApplyConstraint(kvp.Key, kvp.Value);
             }
        }

        // Removed: ExecuteAllBlueprints, TopologicalSort, InitializeGrid, ApplyMapConstraints, ApplyBlueprints
        // These are now handled by WFCBuilder which configures the solver state.


        private WFCCell GetLowestEntropyCell()
        {
            // Bottom-Up Priority
            return cells.Where(c => !c.collapsed && c.Entropy > 0)
                        .OrderBy(c => c.gridPosition.y)
                        .ThenBy(c => c.Entropy)
                        .ThenBy(c => Random.value)
                        .FirstOrDefault();
        }

        private void Propagate(Queue<WFCCell> queue)
        {
            while (queue.Count > 0)
            {
                WFCCell current = queue.Dequeue();

                // Explicit Neighbor Checks (using direction relative to CURRENT cell)
                // If I am checking neighbor to my Right, I look at MY allowed RightNeighbors.
                // NOTE: In strict WFC typically "A allows B" means A.Right contains B AND B.Left contains A.
                // However, user's setup implies "I have an array to know what can be put in front..." 
                // defaulting to one-way authority often simplifies things, but bidirectional is safer.
                // Let's implement Strict Bidirectional check for robustness.

                CheckNeighbor(current, Vector3Int.right, queue);
                CheckNeighbor(current, Vector3Int.left, queue);
                CheckNeighbor(current, Vector3Int.up, queue);
                CheckNeighbor(current, Vector3Int.down, queue);
                CheckNeighbor(current, Vector3Int.forward, queue);
                CheckNeighbor(current, Vector3Int.back, queue);
            }
        }

        private void CheckNeighbor(WFCCell current, Vector3Int dir, Queue<WFCCell> queue)
        {
            Vector3Int nPos = current.gridPosition + dir;
            WFCCell neighbor = cells.FirstOrDefault(c => c.gridPosition == nPos);

            if (neighbor == null || neighbor.collapsed) return;

            // Gather all allowed modules from CURRENT cell's possibilities
            HashSet<WFCModule> allowedByCurrent = new HashSet<WFCModule>();

            foreach (var mod in current.possibleModules)
            {
                WFCModule[] neighbors = GetNeighbors(mod, dir);
                if (neighbors != null)
                {
                    foreach (var n in neighbors)
                    {
                        if (n != null) allowedByCurrent.Add(n);
                    }
                }
            }

            // Filter Neighbor's possibilities
            // A neighbor module 'nMod' is valid IF:
            // 1. It is in the 'allowedByCurrent' set (Current says "I allow you")
            // 2. AND (Optional but recommended) nMod says "I allow Current" (Backwards check)

            // For this implementation, we rely on the logic:
            // "Current.possibleModules" only contains things that CAN exist here.
            // So if "Current" says "I can have X on my Right", then X is a candidate for Neighbor.

            // ISSUE: 'allowedByCurrent' contains Prefab References (Project Assets).
            // 'neighbor.possibleModules' contains Prefab References.
            // Comparison is valid.

            // BUT wait! If we have multiple instances of "Wall", do we drag the prefab into the array? Yes.

            List<WFCModule> keptModules = new List<WFCModule>();
            foreach (var nMod in neighbor.possibleModules)
            {
                // Is this neighbor module allowed by ANY of the current options?
                // Logic: Does ANY (CurrentOption) have (nMod) in its (Dir) list?
                // Yes, that is exactly 'allowedByCurrent'.

                // We MUST check prefab reference equality.
                // As long as the user drags the SAME prefab into the array as is in the Solver list, this works.
                // To be safe, we check names? No, reference should work for Prefabs.

                // Checking by Name is safer if user clones things.
                if (allowedByCurrent.Any(allowed => allowed != null && nMod != null && allowed.name == nMod.name))
                {
                    keptModules.Add(nMod);
                }
            }

            if (neighbor.Constrain(keptModules))
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
