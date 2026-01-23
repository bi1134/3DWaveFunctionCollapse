using UnityEngine;
using System.Collections.Generic;
using System;

namespace WFC_Sudoku
{
    public static class WFCVisualizer
    {
        public static void UpdateVisualsAround(WFCCell centerCell, Func<Vector3Int, WFCCell> getNeighbor, Vector3Int gridSize)
        {
            // Update Center
            ResolveVisualForCell(centerCell, getNeighbor, gridSize);

            // Update Neighbors
            Vector3Int[] directions = {
                Vector3Int.right, Vector3Int.left,
                Vector3Int.up, Vector3Int.down,
                Vector3Int.forward, Vector3Int.back
            };

            foreach(var dir in directions)
            {
                var nPos = centerCell.gridPosition + dir;
                var neighbor = getNeighbor(nPos);
                
                // Only update if valid and "ready" (collapsed or placed)
                if (neighbor != null && neighbor.collapsed)
                {
                    ResolveVisualForCell(neighbor, getNeighbor, gridSize);
                }
            }
        }

        public static void ResolveVisualForCell(WFCCell cell, Func<Vector3Int, WFCCell> getNeighbor, Vector3Int gridSize, Func<Vector3Int, WFCCell> globalLookup = null, Vector2Int chunkCoord = default(Vector2Int))
        {
            if (cell.possibleModules.Count != 1) return; // Not collapsed/initialized properly
            WFCModule archetype = cell.possibleModules[0];

            if (archetype == null) return;
            if (archetype.variants == null || archetype.variants.Count == 0) return;

            // 1. Gather all Valid Variants
            List<WFCVariant> validVariants = new List<WFCVariant>();
            foreach (var variant in archetype.variants)
            {
                if (CheckVariantRules(cell, variant, getNeighbor, gridSize, globalLookup, chunkCoord))
                {
                    validVariants.Add(variant);
                }
            }

            // 2. Fallback if none valid
            if (validVariants.Count == 0) return;

            // 3. Weighted Random Selection
            WFCVariant chosen = PickWeightedVariant(validVariants, cell.gridPosition);
            if (chosen != null)
            {
                cell.SpawnVisual(chosen.prefab);
            }
        }

        private static bool CheckVariantRules(WFCCell cell, WFCVariant variant, Func<Vector3Int, WFCCell> getNeighbor, Vector3Int gridSize, Func<Vector3Int, WFCCell> globalLookup, Vector2Int chunkCoord)
        {
            // Height Check
            if (variant.limitHeight)
            {
                int y = cell.gridPosition.y;
                if (y < variant.minHeight || y > variant.maxHeight) return false;
            }

            // Neighbor Rules
            foreach (var rule in variant.rules)
            {
                Vector3Int targetPos = cell.gridPosition + rule.direction;
                SocketID neighborSocket = GetSocketAt(cell, rule.direction, getNeighbor, gridSize, globalLookup, chunkCoord); 

                bool match = (neighborSocket == rule.requiredSocketID);
                if (rule.mustNotMatch) match = !match;

                if (!match) return false;
            }
            return true;
        }

        private static SocketID GetSocketAt(WFCCell originCell, Vector3Int direction, Func<Vector3Int, WFCCell> getNeighbor, Vector3Int gridSize, Func<Vector3Int, WFCCell> globalLookup, Vector2Int chunkCoord)
        {
            Vector3Int targetPos = originCell.gridPosition + direction;
            
            bool outOfBounds = false;
            if (gridSize != Vector3Int.zero && (targetPos.x < 0 || targetPos.x >= gridSize.x ||
                targetPos.y < 0 || targetPos.y >= gridSize.y ||
                targetPos.z < 0 || targetPos.z >= gridSize.z))
            {
                outOfBounds = true;
            }

            WFCCell neighbor = null;
            if (!outOfBounds)
            {
                 neighbor = getNeighbor(targetPos);
            }
            else if (globalLookup != null)
            {
                // INTEGER MATH LOOKUP
                // GlobalPos = (ChunkCoord * ChunkSize) + LocalPos + Direction
                // We assume Standard Tiling where Chunks are size of GridSize.
                
                int globX = (chunkCoord.x * gridSize.x) + originCell.gridPosition.x + direction.x;
                int globY = originCell.gridPosition.y + direction.y; // Y is usually shared/not chunked vertically here
                int globZ = (chunkCoord.y * gridSize.z) + originCell.gridPosition.z + direction.z; // chunkCoord.y is global Z in 2D grid
                
                neighbor = globalLookup(new Vector3Int(globX, globY, globZ));
            }

            if (neighbor == null && outOfBounds && globalLookup == null) return SocketID.Air;
            if (neighbor == null || !neighbor.collapsed || neighbor.possibleModules.Count != 1) return SocketID.Air;

            WFCModule mod = neighbor.possibleModules[0];
            if (mod == null) return SocketID.Air;

            Vector3Int facingUs = direction * -1;

            if (facingUs == Vector3Int.right) return mod.rightSocket;
            if (facingUs == Vector3Int.left) return mod.leftSocket;
            if (facingUs == Vector3Int.up) return mod.topSocket;
            if (facingUs == Vector3Int.down) return mod.bottomSocket;
            if (facingUs == Vector3Int.forward) return mod.frontSocket;
            if (facingUs == Vector3Int.back) return mod.backSocket;

            return SocketID.Air;
        }

        private static WFCVariant PickWeightedVariant(List<WFCVariant> variants, Vector3Int pos)
        {
            float totalWeight = 0;
            foreach(var v in variants) totalWeight += v.spawnWeight;

            // STABLE RANDOM: Use position to seed the random state
            System.Random pseudoRandom = new System.Random(pos.GetHashCode()); 
            double rVal = pseudoRandom.NextDouble() * totalWeight;
            float r = (float)rVal;

            float current = 0;
            foreach(var v in variants)
            {
                current += v.spawnWeight;
                if (r <= current) return v;
            }
            return variants[0];
        }
    }
}
