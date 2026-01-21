using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class TextureInputGenerator : WFCModifier
    {
        public Texture2D inputTexture;
        [Tooltip("Channel to read from. R=0, G=1, B=2, A=3, Grayscale=4")]
        public int channel = 4;
        [Range(0f, 1f)] public float threshold = 0.5f;
        public bool resizeLayerToTexture = false;

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            if (inputTexture == null) return;
            
            // if resize requested? (Cannot easily resize layer size struct from here without ref issues, but we can resize the map)
            // But layer.size is used elsewhere. 
            // For now, scale texture to map.
            
            Texture2D map = layer.outputMap;
            int w = map.width;
            int h = map.height;
            
            // Simple Nearest Neighbor scaling
            for(int x=0; x<w; x++)
            {
                for(int y=0; y<h; y++)
                {
                    float u = (float)x / w;
                    float v = (float)y / h;
                    
                    Color c = inputTexture.GetPixelBilinear(u, v);
                    float val = 0f;
                    switch(channel)
                    {
                        case 0: val = c.r; break;
                        case 1: val = c.g; break;
                        case 2: val = c.b; break;
                        case 3: val = c.a; break;
                        case 4: val = c.grayscale; break;
                    }
                    
                    if (val > threshold)
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
