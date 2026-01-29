using UnityEngine;
using System.Collections.Generic;

namespace TerrainGenerator
{
    [System.Serializable]
    public class WFCDualGridManager
    {
        [Header("Configuration")]
        public WFCSolver solver;
        public Vector3 cellOffset = new Vector3(-0.5f, 0, -0.5f);
        // Container is managed internally or passed
        private Transform dualGridContainer;

        [Header("Visual Dictionary")]
        public List<GameObject> dualGridPrefabs_REMOVED; // Keeping name to force deletion of serialized data if needed, or just remove. 

        // 16 states based on 4 corners:
        // 0000 (0) = Empty
        // 0001 (1) = BR
        // 0010 (2) = BL
        // 0011 (3) = Bottom Edge
        // ... and so on.
        // Bit order: TL(8) TR(4) BL(2) BR(1)  <-- Common convention, or we define our own.
        // Let's use:
        // Bit 0 (1) = Top Left (x, z+1)  -> Wait, dual grid node is at Intersection.
        // Let's define the 4 cells AROUND the intersection point (x, z).
        // If node is at (x, z), it touches:
        // Cell (x-1, z)
        // Cell (x, z)
        // Cell (x-1, z-1)
        // Cell (x, z-1)
        
        // Actually, let's keep it simple.
        // Dual Grid Node (x,z) is the center of logical cells:
        // (x, z), (x+1, z), (x, z+1), (x+1, z+1) ? 
        // No, standard dual grid implies shifts.
        // Let's assume Dual Grid Node [x,z] corresponds to the corner shared by Logical Cells:
        // Q0: (x-1, z)   [Top-Left relative to intersection?]
        // Q1: (x, z)     [Top-Right]
        // Q2: (x-1, z-1) [Bottom-Left]
        // Q3: (x, z-1)   [Bottom-Right]
        
        // We will implement a visualizer that iterates (x: 0 to N, z: 0 to M)
        // where N, M are grid dimensions. A w*h grid has (w+1)*(h+1) intersections.

        // Container is managed internally or passed
        private HashSet<int> activeLevels = new HashSet<int>();

        private Dictionary<Vector3Int, GameObject> spawnedVisuals = new Dictionary<Vector3Int, GameObject>();

        public void Initialize(WFCSolver solver, Transform parent, HashSet<int> activeLevels)
        {
            this.solver = solver;
            this.activeLevels = activeLevels ?? new HashSet<int>();
            
            // Check if we already have a container or need a new one
            if (dualGridContainer == null)
            {
                // Look for existing
                Transform existing = parent.Find("Dual Grid Container");
                if (existing != null)
                {
                    dualGridContainer = existing;
                }
                else
                {
                    GameObject go = new GameObject("Dual Grid Container");
                    go.transform.SetParent(parent);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    dualGridContainer = go.transform;
                }
            }
            
            // Ensure container is clean and reset
            if (dualGridContainer != null)
            {
                dualGridContainer.localPosition = Vector3.zero;
                dualGridContainer.localRotation = Quaternion.identity;
                dualGridContainer.localScale = Vector3.one;
            }
        }

        public void Clear()
        {
            spawnedVisuals.Clear();
            if (dualGridContainer != null)
            {
                // Destroy all children
                for (int i = dualGridContainer.childCount - 1; i >= 0; i--)
                {
                    Transform child = dualGridContainer.GetChild(i);
                    if (Application.isPlaying) Object.Destroy(child.gameObject);
                    else Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        public void UpdateAround(Vector3Int logicalCellPos)
        {
            // Check if this layer is using Dual Grid
            if (!activeLevels.Contains(logicalCellPos.y)) return;

            // Update 4 intersection corners
            int y = logicalCellPos.y;
            UpdatePoint(new Vector3Int(logicalCellPos.x, y, logicalCellPos.z));
            UpdatePoint(new Vector3Int(logicalCellPos.x + 1, y, logicalCellPos.z));
            UpdatePoint(new Vector3Int(logicalCellPos.x, y, logicalCellPos.z + 1));
            UpdatePoint(new Vector3Int(logicalCellPos.x + 1, y, logicalCellPos.z + 1));
        }

        private void UpdatePoint(Vector3Int dualPos)
        {
            // 4 Neighbors:
            // BL: (x-1, z-1) | BR: (x, z-1)
            // TL: (x-1, z)   | TR: (x, z)
            
            // Check if any corner is truly at world boundary (no neighbor exists)
            // Only if globalLookup is set (meaning we're in a multi-chunk world)
            bool blMissing = IsOutOfBoundsWithNoNeighbor(dualPos.x - 1, dualPos.y, dualPos.z - 1);
            bool brMissing = IsOutOfBoundsWithNoNeighbor(dualPos.x,     dualPos.y, dualPos.z - 1);
            bool tlMissing = IsOutOfBoundsWithNoNeighbor(dualPos.x - 1, dualPos.y, dualPos.z);
            bool trMissing = IsOutOfBoundsWithNoNeighbor(dualPos.x,     dualPos.y, dualPos.z);
            
            // If any corner is at world boundary, skip spawning to avoid edge artifacts
            if (blMissing || brMissing || tlMissing || trMissing)
            {
                SpawnVisual(dualPos, null, 0); // Clear any existing visual
                return;
            }
            
            // Fetch modules
            WFCModule mBL = GetModuleAt(dualPos.x - 1, dualPos.y, dualPos.z - 1);
            WFCModule mBR = GetModuleAt(dualPos.x,     dualPos.y, dualPos.z - 1);
            WFCModule mTL = GetModuleAt(dualPos.x - 1, dualPos.y, dualPos.z);
            WFCModule mTR = GetModuleAt(dualPos.x,     dualPos.y, dualPos.z);

            // Determine Dominant Preset
            WFCModule dominant = mTL ?? mTR ?? mBL ?? mBR; // First non-null
            if (dominant == null) 
            {
                SpawnVisual(dualPos, null, 0); // All Empty
                return;
            }

            // Find Preset for 'dominant'
            WFCTilePreset preset = FindPresetForModule(dominant);
            if (preset == null) return;

            // Calculate Mask based on TerrainType (0/1)
            // We assume 1 (Land) if terrainType > 0.
            
            int cTL = (mTL != null && mTL.terrainType > 0) ? 1 : 0;
            int cTR = (mTR != null && mTR.terrainType > 0) ? 1 : 0;
            int cBL = (mBL != null && mBL.terrainType > 0) ? 1 : 0;
            int cBR = (mBR != null && mBR.terrainType > 0) ? 1 : 0;

            // Mapping to 6-Tuple: Full, L, Line, Stitch, Corner, Empty
            // Mask: TL(8) TR(4) BL(2) BR(1)
            int mask = (cTL << 3) | (cTR << 2) | (cBL << 1) | cBR;
            
            GameObject prefab = null;
            float rotY = 0;

            switch (mask)
            {
                // EMPTY
                case 0: prefab = GetRandom(preset.dualGridProfile.emptyModels); break;
                
                // FULL
                case 15: prefab = GetRandom(preset.dualGridProfile.fullModels); break;
                
                // CORNER (1 Bit) -> Target TL (8)
                case 8: prefab = GetRandom(preset.dualGridProfile.cornerModels); rotY = 0; break;
                case 4: prefab = GetRandom(preset.dualGridProfile.cornerModels); rotY = 90; break;
                case 1: prefab = GetRandom(preset.dualGridProfile.cornerModels); rotY = 180; break;
                case 2: prefab = GetRandom(preset.dualGridProfile.cornerModels); rotY = 270; break;

                // L-SHAPE (3 Bits) -> Target Not-TL (missing TL)
                // If Missing BL (13) -> Rot 0.
                case 13: prefab = GetRandom(preset.dualGridProfile.lShapeModels); rotY = 0; break;
                case 14: prefab = GetRandom(preset.dualGridProfile.lShapeModels); rotY = 270; break;
                case 11: prefab = GetRandom(preset.dualGridProfile.lShapeModels); rotY = 180; break;
                case 7:  prefab = GetRandom(preset.dualGridProfile.lShapeModels); rotY = 90; break;
                    
                // LINE (2 Adjacent) -> Target Top Row (12)
                case 12: prefab = GetRandom(preset.dualGridProfile.lineModels); rotY = 0; break;
                case 5:  prefab = GetRandom(preset.dualGridProfile.lineModels); rotY = 90; break; // Right (TR+BR)
                case 3:  prefab = GetRandom(preset.dualGridProfile.lineModels); rotY = 180; break; // Bot (BL+BR)
                case 10: prefab = GetRandom(preset.dualGridProfile.lineModels); rotY = 270; break; // Left (TL+BL)
                    
                // STITCH (2 Opposite)
                case 9:  prefab = GetRandom(preset.dualGridProfile.stitchModels); rotY = 0; break; // Stitch 1 (TL+BR)
                case 6:  prefab = GetRandom(preset.dualGridProfile.stitchModels); rotY = 90; break; // Stitch 2 (TR+BL)
            }
            
            // DEBUG LOG REMOVED - Was consuming 24+ seconds (172,812 calls)
            // if (prefab != null) Debug.Log($"DG spawn at {dualPos}: Mask={mask} (TL{cTL} TR{cTR} BL{cBL} BR{cBR}) -> {prefab.name}");
            
            SpawnVisual(dualPos, prefab, rotY);
        }

        private WFCModule GetModuleAt(int x, int y, int z)
        {
             return GetModuleAtInternal(x, y, z, true);
        }
        
        private WFCModule GetModuleAtInternal(int x, int y, int z, bool allowEdgeExtend)
        {
             Vector3Int p = new Vector3Int(x, y, z);
             
             // 1. Local Lookup - cellMap (contains both WFCCell and WFCCellData via IWFCCell interface)
             if (solver.cellMap != null && solver.cellMap.TryGetValue(p, out IWFCCell cell))
             {
                 if (cell.Collapsed && cell.PossibleModules.Count > 0) return cell.PossibleModules[0];
                 return null;
             }
             
             // 2. Global Lookup (Cross-Chunk) - use globalCellLookup for any cell type (WFCCell or WFCCellData)
             if (solver.globalCellLookup != null)
             {
                 // Calculate Global Coordinate
                 int globalX = (solver.chunkCoordinate.x * solver.gridSize.x) + x;
                 int globalZ = (solver.chunkCoordinate.y * solver.gridSize.z) + z;
                 Vector3Int globalPos = new Vector3Int(globalX, y, globalZ);
                 
                 IWFCCell neighbor = solver.globalCellLookup(globalPos);
                 if (neighbor != null && neighbor.Collapsed && neighbor.PossibleModules.Count > 0)
                 {
                     return neighbor.PossibleModules[0];
                 }
             }

             // 3. World Boundary Handling: Return null (treated as air/empty - no special tiles)
             // This prevents edge tiles from spawning at world boundaries
             return null;
        }
        
        // Check if position is out of local bounds AND has no neighbor chunk
        private bool IsOutOfBoundsWithNoNeighbor(int x, int y, int z)
        {
            // If within local bounds, it's not a boundary issue
            if (x >= 0 && x < solver.gridSize.x && z >= 0 && z < solver.gridSize.z)
                return false;
            
            // Out of local bounds - check if there's a neighbor chunk
            if (solver.globalChunkExists != null)
            {
                int globalX = (solver.chunkCoordinate.x * solver.gridSize.x) + x;
                int globalZ = (solver.chunkCoordinate.y * solver.gridSize.z) + z;
                Vector3Int globalPos = new Vector3Int(globalX, y, globalZ);
                
                // If chunk exists, then it's NOT a world boundary.
                // Even if the cell at that pos is null (Air), we should treat it as valid Air neighbor
                if (solver.globalChunkExists(globalPos))
                    return false; 
            }
            
            // Out of bounds with no neighbor = true world boundary
            return true;
        }
        
        private WFCTilePreset FindPresetForModule(WFCModule module)
        {
            // Scan Builder's layers
            foreach(var layer in solver.builder.buildLayers)
            {
                foreach(var item in layer.presets)
                {
                    if (item.preset != null && item.preset.modules.Contains(module))
                        return item.preset;
                }
            }
            return null;
        }

        private void SpawnVisual(Vector3Int pos, GameObject prefab, float rotY)
        {
            if (spawnedVisuals.ContainsKey(pos))
            {
                GameObject old = spawnedVisuals[pos];
                if (old != null)
                {
                    if (Application.isPlaying) Object.Destroy(old);
                    else Object.DestroyImmediate(old);
                }
                spawnedVisuals.Remove(pos);
            }

            if (prefab == null) return;

            // Calculate Local Position relative to Chunk
            Vector3 localPos = new Vector3(pos.x - 0.5f, pos.y, pos.z - 0.5f); 
            if (solver.unityGrid != null) localPos = Vector3.Scale(localPos, solver.unityGrid.cellSize);

            // Convert to World Position 
            // We use dualGridContainer (which is Builder's transform or child) to transform point
            Vector3 worldPos = (dualGridContainer != null) ? dualGridContainer.TransformPoint(localPos) : localPos;

            Quaternion rot = Quaternion.Euler(0, rotY, 0);
            
            // Note: Instantiate parent argument sets hierarchy. Position argument is World Position.
            Transform parent = (dualGridContainer != null) ? dualGridContainer : solver.builder.transform;
            
            GameObject go = Object.Instantiate(prefab, worldPos, rot, parent);
            go.name = $"DG_{pos.x}_{pos.y}_{pos.z}";
            spawnedVisuals.Add(pos, go);
        }
        private GameObject GetRandom(List<GameObject> list)
        {
            if (list == null || list.Count == 0) return null;
            return list[UnityEngine.Random.Range(0, list.Count)];
        }
    }
}
