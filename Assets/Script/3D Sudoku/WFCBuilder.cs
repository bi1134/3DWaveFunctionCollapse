using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using WFC_Sudoku.Jobs;

namespace WFC_Sudoku
{
    public class WFCBuilder : MonoBehaviour
    {
        [Header("Grid Configuration")]
        public Grid unityGrid;
        public Vector3Int gridSize = new Vector3Int(5, 5, 5);
        [Tooltip("Measurement offset within cell. (0.5, 0, 0.5) puts anchor on ground, centered.")]
        public Vector3 cellAlignment = new Vector3(0.5f, 0f, 0.5f);
        public WFCCell cellPrefab;
        public bool autoResizeGridToMap = true;
        
        [Header("Strict Layer Settings")]
        public bool strictLayerHeight = true;
        [Tooltip("Module to use for empty/undefined space.")]
        public WFCModule defaultEmptyModule; // User must assign "Empty" or "Air" logic
        
        // [Header("Dual Grid Visualization")]
        // useDualGrid moved to Layers
        public WFCDualGridManager dualGridManager = new WFCDualGridManager();
        
        [Header("Blueprints Definitions")]
        public List<WFCBlueprintLayer> definedBlueprints = new List<WFCBlueprintLayer>();

        [Header("Build Layers (Stack)")]
        public List<WFCBuildLayer> buildLayers = new List<WFCBuildLayer>();

        public WFCSolver solver = new WFCSolver();
        
        [Header("Performance")]
        [Tooltip("Use Jobs + Burst for faster WFC generation. Requires Burst package.")]
        public bool useBurstAcceleration = false;


        private void Awake()
        {
            // Solver is pure class, no component needed.
        }

        private void Start()
        {
             Generate();
        }

        [ContextMenu("Build (Exec Layers)")]
        public void Generate()
        {
            // if (solver == null) solver = new WFCSolver(); // Already initialized inline
            
            solver.OnCellCollapsed = null; // Reset events
            
            // Validation
            if (cellPrefab == null) { Debug.LogError("WFCBuilder: Cell Prefab is missing!"); return; }
            if (unityGrid == null) { Debug.LogError("WFCBuilder: Unity Grid is missing!"); unityGrid = GetComponent<Grid>(); }

            // 0. Ensure Blueprints are Generated (Texture Data Ready)
            ExecuteAllBlueprints();

            // 1. Calculate Grid Size
            Vector3Int size = gridSize; // Default
            if (autoResizeGridToMap && buildLayers.Count > 0)
            {
                foreach (var layer in buildLayers)
                {
                     var bp = GetBlueprint(layer.blueprintName);
                     if (bp != null && bp.outputMap != null)
                     {
                         size.x = bp.outputMap.width;
                         size.z = bp.outputMap.height;
                         break;
                     }
                }
            }

            // 2. Gather Modules
            List<WFCModule> allModules = new List<WFCModule>();
            HashSet<WFCModule> uniqueModules = new HashSet<WFCModule>();
            
            foreach(var layer in buildLayers)
            {
                if (layer != null && layer.active)
                {
                    foreach(var item in layer.presets)
                    {
                        if (item.preset != null)
                        {
                            foreach(var m in item.preset.modules) 
                                if(m!=null) uniqueModules.Add(m);
                        }
                    }
                }
            }
            allModules.AddRange(uniqueModules);

            // 3. Setup Container
            Transform container = transform.Find("Cells Container");
            if (container == null)
            {
                GameObject go = new GameObject("Cells Container");
                go.transform.SetParent(this.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                container = go.transform;
            }

            // 4. Initialize Solver
            // Cleanup previous Dual Grid visuals
            if (dualGridManager != null) dualGridManager.Clear();

            HashSet<int> activeYLevels = null;
            HashSet<int> dualGridYLevels = new HashSet<int>();

            if (strictLayerHeight)
            {
                activeYLevels = new HashSet<int>();
                foreach(var layer in buildLayers)
                {
                    if(layer.active) 
                    {
                        activeYLevels.Add(layer.yOffset);
                        if (layer.useDualGrid) dualGridYLevels.Add(layer.yOffset);
                    }
                }
            }
            else
            {
                // If not strict, checking for dual grid globally is harder unless we scan active layers anyway
                foreach(var layer in buildLayers)
                     if(layer.active && layer.useDualGrid) dualGridYLevels.Add(layer.yOffset);
            }
            
            // Check if any layer needs Dual Grid
            // bool anyDualGrid = buildLayers.Any(l => l.active && l.useDualGrid);
            
            // Initialize Solver with Dual Grid Mask
            solver.Initialize(size, allModules, unityGrid, cellPrefab, cellAlignment, this, container, activeYLevels, dualGridYLevels);
            
            // Always initialize manager to ensure it has reference to container (for clearing or future use)
            // But we only subscribe if active
            dualGridManager.Initialize(solver, this.transform, dualGridYLevels); // Passes active levels
            
            if (dualGridYLevels.Count > 0)
            {
                solver.OnCellCollapsed += dualGridManager.UpdateAround;
            }
            
            // Listen for completion
            solver.OnFinished = () => {
                WFCEvent.TriggerChunkGenerated(this);
            };

            // 5. Apply Map Constraints (Pre-Collapse)
            ApplyBlueprints(size);

            // 6. Run
            if (useBurstAcceleration)
            {
                var burstRunner = new WFCBurstRunner(
                    solver.cells,
                    solver.allModules,
                    GetConstraintsFromSolver(),
                    size,
                    solver.OnFinished,
                    solver.OnCellCollapsed
                );
                StartCoroutine(burstRunner.Run(solver.maxRetries));
            }
            else
            {
                StartCoroutine(solver.RunWFC());
            }
        }
        
        public WFCBlueprintLayer GetBlueprint(string name)
        {
            return definedBlueprints.Find(b => b.layerName == name);
        }
        
        // Editor Helper
        public void ExecuteAllBlueprints()
        {
             int w = gridSize.x;
             int h = gridSize.z;
             List<WFCBlueprintLayer> context = new List<WFCBlueprintLayer>();

             foreach(var bp in definedBlueprints)
             {
                 if(bp.active)
                 {
                     bp.Generate(w, h, context);
                     context.Add(bp);
                 }
             }
        }

        private void ApplyBlueprints(Vector3Int size)
        {
            // Apply Layers (Pre-Collapse) & Strict Height
            // Apply Layers (Pre-Collapse) & Strict Height
            // Note: strict check is now handled during Initialization (Sparse Grid)


            foreach(var layer in buildLayers)
            {
                if (!layer.active) continue;
                var bp = GetBlueprint(layer.blueprintName);
                if (bp == null) continue; // Or handle pure generators
                
                // For each cell in Map...
                int w = (bp.outputMap != null) ? bp.outputMap.width : size.x;
                int h = (bp.outputMap != null) ? bp.outputMap.height : size.z;
                
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        Color pixel = (bp.outputMap != null) ? bp.outputMap.GetPixel(x, y) : Color.white;
                        int targetLayerIdx;
                        WFCModule forcedMod = layer.GetModuleForColor(pixel, out targetLayerIdx);
                        
                        if (forcedMod != null)
                        {
                            Vector3Int pos = new Vector3Int(x, layer.yOffset, y); 
                            solver.ForceCollapse(pos, forcedMod);
                        }
                    }
                }
            }

            // Strict Layer Logic: Loop REMOVED. 
            // We now skip creating cells at invalid Y levels in Solver.Initialize
            if (strictLayerHeight && defaultEmptyModule == null)
            {
                 // Debug.LogWarning("WFCBuilder: Strict Layer Height is ON (Sparse Mode)");
            }

            // 5. Run Solver
            // NOTE: Moved to Generate() to support Burst toggle
        }

        // Helper to get constraints for Burst runner
        private Dictionary<Vector3Int, WFCModule> GetConstraintsFromSolver()
        {
            // Build constraints from what was applied via ForceCollapse
            // Since constraints is private in solver, we rebuild from initial queue state
            var result = new Dictionary<Vector3Int, WFCModule>();
            foreach (var cell in solver.cells)
            {
                if (cell.collapsed && cell.possibleModules.Count > 0)
                {
                    result[cell.gridPosition] = cell.possibleModules[0];
                }
            }
            return result;
        }
    }
}
