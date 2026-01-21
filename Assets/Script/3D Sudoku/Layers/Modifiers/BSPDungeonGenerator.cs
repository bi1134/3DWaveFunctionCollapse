using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class BSPDungeonGenerator : WFCModifier
    {
        public int minRoomSize = 4;
        public int splitIterations = 3;
        public int corridorWidth = 1;
        public int seed;
        public bool useRandomSeed = false;

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            if(useRandomSeed) seed = Random.Range(0, 100000);
            System.Random rng = new System.Random(seed);

            Texture2D map = layer.outputMap;
            int w = map.width;
            int h = map.height;
            
            // Clear
            for(int x=0; x<w; x++) for(int y=0; y<h; y++) map.SetPixel(x, y, layer.BackgroundColor);

            // BSP Tree
            List<RectInt> rooms = new List<RectInt>();
            List<RectInt> nodes = new List<RectInt>();
            nodes.Add(new RectInt(0, 0, w, h));
            
            for(int i=0; i<splitIterations; i++)
            {
                List<RectInt> newNodes = new List<RectInt>();
                foreach(var node in nodes)
                {
                    // Attempt split
                    bool splitH = rng.NextDouble() > 0.5;
                    if (node.width > node.height && node.width / node.height >= 1.25) splitH = false; // vertical split
                    else if (node.height > node.width && node.height / node.width >= 1.25) splitH = true; // horizontal split

                    int max = (splitH ? node.height : node.width) - minRoomSize;
                    if (max < minRoomSize) 
                    {
                        newNodes.Add(node); // too small
                        continue;
                    }
                    
                    int split = rng.Next(minRoomSize, max);
                    
                    if (splitH)
                    {
                        newNodes.Add(new RectInt(node.x, node.y, node.width, split));
                        newNodes.Add(new RectInt(node.x, node.y + split, node.width, node.height - split));
                    }
                    else
                    {
                        newNodes.Add(new RectInt(node.x, node.y, split, node.height));
                        newNodes.Add(new RectInt(node.x + split, node.y, node.width - split, node.height));
                    }
                }
                nodes = newNodes;
            }

            // Create Rooms inside nodes
            foreach(var node in nodes)
            {
                // Safety check: Node must be bigger than room + margin
                if (node.width <= minRoomSize + 2 || node.height <= minRoomSize + 2) continue;

                int maxW = node.width - 2;
                int maxH = node.height - 2;
                
                // Ensure room doesn't exceed node (double check math)
                // rng.Next(min, max) => max must be > min
                if (maxW <= minRoomSize) maxW = minRoomSize + 1;
                if (maxH <= minRoomSize) maxH = minRoomSize + 1;

                int rW = rng.Next(minRoomSize, maxW);
                int rH = rng.Next(minRoomSize, maxH);
                
                int maxX = node.width - rW - 1;
                int maxY = node.height - rH - 1;
                
                if (maxX < 1) maxX = 1;
                if (maxY < 1) maxY = 1;

                int rX = node.x + rng.Next(1, maxX + 1); // +1 because Next exclusive
                int rY = node.y + rng.Next(1, maxY + 1);
                
                RectInt room = new RectInt(rX, rY, rW, rH);
                rooms.Add(room);
                
                // Paint Room
                for(int px=room.x; px<room.x+room.width; px++)
                {
                    for(int py=room.y; py<room.y+room.height; py++)
                    {
                        map.SetPixel(px, py, layer.activeColor);
                    }
                }
            }

            // Simple Corridors (Connect centers) - A naive approach for BSP is connect siblings
            // Better: connect to nearest? 
            // Let's do a simple MST or just connect sequentially used for simple tree logic
            // For now, simple Previous-to-Current connection
             for(int i=0; i<rooms.Count - 1; i++)
             {
                 Vector2Int c1 = Vector2Int.RoundToInt(rooms[i].center);
                 Vector2Int c2 = Vector2Int.RoundToInt(rooms[i+1].center);
                 DrawCorridor(map, c1, c2, corridorWidth, layer.activeColor);
             }
             
             // Connect last to first?
             // DrawCorridor(map, rooms[rooms.Count-1].center, rooms[0].center, corridorWidth, layer.activeColor);

            map.Apply();
        }

        private void DrawCorridor(Texture2D map, Vector2Int start, Vector2Int end, int width, Color col)
        {
            // L-Shaped corridor
            int x = start.x;
            int y = start.y;
            
            while(x != end.x)
            {
                for(int i= -width/2; i<=width/2; i++) map.SetPixel(Mathf.Clamp(x,0,map.width), Mathf.Clamp(y+i,0,map.height), col);
                x += (end.x > x) ? 1 : -1;
            }
            while(y != end.y)
            {
                for(int i= -width/2; i<=width/2; i++) map.SetPixel(Mathf.Clamp(x+i,0,map.width), Mathf.Clamp(y,0,map.height), col);
                y += (end.y > y) ? 1 : -1;
            }
        }
    }
}
