using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class GrowModifier : WFCModifier
    {
        [Range(1, 5)] public int radius = 1;

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            int width = map.width;
            int height = map.height;
            Color[] pixels = map.GetPixels();
            Color[] newPixels = new Color[pixels.Length];
            System.Array.Copy(pixels, newPixels, pixels.Length);
            
            // For each pixel, if it touches an Active pixel, it becomes Active.
            // Dilation.
            
            for(int r = 0; r < radius; r++)
            {
                 Color[] iterPixels = new Color[pixels.Length];
                 System.Array.Copy(newPixels, iterPixels, pixels.Length);
                 
                 for (int x = 0; x < width; x++)
                 {
                     for (int y = 0; y < height; y++)
                     {
                         int idx = x + y * width;
                         
                         // Optimization: If already active, skip
                         if (!IsColorMatch(iterPixels[idx], layer.BackgroundColor)) 
                         {
                             newPixels[idx] = layer.activeColor;
                             continue;
                         }

                         // Check neighbors for ANY active
                         if (IsNeighborActive(x, y, width, height, iterPixels, layer))
                         {
                             newPixels[idx] = layer.activeColor;
                         }
                     }
                 }
            }

            map.SetPixels(newPixels);
            map.Apply();
        }

        private bool IsNeighborActive(int x, int y, int w, int h, Color[] grid, WFCBlueprintLayer layer)
        {
            // 8-way or 4-way? Usually dilation is better with 8-way for rounded, 4-way for diamond
            // Let's do 4-way for safety first
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };
            
            for(int i=0; i<4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                
                if (nx >=0 && nx < w && ny >=0 && ny < h)
                {
                    Color c = grid[nx + ny * w];
                    if (!IsColorMatch(c, layer.BackgroundColor)) return true; // It is active
                }
            }
            return false;
        }

        private bool IsColorMatch(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f &&
                   Mathf.Abs(a.g - b.g) < 0.01f &&
                   Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }
}
