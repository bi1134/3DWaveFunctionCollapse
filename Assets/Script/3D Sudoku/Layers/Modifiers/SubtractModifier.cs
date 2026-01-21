using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class SubtractModifier : WFCModifier
    {
        [Tooltip("The NAME of the layer to subtract.")]
        public string inputLayerName;

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            if (context == null) return;
            
            // Find layer by name
            WFCBlueprintLayer sourceLayer = null;
            foreach(var l in context)
            {
                if(l.layerName == inputLayerName)
                {
                    sourceLayer = l;
                    break;
                }
            }
            
            if (sourceLayer == null || sourceLayer.outputMap == null) return;

            Texture2D source = sourceLayer.outputMap;
            Texture2D map = layer.outputMap;
            
            for (int x = 0; x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    // If source is Active at this spot, clear it in our map
                    // (Difference: A - B)
                    
                    if (x < source.width && y < source.height)
                    {
                        Color srcColor = source.GetPixel(x, y);
                        // If source IS active (not background)
                        if (!IsColorMatch(srcColor, sourceLayer.BackgroundColor))
                        {
                            // Erase self
                            map.SetPixel(x, y, layer.BackgroundColor);
                        }
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
