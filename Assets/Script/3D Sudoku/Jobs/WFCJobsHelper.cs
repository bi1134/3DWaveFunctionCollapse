using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace WFC_Sudoku.Jobs
{
    /// <summary>
    /// Utility class for converting between managed WFC objects and Burst-compatible data.
    /// </summary>
    public static class WFCJobsHelper
    {
        /// <summary>
        /// Converts a list of WFCModule to NativeArray of WFCModuleData.
        /// </summary>
        public static NativeArray<WFCModuleData> ConvertModulesToNative(List<WFCModule> modules, Allocator allocator)
        {
            int count = modules.Count;
            if (count > 64)
            {
                Debug.LogWarning($"WFCJobsHelper: Module count ({count}) exceeds 64. Only first 64 will be used.");
                count = 64;
            }
            
            var nativeModules = new NativeArray<WFCModuleData>(count, allocator);
            
            for (int i = 0; i < count; i++)
            {
                var mod = modules[i];
                nativeModules[i] = new WFCModuleData
                {
                    weight = mod.spawnWeight,
                    rightNeighborsMask = BuildNeighborMask(mod.rightNeighbors, modules),
                    leftNeighborsMask = BuildNeighborMask(mod.leftNeighbors, modules),
                    topNeighborsMask = BuildNeighborMask(mod.topNeighbors, modules),
                    bottomNeighborsMask = BuildNeighborMask(mod.bottomNeighbors, modules),
                    frontNeighborsMask = BuildNeighborMask(mod.frontNeighbors, modules),
                    backNeighborsMask = BuildNeighborMask(mod.backNeighbors, modules),
                };
            }
            
            return nativeModules;
        }
        
        /// <summary>
        /// Converts a list of WFCCell to NativeArray of WFCCellData.
        /// </summary>
        public static NativeArray<WFCCellData> ConvertCellsToNative(List<WFCCell> cells, int moduleCount, Allocator allocator)
        {
            var nativeCells = new NativeArray<WFCCellData>(cells.Count, allocator);
            
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var data = new WFCCellData
                {
                    gridPosition = new int3(cell.gridPosition.x, cell.gridPosition.y, cell.gridPosition.z),
                    collapsed = cell.collapsed,
                    collapsedModuleIndex = -1,
                };
                
                // Build possibility mask from cell's possibleModules
                data.possibleModulesMask = 0;
                if (cell.collapsed && cell.possibleModules.Count > 0)
                {
                    // Find module index
                    // Note: This requires knowing the module list
                    data.collapsedModuleIndex = -1; // Will be set separately if needed
                }
                else
                {
                    // Set all possible as allowed initially (will be refined by constraints)
                    data.Reset(moduleCount);
                }
                
                nativeCells[i] = data;
            }
            
            return nativeCells;
        }
        
        /// <summary>
        /// Creates a cell index map for O(1) position lookups.
        /// </summary>
        public static NativeParallelHashMap<int3, int> BuildCellIndexMap(List<WFCCell> cells, Allocator allocator)
        {
            var map = new NativeParallelHashMap<int3, int>(cells.Count, allocator);
            
            for (int i = 0; i < cells.Count; i++)
            {
                var pos = cells[i].gridPosition;
                map.TryAdd(new int3(pos.x, pos.y, pos.z), i);
            }
            
            return map;
        }
        
        /// <summary>
        /// Copies job results back to managed WFCCell objects.
        /// </summary>
        public static void CopyResultsToCells(NativeArray<WFCCellData> nativeCells, List<WFCCell> cells, List<WFCModule> modules)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                var data = nativeCells[i];
                var cell = cells[i];
                
                cell.collapsed = data.collapsed;
                
                if (data.collapsed && data.collapsedModuleIndex >= 0 && data.collapsedModuleIndex < modules.Count)
                {
                    var module = modules[data.collapsedModuleIndex];
                    cell.possibleModules.Clear();
                    cell.possibleModules.Add(module);
                }
            }
        }
        
        /// <summary>
        /// Builds a bitmask from an array of neighbor modules.
        /// </summary>
        private static ulong BuildNeighborMask(WFCModule[] neighbors, List<WFCModule> allModules)
        {
            ulong mask = 0;
            if (neighbors == null) return mask;
            
            foreach (var neighbor in neighbors)
            {
                if (neighbor == null) continue;
                int idx = allModules.IndexOf(neighbor);
                if (idx >= 0 && idx < 64)
                {
                    mask |= 1UL << idx;
                }
            }
            
            return mask;
        }
    }
}
