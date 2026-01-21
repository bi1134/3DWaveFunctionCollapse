using UnityEngine;
using UnityEditor;
using WFC_Sudoku;

[CustomPropertyDrawer(typeof(MapColorRule))]
public class MapColorRuleDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Using BeginProperty / EndProperty on the parent property means that
        // prefab override logic works on the entire property.
        EditorGUI.BeginProperty(position, label, property);

        // Draw label
        // position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // Don't indent
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        // Calculate rects
        float h = EditorGUIUtility.singleLineHeight;
        
        // Single Line Layout: [Check] [Color] [Module] [Layer] [Tolerance]
        // Actually, let's keep it compact or multi-line?
        // Let's do a compact single line if possible, or 2 lines. 
        // Default standard drawer was multi-line. Let's try to mimic standard but with a checkbox on top or left.
        
        Rect checkRect = new Rect(position.x, position.y, 20, h);
        Rect contentRect = new Rect(position.x + 20, position.y, position.width - 20, position.height);

        // Draw Checkbox
        SerializedProperty activeProp = property.FindPropertyRelative("active");
        if(activeProp != null) EditorGUI.PropertyField(checkRect, activeProp, GUIContent.none);

        // Draw the rest using PropertyField? 
        // No, we must manually draw children if we override.
        // Or we can just draw properties relative to contentRect.
        
        // Let's rely on standard PropertyField for children but shifted? 
        // No, PropertyDrawer replaces the whole thing.
        
        // Let's just draw specific fields.
        Rect line1 = new Rect(contentRect.x, contentRect.y, contentRect.width, h);
        
        // [Name] [Color]
        float w = line1.width;
        EditorGUI.PropertyField(new Rect(line1.x, line1.y, w * 0.4f, h), property.FindPropertyRelative("ruleName"), GUIContent.none);
        EditorGUI.PropertyField(new Rect(line1.x + w * 0.4f + 5, line1.y, w * 0.2f, h), property.FindPropertyRelative("sourceColor"), GUIContent.none);
        
        // [Module]
        EditorGUI.PropertyField(new Rect(line1.x + w * 0.6f + 10, line1.y, w * 0.35f - 10, h), property.FindPropertyRelative("modulePrefab"), GUIContent.none);

        // Line 2 (Layer, Tolerance)
        if (position.height > h * 1.5f)
        {
             Rect line2 = new Rect(contentRect.x, contentRect.y + h + 2, contentRect.width, h);
             EditorGUI.LabelField(new Rect(line2.x, line2.y, 40, h), "Layer:");
             EditorGUI.PropertyField(new Rect(line2.x + 45, line2.y, 30, h), property.FindPropertyRelative("targetLayer"), GUIContent.none);
             
             EditorGUI.LabelField(new Rect(line2.x + 85, line2.y, 35, h), "Tol:");
             EditorGUI.PropertyField(new Rect(line2.x + 120, line2.y, line2.width - 120, h), property.FindPropertyRelative("tolerance"), GUIContent.none);
        }

        EditorGUI.indentLevel = indent;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 2 + 6;
    }
}
