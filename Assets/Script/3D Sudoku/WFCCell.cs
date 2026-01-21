using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    public class WFCCell : MonoBehaviour
    {
        public Vector3Int gridPosition;
        public bool collapsed = false;
        public List<WFCModule> possibleModules = new List<WFCModule>();
        
        // References for visualization
        private GameObject spawnedObject;
        public int totalGridHeight = 1;

        public float heightRatio = 0;


        // Entropy: Number of remaining possibilities
        public int Entropy => possibleModules.Count;

        public void Initialize(List<WFCModule> allModules, Vector3Int pos, int gridHeight)
        {
            // Visual Cleanup (for reuse)
            if (spawnedObject != null)
            {
                 if (Application.isPlaying) Destroy(spawnedObject);
                 else DestroyImmediate(spawnedObject);
                 spawnedObject = null;
            }
            currentVisualPrefab = null;

            possibleModules = new List<WFCModule>(allModules);
            gridPosition = pos;
            totalGridHeight = gridHeight;
            heightRatio = (float)pos.y / ((float)gridHeight - 1);
            collapsed = false;
        }

        public void Collapse()
        {
            if (possibleModules.Count == 0)
            {
                Debug.LogError($"Cell at {gridPosition} has 0 entropy! Collapse failed.");
                return;
            }

            WFCModule selected = SelectModuleWeighted();
            
            possibleModules.Clear();
            possibleModules.Add(selected);
            collapsed = true;

            // Visualize
            if (spawnedObject != null)
            {
                 if (Application.isPlaying) Destroy(spawnedObject);
                 else DestroyImmediate(spawnedObject);
            }
            spawnedObject = Instantiate(selected.gameObject, transform.position, selected.transform.rotation, transform);
            
            // Cleanup Name (Remove (Clone))
            spawnedObject.name = selected.name; // Template name is already clean (e.g. Wall_Rot90)
        }

        private GameObject currentVisualPrefab;

        public void SpawnVisual(GameObject prefab)
        {
            // Optimization: Skip if we are strictly spawning the same visual
            if (currentVisualPrefab == prefab) return;

            if (spawnedObject != null)
            {
                if (Application.isPlaying) Destroy(spawnedObject);
                else DestroyImmediate(spawnedObject);
            }
            
            // USE PREFAB ROTATION relative to Cell
            // If Cell is rotated (during WFC), we combine. If Cell is Identity, we just use prefab rotation.
            Quaternion finalRot = transform.rotation * prefab.transform.rotation;
            
            spawnedObject = Instantiate(prefab, transform.position, finalRot, transform); 
            spawnedObject.name = prefab.name + "_Variant";
            
            currentVisualPrefab = prefab;
        }

        public bool Constrain(List<WFCModule> allowedModules)
        {
            if (collapsed) return false;

            int originalCount = possibleModules.Count;
            
            // Intersect current possibilities with allowed possibilities from neighbor
            // Logic: Keep modules that are present in BOTH lists
            // Note: In real WFC, allowedModules is usually derived from neighbor's valid sockets.
            // But here we might receive the "Allowed List" directly.
            
            // Actually, for efficiency, we usually iterate our modules and check if they are compatible with ANY of the neighbor's current potential modules.
            // But let's assume the Solver passes us a "Valid Set" to intersect.
            
            // Implementation Detail: The Solver usually calculates the "Allowed" list based on Neighbor Sockets
            // Then passes that "Allowed" list here.
            
            // This Intersect is expensive O(N*M). Optimization: HashSets.
            // For small module counts (Total < 100), simple loop is okay.
            
            var allowedSet = new HashSet<WFCModule>(allowedModules);
            possibleModules.RemoveAll(m => !allowedSet.Contains(m));

            return possibleModules.Count < originalCount;
        }

        private WFCModule SelectModuleWeighted()
        {
            // Calculate weights
            List<float> weights = new List<float>();
            float totalWeight = 0;

            foreach(var mod in possibleModules)
            {
                float w = GetWeight(mod);
                weights.Add(w);
                totalWeight += w;
            }

            // Random pick
            float randomValue = Random.Range(0, totalWeight);
            float currentSum = 0;

            for(int i=0; i < possibleModules.Count; i++)
            {
                currentSum += weights[i];
                if(randomValue <= currentSum)
                {
                    return possibleModules[i];
                }
            }
            
            return possibleModules[0];
        }

        private float GetWeight(WFCModule module)
        {
            float w = 1f;

            switch (module.role)
            {
                case WFCModule.ModuleRole.Base:
                    // Plate/Bottom: ONLY at Y=0
                    return (gridPosition.y == 0) ? 100f : 0f;

                case WFCModule.ModuleRole.Roof:
                    // Roof: NEVER at Y=0
                    if (gridPosition.y == 0) return 0f;
                    
                    // High Weight at Top
                    if (gridPosition.y == totalGridHeight - 1) return 100000f;

                    // Scaling weight in between
                    return 1f + (heightRatio * 50f);

                case WFCModule.ModuleRole.Body:
                    // Walls/Toppings:
                    // If at Max Height, we generally DON'T want walls (unless they are self-capped?)
                    // User complained "Wall at top". So we kill the weight at Top.
                    if (gridPosition.y == totalGridHeight - 1) return 0f; 
                    
                    // Look-Ahead Logic:
                    // If this Body supports a Roof, boost it as we go up.
                    if (SupportsRoof(module))
                    {
                        return 1f + (heightRatio * 20f);
                    }
                    return 1f;

                case WFCModule.ModuleRole.Air:
                    // Air is valid everywhere.
                    // Maybe slight preference at top to allow "Ending early"
                    if (gridPosition.y == totalGridHeight - 1) return 50000f; // Compete with Roofs
                    return 1f * module.spawnWeight;
            }

            return w * module.spawnWeight;
        }

        private bool SupportsRoof(WFCModule module)
        {
            if (module.topNeighbors == null) return false;
            foreach(var neighbor in module.topNeighbors)
            {
                if (neighbor != null && neighbor.role == WFCModule.ModuleRole.Roof)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
