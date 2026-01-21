using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class WFCBlueprintLayer
    {
        public string layerName = "New Layer";
        public bool active = true; // Toggle
        public Vector2Int size = new Vector2Int(20, 20);
        public Color activeColor = Color.white;
        private readonly Color backgroundColor = Color.black;
        
        [Header("Output")]
        public Texture2D outputMap;
        [HideInInspector] public Color[] storedMapData; // SNAPSHOT

        [Header("Procedural Generation")]
        [SerializeReference] // CRITICAL for polymorphism
        public List<WFCModifier> modifiers = new List<WFCModifier>();

        public Color BackgroundColor => backgroundColor;

        public bool ValidateMap()
        {
            // If texture is missing but we have data, Reconstruct
            if (outputMap == null && storedMapData != null && storedMapData.Length == size.x * size.y)
            {
                outputMap = new Texture2D(size.x, size.y);
                outputMap.filterMode = FilterMode.Point;
                outputMap.wrapMode = TextureWrapMode.Clamp;
                outputMap.name = "Generated Map (Restored)";
                outputMap.hideFlags = HideFlags.DontSave;
                outputMap.SetPixels(storedMapData);
                outputMap.Apply();
                return true; 
            }
            // If texture exists, all good
            return outputMap != null;
        }

        public void Generate(List<WFCBlueprintLayer> context)
        {
            // Initializes Texture
            if (outputMap == null || outputMap.width != size.x || outputMap.height != size.y)
            {
                outputMap = new Texture2D(size.x, size.y);
                outputMap.filterMode = FilterMode.Point;
                outputMap.wrapMode = TextureWrapMode.Clamp;
                outputMap.name = "Generated Map"; 
                // Don't save this runtime texture to disk automatically, prevents errors
                outputMap.hideFlags = HideFlags.DontSave; 
            }

            // Fill Background
            Color[] cols = new Color[size.x * size.y];
            for(int i=0; i<cols.Length; i++) cols[i] = backgroundColor;
            outputMap.SetPixels(cols);
            outputMap.Apply();

            // Run Modifiers
            foreach (var mod in modifiers)
            {
                if (mod != null && mod.active)
                {
                    mod.Apply(this, context);
                }
            }

            // CACHE RESULT
            storedMapData = outputMap.GetPixels();

        }
    }
}
