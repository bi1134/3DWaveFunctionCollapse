using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        
        [Header("Blueprints Definitions")]
        public List<WFCBlueprintLayer> definedBlueprints = new List<WFCBlueprintLayer>();

        [Header("Build Layers (Stack)")]
        public List<WFCBuildLayer> buildLayers = new List<WFCBuildLayer>();

        public WFCSolver solver = new WFCSolver();


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
            HashSet<int> activeYLevels = null;
            if (strictLayerHeight)
            {
                activeYLevels = new HashSet<int>();
                foreach(var layer in buildLayers) 
                    if(layer.active) activeYLevels.Add(layer.yOffset);
            }
            
            solver.Initialize(size, allModules, unityGrid, cellPrefab, cellAlignment, this, container, activeYLevels);
            
            // Listen for completion
            solver.OnFinished = () => {
                WFCEvent.TriggerChunkGenerated(this);
            };

            // 5. Apply Map Constraints (Pre-Collapse)
            ApplyBlueprints(size);

            // 6. Run
            StartCoroutine(solver.RunWFC());
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
            StartCoroutine(solver.RunWFC());
        }

    }
}
