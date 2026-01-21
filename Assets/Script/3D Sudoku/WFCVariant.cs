using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class WFCVariant
    {
        public string variantName;
        public GameObject prefab;
        
        [Tooltip("Relative weight for random selection (Default 1.0). Higher = More likely.")]
        public float spawnWeight = 1.0f;

        [Header("Height Restrictions")]
        public bool limitHeight = false;
        public int minHeight = 0;
        public int maxHeight = 0;

        [Tooltip("All rules must be met for this variant to be chosen.")]
        public List<VariantRule> rules;
    }

    [System.Serializable]
    public class VariantRule
    {
        [Tooltip("Direction to check (e.g. 1,0,0 is Right)")]
        public Vector3Int direction;

        [Tooltip("The socket ID required on the neighbor's connecting face.")]
        public SocketID requiredSocketID;

        [Tooltip("If true, this rule passes ONLY if the neighbor does NOT match the socket.")]
        public bool mustNotMatch;
    }
}
