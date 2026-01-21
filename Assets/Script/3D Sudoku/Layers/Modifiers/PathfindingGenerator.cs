using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class PathfindingGenerator : WFCModifier
    {
        public enum HeuristicType { Euclidean, Manhattan, Dijkstra }
        
        [Header("Path Settings")]
        public Vector2Int startPoint = Vector2Int.zero;
        public Vector2Int endPoint = new Vector2Int(19, 19);
        public bool randomPoints = false;
        public int seed;
        
        [Tooltip("Force path to visit random intermediate points to make it longer.")]
        [Range(0, 10)] public int waypoints = 0; 
        
        [Header("Visuals")]
        public int pathWidth = 1;

        [Header("Algorithm Tuning")]
        public HeuristicType heuristic = HeuristicType.Manhattan;
        [Tooltip("Cost for making a 90-degree turn. Higher values make paths straighter (L-haped).")]
        public float turnCost = 0.5f; 
        [Tooltip("Weight of the heuristic. > 1 Makes it 'Greedy' (Zig-zags), 1 is Optimal.")]
        [Range(0.5f, 5f)] public float heuristicWeight = 1f;
        
        [Header("Cost Map & Noise")]
        public string costLayerName; 
        [Tooltip("Adds Perlin noise to movement cost to create winding paths.")]
        [Range(0f, 10f)] public float noiseCost = 0f;
        public float noiseScale = 0.1f;
        public Vector2 noiseOffset; 

        public PathfindingGenerator() { }

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            int w = map.width;
            int h = map.height;
            
            if (randomPoints || seed != 0) 
            {
                Random.InitState(seed);
                startPoint = new Vector2Int(Random.Range(0, w), Random.Range(0, h));
                endPoint = new Vector2Int(Random.Range(0, w), Random.Range(0, h));
                noiseOffset = new Vector2(Random.Range(-1000f, 1000f), Random.Range(-1000f, 1000f));
            }
            
            // Generate Waypoints
            List<Vector2Int> points = new List<Vector2Int>();
            points.Add(startPoint);
            for(int i=0; i<waypoints; i++)
            {
                points.Add(new Vector2Int(Random.Range(0, w), Random.Range(0, h)));
            }
            points.Add(endPoint);

            // Get Cost Map
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

            // Pathfind Segment by Segment
            List<Vector2Int> totalPath = new List<Vector2Int>();
            for(int i=0; i<points.Count-1; i++)
            {
                var segment = FindPath(points[i], points[i+1], obstacles, w, h);
                if (segment != null)
                {
                    totalPath.AddRange(segment);
                }
            }
            
            // Draw
            foreach(var p in totalPath)
            {
                    for(int dx = -pathWidth/2; dx <= pathWidth/2; dx++)
                    {
                        for(int dy = -pathWidth/2; dy <= pathWidth/2; dy++)
                        {
                            int px = p.x + dx;
                            int py = p.y + dy;
                            if(px >=0 && px < w && py>=0 && py < h)
                            {
                                map.SetPixel(px, py, layer.activeColor);
                            }
                        }
                    }
            }
            map.Apply();
        }
        
        private List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, bool[,] walls, int w, int h)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            
            var openSet = new List<Node>();
            var closedSet = new HashSet<Vector2Int>();
            
            Node startNode = new Node(start, 0, GetHeuristic(start, end) * heuristicWeight, null, Vector2Int.zero);
            openSet.Add(startNode);
            
            int safety = 0;
            while(openSet.Count > 0 && safety < 10000)
            {
                safety++;
                openSet.Sort((a,b)=>a.f.CompareTo(b.f));
                Node current = openSet[0];
                openSet.RemoveAt(0);
                
                if (current.pos == end)
                {
                    while(current != null)
                    {
                        path.Add(current.pos);
                        current = current.parent;
                    }
                    path.Reverse(); // Correct order
                    return path;
                }
                
                closedSet.Add(current.pos);
                
                Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach(var d in dirs)
                {
                    Vector2Int neighborPos = current.pos + d;
                    
                    if (neighborPos.x < 0 || neighborPos.x >= w || neighborPos.y < 0 || neighborPos.y >= h) continue;
                    if (walls[neighborPos.x, neighborPos.y]) continue;
                    if (closedSet.Contains(neighborPos)) continue;
                    
                    float moveCost = 1;
                    
                    // Turn Cost
                    if (current.direction != Vector2Int.zero && current.direction != d)
                    {
                        moveCost += turnCost;
                    }

                    // Noise Cost
                    if (noiseCost > 0)
                    {
                        float pX = (float)neighborPos.x * noiseScale + noiseOffset.x;
                        float pY = (float)neighborPos.y * noiseScale + noiseOffset.y;
                        float nVal = Mathf.PerlinNoise(pX, pY); 
                        moveCost += nVal * noiseCost;
                    }

                    float newG = current.g + moveCost;
                    Node neighborNode = openSet.Find(n => n.pos == neighborPos);
                    
                    if (neighborNode == null)
                    {
                        neighborNode = new Node(neighborPos, newG, GetHeuristic(neighborPos, end) * heuristicWeight, current, d);
                        openSet.Add(neighborNode);
                    }
                    else if (newG < neighborNode.g)
                    {
                        neighborNode.g = newG;
                        neighborNode.parent = current;
                        neighborNode.direction = d; 
                    }
                }
            }
            return null; 
        }

        private float GetHeuristic(Vector2Int a, Vector2Int b)
        {
            if (heuristic == HeuristicType.Manhattan)
                return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
            if (heuristic == HeuristicType.Dijkstra)
                return 0; // Pure cost-based
            return Vector2Int.Distance(a, b); // Euclidean
        }
        
        class Node
        {
            public Vector2Int pos;
            public float g; 
            public float h; 
            public float f => g + h;
            public Node parent;
            public Vector2Int direction; 
            public Node(Vector2Int p, float g, float h, Node parent, Vector2Int dir) 
            { 
                this.pos = p; this.g = g; this.h = h; this.parent = parent; this.direction = dir;
            }
        }

        private bool IsColorMatch(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f &&
                   Mathf.Abs(a.g - b.g) < 0.01f &&
                   Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }

}
 
