using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator
{
    /// <summary>
    /// Common interface for WFC cells, allowing both MonoBehaviour-based WFCCell
    /// and lightweight WFCCellData to work with the WFC algorithm.
    /// </summary>
    public interface IWFCCell
    {
        Vector3Int GridPosition { get; }
        bool Collapsed { get; set; }
        List<WFCModule> PossibleModules { get; }
        int Entropy { get; }
        float HeightRatio { get; }
        int TotalGridHeight { get; }
        
        /// <summary>
        /// Initialize the cell with all possible modules.
        /// </summary>
        void Initialize(List<WFCModule> allModules, Vector3Int pos, int gridHeight);
        
        /// <summary>
        /// Collapse this cell to a single module using weighted random selection.
        /// </summary>
        void Collapse();
        
        /// <summary>
        /// Constrain this cell's possible modules to the intersection with the allowed set.
        /// Returns true if any modules were removed.
        /// </summary>
        bool Constrain(List<WFCModule> allowedModules);
    }
}
