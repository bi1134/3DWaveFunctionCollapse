using Unity.Collections;

namespace TerrainGenerator.Jobs
{
    /// <summary>
    /// Burst-compatible module data structure.
    /// Stores neighbor constraints as bitmasks (matching WFCCellData's possibleModulesMask).
    /// </summary>
    public struct WFCModuleData
    {
        public float weight;
        
        // Neighbor bitmasks - each bit represents if that module index is allowed
        // Supports up to 64 modules (same as WFCCellData)
        public ulong rightNeighborsMask;
        public ulong leftNeighborsMask;
        public ulong topNeighborsMask;
        public ulong bottomNeighborsMask;
        public ulong frontNeighborsMask;
        public ulong backNeighborsMask;
        
        public ulong GetNeighborMask(int direction)
        {
            // direction: 0=right, 1=left, 2=up, 3=down, 4=forward, 5=back
            switch (direction)
            {
                case 0: return rightNeighborsMask;
                case 1: return leftNeighborsMask;
                case 2: return topNeighborsMask;
                case 3: return bottomNeighborsMask;
                case 4: return frontNeighborsMask;
                case 5: return backNeighborsMask;
                default: return 0;
            }
        }
    }
}
