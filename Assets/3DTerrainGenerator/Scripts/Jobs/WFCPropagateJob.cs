using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TerrainGenerator.Jobs
{
    /// <summary>
    /// Burst-compiled constraint propagation job.
    /// Processes a queue of cells and propagates constraints to neighbors.
    /// </summary>
    [BurstCompile]
    public struct WFCPropagateJob : IJob
    {
        public NativeArray<WFCCellData> cells;
        [ReadOnly] public NativeArray<WFCModuleData> modules;
        [ReadOnly] public NativeParallelHashMap<int3, int> cellIndexMap;
        public NativeQueue<int> propagationQueue;
        public int3 gridSize;
        public int moduleCount;
        
        // Direction vectors
        private static readonly int3 Right = new int3(1, 0, 0);
        private static readonly int3 Left = new int3(-1, 0, 0);
        private static readonly int3 Up = new int3(0, 1, 0);
        private static readonly int3 Down = new int3(0, -1, 0);
        private static readonly int3 Forward = new int3(0, 0, 1);
        private static readonly int3 Back = new int3(0, 0, -1);
        
        public void Execute()
        {
            while (propagationQueue.TryDequeue(out int currentIdx))
            {
                var current = cells[currentIdx];
                
                CheckNeighbor(ref current, currentIdx, Right, 0);   // right -> use rightNeighborsMask
                CheckNeighbor(ref current, currentIdx, Left, 1);    // left -> use leftNeighborsMask
                CheckNeighbor(ref current, currentIdx, Up, 2);      // up -> use topNeighborsMask
                CheckNeighbor(ref current, currentIdx, Down, 3);    // down -> use bottomNeighborsMask
                CheckNeighbor(ref current, currentIdx, Forward, 4); // forward -> use frontNeighborsMask
                CheckNeighbor(ref current, currentIdx, Back, 5);    // back -> use backNeighborsMask
            }
        }
        
        private void CheckNeighbor(ref WFCCellData current, int currentIdx, int3 dir, int dirIndex)
        {
            int3 nPos = current.gridPosition + dir;
            
            // Check bounds (optional, cellIndexMap will handle missing entries)
            if (!cellIndexMap.TryGetValue(nPos, out int neighborIdx)) return;
            
            var neighbor = cells[neighborIdx];
            if (neighbor.collapsed) return;
            
            // Build allowed mask: union of all neighbor masks from current's possible modules
            ulong allowedMask = 0;
            ulong currentMask = current.possibleModulesMask;
            
            for (int m = 0; m < moduleCount; m++)
            {
                if ((currentMask & (1UL << m)) != 0)
                {
                    allowedMask |= modules[m].GetNeighborMask(dirIndex);
                }
            }
            
            // Constrain neighbor: keep only modules that are in both allowed AND neighbor's current possibilities
            ulong oldMask = neighbor.possibleModulesMask;
            ulong newMask = oldMask & allowedMask;
            
            if (newMask != oldMask)
            {
                neighbor.possibleModulesMask = newMask;
                cells[neighborIdx] = neighbor;
                propagationQueue.Enqueue(neighborIdx);
            }
        }
    }
}
