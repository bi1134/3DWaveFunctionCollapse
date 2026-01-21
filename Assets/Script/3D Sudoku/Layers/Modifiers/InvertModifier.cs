using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class InvertModifier : WFCModifier
    {
        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            int width = map.width;
            int height = map.height;
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Color c = map.GetPixel(x, y);
                    
                    // If Active -> Background
                    // If Background -> Active
                    
                    if (IsColorMatch(c, layer.activeColor))
                    {
                        map.SetPixel(x, y, layer.BackgroundColor);
                    }
                    else
                    {
                        map.SetPixel(x, y, layer.activeColor);
                    }
                }
            }
            map.Apply();
        }
        
        private bool IsColorMatch(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f &&
                   Mathf.Abs(a.g - b.g) < 0.01f &&
                   Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }
}
