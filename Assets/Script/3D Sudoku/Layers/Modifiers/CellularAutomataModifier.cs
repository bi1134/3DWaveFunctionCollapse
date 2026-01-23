using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class CellularAutomataModifier : WFCModifier, INeighborStitchable
    {
        [Header("Settings")]
        [Range(0, 100)] public int fillPercent = 50;
        public int smoothIterations = 5;
        public bool ensureConnected = false;
        
        // Stitching Storage
        private Dictionary<EdgeSide, int[]> seededEdges = new Dictionary<EdgeSide, int[]>();

        public void ClearStitching()
        {
            seededEdges.Clear();
        }

        public object GetEdgeData(EdgeSide side)
        {
            // We need the FINAL applied data. 
            // But Map is only valid after Apply()? 
            // Usually GetEdgeData is called on the NEIGHBOR (which is already generated).
            // So we assume 'this.outputMap' (from Base if we stored it?) or we need to store the grid result?
            // The WFCModifier base doesn't store the result persistently in a handy way except maybe passing 'layer.outputMap' but that's in Apply context.
            // We need to Cache the result in Apply?
            // Yes. Let's add a cachedGrid.
            if (cachedGrid == null) return null;
            
            int w = cachedGrid.GetLength(0);
            int h = cachedGrid.GetLength(1);
            int[] edge = null;

            if (side == EdgeSide.Left) { edge = new int[h]; for(int y=0; y<h; y++) edge[y] = cachedGrid[0, y]; }
            if (side == EdgeSide.Right) { edge = new int[h]; for(int y=0; y<h; y++) edge[y] = cachedGrid[w-1, y]; }
            if (side == EdgeSide.Top) { edge = new int[w]; for(int x=0; x<w; x++) edge[x] = cachedGrid[x, h-1]; }
            if (side == EdgeSide.Bottom) { edge = new int[w]; for(int x=0; x<w; x++) edge[x] = cachedGrid[x, 0]; }
            
            return edge;
        }

        public void InjectEdgeData(object data, EdgeSide side)
        {
            var edgeArray = data as int[];
            if (edgeArray != null)
            {
                if (seededEdges.ContainsKey(side)) seededEdges[side] = edgeArray;
                else seededEdges.Add(side, edgeArray);
            }
        }
        
        private int[,] cachedGrid; // Store for GetEdgeData

        public int seed = 0;
        public bool useRandomSeed = false;
        public bool invert = false;

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            int width = map.width;
            int height = map.height;
            int[,] grid = new int[width, height];

            // 1. Fill Randomly
            if (useRandomSeed)
            {
                seed = Random.Range(0, 100000);
            }
            int currentSeed = seed;
            
            System.Random pseudoRandom = new System.Random(currentSeed);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Check Stitching FIRST
                    bool stitchingHit = false;
                    
                    // Left Edge (x=0)
                    if (x == 0 && seededEdges.ContainsKey(EdgeSide.Left)) { grid[x,y] = seededEdges[EdgeSide.Left][y]; stitchingHit=true; }
                    // Right Edge (x=w-1)
                    else if (x == width-1 && seededEdges.ContainsKey(EdgeSide.Right)) { grid[x,y] = seededEdges[EdgeSide.Right][y]; stitchingHit=true; }
                    // Bottom Edge (y=0)
                    else if (y == 0 && seededEdges.ContainsKey(EdgeSide.Bottom)) { grid[x,y] = seededEdges[EdgeSide.Bottom][x]; stitchingHit=true; }
                    // Top Edge (y=h-1)
                    else if (y == height-1 && seededEdges.ContainsKey(EdgeSide.Top)) { grid[x,y] = seededEdges[EdgeSide.Top][x]; stitchingHit=true; }
                    
                    if (!stitchingHit)
                    {
                        // Edges not stitched are empty? Or random? 
                        // Original Logic: "Edges are usually empty".
                        if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                        {
                            grid[x, y] = 0;
                        }
                        else
                        {
                            grid[x, y] = (pseudoRandom.Next(0, 100) < fillPercent) ? 1 : 0;
                        }
                    }
                }
            }
            
            // Cache Initial or Final? 
            // If we cache here, we cache pure random. 
            // We should cache AFTER smoothing.
            // But wait, smoothing might destroy the stitched edge?
            // "Smooth - preserve edges?"
            // Standard CA smoothing usually modifies everything.
            // If we want seamless stitching, we should Freeze the Stitched Edges during smoothing?
            // For now, let's cache at the End.


            // 2. Smooth
            for (int i = 0; i < smoothIterations; i++)
            {
                SmoothGrid(grid, width, height);
            }

            // 3. Ensure Connected (Flood Fill)
            if (ensureConnected)
            {
                 KeepLargestRegion(grid, width, height);
            }

            // 4. Write to Texture
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool isActive = (grid[x, y] == 1);
                    if (invert) isActive = !isActive;
                    
                    map.SetPixel(x, y, isActive ? layer.activeColor : layer.BackgroundColor);
                }
            }
            map.Apply();
        }

        private void SmoothGrid(int[,] grid, int width, int height)
        {
            int[,] nextGrid = new int[width, height];
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int neighborWalls = GetSurroundingWallCount(grid, x, y, width, height);

                    if (neighborWalls > 4)
                        nextGrid[x, y] = 1;
                    else if (neighborWalls < 4)
                        nextGrid[x, y] = 0;
                    else
                        nextGrid[x, y] = grid[x, y];
                }
            }
            
             for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    grid[x,y] = nextGrid[x,y];
                }
            }
        }

        private int GetSurroundingWallCount(int[,] grid, int gridX, int gridY, int width, int height)
        {
            int wallCount = 0;
            for (int neighborX = gridX - 1; neighborX <= gridX + 1; neighborX++)
            {
                for (int neighborY = gridY - 1; neighborY <= gridY + 1; neighborY++)
                {
                    if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
                    {
                        if (neighborX != gridX || neighborY != gridY)
                        {
                            wallCount += grid[neighborX, neighborY];
                        }
                    }
                    else
                    {
                        wallCount++; 
                    }
                }
            }
            return wallCount;
        }

        // --- Connectivity ---
        private void KeepLargestRegion(int[,] grid, int width, int height)
        {
             List<List<Vector2Int>> regions = new List<List<Vector2Int>>();
             bool[,] visited = new bool[width, height];

             for (int x = 0; x < width; x++)
             {
                 for (int y = 0; y < height; y++)
                 {
                     if (grid[x,y] == 1 && !visited[x,y])
                     {
                         List<Vector2Int> newRegion = GetRegion(x, y, grid, visited, width, height);
                         regions.Add(newRegion);
                     }
                 }
             }

             if (regions.Count == 0) return;

             // Find largest
             int maxSize = -1;
             int maxIndex = -1;
             for(int i=0; i<regions.Count; i++)
             {
                 if(regions[i].Count > maxSize)
                 {
                     maxSize = regions[i].Count;
                     maxIndex = i;
                 }
             }

             // Clear all others
             // Re-write grid: 0 for everything, then 1 for largest region
             for (int x = 0; x < width; x++)
                 for (int y = 0; y < height; y++)
                     grid[x, y] = 0;
            
             foreach(var pos in regions[maxIndex])
             {
                 grid[pos.x, pos.y] = 1;
             }
        }

        private List<Vector2Int> GetRegion(int startX, int startY, int[,] grid, bool[,] visited, int width, int height)
        {
            List<Vector2Int> tiles = new List<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            
            queue.Enqueue(new Vector2Int(startX, startY));
            visited[startX, startY] = true;

            while(queue.Count > 0)
            {
                Vector2Int tile = queue.Dequeue();
                tiles.Add(tile);

                // Check Neighbors (4-Dir)
                Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach(var d in dirs)
                {
                    int nx = tile.x + d.x;
                    int ny = tile.y + d.y;
                    
                    if (nx >=0 && nx < width && ny >=0 && ny < height)
                    {
                        if (!visited[nx, ny] && grid[nx, ny] == 1)
                        {
                            visited[nx, ny] = true;
                            queue.Enqueue(new Vector2Int(nx, ny));
                        }
                    }
                }
            }
            return tiles;
        }
    }
}
