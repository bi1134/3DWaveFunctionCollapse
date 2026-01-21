using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class WFCBuildLayer
    {
        public bool active = true; // Toggle
        public string layerName = "Main Layer";
        
        [Header("Source Data")]
        [Tooltip("Name of the embedded Blueprint Layer to use.")]
        public string blueprintName;
        
        [Tooltip("Shift the application of this layer up by Y")]
        public int yOffset = 0;



        [System.Serializable]
        public struct TilePresetItem
        {
            public WFCTilePreset preset;
            [Range(0f, 1f)] public float weight;
        }

        [Header("Tile Presets")]
        public List<TilePresetItem> presets = new List<TilePresetItem>();
        
        public WFCModule GetModuleForColor(Color pixelColor, out int targetLayer)
        {
             targetLayer = 0;
             
             // Gather all candidates from all weighted presets
             List<WFCModule> candidates = new List<WFCModule>();
             List<float> candidateWeights = new List<float>();
             
             foreach(var item in presets)
             {
                 if (item.preset == null) continue;
                 int tLayer;
                 WFCModule m = item.preset.GetModuleForColor(pixelColor, out tLayer);
                 if (m != null)
                 {
                     targetLayer = tLayer; 
                     candidates.Add(m);
                     candidateWeights.Add(item.weight);
                 }
             }
             
             if (candidates.Count == 0) return null;
             if (candidates.Count == 1) return candidates[0];
             
             // Weighted Random Selection
             float totalWeight = 0;
             foreach(float w in candidateWeights) totalWeight += w;
             
             if (totalWeight <= 0) return candidates[Random.Range(0, candidates.Count)];
             
             float randomPoint = Random.value * totalWeight;
             float currentWeight = 0;
             
             for(int i = 0; i < candidates.Count; i++)
             {
                 currentWeight += candidateWeights[i];
                 if (randomPoint <= currentWeight) return candidates[i];
             }
             
             return candidates[candidates.Count - 1]; // Fallback
        }

        private bool IsColorMatch(Color a, Color b, float tolerance)
        {
            return Mathf.Abs(a.r - b.r) <= tolerance &&
                   Mathf.Abs(a.g - b.g) <= tolerance &&
                   Mathf.Abs(a.b - b.b) <= tolerance;
        }
    }
}
