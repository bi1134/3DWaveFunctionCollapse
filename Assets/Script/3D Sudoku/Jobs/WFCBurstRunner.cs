using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace WFC_Sudoku.Jobs
{
    /// <summary>
    /// Burst-accelerated WFC solver runner.
    /// Call from WFCSolver or WFCBuilder to use Jobs-based WFC.
    /// </summary>
    public class WFCBurstRunner
    {
        private List<WFCCell> cells;
        private List<WFCModule> modules;
        private Dictionary<Vector3Int, WFCModule> constraints;
        private System.Action onFinished;
        private System.Action<Vector3Int> onCellCollapsed;
        
        // Native data
        private NativeArray<WFCCellData> nativeCells;
        private NativeArray<WFCModuleData> nativeModules;
        private NativeParallelHashMap<int3, int> cellIndexMap;
        private NativeQueue<int> propagationQueue;
        private NativeReference<int> lowestEntropyResult;
        
        private uint randomSeed;
        private int3 gridSize;
        
        public WFCBurstRunner(
            List<WFCCell> cells,
            List<WFCModule> modules,
            Dictionary<Vector3Int, WFCModule> constraints,
            Vector3Int gridSize,
            System.Action onFinished,
            System.Action<Vector3Int> onCellCollapsed)
        {
            this.cells = cells;
            this.modules = modules;
            this.constraints = constraints;
            this.gridSize = new int3(gridSize.x, gridSize.y, gridSize.z);
            this.onFinished = onFinished;
            this.onCellCollapsed = onCellCollapsed;
            this.randomSeed = (uint)UnityEngine.Random.Range(1, int.MaxValue);
        }
        
        public IEnumerator Run(int maxRetries = 5)
        {
            int moduleCount = Mathf.Min(modules.Count, 64);
            int currentRetries = 0;
            
            while (currentRetries <= maxRetries)
            {
                // Allocate native containers
                AllocateNativeData(moduleCount);
                
                try
                {
                    // Apply constraints
                    ApplyConstraintsToNative();
                    
                    bool contradictionFound = false;
                    int steps = 0;
                    
                    // Initial propagation from constraints
                    if (propagationQueue.Count > 0)
                    {
                        RunPropagation(moduleCount);
                    }
                    
                    // Main WFC loop
                    while (true)
                    {
                        // Find lowest entropy cell
                        int cellToCollapse = FindLowestEntropyCell();
                        
                        if (cellToCollapse < 0)
                        {
                            // Check if finished or contradiction
                            if (HasUncolllapsedCells())
                            {
                                Debug.LogWarning($"Contradiction found! (Attempt {currentRetries + 1}/{maxRetries + 1})");
                                contradictionFound = true;
                                break;
                            }
                            
                            // Successfully completed!
                            CopyResultsToManagedCells();
                            DisposeNativeData();
                            onFinished?.Invoke();
                            yield break;
                        }
                        
                        // Collapse the cell
                        CollapseCell(cellToCollapse, moduleCount);
                        
                        // Notify callback
                        var cellData = nativeCells[cellToCollapse];
                        onCellCollapsed?.Invoke(new Vector3Int(cellData.gridPosition.x, cellData.gridPosition.y, cellData.gridPosition.z));
                        
                        // Propagate constraints
                        propagationQueue.Enqueue(cellToCollapse);
                        RunPropagation(moduleCount);
                        
                        steps++;
                        if (steps % 50 == 0) yield return null;
                    }
                    
                    if (contradictionFound)
                    {
                        currentRetries++;
                        DisposeNativeData();
                        yield return new WaitForSeconds(0.1f);
                        continue;
                    }
                }
                finally
                {
                    // Ensure cleanup even on exception
                }
            }
            
            DisposeNativeData();
            Debug.LogError("WFC Burst Failed to generate a valid grid after max retries.");
        }
        
        private void AllocateNativeData(int moduleCount)
        {
            // Convert modules first (only once, they don't change)
            nativeModules = WFCJobsHelper.ConvertModulesToNative(modules, Allocator.Persistent);
            
            // Allocate cells
            nativeCells = new NativeArray<WFCCellData>(cells.Count, Allocator.Persistent);
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var data = new WFCCellData
                {
                    gridPosition = new int3(cell.gridPosition.x, cell.gridPosition.y, cell.gridPosition.z),
                    collapsed = false,
                    collapsedModuleIndex = -1,
                };
                data.Reset(moduleCount);
                nativeCells[i] = data;
            }
            
            // Build index map
            cellIndexMap = WFCJobsHelper.BuildCellIndexMap(cells, Allocator.Persistent);
            
            // Allocate queue and result holder
            propagationQueue = new NativeQueue<int>(Allocator.Persistent);
            lowestEntropyResult = new NativeReference<int>(Allocator.Persistent);
        }
        
        private void DisposeNativeData()
        {
            if (nativeCells.IsCreated) nativeCells.Dispose();
            if (nativeModules.IsCreated) nativeModules.Dispose();
            if (cellIndexMap.IsCreated) cellIndexMap.Dispose();
            if (propagationQueue.IsCreated) propagationQueue.Dispose();
            if (lowestEntropyResult.IsCreated) lowestEntropyResult.Dispose();
        }
        
        private void ApplyConstraintsToNative()
        {
            foreach (var kvp in constraints)
            {
                var pos = new int3(kvp.Key.x, kvp.Key.y, kvp.Key.z);
                if (cellIndexMap.TryGetValue(pos, out int cellIdx))
                {
                    int moduleIdx = modules.IndexOf(kvp.Value);
                    if (moduleIdx >= 0 && moduleIdx < 64)
                    {
                        var cell = nativeCells[cellIdx];
                        cell.CollapseToModule(moduleIdx);
                        nativeCells[cellIdx] = cell;
                        propagationQueue.Enqueue(cellIdx);
                    }
                }
            }
        }
        
        private int FindLowestEntropyCell()
        {
            randomSeed = randomSeed * 1103515245 + 12345; // LCG for deterministic sequence
            
            var job = new WFCFindLowestEntropyJob
            {
                cells = nativeCells,
                resultIndex = lowestEntropyResult,
                randomSeed = randomSeed
            };
            
            job.Schedule().Complete();
            return lowestEntropyResult.Value;
        }
        
        private void CollapseCell(int cellIndex, int moduleCount)
        {
            randomSeed = randomSeed * 1103515245 + 12345;
            
            var job = new WFCCollapseJob
            {
                cells = nativeCells,
                modules = nativeModules,
                cellIndex = cellIndex,
                randomSeed = randomSeed,
                moduleCount = moduleCount
            };
            
            job.Schedule().Complete();
        }
        
        private void RunPropagation(int moduleCount)
        {
            var job = new WFCPropagateJob
            {
                cells = nativeCells,
                modules = nativeModules,
                cellIndexMap = cellIndexMap,
                propagationQueue = propagationQueue,
                gridSize = gridSize,
                moduleCount = moduleCount
            };
            
            job.Schedule().Complete();
        }
        
        private bool HasUncolllapsedCells()
        {
            for (int i = 0; i < nativeCells.Length; i++)
            {
                if (!nativeCells[i].collapsed) return true;
            }
            return false;
        }
        
        private void CopyResultsToManagedCells()
        {
            WFCJobsHelper.CopyResultsToCells(nativeCells, cells, modules);
        }
    }
}
