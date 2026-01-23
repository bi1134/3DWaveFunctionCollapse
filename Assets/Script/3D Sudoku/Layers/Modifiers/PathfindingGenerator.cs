using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    /// <summary>
    /// A simplified pathfinding generator using A* algorithm.
    /// For cleaner TD paths, use TowerDefensePathGenerator instead.
    /// </summary>
    [System.Serializable]
    public class PathfindingGenerator : WFCModifier
    {
        [Header("Path Settings")]
        public Vector2Int startPoint = Vector2Int.zero;
        public Vector2Int endPoint = new Vector2Int(19, 19);
        public bool randomPoints = false;
        public int seed;
        
        [Header("Visuals")]
        public int pathWidth = 1;
        public bool markEndpoints = false;
        public Color startColor = Color.green;
        public Color endColor = Color.red;

        [Header("Algorithm")]
        [Tooltip("Cost for turns. Higher = straighter paths.")]
        public float turnCost = 0.5f;
        
        [Header("Cost Map")]
        public string costLayerName; 

        public PathfindingGenerator() { }

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            int w = map.width;
            int h = map.height;
            
            if (randomPoints || seed != 0) 
            {
                Random.InitState(seed == 0 ? System.DateTime.Now.Millisecond : seed);
                if (randomPoints)
                {
                    startPoint = new Vector2Int(Random.Range(0, w), Random.Range(0, h));
                    endPoint = new Vector2Int(Random.Range(0, w), Random.Range(0, h));
                }
            }
            
            // Prepare obstacles
            bool[,] obstacles = new bool[w, h];
            if (!string.IsNullOrEmpty(costLayerName) && context != null)
            {
                var costL = context.Find(l => l.layerName == costLayerName);
                if (costL != null && costL.outputMap != null)
                {
                    for(int x=0; x<w; x++)
                    {
                         for(int y=0; y<h; y++)
                        {
                            if (costL.outputMap.width > x && costL.outputMap.height > y)
                            {
                                Color c = costL.outputMap.GetPixel(x, y);
                                if (!IsColorMatch(c, costL.BackgroundColor)) obstacles[x, y] = true;
                            }
                        }
                    }
                }
            }
            
            // Clamp points
            startPoint.x = Mathf.Clamp(startPoint.x, 0, w - 1);
            startPoint.y = Mathf.Clamp(startPoint.y, 0, h - 1);
            endPoint.x = Mathf.Clamp(endPoint.x, 0, w - 1);
            endPoint.y = Mathf.Clamp(endPoint.y, 0, h - 1);
            
            // Find path
            var path = FindPath(startPoint, endPoint, obstacles, w, h);
            
            // Draw
            if (path != null)
            {
                foreach(var p in path)
                {
                    DrawPoint(map, p, layer.activeColor, w, h);
                }
                
                if (markEndpoints)
                {
                    DrawPoint(map, startPoint, startColor, w, h);
                    DrawPoint(map, endPoint, endColor, w, h);
                }
            }

            map.Apply();
        }
        
        private void DrawPoint(Texture2D map, Vector2Int center, Color c, int w, int h)
        {
             for(int dx = -pathWidth/2; dx <= pathWidth/2; dx++)
             {
                 for(int dy = -pathWidth/2; dy <= pathWidth/2; dy++)
                 {
                     int px = center.x + dx;
                     int py = center.y + dy;
                     if(px >=0 && px < w && py>=0 && py < h)
                     {
                         map.SetPixel(px, py, c);
                     }
                 }
             }
        }
        
        private List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, bool[,] walls, int w, int h)
        {
            var openSet = new List<Node>();
            var closedSet = new HashSet<Vector2Int>();
            
            openSet.Add(new Node(start, 0, Heuristic(start, end), null, Vector2Int.zero));
            
            int safety = 0;
            while(openSet.Count > 0 && safety < 10000)
            {
                safety++;
                openSet.Sort((a,b)=>a.f.CompareTo(b.f));
                Node current = openSet[0];
                openSet.RemoveAt(0);
                
                if (current.pos == end)
                {
                    List<Vector2Int> path = new List<Vector2Int>();
                    while(current != null) { path.Add(current.pos); current = current.parent; }
                    path.Reverse();
                    return path;
                }
                
                if (closedSet.Contains(current.pos)) continue;
                closedSet.Add(current.pos);
                
                Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach(var d in dirs)
                {
                    Vector2Int np = current.pos + d;
                    if (np.x < 0 || np.x >= w || np.y < 0 || np.y >= h) continue;
                    if (walls[np.x, np.y]) continue;
                    if (closedSet.Contains(np)) continue;
                    
                    float cost = 1;
                    if (current.dir != Vector2Int.zero && current.dir != d) cost += turnCost;
                    
                    openSet.Add(new Node(np, current.g + cost, Heuristic(np, end), current, d));
                }
            }
            return null; 
        }

        private float Heuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
        
        class Node
        {
            public Vector2Int pos;
            public float g, h;
            public float f => g + h;
            public Node parent;
            public Vector2Int dir;
            public Node(Vector2Int p, float g, float h, Node parent, Vector2Int d) 
            { pos = p; this.g = g; this.h = h; this.parent = parent; dir = d; }
        }

        private bool IsColorMatch(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f &&
                   Mathf.Abs(a.g - b.g) < 0.01f &&
                   Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }
}
