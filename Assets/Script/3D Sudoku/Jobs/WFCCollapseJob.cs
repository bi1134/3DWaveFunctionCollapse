using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WFC_Sudoku.Jobs
{
    /// <summary>
    /// Burst-compiled job to collapse a cell to a single module.
    /// Uses weighted random selection based on module weights.
    /// </summary>
    [BurstCompile]
    public struct WFCCollapseJob : IJob
    {
        public NativeArray<WFCCellData> cells;
        [ReadOnly] public NativeArray<WFCModuleData> modules;
        public int cellIndex;
        public uint randomSeed;
        public int moduleCount;
        
        public void Execute()
        {
            var cell = cells[cellIndex];
            if (cell.collapsed || cell.Entropy <= 0) return;
            
            var random = new Random(randomSeed);
            
            // Calculate total weight of possible modules
            float totalWeight = 0;
            for (int m = 0; m < moduleCount; m++)
            {
                if (cell.HasModule(m))
                {
                    totalWeight += modules[m].weight;
                }
            }
            
            if (totalWeight <= 0)
            {
                // Fallback: pick first available
                for (int m = 0; m < moduleCount; m++)
                {
                    if (cell.HasModule(m))
                    {
                        cell.CollapseToModule(m);
                        cells[cellIndex] = cell;
                        return;
                    }
                }
                return; // No modules available - contradiction
            }
            
            // Weighted random selection
            float pick = random.NextFloat() * totalWeight;
            float cumulative = 0;
            
            for (int m = 0; m < moduleCount; m++)
            {
                if (cell.HasModule(m))
                {
                    cumulative += modules[m].weight;
                    if (pick <= cumulative)
                    {
                        cell.CollapseToModule(m);
                        cells[cellIndex] = cell;
                        return;
                    }
                }
            }
            
            // Fallback (shouldn't reach here)
            for (int m = 0; m < moduleCount; m++)
            {
                if (cell.HasModule(m))
                {
                    cell.CollapseToModule(m);
                    cells[cellIndex] = cell;
                    return;
                }
            }
        }
    }
}
