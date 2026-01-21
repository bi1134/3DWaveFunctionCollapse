using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class MapColorRule
    {
        public bool active = true; // TOGGLE
        public string ruleName; 
        public Color sourceColor;
        [Tooltip("Module to force at this location.")]
        public WFCModule modulePrefab;
        [Tooltip("The Y-Layer to apply this module to (usually 0 for ground).")]
        public int targetLayer;
        [Range(0f, 1f)]
        public float tolerance; // Color matching tolerance
    }

    [CreateAssetMenu(menuName = "WFC/Map Profile")]
    public class WFCMapProfile : ScriptableObject
    {
        [Tooltip("The texture input. Pixels map 1:1 to Grid cells (X, Z).")]
        public Texture2D sourceTexture;
        
        public List<MapColorRule> rules = new List<MapColorRule>();

        public WFCModule GetModuleForColor(Color pixelColor, out int layerIndex)
        {
            layerIndex = 0;
            foreach (var rule in rules)
            {
                if (!rule.active) continue;
                if (IsColorMatch(pixelColor, rule.sourceColor, rule.tolerance))
                {
                    layerIndex = rule.targetLayer;
                    return rule.modulePrefab;
                }
            }
            return null;
        }

        private bool IsColorMatch(Color a, Color b, float tolerance)
        {
            return Mathf.Abs(a.r - b.r) <= tolerance &&
                   Mathf.Abs(a.g - b.g) <= tolerance &&
                   Mathf.Abs(a.b - b.b) <= tolerance;
        }
    }
}
