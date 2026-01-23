using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    // Common Enum for Stitching
    public enum EdgeSide { Left, Right, Top, Bottom }

    public interface INeighborStitchable
    {
        // Return data representing the edge state on the given side.
        // Can be List<PathPoint>, float[] array of pixels, etc.
        object GetEdgeData(EdgeSide side);

        // Accept data from a neighbor to influence generation on this side.
        void InjectEdgeData(object data, EdgeSide side);
        
        // Clear any injected data (for clean re-runs)
        void ClearStitching();
    }
}
