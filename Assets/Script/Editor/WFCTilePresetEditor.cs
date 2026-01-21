using UnityEngine;
using UnityEditor;

namespace WFC_Sudoku
{
    [CustomEditor(typeof(WFCTilePreset))]
    public class WFCTilePresetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            WFCTilePreset registry = (WFCTilePreset)target;

            GUILayout.Space(10);
            
            if (GUILayout.Button("Auto-Generate Neighbors (Sockets)", GUILayout.Height(30)))
            {
                registry.GenerateNeighbors();
            }
            
            GUILayout.Label("Generates neighbor rules for all modules in this registry based on their socket IDs.", EditorStyles.miniLabel);
        }
    }
}
