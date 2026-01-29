using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TerrainGenerator
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
             
             // Pass 1: Instantiate All Chunks
             for(int x = -viewDistance; x <= viewDistance; x++)
             {
                 for(int y = -viewDistance; y <= viewDistance; y++)
                 {
                     Vector2Int coord = centerChunk + new Vector2Int(x, y);
                     CreateChunkInitialized(coord);
                 }
             }

             // Pass 2: Trigger Generation
             foreach(var kvp in loadedChunks)
             {
                 if (kvp.Value != null)
                 {
                     kvp.Value.Generate();
                 }
             }
        }
        
        // Renamed from CreateChunk to separate instantiation from generation
        private void CreateChunkInitialized(Vector2Int coord)
        {
             if (loadedChunks.ContainsKey(coord)) return;
             
             // 1. Instantiate
             float xPos = coord.x * chunkSize.x * worldScale;
             float zPos = coord.y * chunkSize.z * worldScale;
             Vector3 pos = new Vector3(xPos, 0, zPos); 
             
             WFCBuilder newChunk = Instantiate(chunkPrefab, pos, Quaternion.identity, this.transform);
             newChunk.name = $"Chunk_{coord.x}_{coord.y}";
             newChunk.gridSize = chunkSize;
             newChunk.autoResizeGridToMap = false; 
             newChunk.generateOnStart = false; 
             newChunk.solver.globalLookup = GetGlobalCell;
             newChunk.solver.globalCellLookup = GetGlobalCellAny; 
             newChunk.solver.globalChunkExists = IsChunkLoaded; 
             newChunk.solver.chunkCoordinate = coord;
             newChunk.solver.worldScale = worldScale;
             
             // Clear shared blueprint textures
             foreach (var bp in newChunk.definedBlueprints)
                 bp.outputMap = null;
             
             // 2. Stitching Logic
             foreach(var bp in newChunk.definedBlueprints)
             {
                 var stitchable = bp.modifiers.FirstOrDefault(m => m is INeighborStitchable) as INeighborStitchable;
                 if (stitchable == null) continue;
                 
                 stitchable.ClearStitching();
                 
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.left,   EdgeSide.Left,  EdgeSide.Right);
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.right,  EdgeSide.Right, EdgeSide.Left);
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.up,     EdgeSide.Top,   EdgeSide.Bottom);
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.down,   EdgeSide.Bottom,EdgeSide.Top);
             }
             
             // NO Generate() here.
             
             loadedChunks.Add(coord, newChunk);
        }
        
        private void RefreshNeighborBorders(Vector2Int coord)
        {
             // Cardinals
             // Notify Neighbor to refresh THEIR edge facing ME
             // And Refresh MY edge facing THEM (Handshake)
             
             if (!loadedChunks.TryGetValue(coord, out WFCBuilder me)) return;
             if (me == null || me.solver == null) return;

             // Left Neighbor (My Left)
             NotifyNeighbor(coord + Vector2Int.left, Vector3Int.right);
             me.solver.RefreshVisualsOnEdge(Vector3Int.left);

             // Right Neighbor (My Right)
             NotifyNeighbor(coord + Vector2Int.right, Vector3Int.left);
             me.solver.RefreshVisualsOnEdge(Vector3Int.right);

             // Up Neighbor (My Back - Z+)
             NotifyNeighbor(coord + Vector2Int.up, Vector3Int.back); // Neighbor's Back Edge
             me.solver.RefreshVisualsOnEdge(Vector3Int.forward); // My Forward Edge (Z+)

             // Down Neighbor (My Forward - Z-)
             NotifyNeighbor(coord + Vector2Int.down, Vector3Int.forward); // Neighbor's Forward Edge
             me.solver.RefreshVisualsOnEdge(Vector3Int.back); // My Back Edge (Z-)
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
                
                return builder.solver.GetVisualCellAt(new Vector3Int(localX, localY, localZ));
            }
            return null;
        }
        
        /// <summary>
        /// Get any cell (WFCCell or WFCCellData) at a global grid position.
        /// Used for dual grid cross-chunk lookups.
        /// </summary>
        public IWFCCell GetGlobalCellAny(Vector3Int globalGridPos)
        {
            int sx = chunkSize.x;
            int sz = chunkSize.z;
            
            if (sx == 0 || sz == 0) return null;

            int chunkX = Mathf.FloorToInt((float)globalGridPos.x / sx);
            int chunkY = Mathf.FloorToInt((float)globalGridPos.z / sz);
            
            Vector2Int chunkCoord = new Vector2Int(chunkX, chunkY);
            
            if (loadedChunks.TryGetValue(chunkCoord, out WFCBuilder builder))
            {
                if (builder == null || builder.solver == null) return null;
                
                int localX = (globalGridPos.x % sx + sx) % sx;
                int localZ = (globalGridPos.z % sz + sz) % sz;
                int localY = globalGridPos.y;
                
                return builder.solver.GetCellAt(new Vector3Int(localX, localY, localZ));
            }
            return null;
        }

        public bool IsChunkLoaded(Vector3Int globalGridPos)
        {
            int sx = chunkSize.x;
            int sz = chunkSize.z;
            if (sx == 0 || sz == 0) return false;

            int chunkX = Mathf.FloorToInt((float)globalGridPos.x / sx);
            int chunkY = Mathf.FloorToInt((float)globalGridPos.z / sz);
            
            return loadedChunks.ContainsKey(new Vector2Int(chunkX, chunkY));
        }
    }
}

