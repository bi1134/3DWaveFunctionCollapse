using UnityEngine;
using System;

namespace WFC_Sudoku
{
    public static class WFCEvent
    {
        // Event triggered when a WFCBuilder finishes generating a chunk.
        // Parameter: The WFCBuilder instance that finished.
        public static Action<WFCBuilder> OnChunkGenerated;

        public static void TriggerChunkGenerated(WFCBuilder chunk)
        {
            OnChunkGenerated?.Invoke(chunk);
        }
    }
}
