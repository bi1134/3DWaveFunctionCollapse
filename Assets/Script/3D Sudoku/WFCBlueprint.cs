using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [CreateAssetMenu(menuName = "WFC/Blueprint")]
    public class WFCBlueprint : ScriptableObject
    {
        public Vector3Int size = new Vector3Int(3, 3, 3);
        
        // Linearized 3D array: index = x + y * size.x + z * size.x * size.y
        [HideInInspector] // We'll probably want a custom editor later, but for now simple list is ok or we can just expose it safely
        public List<WFCModule> modules = new List<WFCModule>();

        public WFCModule GetModule(int x, int y, int z)
        {
            if (x < 0 || x >= size.x || y < 0 || y >= size.y || z < 0 || z >= size.z) return null;
            
            int index = GetIndex(x, y, z);
            if (index < 0 || index >= modules.Count) return null;
            
            return modules[index];
        }

        public void SetModule(int x, int y, int z, WFCModule module)
        {
             // Ensure list is big enough
            int requiredSize = size.x * size.y * size.z;
            while (modules.Count < requiredSize) modules.Add(null);
            
            if (x < 0 || x >= size.x || y < 0 || y >= size.y || z < 0 || z >= size.z) return;
            
            int index = GetIndex(x, y, z);
            modules[index] = module;
        }

        private int GetIndex(int x, int y, int z)
        {
            return x + (y * size.x) + (z * size.x * size.y);
        }

        public void Resize(Vector3Int newSize)
        {
             // Naive resize - creates a new list and tries to copy over (optional, for later)
             // For now, simple initialization
             size = newSize;
             modules.Clear();
             int count = size.x * size.y * size.z;
             for(int i=0; i<count; i++) modules.Add(null);
        }
        
        private void OnValidate()
        {
            // Ensure list matches size in Editor
            int requiredCount = size.x * size.y * size.z;
            if (modules.Count != requiredCount)
            {
                // Simple resize if mismatch (might lose data if shrinking, but safe for init)
                while(modules.Count < requiredCount) modules.Add(null);
                if(modules.Count > requiredCount) modules.RemoveRange(requiredCount, modules.Count - requiredCount);
            }
        }
    }
}
