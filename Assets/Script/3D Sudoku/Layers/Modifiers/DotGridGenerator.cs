using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class DotGridGenerator : WFCModifier
    {
        public int spacing = 2;
        public Vector2Int offset = Vector2Int.zero;

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            int w = map.width;
            int h = map.height;
            
            for(int x=0; x<w; x++)
            {
                for(int y=0; y<h; y++)
                {
                     // (x - off) % spacing == 0
                     int rx = x - offset.x;
                     int ry = y - offset.y;
                     
                     if (spacing > 0 && rx % spacing == 0 && ry % spacing == 0)
                     {
                         map.SetPixel(x, y, layer.activeColor);
                     }
                     else
                     {
                         map.SetPixel(x, y, layer.BackgroundColor);
                     }
                }
            }
            map.Apply();
        }
    }
}
