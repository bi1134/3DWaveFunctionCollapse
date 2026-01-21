using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class NoiseGenerator : WFCModifier
    {
        public float scale = 10f;
        [Range(0f, 1f)] public float threshold = 0.5f;
        public Vector2 offset;
        public int seed;
        public bool useRandomSeed = false;

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            if (useRandomSeed) seed = Random.Range(0, 100000);
            
            Texture2D map = layer.outputMap;
            int w = map.width;
            int h = map.height;

            for(int x=0; x<w; x++)
            {
                for(int y=0; y<h; y++)
                {
                    float xCoord = (float)x / w * scale + offset.x + seed;
                    float yCoord = (float)y / h * scale + offset.y + seed;
                    
                    float sample = Mathf.PerlinNoise(xCoord, yCoord);
                    
                    if (sample > threshold)
                    {
                        map.SetPixel(x, y, layer.activeColor);
                    }
                    // Else leave as is (or background? Usually noise fills everything)
                    // Let's assume it writes background too
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
