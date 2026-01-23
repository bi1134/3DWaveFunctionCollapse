using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class NeighborModifier : WFCModifier
    {
        [Header("Targeting")]
        [Tooltip("The color to look for/expand FROM.")]
        public Color sourceColor = Color.red;
        [Tooltip("If true, treats ANY non-background color as source.")]
        public bool useAnyNonEmptySource = false;
        
        [Header("Output")]
        [Tooltip("The color to paint the neighbors.")]
        public Color neighborColor = Color.blue;
        [Tooltip("How many layers of neighbors to add.")]
        [Range(1, 5)] public int radius = 1;
        
        [Header("Rules")]
        [Tooltip("Chance to spawn a neighbor (0.0 - 1.0).")]
        [Range(0f, 1f)] public float probability = 1.0f;
        [Tooltip("Include diagonal neighbors?")]
        public bool includeDiagonals = false;
        [Tooltip("If true, neighbors can overwrite existing non-source colors. If false, only paints empty pixels.")]
        public bool overwriteExisting = false;

        public NeighborModifier() { name = "Neighbor Expander"; }

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            int w = map.width;
            int h = map.height;
            
            // We run 'radius' iterations
            for(int r=0; r<radius; r++)
            {
                // Snapshot current state to avoid cascading expansion in single step
                Color[] pixels = map.GetPixels();
                Color[] newPixels = map.GetPixels(); // Copy to write to
                
                bool changed = false;

                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        int idx = y * w + x;
                        Color c = pixels[idx];
                        
                        // Check if this pixel is a Source (or a Neighbor from previous loop acting as source?)
                        // "Neighbors" usually expand from the original structure.
                        // If we want concentric rings, we should expand from 'sourceColor' OR 'neighborColor'?
                        // Let's assume we expand from Source OR Neighbor if radius > 1 to grow outward.
                        
                        bool isSource = IsMatch(c);
                        
                        if (isSource)
                        {
                            // Expand to neighbors
                            Expand(x, y, w, h, pixels, newPixels);
                            changed = true;
                        }
                    }
                }
                
                map.SetPixels(newPixels);
                if (!changed) break; 
            }
            map.Apply();
        }
        
        private void Expand(int x, int y, int w, int h, Color[] current, Color[] next)
        {
            // Directions
            List<Vector2Int> dirs = new List<Vector2Int> { 
                Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right 
            };
            
            if (includeDiagonals)
            {
                dirs.Add(new Vector2Int(1, 1));
                dirs.Add(new Vector2Int(1, -1));
                dirs.Add(new Vector2Int(-1, 1));
                dirs.Add(new Vector2Int(-1, -1));
            }
            
            foreach(var d in dirs)
            {
                int nx = x + d.x;
                int ny = y + d.y;
                
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                
                int nIdx = ny * w + nx;
                Color existing = current[nIdx];
                Color potential = next[nIdx]; // Check if we already wrote to it this frame
                
                // Logic:
                // 1. Must not be Source (don't overwrite self)
                // 2. Must not be Neighbor already (unless probability failed before? No, simpler)
                // 3. Must be Empty (Color.clear/black) OR Overwrite=True
                
                if (IsMatch(existing)) continue; // Don't overwrite source
                if (potential == neighborColor) continue; // Already marked
                
                // Overwrite check (Assuming Background is Clear or Black with 0 alpha)
                bool isEmpty = (existing.a == 0); 
                
                if (isEmpty || overwriteExisting)
                {
                    // Probability Check
                    if (Random.value <= probability)
                    {
                        next[nIdx] = neighborColor;
                    }
                }
            }
        }
        
        private bool IsMatch(Color c)
        {
            if (useAnyNonEmptySource) return c.a > 0 && c != neighborColor; // Don't self-expand neighbors endlessly unless intended
            // Actually, for radius loop, we usually WANT to expand from the newly created neighbors too?
            // If radius=2, we want path -> neighbor -> neighbor.
            // So 'Source' for expansion should include 'neighborColor' from previous steps.
            // But 'IsMatch' handles strict source identification.
            
            // Correction: For multi-step expansion, we need to treat 'neighborColor' as a valid source for the NEXT step.
            return (c == sourceColor || c == neighborColor);
        }
    }
}
