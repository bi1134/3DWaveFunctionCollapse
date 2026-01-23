using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class TowerDefensePathGenerator : WFCModifier, INeighborStitchable
    {
        // Replaced custom enum with global EdgeSide or kept for compatibility?
        // User already has serialized data using local EdgeSide.
        // To avoid data loss, we should keep local Enum but map it?
        // OR: Update INeighborStitchable to use the Class-Defined Enum? 
        // No, Interface needs common type.
        // Let's use WFC_Sudoku.EdgeSide in interface, and map it to local or replace local.
        // Replacing local is risky for existing prefabs.
        // Let's keep local and map it in the Interface implementation.
        
        public enum LocalEdgeSide { Left, Right, Top, Bottom }
        // Wait, previously I qualified it as TowerDefensePathGenerator.EdgeSide.
        // Let's alias it for now or just Map it.

        // Actually, if I change the type of the field, Unity serialization breaks.
        // So I must keep 'public enum EdgeSide' inside this class same as before.
        public enum EdgeSide { Left, Right, Top, Bottom } 

        [System.Serializable]
        public class PathPoint
        {
            public EdgeSide edge;
            [Range(0f, 1f)] public float position = 0.5f;
        }
        
        [Header("Seed Settings")]
        [Tooltip("Use a random seed every generation.")]
        public bool useRandomSeed = true;
        public int seed;
        
        [Header("Counts")]
        [Range(0, 5)] public int numberOfStarts = 1;
        [Range(0, 5)] public int numberOfEnds = 1;
        [Tooltip("If true, completely regenerates the point lists on execute.")]
        public bool generateRandomPoints = true;
        [Tooltip("Minimum distance (in cells) between any two Start/End points.")]
        [Range(1, 10)] public int minPointSeparation = 3;

        [Header("Manual Points")]
        public List<PathPoint> startPoints = new List<PathPoint>();
        public List<PathPoint> endPoints = new List<PathPoint>();
        
        [Header("Neighbor Stitching")]
        [Tooltip("The name of the layer to connect to.")]
        public string inputLayerName; // Used for Neighbor reference
        public enum StitchDirection { LeftOfNeighbor, RightOfNeighbor, AboveNeighbor, BelowNeighbor }
        public StitchDirection stitchDirection;
        public bool stitchEnabled = false;

        [Header("Path Complexity")]
        [Range(0.01f, 0.5f)] public float noiseScale = 0.1f;
        [Range(0f, 50f)] public float windingStrength = 10f;
        [Range(0.1f, 1f)] public float wallThreshold = 0.8f;
        [Range(0f, 2f)] public float heuristicWeight = 1.0f;
        
        [Header("U-Turn Logic")]
        [Tooltip("If Start/End on same edge, force path to go this % deep into the map.")]
        [Range(0.1f, 0.9f)] public float uTurnDepth = 0.75f;

        [Header("Constraint")]
        [Tooltip("The path must stay this many pixels away from the edge (except at start/end).")]
        [Range(0, 5)] public int pathPadding = 1;

        [Header("Visuals")]
        public int pathWidth = 1;
        public bool markEndpoints = true;
        public Color startColor = Color.green;
        public Color endColor = Color.red;

        [Header("Output")]
        [HideInInspector] public List<Vector2Int> generatedPath = new List<Vector2Int>();
        
        // Transient Buffer for WorldManager
        private List<PathPoint> externalStartPoints = new List<PathPoint>();
        private List<PathPoint> externalEndPoints = new List<PathPoint>();

        public TowerDefensePathGenerator() { name = "TD Path Generator"; }

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            int w = map.width;
            int h = map.height;
            
            if (useRandomSeed)
            {
                seed = Random.Range(1, 999999);
            }
            int currentSeed = seed;
            System.Random prng = new System.Random(currentSeed);
            Random.InitState(currentSeed); // Unity Random for some utils

            generatedPath.Clear();
            
            // 0. Cleanup Logic
            if (generateRandomPoints)
            {
                startPoints.Clear();
                endPoints.Clear();
            }
            
            // 0b. Restore External Points (Injected by WorldManager)
            if (externalStartPoints.Count > 0) startPoints.AddRange(externalStartPoints);
            if (externalEndPoints.Count > 0) endPoints.AddRange(externalEndPoints);
            
            // 1. Stitching First (Injects Anchors from Internal References)
            if (stitchEnabled && !string.IsNullOrEmpty(inputLayerName))
            {
                ApplyStitching(context, w, h);
            }
            
            // 2. Random Generation (Fills gaps)
            if (generateRandomPoints)
            {
                GenerateDistinctPoints(w, h, prng);
            }
            
            // Resolve actual coordinates
            List<Vector2Int> sCoords = startPoints.Select(p => GetEdgePoint(p.edge, p.position, w, h)).ToList();
            List<Vector2Int> eCoords = endPoints.Select(p => GetEdgePoint(p.edge, p.position, w, h)).ToList();

            // 2. Generate Cost Map
            float[,] costMap = new float[w, h];
            float offset = (float)prng.NextDouble() * 1000f; 
            
            // Mark ALL points as Obstacles initially (to prevent crossing them)
            // But we must check against this list carefully during pathfinding (allow Target, deny others)
            HashSet<Vector2Int> allPoints = new HashSet<Vector2Int>();
            allPoints.UnionWith(sCoords);
            allPoints.UnionWith(eCoords);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float val = Mathf.PerlinNoise(x * noiseScale + offset, y * noiseScale + offset);
                    
                    // Boundary/Padding Logic
                    // User wants path to spawn INSIDE the zone 18x18 (for 20x20).
                    // So x=0, x=w-1, y=0, y=h-1 should be high cost/walls
                    // BUT Start/End points are ON the edge, so they must be allowed.
                    
                    if (x < pathPadding || x >= w - pathPadding || y < pathPadding || y >= h - pathPadding)
                    {
                        // Check if this pixel is close to a Start/End point
                        // If it is, allow it (bridge). If not, block it.
                        // We allow the point itself and maybe 1 neighbor to be safe?
                        // Actually, just checking if it is IN sCoords/eCoords is enough if padding is 1.
                        // If padding > 1, we might block the entry.
                        // Assumption: Padding is usually 1.
                        
                        bool isSafe = false;
                        if (allPoints.Contains(new Vector2Int(x, y))) isSafe = true;
                        
                        if (!isSafe)
                        {
                            val += 10f; // Soft wall (high cost) or Hard wall?
                            // User said "path should not reach w-1", implying Hard Limit.
                            // Let's add a massive cost but keep it technically passable if desperate?
                            // Or just rely on Wall Threshold?
                            // Let's default to setting it very high so it acts like a wall.
                            val = 1.0f; // Max Perlin is 1.0. 
                            // Current logic: moveCost = 1 + val * strength.
                            // If val is 1, cost is 11.
                            // If we want it to be a WALL, we should ensure it exceeds wallThreshold.
                            // wallThreshold is 0.8 by default.
                            val = 2.0f; // Guaranteed Wall.
                        }
                    }
                    
                    costMap[x, y] = val;
                }
            }

            // 3. Tree Generation
            // Trunk: S[0] -> E[0]
            if (sCoords.Count > 0 && eCoords.Count > 0)
            {
                 // Create Trunk
                 Vector2Int p1 = sCoords[0];
                 Vector2Int p2 = eCoords[0];
                 
                 // Enable target in exclusion list for this specific search
                 List<Vector2Int> trunk = GenerateSmartPath(p1, p2, costMap, w, h, prng, allPoints);
                 generatedPath.AddRange(trunk);
                 
                 // Set of existing path cells for branching
                 HashSet<Vector2Int> treeCells = new HashSet<Vector2Int>(trunk);
                 
                 // Remaining Orphans
                 List<Vector2Int> orphans = new List<Vector2Int>();
                 for(int i=1; i<sCoords.Count; i++) orphans.Add(sCoords[i]);
                 for(int i=1; i<eCoords.Count; i++) orphans.Add(eCoords[i]);
                 
                 // Iteratively connect orphans to the tree
                 // To create a nice tree, we should connect each orphan to the NEAREST point on the current tree.
                 // We can do this efficiently by running Dijkstra started from ALL treeCells simultaneously.
                 
                 while(orphans.Count > 0)
                 {
                     // Optimization: If many orphans, running flood fill for ALL might be slow.
                     // But simpler is: Just iterate orphans, find shortest dist to tree, pick BEST orphan to connect (Prim's algorithm style).
                     // For now, let's just connect them order-wise.
                     
                     Vector2Int orphan = orphans[0];
                     orphans.RemoveAt(0);
                     
                     // Find path from Tree -> Orphan
                     List<Vector2Int> branch = ConnectToTree(treeCells, orphan, costMap, w, h, allPoints);
                     
                     // Add branch to tree
                     if (branch != null)
                     {
                         generatedPath.AddRange(branch);
                         treeCells.UnionWith(branch);
                     }
                 }
            }

            DrawPath(map, layer.activeColor, w, h);
            map.Apply();
        }

        private void ApplyStitching(List<WFCBlueprintLayer> context, int w, int h)
        {
            // Find Neighbor Layer
            var neighborLayer = context.FirstOrDefault(l => l.layerName == inputLayerName);
            if (neighborLayer == null) return;
            
            // Find Neighbor TD Generator (Assumes only one or first one)
            var neighborGen = neighborLayer.modifiers.OfType<TowerDefensePathGenerator>().FirstOrDefault();
            if (neighborGen == null) return; // Neighbor doesn't have path data
            
            // Determine Shared Edge
            // Example: If I am LEFT of Neighbor. My RIGHT edge touches Neighbor's LEFT edge.
            EdgeSide myEdge = EdgeSide.Right;
            EdgeSide neighborEdge = EdgeSide.Left;
            
            switch (stitchDirection)
            {
                case StitchDirection.LeftOfNeighbor: // I am Left. Matches: My Right <-> Their Left
                    myEdge = EdgeSide.Right; neighborEdge = EdgeSide.Left; break;
                case StitchDirection.RightOfNeighbor: // I am Right. Matches: My Left <-> Their Right
                    myEdge = EdgeSide.Left; neighborEdge = EdgeSide.Right; break;
                case StitchDirection.AboveNeighbor: // I am Top. Matches: My Bottom <-> Their Top
                    myEdge = EdgeSide.Bottom; neighborEdge = EdgeSide.Top; break;
                case StitchDirection.BelowNeighbor: // I am Bottom. Matches: My Top <-> Their Bottom
                    myEdge = EdgeSide.Top; neighborEdge = EdgeSide.Bottom; break;
            }
            
            // Remove any existing points of mine on 'myEdge' (to avoid conflict/redundancy)
            startPoints.RemoveAll(p => p.edge == myEdge);
            endPoints.RemoveAll(p => p.edge == myEdge);
            
            // Transfer Points Logic:
            // Neighbor START at 'neighborEdge' --> My END at 'myEdge'.
            // Neighbor END at 'neighborEdge' --> My START at 'myEdge'.
            
            // 1. neighbor.Starts -> my.Ends
            foreach(var ns in neighborGen.startPoints)
            {
                if (ns.edge == neighborEdge)
                {
                    PathPoint newEnd = new PathPoint { edge = myEdge, position = ns.position };
                    endPoints.Add(newEnd);
                }
            }
            
            // 2. neighbor.Ends -> my.Starts
            foreach(var ne in neighborGen.endPoints)
            {
                if (ne.edge == neighborEdge)
                {
                    PathPoint newStart = new PathPoint { edge = myEdge, position = ne.position };
                    startPoints.Add(newStart);
                }
            }
            
            // Note: If random generation caused duplication, we might want to deduplicate, but lists allow multiple.
            // These points are now "Anchors".
        }

        private void GenerateDistinctPoints(int w, int h, System.Random prng)
        {
            // Update: Do NOT clear. We append to existing (e.g. Stitched) points.
            
            List<Vector2Int> allLocs = new List<Vector2Int>();
            // Pre-fill allLocs with existing points to ensure separation
            foreach(var p in startPoints) allLocs.Add(GetEdgePoint(p.edge, p.position, w, h));
            foreach(var p in endPoints) allLocs.Add(GetEdgePoint(p.edge, p.position, w, h));
            
            // Determine Counts
            int targetS = (numberOfStarts == 0) ? prng.Next(1, 4) : numberOfStarts;
            int targetE = (numberOfEnds == 0) ? prng.Next(1, 4) : numberOfEnds;
            
            // Fill Starts
            // Only add if we need more (and if manual count > current count, or if random logic dictates)
            // If numberOfStarts was 0 (auto), and we already have some (from stitching), do we add more?
            // "if start or end point = 0 then randomize 1-3 ... if we have 2 start point right... theres 0 end point then randomize 1-3 end point"
            // This implies: If Auto(0), and count is 0, add 1-3. 
            // If Auto(0) and count > 0 (stitching), we consider it "Satisfied" (don't add redundant random starts).
            
            int neededS = 0;
            if (numberOfStarts == 0) 
            {
                // Auto Mode:
                // Rule 1: If we have NO points at all, add targetS (1-4).
                if (startPoints.Count == 0) neededS = targetS;
                else
                {
                    // Rule 2: Connectivity Bias
                    // If we have points (from stitching), checks if they are all clustered on one side?
                    // Example: We have input on Left. We probably want output on Right/Top/Bottom.
                    // But 'startPoints' and 'endPoints' are directional.
                    // If we have 1 Start (from Stitching Left), we probably want 1 End elsewhere.
                    // The logic below for Ends handles the "Exit" creation.
                    // So for Starts (Entries), we usually accept whatever neighbors gave us.
                    neededS = 0;
                }
            }
            else
            {
                neededS = Mathf.Max(0, numberOfStarts - startPoints.Count);
            }
            
            for(int i=0; i<neededS; i++)
            {
                // Try up to 10 times to find a valid spot
                for(int attempt=0; attempt<10; attempt++)
                {
                    EdgeSide edge = (EdgeSide)prng.Next(0, 4);
                    // Bias?
                    
                    float pos = (float)prng.NextDouble();
                    Vector2Int pt = GetEdgePoint(edge, pos, w, h);
                    
                    bool tooClose = false;
                    foreach(var existing in allLocs)
                    {
                        if (Vector2Int.Distance(existing, pt) < minPointSeparation) { tooClose = true; break; }
                    }
                    
                    if (!tooClose)
                    {
                        PathPoint p = new PathPoint{ edge = edge, position = pos };
                        startPoints.Add(p);
                        allLocs.Add(pt);
                        break; // Success
                    }
                }
            }
            
            // Collect used Start edges to forbid them for Ends (Updated with ALL starts)
            HashSet<EdgeSide> startEdges = new HashSet<EdgeSide>(startPoints.Select(p => p.edge));
            
            // Fill Ends (Exits)
            int neededE = 0;
            if (numberOfEnds == 0)
            {
                // Auto Mode:
                // If we have 0 Ends, definitely add some.
                if (endPoints.Count == 0) 
                {
                    neededE = targetE;
                }
                else
                {
                    // If we have some Ends (maybe from stitching on Right?), do we need more?
                    // Generally, if we have at least 1 End, we are valid.
                    // BUT, to prevent "Groups don't connect", we might want to encourage expanding to empty sides?
                    // Let's stick to: If count > 0, we are good.
                    neededE = 0;
                }

                // CONNECTIVITY FIX:
                // If we have Starts but NO Ends, the path dies here.
                // If we have 0 Ends, we forced neededE = targetE above.
                // But what if we have 1 End (e.g. Stitched from Right)? 
                // That means we have an Entry from Right (since Neighbor Start -> My End).
                // So traffic comes FROM Right.
                // We need a START to go TO somewhere. 
                
                // Wait, terminology:
                // Start = Spawn = Entry. Path goes FROM Start.
                // End = Base = Exit. Path goes TO End.
                
                // Stitching: Neighbor Exit (End) -> My Entry (Start).
                // Stitching: Neighbor Entry (Start) -> My Exit (End).
                
                // If we have 1 Start (Entry from Left), we need at least 1 End (Exit to somewhere).
                if (startPoints.Count > 0 && endPoints.Count == 0 && neededE == 0)
                {
                    neededE = 1; // Force at least 1 exit if we have entries
                }
                // Conversely, if we have Ends (Exits) but no Starts (Entries), we need a Start.
                if (endPoints.Count > 0 && startPoints.Count == 0 && neededS == 0)
                {
                     // Add a Start
                     for(int attempt=0; attempt<10; attempt++)
                     {
                        EdgeSide edge = (EdgeSide)prng.Next(0, 4);
                        float pos = (float)prng.NextDouble();
                        Vector2Int pt = GetEdgePoint(edge, pos, w, h);
                        
                        bool tooClose = false;
                        foreach(var existing in allLocs)
                        {
                            if (Vector2Int.Distance(existing, pt) < minPointSeparation) { tooClose = true; break; }
                        }
                        
                        if (!tooClose)
                        {
                            PathPoint p = new PathPoint{ edge = edge, position = pos };
                            startPoints.Add(p);
                            allLocs.Add(pt);
                            break; 
                        }
                     }
                }
            }
            else
            {
                neededE = Mathf.Max(0, numberOfEnds - endPoints.Count);
            }

            for(int i=0; i<neededE; i++) TryAddDistinctPoint(endPoints, w, h, prng, allLocs, startEdges);
        }
        
        private bool TryAddDistinctPoint(List<PathPoint> list, int w, int h, System.Random prng, List<Vector2Int> allLocs, HashSet<EdgeSide> forbiddenEdges)
        {
            int safety = 0;
            while(safety < 50)
            {
                safety++;
                PathPoint p = new PathPoint();
                
                // Edge Selection
                if (forbiddenEdges != null && forbiddenEdges.Count < 4)
                {
                    List<EdgeSide> allowed = new List<EdgeSide> { EdgeSide.Left, EdgeSide.Right, EdgeSide.Top, EdgeSide.Bottom };
                    allowed.RemoveAll(e => forbiddenEdges.Contains(e));
                    if (allowed.Count == 0) p.edge = (EdgeSide)prng.Next(0, 4);
                    else p.edge = allowed[prng.Next(allowed.Count)];
                }
                else
                {
                    p.edge = (EdgeSide)prng.Next(0, 4);
                }
                
                p.position = (float)prng.NextDouble();
                Vector2Int pos = GetEdgePoint(p.edge, p.position, w, h);
                
                // Check separation
                bool ok = true;
                foreach(var existing in allLocs)
                {
                    if (Vector2Int.Distance(pos, existing) < minPointSeparation)
                    {
                        ok = false; break;
                    }
                }
                
                if (ok)
                {
                    list.Add(p);
                    allLocs.Add(pos);
                    return true;
                }
            }
            return false;
        }
        
        // Complex logic for Trunk (includes U-Turn)
        private List<Vector2Int> GenerateSmartPath(Vector2Int start, Vector2Int end, float[,] costMap, int w, int h, System.Random prng, HashSet<Vector2Int> blockedPoints)
        {
             List<Vector2Int> result = new List<Vector2Int>();
             List<Vector2Int> waypoints = new List<Vector2Int>();
             waypoints.Add(start);
             
             // U-Turn check logic requires EdgeSide info which we lost by converting to Vector2Int.
             // We can infer or check distance.
             // Simpler: Just rely on simple pathfinding. U-Turn logic for trunk is nice but not critical for branches.
             // However, user LIKES the u-turn.
             // Let's re-detect edge side for U-Turn logic roughly.
             
             // If start/end on same edge (dist < w/2 + h/2 usually implies proximity, but same edge is strict)
             // Let's infer edge.
             // Actually, pass explicit edge info? No, messy for tree logic.
             // Let's skip explicit U-turn for tree mode unless needed.
             // Or check if they are close on boundary?
             
             // Just use standard pathfind with obstacles check.
             
             waypoints.Add(end);
             
             for(int i=0; i < waypoints.Count - 1; i++)
             {
                Vector2Int p1 = waypoints[i];
                Vector2Int p2 = waypoints[i+1];
                
                List<Vector2Int> segment = FindPath(p1, p2, costMap, w, h, blockedPoints);
                if (segment == null || segment.Count == 0) segment = FindPath(p1, p2, costMap, w, h, blockedPoints, true); // Strict -> Relaxed Walls
                if (segment == null || segment.Count == 0) segment = GetDirectLine(p1, p2);
                
                result.AddRange(segment);
             }
             return result;
        }

        // --- INeighborStitchable Implementation ---

        public void ClearStitching()
        {
            ClearExternalPoints();
        }



        public class TDStitchData
        {
            public List<PathPoint> starts = new List<PathPoint>();
            public List<PathPoint> ends = new List<PathPoint>();
        }

        public void InjectEdgeData(object data, WFC_Sudoku.EdgeSide side)
        {
            var packet = data as TDStitchData;
            if (packet == null) return;
            
            // INVERSION LOGIC HAPPENS HERE?
            // Or does GetEdgeData return "My Starts"?
            // Let's assume GetEdgeData returns "What is on my edge".
            // So packet.starts = Neighbor's Starts.
            // packet.ends = Neighbor's Ends.
            // Logic: Neighbor Starts -> My Ends. Neighbor Ends -> My Starts.
            
            EdgeSide localSide = MapEdgeSide(side);
            
            List<PathPoint> newStarts = new List<PathPoint>();
            List<PathPoint> newEnds = new List<PathPoint>();
            
            // Map End -> Start
            foreach(var p in packet.ends)
            {
                 newStarts.Add(new PathPoint { edge = localSide, position = p.position });
            }
            // Map Start -> End
            foreach(var p in packet.starts)
            {
                 newEnds.Add(new PathPoint { edge = localSide, position = p.position });
            }
            
            InjectPoints(newStarts, newEnds, localSide);
        }
        
        public object GetEdgeData(WFC_Sudoku.EdgeSide side)
        {
            EdgeSide localSide = MapEdgeSide(side);
            TDStitchData data = new TDStitchData();
            
            foreach(var p in startPoints) if (p.edge == localSide) data.starts.Add(p);
            foreach(var p in endPoints) if (p.edge == localSide) data.ends.Add(p);
            
            return data;
        }

        private EdgeSide MapEdgeSide(WFC_Sudoku.EdgeSide s)
        {
            return (EdgeSide)s; // Assumes Enum int values match (Left=0, Right=1...)
            // 0=Left, 1=Right, 2=Top, 3=Bottom. Matches standard.
        }
        
        // Old Methods kept for internal compatibility or refactor?
        // Let's refactor InjectPoints to be private or used by InjectEdgeData.

        // Public API for WorldManager (Legacy - to be replaced by Interface call)
        public void ClearExternalPoints()
        {
            externalStartPoints.Clear();
            externalEndPoints.Clear();
        }

        public void InjectPoints(List<PathPoint> starts, List<PathPoint> ends, EdgeSide edgeToClear)
        {
            // Remove points from pending buffers that might conflict
            externalStartPoints.RemoveAll(p => p.edge == edgeToClear);
            externalEndPoints.RemoveAll(p => p.edge == edgeToClear);
            
            if(starts != null) externalStartPoints.AddRange(starts);
            if(ends != null) externalEndPoints.AddRange(ends);
        }

        private List<Vector2Int> ConnectToTree(HashSet<Vector2Int> treeCells, Vector2Int target, float[,] costs, int w, int h, HashSet<Vector2Int> blockedPoints)
        {
             // Attempt 1: Strict (Respect Walls)
             List<Vector2Int> path = RunConnectSearch(treeCells, target, costs, w, h, blockedPoints, false);
             
             // Attempt 2: Relaxed (Ignore Walls)
             if (path == null)
             {
                 path = RunConnectSearch(treeCells, target, costs, w, h, blockedPoints, true);
             }
             
             // Attempt 3: Direct Line (Tunnel)
             if (path == null)
             {
                 // Find nearest tree cell
                 Vector2Int nearest = Vector2Int.zero;
                 float minDist = float.MaxValue;
                 foreach(var c in treeCells)
                 {
                     float d = Vector2Int.Distance(c, target);
                     if (d < minDist) { minDist = d; nearest = c; }
                 }
                 path = GetDirectLine(nearest, target);
             }
             
             return path;
        }

        private List<Vector2Int> RunConnectSearch(HashSet<Vector2Int> treeCells, Vector2Int target, float[,] costs, int w, int h, HashSet<Vector2Int> blockedPoints, bool ignoreWalls)
        {
             // Dijkstra from Tree -> Target
             var openSet = new List<Node>();
             var closedSet = new HashSet<Vector2Int>();
             
             // Add all tree cells as start nodes (cost 0)
             foreach(var cell in treeCells)
             {
                 openSet.Add(new Node(cell, 0, GetHeuristic(cell, target) * heuristicWeight, null));
                 closedSet.Add(cell);
             }
             
             int safety = 0;
             while(openSet.Count > 0 && safety < 10000) // Lower safety for flood fill parts
             {
                 safety++;
                 openSet.Sort((a,b)=>a.f.CompareTo(b.f));
                 Node current = openSet[0];
                 openSet.RemoveAt(0);
                 
                 if (current.pos == target) return RetracePath(current);
                 
                 Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                 Shuffle(dirs);
                 
                 foreach(var d in dirs)
                 {
                     Vector2Int neighborPos = current.pos + d;
                     if (neighborPos.x < 0 || neighborPos.x >= w || neighborPos.y < 0 || neighborPos.y >= h) continue;
                     
                     if (closedSet.Contains(neighborPos)) continue;
                     
                     if (blockedPoints.Contains(neighborPos) && neighborPos != target) continue;
                     
                     float noiseVal = costs[neighborPos.x, neighborPos.y];
                     // Strict check
                     if (!ignoreWalls && noiseVal > wallThreshold) continue;
                     
                     float moveCost = 1f + (noiseVal * windingStrength);
                     float newG = current.g + moveCost;
                     float hVal = GetHeuristic(neighborPos, target) * heuristicWeight;
                     
                     openSet.Add(new Node(neighborPos, newG, hVal, current));
                     closedSet.Add(neighborPos);
                 }
             }
             return null;
        }

        private Vector2Int GetEdgePoint(EdgeSide edge, float t, int w, int h)
        {
            t = Mathf.Clamp01(t);
            switch (edge)
            {
                case EdgeSide.Left: return new Vector2Int(0, Mathf.RoundToInt(t * (h - 1)));
                case EdgeSide.Right: return new Vector2Int(w - 1, Mathf.RoundToInt(t * (h - 1)));
                case EdgeSide.Top: return new Vector2Int(Mathf.RoundToInt(t * (w - 1)), h - 1);
                case EdgeSide.Bottom: return new Vector2Int(Mathf.RoundToInt(t * (w - 1)), 0);
            }
            return Vector2Int.zero;
        }

        private List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, float[,] costs, int w, int h, HashSet<Vector2Int> blockedPoints, bool ignoreWalls = false)
        {
            var openSet = new List<Node>();
            var closedSet = new HashSet<Vector2Int>();
            
            Node startNode = new Node(start, 0, GetHeuristic(start, end) * heuristicWeight, null);
            openSet.Add(startNode);
            
            int safety = 0;
            while(openSet.Count > 0 && safety < 50000) 
            {
                safety++;
                openSet.Sort((a,b)=>a.f.CompareTo(b.f));
                Node current = openSet[0];
                openSet.RemoveAt(0);
                
                if (current.pos == end) return RetracePath(current);
                
                if (closedSet.Contains(current.pos)) continue;
                closedSet.Add(current.pos);
                
                Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                Shuffle(dirs);

                foreach(var d in dirs)
                {
                    Vector2Int neighborPos = current.pos + d;
                    
                    if (neighborPos.x < 0 || neighborPos.x >= w || neighborPos.y < 0 || neighborPos.y >= h) continue;
                    if (closedSet.Contains(neighborPos)) continue;
                    
                    // Blocked Points check
                    if (blockedPoints.Contains(neighborPos) && neighborPos != end) continue;
                    
                    float noiseVal = costs[neighborPos.x, neighborPos.y];
                    if (!ignoreWalls && noiseVal > wallThreshold) continue; 
                    
                    float moveCost = 1f + (noiseVal * windingStrength);
                    float newG = current.g + moveCost;
                    float hVal = GetHeuristic(neighborPos, end) * heuristicWeight;
                    
                    openSet.Add(new Node(neighborPos, newG, hVal, current));
                }
            }
            return null;
        }
        
        private void Shuffle(Vector2Int[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                Vector2Int temp = array[i];
                int randomIndex = Random.Range(i, array.Length);
                array[i] = array[randomIndex];
                array[randomIndex] = temp;
            }
        }

        private List<Vector2Int> RetracePath(Node endNode)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Node current = endNode;
            while(current != null)
            {
                path.Add(current.pos);
                current = current.parent;
            }
            path.Reverse();
            return path;
        }

        private float GetHeuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        class Node
        {
            public Vector2Int pos;
            public float g, h;
            public float f => g + h;
            public Node parent;
            public Node(Vector2Int p, float g, float h, Node parent) { pos=p; this.g=g; this.h=h; this.parent=parent; }
        }
        
        private List<Vector2Int> GetDirectLine(Vector2Int a, Vector2Int b)
        {
            List<Vector2Int> line = new List<Vector2Int>();
            int x0 = a.x; int y0 = a.y;
            int x1 = b.x; int y1 = b.y;
            int dx = System.Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -System.Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2;
            while (true) {
                line.Add(new Vector2Int(x0, y0));
                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
            return line;
        }

        private void DrawPath(Texture2D map, Color c, int w, int h)
        {
             foreach(var p in generatedPath)
             {
                 DrawPoint(map, p, c, w, h);
             }
             if (markEndpoints)
             {
                 foreach(var sp in startPoints) DrawPoint(map, GetEdgePoint(sp.edge, sp.position, w, h), startColor, w, h);
                 foreach(var ep in endPoints) DrawPoint(map, GetEdgePoint(ep.edge, ep.position, w, h), endColor, w, h);
             }
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
    }
}
