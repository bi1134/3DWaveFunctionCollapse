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

        public static void ResolveVisualForCell(WFCCell cell, Func<Vector3Int, WFCCell> getNeighbor, Vector3Int gridSize)
        {
            if (cell.possibleModules.Count != 1) return; // Not collapsed/initialized properly
            WFCModule archetype = cell.possibleModules[0];

            if (archetype == null) return;
            if (archetype.variants == null || archetype.variants.Count == 0) return;

            // 1. Gather all Valid Variants
            List<WFCVariant> validVariants = new List<WFCVariant>();
            foreach (var variant in archetype.variants)
            {
                if (CheckVariantRules(cell, variant, getNeighbor, gridSize))
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

        private static bool CheckVariantRules(WFCCell cell, WFCVariant variant, Func<Vector3Int, WFCCell> getNeighbor, Vector3Int gridSize)
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
                SocketID neighborSocket = GetSocketAt(targetPos, rule.direction * -1, getNeighbor, gridSize); 

                bool match = (neighborSocket == rule.requiredSocketID);
                if (rule.mustNotMatch) match = !match;

                if (!match) return false;
            }
            return true;
        }

        private static SocketID GetSocketAt(Vector3Int pos, Vector3Int facingUs, Func<Vector3Int, WFCCell> getNeighbor, Vector3Int gridSize)
        {
            // Out of bounds = Air
            // If gridSize is Zero (infinite mode?), we might skip this check, but for now strict check:
            if (gridSize != Vector3Int.zero && (pos.x < 0 || pos.x >= gridSize.x ||
                pos.y < 0 || pos.y >= gridSize.y ||
                pos.z < 0 || pos.z >= gridSize.z))
            {
                return SocketID.Air;
            }

            WFCCell neighbor = getNeighbor(pos);
            
            // If neighbor exists but hasn't been placed/collapsed yet -> Treat as Air?
            // Or if it's explicitly "Empty" module?
            // For now: null or uncollapsed = Air
            if (neighbor == null || !neighbor.collapsed || neighbor.possibleModules.Count != 1) return SocketID.Air;

            WFCModule mod = neighbor.possibleModules[0];
            if (mod == null) return SocketID.Air;

            // Return the socket on the face pointing towards us
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
