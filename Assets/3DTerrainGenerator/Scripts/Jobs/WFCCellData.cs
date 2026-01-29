using Unity.Collections;
using Unity.Mathematics;

namespace TerrainGenerator.Jobs
{
    /// <summary>
    /// Burst-compatible cell data structure.
    /// Stores cell state without managed references.
    /// </summary>
    public struct WFCCellData
    {
        public int3 gridPosition;
        public bool collapsed;
        public int collapsedModuleIndex;  // -1 if not collapsed
        
        // Bitmask for possible modules (supports up to 64 modules)
        // More efficient than FixedList for small module counts
        public ulong possibleModulesMask;
        
        public int Entropy => math.countbits(possibleModulesMask);
        
        public bool HasModule(int moduleIndex)
        {
            return (possibleModulesMask & (1UL << moduleIndex)) != 0;
        }
        
        public void RemoveModule(int moduleIndex)
        {
            possibleModulesMask &= ~(1UL << moduleIndex);
        }
        
        public void SetModule(int moduleIndex, bool allowed)
        {
            if (allowed)
                possibleModulesMask |= (1UL << moduleIndex);
            else
                possibleModulesMask &= ~(1UL << moduleIndex);
        }
        
        public void CollapseToModule(int moduleIndex)
        {
            collapsed = true;
            collapsedModuleIndex = moduleIndex;
            possibleModulesMask = 1UL << moduleIndex;
        }
        
        public void Reset(int moduleCount)
        {
            collapsed = false;
            collapsedModuleIndex = -1;
            // Set all bits up to moduleCount
            possibleModulesMask = moduleCount >= 64 ? ulong.MaxValue : (1UL << moduleCount) - 1;
        }
    }
}
