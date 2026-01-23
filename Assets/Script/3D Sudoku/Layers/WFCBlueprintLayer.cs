using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class WFCBlueprintLayer
    {
        public string layerName = "New Layer";
        public bool active = true; // Toggle
        public Color activeColor = Color.white;
        private readonly Color backgroundColor = Color.black;
        
        [Header("Output")]
        public Texture2D outputMap;
        [HideInInspector] public Color[] storedMapData; // SNAPSHOT

        [Header("Procedural Generation")]
        [SerializeReference] // CRITICAL for polymorphism
        public List<WFCModifier> modifiers = new List<WFCModifier>();

        public Color BackgroundColor => backgroundColor;

        public bool ValidateMap(int width, int height)
        {
            // If texture is missing but we have data, Reconstruct
            if (outputMap == null && storedMapData != null && storedMapData.Length == width * height)
            {
                outputMap = new Texture2D(width, height);
                outputMap.filterMode = FilterMode.Point;
                outputMap.wrapMode = TextureWrapMode.Clamp;
                outputMap.name = "Generated Map (Restored)";
                outputMap.hideFlags = HideFlags.DontSave;
                outputMap.SetPixels(storedMapData);
                outputMap.Apply();
                return true; 
            }
            // If texture exists and matches, all good. If size mismatch, we might need regen.
            if (outputMap != null && (outputMap.width != width || outputMap.height != height)) return false;
            
            return outputMap != null;
        }

        public void Generate(int width, int height, List<WFCBlueprintLayer> context)
        {
            // Initializes Texture
            if (outputMap == null || outputMap.width != width || outputMap.height != height)
            {
                outputMap = new Texture2D(width, height);
                outputMap.filterMode = FilterMode.Point;
                outputMap.wrapMode = TextureWrapMode.Clamp;
                outputMap.name = "Generated Map"; 
                // Don't save this runtime texture to disk automatically, prevents errors
                outputMap.hideFlags = HideFlags.DontSave; 
            }

            // Fill Background
            Color[] cols = new Color[width * height];
            for(int i=0; i<cols.Length; i++) cols[i] = backgroundColor;
            outputMap.SetPixels(cols);
            outputMap.Apply();

            // Run Modifiers
            foreach (var mod in modifiers)
            {
                if (mod != null && mod.active)
                {
                    mod.Apply(this, context);
                    // Pass dimensions? Modifiers read map.width/height, so they are auto-updated.
                }
            }

            // CACHE RESULT
            storedMapData = outputMap.GetPixels();

        }
    }
}
