using UnityEngine;
using UnityEditor;

namespace WFC_Sudoku
{
    [CustomEditor(typeof(WFCTilePreset))]
    public class WFCTilePresetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty modulesProp = serializedObject.FindProperty("modules");
            SerializedProperty mapRulesProp = serializedObject.FindProperty("mapRules");
            SerializedProperty dualGridProp = serializedObject.FindProperty("dualGridProfile");

            // Modules
            EditorGUILayout.PropertyField(modulesProp, true);
            EditorGUILayout.PropertyField(mapRulesProp, true);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Dual Grid Visualization (6-Tuple)", EditorStyles.boldLabel);
            
            // Draw Custom Preview
            DrawDualGridProfile(dualGridProp);
            
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Auto-Generate Neighbors (Sockets)", GUILayout.Height(30)))
            {
                ((WFCTilePreset)target).GenerateNeighbors();
            }
            GUILayout.Label("Generates neighbor rules for all modules in this registry based on their socket IDs.", EditorStyles.miniLabel);
            
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDualGridProfile(SerializedProperty profile)
        {
            EditorGUILayout.BeginVertical("box");
            
            DrawProfileItem(profile, "fullModels", "Full Tile", 1, 1, 1, 1);
            DrawProfileItem(profile, "lShapeModels", "L-Shape", 1, 1, 0, 1);
            DrawProfileItem(profile, "lineModels", "Line Shape", 1, 1, 0, 0);
            DrawProfileItem(profile, "stitchModels", "Stitch (Opposite)", 1, 0, 0, 1);
            DrawProfileItem(profile, "cornerModels", "Corner", 1, 0, 0, 0);
            DrawProfileItem(profile, "emptyModels", "Empty", 0, 0, 0, 0);

            EditorGUILayout.EndVertical();
        }

        private void DrawProfileItem(SerializedProperty root, string propName, string label, int tl, int tr, int bl, int br)
        {
            SerializedProperty prop = root.FindPropertyRelative(propName);
            
            EditorGUILayout.BeginHorizontal();
            
            // Draw Mini Grid (20x20)
            Rect iconRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24));
            // Center the icon vertically relative to the first line
            Rect centeredIcon = new Rect(iconRect.x, iconRect.y + 2, 24, 24); 
            DrawMiniGrid(centeredIcon, tl, tr, bl, br);
            
            GUILayout.Space(10);

            // Property (List)
            // Use Label as the field label, includeChildren=true for list support
            EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
            
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        private void DrawMiniGrid(Rect r, int tl, int tr, int bl, int br)
        {
            float w = r.width / 2;
            float h = r.height / 2;

            // Colors
            Color on = Color.white;
            Color off = Color.black;

            EditorGUI.DrawRect(new Rect(r.x, r.y, w, h), (tl == 1) ? on : off);          // TL
            EditorGUI.DrawRect(new Rect(r.x + w, r.y, w, h), (tr == 1) ? on : off);      // TR
            EditorGUI.DrawRect(new Rect(r.x, r.y + h, w, h), (bl == 1) ? on : off);      // BL
            EditorGUI.DrawRect(new Rect(r.x + w, r.y + h, w, h), (br == 1) ? on : off);  // BR
        }
    }
}
