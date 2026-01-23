using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WFC_Sudoku
{
    public class WFCWorldManager : MonoBehaviour
    {
        [Header("Settings")]
        public Vector3Int chunkSize = new Vector3Int(20, 10, 20);
        public float worldScale = 4.0f; // Multiplier for cell size (e.g. 4 if cells are 4x4)
        public WFCBuilder chunkPrefab; // The Prefab that contains the WFCBuilder logic
        public int viewDistance = 1;
        
        [Header("State")]
        public Vector2Int centerChunk;
        
        // Storage
        private Dictionary<Vector2Int, WFCBuilder> loadedChunks = new Dictionary<Vector2Int, WFCBuilder>();
        
        // This class is responsible for Spawning Chunks and linking them.
        
        private void Start()
        {
            GenerateWorld();
        }

        private void OnEnable()
        {
            WFCEvent.OnChunkGenerated += HandleChunkGenerated;
        }

        private void OnDisable()
        {
            WFCEvent.OnChunkGenerated -= HandleChunkGenerated;
        }

        private void HandleChunkGenerated(WFCBuilder chunk)
        {
            // Reverse lookup coord from chunk name or position
            // Name format: "Chunk_X_Y"
            
            string[] parts = chunk.name.Split('_');
            if (parts.Length == 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
            {
                RefreshNeighborBorders(new Vector2Int(x, y));
            }
        }

        [ContextMenu("Generate World")]
        public void GenerateWorld()
        {
             // Clear Old
             foreach(var kvp in loadedChunks)
             {
                 if(kvp.Value != null) DestroyImmediate(kvp.Value.gameObject);
             }
             loadedChunks.Clear();
             
             // Generate Center + Neighbors
             for(int x = -viewDistance; x <= viewDistance; x++)
             {
                 for(int y = -viewDistance; y <= viewDistance; y++)
                 {
                     Vector2Int coord = centerChunk + new Vector2Int(x, y);
                     CreateChunk(coord);
                 }
             }
        }
        
        private void CreateChunk(Vector2Int coord)
        {
             if (loadedChunks.ContainsKey(coord)) return;
             
             // 1. Instantiate
             // Fix: Scale by worldScale (Cell Size)
             float xPos = coord.x * chunkSize.x * worldScale;
             float zPos = coord.y * chunkSize.z * worldScale;
             Vector3 pos = new Vector3(xPos, 0, zPos); 
             
             WFCBuilder newChunk = Instantiate(chunkPrefab, pos, Quaternion.identity, this.transform);
             newChunk.name = $"Chunk_{coord.x}_{coord.y}";
             newChunk.gridSize = chunkSize;
             newChunk.autoResizeGridToMap = false; // Trust WorldManager settings
             newChunk.autoResizeGridToMap = false; // Trust WorldManager settings
             newChunk.solver.globalLookup = GetGlobalCell;
             newChunk.solver.chunkCoordinate = coord;
             newChunk.solver.worldScale = worldScale;
             
             // newChunk.Generate(); // Force internal cleanup first? or Start() will handle it. 
             // Note: Start() happens later. We want to Prepare then Build.
             // We can manually call newChunk.Generate() AT THE END.
             
             // 2. Stitching Logic
             // We iterate all Blueprint Layers in the new chunk.
             // If they are TD Gen, we look for neighbors.
             
             // 2. Stitching Logic
             foreach(var bp in newChunk.definedBlueprints)
             {
                 var stitchable = bp.modifiers.FirstOrDefault(m => m is INeighborStitchable) as INeighborStitchable;
                 if (stitchable == null) continue;
                 
                 stitchable.ClearStitching();
                 
                 // Check 4 Neighbors
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.left,   EdgeSide.Left,  EdgeSide.Right);
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.right,  EdgeSide.Right, EdgeSide.Left);
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.up,     EdgeSide.Top,   EdgeSide.Bottom);
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.down,   EdgeSide.Bottom,EdgeSide.Top);
             }
             
             // 3. Execute Generation
             
             newChunk.Generate(); 
             
             loadedChunks.Add(coord, newChunk);
        }
        
        private void RefreshNeighborBorders(Vector2Int coord)
        {
             // Cardinals
             NotifyNeighbor(coord + Vector2Int.left, Vector3Int.right);
             NotifyNeighbor(coord + Vector2Int.right, Vector3Int.left);
             NotifyNeighbor(coord + Vector2Int.up, Vector3Int.back); 
             NotifyNeighbor(coord + Vector2Int.down, Vector3Int.forward);
             
             // Diagonals (Just in case rules check corners? Though RefreshVisualsOnEdge only iterates borders...)
             // Wait. If I update my visual, do I trigger my diagonal neighbor?
             // My corner cell (19,19) might affect neighbor's (0,0).
             // But 'edgeToRefresh' is a single direction.
             // If I send "Refresh Right Edge" to a Diagonal neighbor? No.
             
             // If I am at (0,0). Diagonal neighbor is (1,1).
             // My corner touches their corner.
             // They don't share an edge.
             // So RefreshNeighborBorders(Cardinal) is correct for Edges.
        }
        
        private void NotifyNeighbor(Vector2Int neighborCoord, Vector3Int edgeToRefresh)
        {
            if (loadedChunks.TryGetValue(neighborCoord, out WFCBuilder neighbor))
            {
                if (neighbor != null && neighbor.solver != null)
                {
                    neighbor.solver.RefreshVisualsOnEdge(edgeToRefresh);
                }
            }
        }
        
        private void StitchWithNeighbor(INeighborStitchable myGen, string layerName, Vector2Int neighborCoord, EdgeSide myEdge, EdgeSide neighborEdge)
        {
            if (!loadedChunks.TryGetValue(neighborCoord, out WFCBuilder neighbor)) return;
            
            // Find matching blueprint
            var neighborBP = neighbor.GetBlueprint(layerName);
            if (neighborBP == null) return;
            
            // Find Neighbor Stitchable
            var neighborGen = neighborBP.modifiers.FirstOrDefault(m => m is INeighborStitchable) as INeighborStitchable;
            if (neighborGen == null) return;
            
            // Generic Data Packet Exchange
            object neighborData = neighborGen.GetEdgeData(neighborEdge);
            if (neighborData != null)
            {
                 Debug.Log($"[WFCWorld] Stitching {layerName}: Connecting {myEdge} with {neighborCoord}'s {neighborEdge}.");
                 myGen.InjectEdgeData(neighborData, myEdge);
            }
        }

        // --- Global Query for Visualizer ---
        
        // This allows a cell in Chunk A to query a neighbor cell in Chunk B using GLOBAL GRID COORDINATES
        public WFCCell GetGlobalCell(Vector3Int globalGridPos)
        {
            // Grid Size per chunk
            int sx = chunkSize.x;
            int sz = chunkSize.z; // y is ignored/not chunked in this logic (vertical stacking? assumed single layer of chunks for now)
            
            if (sx == 0 || sz == 0) return null;

            // 1. Calculate Chunk Coordinate
            // Using Floor Division for standard tiling
            int chunkX = Mathf.FloorToInt((float)globalGridPos.x / sx);
            int chunkY = Mathf.FloorToInt((float)globalGridPos.z / sz);
            
            Vector2Int chunkCoord = new Vector2Int(chunkX, chunkY);
            
            if (loadedChunks.TryGetValue(chunkCoord, out WFCBuilder builder))
            {
                if (builder == null || builder.solver == null) return null;
                
                // 2. Calculate Local Index
                // Proper Modulo for negative numbers: (x % m + m) % m
                int localX = (globalGridPos.x % sx + sx) % sx;
                int localZ = (globalGridPos.z % sz + sz) % sz;
                int localY = globalGridPos.y; // Height is local = global in this 2D chunking setup
                
                return builder.solver.GetCellAt(new Vector3Int(localX, localY, localZ));
            }
            return null;
        }
    }
}

