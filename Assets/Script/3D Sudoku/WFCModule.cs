using System.Collections.Generic;
using UnityEngine;

namespace WFC_Sudoku
{
    public class WFCModule : MonoBehaviour
    {


        [Header("Editor Settings")]
        [Tooltip("Purely for organization in the inspector")]
        public string category = "General";

        public enum ModuleRole
        {
            Body,    // Toppings (Wall/Pillar) - Default
            Roof,    // Top Bun (Roof)
            Base,    // Plate (Bottom Bun) - Only Y=0
            Air      // Seasoning (Empty) - Any Layer
        }

        [Header("Rules")]
        public ModuleRole role = ModuleRole.Body;
        [Tooltip("Higher value = More likely to appear. Lower = Rarity (e.g. Tower = 0.1)")]
        public float spawnWeight = 1.0f;

        [Header("Sockets (Auto-Generation)")]
        [Tooltip("ID for the Right face.")] public SocketID rightSocket = SocketID.Air;
        [Tooltip("ID for the Left face.")] public SocketID leftSocket = SocketID.Air;
        [Tooltip("ID for the Front face (Z+).")] public SocketID frontSocket = SocketID.Air;
        [Tooltip("ID for the Back face (Z-).")] public SocketID backSocket = SocketID.Air;
        [Tooltip("ID for the Top face (Y+).")] public SocketID topSocket = SocketID.Air;
        [Tooltip("ID for the Bottom face (Y-).")] public SocketID bottomSocket = SocketID.Air;

        [Header("Explicit Neighbor Arrays (Auto-Filled)")]
        public WFCModule[] rightNeighbors;
        public WFCModule[] leftNeighbors;
        public WFCModule[] frontNeighbors;
        public WFCModule[] backNeighbors;
        public WFCModule[] topNeighbors;
        public WFCModule[] bottomNeighbors;

        [Header("Visual Variations (Townscaper Style)")]
        [Tooltip("If populated, the Solver will pick one of these based on neighbors instead of the default object.")]
        public List<WFCVariant> variants;
    }
}
