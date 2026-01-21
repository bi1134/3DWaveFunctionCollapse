using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class ShrinkModifier : WFCModifier
    {
        public int radii = 1;
        public bool useDiagonals = false;

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            for (int i = 0; i < radii; i++)
            {
                 ShrinkOnce(ref map, layer);
            }
        }

        private void ShrinkOnce(ref Texture2D map, WFCBlueprintLayer layer)
        {
             int width = map.width;
             int height = map.height;
             
             Color[] pixels = map.GetPixels();
             Color[] newPixels = new Color[pixels.Length];
             System.Array.Copy(pixels, newPixels, pixels.Length);

             for (int x = 0; x < width; x++)
             {
                 for (int y = 0; y < height; y++)
                 {
                     int idx = x + y * width;
                     Color self = pixels[idx];

                     // optimization: if already background, skip
                     if (IsColorMatch(self, layer.BackgroundColor)) continue; 

                     // Check neighbors
                     bool touchesEmpty = false;
                     
                     // 4-Dir
                     if (IsNeighborBackground(pixels, x+1, y, width, height, layer)) touchesEmpty = true;
                     else if (IsNeighborBackground(pixels, x-1, y, width, height, layer)) touchesEmpty = true;
                     else if (IsNeighborBackground(pixels, x, y+1, width, height, layer)) touchesEmpty = true;
                     else if (IsNeighborBackground(pixels, x, y-1, width, height, layer)) touchesEmpty = true;
                     
                     if (useDiagonals && !touchesEmpty)
                     {
                         if (IsNeighborBackground(pixels, x+1, y+1, width, height, layer)) touchesEmpty = true;
                         else if (IsNeighborBackground(pixels, x-1, y+1, width, height, layer)) touchesEmpty = true;
                         else if (IsNeighborBackground(pixels, x+1, y-1, width, height, layer)) touchesEmpty = true;
                         else if (IsNeighborBackground(pixels, x-1, y-1, width, height, layer)) touchesEmpty = true;
                     }

                     if (touchesEmpty)
                     {
                         newPixels[idx] = layer.BackgroundColor;
                     }
                 }
             }
             
             map.SetPixels(newPixels);
             map.Apply();
        }

        private bool IsNeighborBackground(Color[] grid, int x, int y, int w, int h, WFCBlueprintLayer layer)
        {
            if (x < 0 || x >= w || y < 0 || y >= h) return true; // Edge considers empty
            
            Color c = grid[x + y * w];
            return IsColorMatch(c, layer.BackgroundColor);
        }

        private bool IsColorMatch(Color a, Color b)
        {
             return Mathf.Abs(a.r - b.r) < 0.01f &&
                   Mathf.Abs(a.g - b.g) < 0.01f &&
                   Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }
}
