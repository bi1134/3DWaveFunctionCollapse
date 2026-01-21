using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [CreateAssetMenu(fileName = "New Tile Preset", menuName = "WFC/Tile Preset")]
    public class WFCTilePreset : ScriptableObject
    {
        [Header("Module Database")]
        [Tooltip("All available WFC Modules for this theme.")]
        public List<WFCModule> modules = new List<WFCModule>();

        [Header("Map Generation Rules")]
        [Tooltip("Color mapping rules for generating maps from textures.")]
        public List<MapColorRule> mapRules = new List<MapColorRule>();

        // Helper to find module by name (if needed)
        public WFCModule GetModule(string name)
        {
            return modules.Find(m => m.name == name);
        }

        public WFCModule GetModuleForColor(Color pixelColor, out int targetLayer)
        {
            targetLayer = 0;
            foreach (var rule in mapRules)
            {
                if (!rule.active) continue;
                if (IsColorMatch(pixelColor, rule.sourceColor, rule.tolerance))
                {
                    targetLayer = rule.targetLayer;
                    return rule.modulePrefab;
                }
            }
            return null;
        }

        private bool IsColorMatch(Color a, Color b, float tolerance)
        {
            return Mathf.Abs(a.r - b.r) <= tolerance &&
                   Mathf.Abs(a.g - b.g) <= tolerance &&
                   Mathf.Abs(a.b - b.b) <= tolerance;
        }

#if UNITY_EDITOR
        public void GenerateNeighbors()
        {
            Debug.Log($"Generating neighbors for {modules.Count} modules in Registry...");

            // 1. Clean old neighbors
            foreach (var mod in modules)
            {
                mod.rightNeighbors = new WFCModule[0];
                mod.leftNeighbors = new WFCModule[0];
                mod.frontNeighbors = new WFCModule[0];
                mod.backNeighbors = new WFCModule[0];
                mod.topNeighbors = new WFCModule[0];
                mod.bottomNeighbors = new WFCModule[0];
            }

            // 2. Pairwise Check
            foreach (var A in modules)
            {
                if (A == null) continue;

                List<WFCModule> right = new List<WFCModule>();
                List<WFCModule> left = new List<WFCModule>();
                List<WFCModule> front = new List<WFCModule>();
                List<WFCModule> back = new List<WFCModule>();
                List<WFCModule> top = new List<WFCModule>();
                List<WFCModule> bottom = new List<WFCModule>();

                foreach (var B in modules)
                {
                    if (B == null) continue;

                    // Standard Socket Matching (Symmetric)
                    // A.Right connects to B.Left if A.RightSocket == B.LeftSocket
                    if (A.rightSocket == B.leftSocket) right.Add(B);
                    if (A.leftSocket == B.rightSocket) left.Add(B);
                    
                    if (A.frontSocket == B.backSocket) front.Add(B);
                    if (A.backSocket == B.frontSocket) back.Add(B);
                    
                    if (A.topSocket == B.bottomSocket) top.Add(B);
                    if (A.bottomSocket == B.topSocket) bottom.Add(B);
                }

                A.rightNeighbors = right.ToArray();
                A.leftNeighbors = left.ToArray();
                A.frontNeighbors = front.ToArray();
                A.backNeighbors = back.ToArray();
                A.topNeighbors = top.ToArray();
                A.bottomNeighbors = bottom.ToArray();

                UnityEditor.EditorUtility.SetDirty(A);
            }
            
            Debug.Log("Registry Neighbor Generation Complete!");
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
