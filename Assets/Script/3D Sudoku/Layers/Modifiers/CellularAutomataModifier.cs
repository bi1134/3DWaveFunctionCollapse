using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class CellularAutomataModifier : WFCModifier
    {
        [Header("Settings")]
        [Range(0, 100)] public int fillPercent = 50;
        public int smoothIterations = 5;
        public bool ensureConnected = false;
        
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
                    // Edges are usually empty 
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
