using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public abstract class WFCModifier
    {
        public bool active = true; // TOGGLE
        [HideInInspector] public bool expanded = true;
        [HideInInspector] public string name; // Display name for the list

        public WFCModifier() { name = GetType().Name; }

        /// <summary>
        /// Apply this modifier to the given texture.
        /// </summary>
        /// <param name="map">The texture to modify</param>
        /// <param name="context">List of other layers (for reference)</param>
        public abstract void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context);
    }
}
