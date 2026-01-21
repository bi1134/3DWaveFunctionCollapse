using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class PoissonDiscGenerator : WFCModifier
    {
        public float radius = 4f;
        public int seed;
        public bool useRandomSeed = false;

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            if (useRandomSeed) seed = Random.Range(0, 100000);
            
            Texture2D map = layer.outputMap;
            int w = map.width;
            int h = map.height;
            
            // Clear
            for(int x=0; x<w; x++) 
                for(int y=0; y<h; y++) 
                    map.SetPixel(x, y, layer.BackgroundColor);

            List<Vector2> points = GeneratePoints(radius, new Vector2(w, h), seed);
            
            foreach(var p in points)
            {
                int x = Mathf.FloorToInt(p.x);
                int y = Mathf.FloorToInt(p.y);
                if (x >=0 && x < w && y >=0 && y < h)
                {
                    map.SetPixel(x, y, layer.activeColor);
                }
            }
            map.Apply();
        }

        private List<Vector2> GeneratePoints(float radius, Vector2 sampleRegionSize, int seed)
        {
            System.Random prng = new System.Random(seed);
            int k = 30; // rejection limit
            float cellSize = radius / Mathf.Sqrt(2);
            
            int cols = Mathf.CeilToInt(sampleRegionSize.x / cellSize);
            int rows = Mathf.CeilToInt(sampleRegionSize.y / cellSize);
            
            int[,] grid = new int[cols, rows];
            List<Vector2> points = new List<Vector2>();
            List<Vector2> activeList = new List<Vector2>();

            // Initial point
            Vector2 p0 = new Vector2((float)prng.NextDouble() * sampleRegionSize.x, (float)prng.NextDouble() * sampleRegionSize.y);
            activeList.Add(p0);
            points.Add(p0);
            
            int x0 = Mathf.FloorToInt(p0.x / cellSize);
            int y0 = Mathf.FloorToInt(p0.y / cellSize);
            if(x0>=0 && x0<cols && y0>=0 && y0<rows) grid[x0, y0] = points.Count; // 1-based index

            while(activeList.Count > 0)
            {
                int idx = prng.Next(0, activeList.Count);
                Vector2 p = activeList[idx];
                bool found = false;
                
                for(int i=0; i<k; i++)
                {
                    float angle = (float)prng.NextDouble() * Mathf.PI * 2;
                    float dist = radius * (float)(prng.NextDouble() + 1); // r to 2r
                    Vector2 newP = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                    
                    if (newP.x >= 0 && newP.x < sampleRegionSize.x && newP.y >= 0 && newP.y < sampleRegionSize.y)
                    {
                        // Check collision
                        int gx = Mathf.FloorToInt(newP.x / cellSize);
                        int gy = Mathf.FloorToInt(newP.y / cellSize);
                        
                        // Check neighbors
                        bool tooClose = false;
                        for(int iX = -2; iX <= 2; iX++)
                        {
                            for(int iY = -2; iY <= 2; iY++)
                            {
                                int nx = gx + iX;
                                int ny = gy + iY;
                                if (nx >=0 && nx < cols && ny >= 0 && ny < rows)
                                {
                                    int neighborIdx = grid[nx, ny];
                                    if (neighborIdx > 0)
                                    {
                                        Vector2 neighbor = points[neighborIdx-1];
                                        if (Vector2.Distance(neighbor, newP) < radius)
                                        {
                                            tooClose = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (tooClose) break;
                        }

                        if (!tooClose)
                        {
                            activeList.Add(newP);
                            points.Add(newP);
                            grid[gx, gy] = points.Count;
                            found = true;
                            break;
                        }
                    }
                }
                
                if (!found)
                {
                    activeList.RemoveAt(idx);
                }
            }
            return points;
        }
    }
}
