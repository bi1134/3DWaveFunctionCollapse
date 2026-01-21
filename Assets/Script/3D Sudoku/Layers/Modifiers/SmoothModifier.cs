using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class SmoothModifier : WFCModifier
    {
        [Range(1, 10)] public int iterations = 1;

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            int width = map.width;
            int height = map.height;
            
            int[,] grid = new int[width, height];

            // Read
            for(int x=0; x<width; x++)
            {
                for(int y=0; y<height; y++)
                {
                    Color c = map.GetPixel(x, y);
                    grid[x, y] = IsColorMatch(c, layer.BackgroundColor) ? 0 : 1;
                }
            }

            // Smooth
            for(int i=0; i<iterations; i++)
            {
                grid = SmoothGrid(grid, width, height);
            }

            // Write
            for(int x=0; x<width; x++)
            {
                for(int y=0; y<height; y++)
                {
                    map.SetPixel(x, y, grid[x, y] == 1 ? layer.activeColor : layer.BackgroundColor);
                }
            }
            map.Apply();
        }

        private int[,] SmoothGrid(int[,] grid, int width, int height)
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
            return nextGrid;
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

        private bool IsColorMatch(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f &&
                   Mathf.Abs(a.g - b.g) < 0.01f &&
                   Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }
}
