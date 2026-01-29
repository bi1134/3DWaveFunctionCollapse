using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TerrainGenerator.Jobs
{
    /// <summary>
    /// Burst-compiled job to find the cell with lowest entropy.
    /// Priority: Lower Y first, then lower entropy, then random tiebreak.
    /// </summary>
    [BurstCompile]
    public struct WFCFindLowestEntropyJob : IJob
    {
        [ReadOnly] public NativeArray<WFCCellData> cells;
        public NativeReference<int> resultIndex;
        public uint randomSeed;
        
        public void Execute()
        {
            int bestIdx = -1;
            int bestEntropy = int.MaxValue;
            int bestY = int.MaxValue;
            
            var random = new Random(randomSeed);
            float bestRandom = float.MaxValue;
            
            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i];
                if (c.collapsed || c.Entropy <= 0) continue;
                
                int y = c.gridPosition.y;
                int entropy = c.Entropy;
                
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
                        float r = random.NextFloat();
                        if (r < bestRandom)
                        {
                            isBetter = true;
                            bestRandom = r;
                        }
                    }
                }
                
                if (isBetter)
                {
                    bestIdx = i;
                    bestEntropy = entropy;
                    bestY = y;
                    if (bestRandom == float.MaxValue) 
                        bestRandom = random.NextFloat();
                }
            }
            
            resultIndex.Value = bestIdx;
        }
    }
}
