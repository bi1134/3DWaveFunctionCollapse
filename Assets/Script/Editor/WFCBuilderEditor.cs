using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;

namespace WFC_Sudoku
{
    [CustomEditor(typeof(WFCBuilder))]
    public class WFCBuilderEditor : Editor
    {
        private SerializedProperty definedBlueprintsProp;
        private SerializedProperty buildLayersProp;
        private ReorderableList buildLayersList;

        private void OnEnable()
        {
            modifierLists.Clear(); // Clear cache
            definedBlueprintsProp = serializedObject.FindProperty("definedBlueprints");
            buildLayersProp = serializedObject.FindProperty("buildLayers");

            // Setup Build Layers List
            buildLayersList = new ReorderableList(serializedObject, buildLayersProp, true, true, true, true);
            
            buildLayersList.drawHeaderCallback = (Rect rect) => 
            {
                EditorGUI.LabelField(rect, "Build Layers (Stack)");
            };

            buildLayersList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => 
            {
                SerializedProperty element = buildLayersList.serializedProperty.GetArrayElementAtIndex(index);
                SerializedProperty nameProp = element.FindPropertyRelative("layerName");
                SerializedProperty bpNameProp = element.FindPropertyRelative("blueprintName");
                SerializedProperty yProp = element.FindPropertyRelative("yOffset");
                // rules removed

                rect.y += 2;
                float height = EditorGUIUtility.singleLineHeight;
                
                // Foldout & Name
                // Foldout & Name
                Rect checkRect = new Rect(rect.x, rect.y, 20, height);
                SerializedProperty activeProp = element.FindPropertyRelative("active");
                if (activeProp != null) EditorGUI.PropertyField(checkRect, activeProp, GUIContent.none);
                
                Rect foldoutRect = new Rect(rect.x + 25, rect.y, rect.width - 25, height);
                element.isExpanded = EditorGUI.Foldout(foldoutRect, element.isExpanded, string.IsNullOrEmpty(nameProp.stringValue) ? "New Layer" : nameProp.stringValue);
                
                if (element.isExpanded)
                {
                    rect.y += height + 2;
                    EditorGUI.indentLevel++;

                    // Name
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, height), nameProp);
                    rect.y += height + 2;
                    
                    // Blueprint Name Dropdown
                    WFCBuilder builder = (WFCBuilder)target;
                    List<string> options = builder.definedBlueprints.Select(b => b.layerName).ToList();
                    options.Insert(0, "None"); 
                    
                    int currentIndex = 0;
                    if (!string.IsNullOrEmpty(bpNameProp.stringValue))
                    {
                        int found = options.IndexOf(bpNameProp.stringValue);
                        if (found > 0) currentIndex = found;
                    }

                    int newIndex = EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width, height), "Blueprint Source", currentIndex, options.ToArray());
                    if (newIndex > 0) bpNameProp.stringValue = options[newIndex];
                    else bpNameProp.stringValue = "";

                    rect.y += height + 2;
                    
                    // Y Offset
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, height), yProp);
                    rect.y += height + 5;

                    // Tile Presets (Weighted)
                    SerializedProperty presetsProp = element.FindPropertyRelative("presets");
                    
                    // Simple property field for the list handles ReorderableList inside ReorderableList poorly usually,
                    // but since PropertyField(list, true) works decently for simple internal logic...
                    // Let's force it to expand
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, height), presetsProp, true); 
                    
                    // We need to calculate height properly but for now let's trust DoLayout or check height
                    
                    rect.y += height + 5;
                    EditorGUI.indentLevel--;
                }
            };

            buildLayersList.elementHeightCallback = (int index) => 
            {
                SerializedProperty element = buildLayersList.serializedProperty.GetArrayElementAtIndex(index);
                float h = EditorGUIUtility.singleLineHeight + 6;
                if (element.isExpanded)
                {
                    float presetsHeight = EditorGUI.GetPropertyHeight(element.FindPropertyRelative("presets"), true);
                    h += (EditorGUIUtility.singleLineHeight + 2) * 3 + 5 + presetsHeight; 
                }
                return h;
            };
        }

        public override void OnInspectorGUI()
        {
            WFCBuilder builder = (WFCBuilder)target;
            serializedObject.Update();

            // --- HEADER ---
            EditorGUILayout.LabelField("WFC Builder Control", EditorStyles.boldLabel);
            
            // Global Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("EXECUTE BLUEPRINTS", GUILayout.Height(30)))
            {
                builder.ExecuteAllBlueprints();
                EditorUtility.SetDirty(builder);
            }
            string buildButtonText = (builder.solver != null && builder.solver.cells != null && builder.solver.cells.Count > 0) ? "UPDATE BUILD" : "EXECUTE BUILD";
            if (GUILayout.Button(buildButtonText, GUILayout.Height(30)))
            {
                builder.Generate(); // Replaces 'GenerateBuildLayersInEditor' + 'InitializeGrid'
            }
            GUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("1. Global Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("unityGrid"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gridSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cellAlignment"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cellPrefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoResizeGridToMap"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("strictLayerHeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultEmptyModule"));

            EditorGUILayout.Space(10);
            
            // Blueprints and Layers are below
            // EditorGUILayout.PropertyField(serializedObject.FindProperty("maxRetries"));
            // EditorGUILayout.PropertyField(serializedObject.FindProperty("fixedBlueprints"), true); 

            EditorGUILayout.Space(20);

            // --- TAB 1: DEFINITIONS (Blueprints) ---
            EditorGUILayout.LabelField("1. Blueprint Definitions", EditorStyles.boldLabel);
            
            for (int i = 0; i < definedBlueprintsProp.arraySize; i++)
            {
                // Restore map preview if missing
                var bp = ((WFCBuilder)target).definedBlueprints[i];
                bp.ValidateMap();

                SerializedProperty bpProp = definedBlueprintsProp.GetArrayElementAtIndex(i);
                // We return TRUE if we should delete this
                if (DrawBlueprintDefinition(bpProp, bp, builder))
                {
                    definedBlueprintsProp.DeleteArrayElementAtIndex(i);
                    i--; // Adjust index
                }
                EditorGUILayout.Space(5);
            }

            if (GUILayout.Button("Add New Blueprint Definition", GUILayout.Height(30)))
            {
                builder.definedBlueprints.Add(new WFCBlueprintLayer());
            }

            EditorGUILayout.Space(20);

            // --- TAB 2: BUILD LAYERS (Builder) ---
            EditorGUILayout.LabelField("2. Build Layers (Builder)", EditorStyles.boldLabel);
            
            // autoResize is in Global Config now
            buildLayersList.DoLayoutList();

            EditorGUILayout.Space(20);
            
            // --- RUNTIME ---
            // --- RUNTIME ---
            // --- DEBUG ---
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Debug / Solver State", EditorStyles.boldLabel);
            
            SerializedProperty solverProp = serializedObject.FindProperty("solver");
            if (solverProp != null)
            {
                SerializedProperty cellsProp = solverProp.FindPropertyRelative("cells");
                if (cellsProp != null)
                {
                     EditorGUILayout.PropertyField(cellsProp, true); // Show the list
                }
            }
            
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Application is Playing", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private bool DrawBlueprintDefinition(SerializedProperty bpProp, WFCBlueprintLayer blueprint, WFCBuilder builder)
        {
            bool deleteMe = false;
            EditorGUILayout.BeginVertical("box"); // Redesigned Header
            
            EditorGUILayout.BeginHorizontal();
            
            // 1. Toggle (Left)
            SerializedProperty activeProp = bpProp.FindPropertyRelative("active");
            if (activeProp != null)
            {
                EditorGUILayout.PropertyField(activeProp, GUIContent.none, GUILayout.Width(20));
            }

            // 2. Mini Preview (Left)
            if (blueprint.outputMap != null)
            {
                 // Draw Preview
                 GUILayout.Label(blueprint.outputMap, GUILayout.Width(24), GUILayout.Height(24));
            }
            else
            {
                 GUILayout.Space(28); 
            }

            // 3. Name (Editable Title)
            SerializedProperty nameProp = bpProp.FindPropertyRelative("layerName");
            EditorGUILayout.PropertyField(nameProp, GUIContent.none);
            
            // 4. Remove (Right)
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                 if (EditorUtility.DisplayDialog("Delete Blueprint", $"Delete '{nameProp.stringValue}'?", "Yes", "No"))
                 {
                     deleteMe = true;
                 }
            }
            EditorGUILayout.EndHorizontal();

            // Properties
            if (activeProp == null || activeProp.boolValue || bpProp.isExpanded) 
            {
                 // Keep expanded logic if desired, or just always show properties if "Active" or if we add a foldout
                 // Let's add a manual foldout button or just assume if it's there, show it?
                 // The old code used the Title as a foldout.
                 // Let's add a small Foldout arrow
            }
            
            // Foldout Content
            bpProp.isExpanded = EditorGUILayout.Foldout(bpProp.isExpanded, "Settings", true);
            if (bpProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(bpProp.FindPropertyRelative("size"));
                EditorGUILayout.PropertyField(bpProp.FindPropertyRelative("activeColor"));

                // Modifiers List
                DrawModifierList(bpProp, blueprint);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            return deleteMe;
        }


        // Cache ReorderableLists to avoid recreating per frame
        private Dictionary<string, ReorderableList> modifierLists = new Dictionary<string, ReorderableList>();

        private void DrawModifierList(SerializedProperty bpProp, WFCBlueprintLayer blueprint)
        {
            SerializedProperty modifiersProp = bpProp.FindPropertyRelative("modifiers");
            WFCBuilder builder = (WFCBuilder)target;
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Modifiers Queue", EditorStyles.boldLabel);
            
            // 1. Get or Create ReorderableList
            // Use property path as key to be unique per element
            string key = modifiersProp.propertyPath;
            
            if (!modifierLists.ContainsKey(key))
            {
                ReorderableList list = new ReorderableList(serializedObject, modifiersProp, true, true, false, false); // No Header/Add/Remove drawn by default, we customize
                
                list.drawHeaderCallback = (Rect r) => { EditorGUI.LabelField(r, "Execution Order"); };

                list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => 
                {
                    if (index >= modifiersProp.arraySize) return;
                    
                    SerializedProperty modProp = modifiersProp.GetArrayElementAtIndex(index);
                    WFCModifier modObj = blueprint.modifiers[index];
                    string modTitle = (modObj != null) ? modObj.name : "Null Modifier";
                    
                    rect.y += 2;
                    // Layout: [Check] [Name/Foldout] [X]
                    
                    // 1. Checkbox
                    SerializedProperty activeProp = modProp.FindPropertyRelative("active");
                    Rect checkRect = new Rect(rect.x, rect.y, 20, EditorGUIUtility.singleLineHeight);
                    if(activeProp != null) EditorGUI.PropertyField(checkRect, activeProp, GUIContent.none);
                    
                    // 2. Name & Foldout
                    Rect nameRect = new Rect(rect.x + 25, rect.y, rect.width - 50, EditorGUIUtility.singleLineHeight);
                    modProp.isExpanded = EditorGUI.Foldout(nameRect, modProp.isExpanded, modTitle, true);
                    
                    // 3. Remove Button
                    Rect removeRect = new Rect(rect.x + rect.width - 20, rect.y, 20, EditorGUIUtility.singleLineHeight);
                    if (GUI.Button(removeRect, "X"))
                    {
                        // Delete requested
                        modifiersProp.DeleteArrayElementAtIndex(index);
                        return;
                        // Note: DeleteArrayElementAtIndex usually handles list update, but safe to return
                    }
                    
                    // 4. Content (if expanded)
                    if (modProp.isExpanded)
                    {
                        // Calculate height needed (approximate or just standard PropertyField behavior?)
                        // ReorderableList element height must be calculated in elementHeightCallback.
                        // So here we only draw if we have space, but actually standard PropertyDrawer works better.
                        // We need to iterate children here similar to before.
                        
                        rect.y += EditorGUIUtility.singleLineHeight + 2;
                        
                        SerializedProperty endProp = modProp.GetEndProperty();
                        SerializedProperty iter = modProp.Copy();
                        
                        if (iter.NextVisible(true))
                        {
                            do
                            {
                                if (SerializedProperty.EqualContents(iter, endProp)) break;
                                if (iter.name == "m_Script" || iter.name == "active") continue; 
                                
                                float h = EditorGUI.GetPropertyHeight(iter, true);
                                // Custom Logic: if name is "inputLayerName" or "costLayerName", draw dropdown
                                if (iter.name == "inputLayerName" || iter.name == "costLayerName")
                                {
                                    // Get Options
                                    // Get Options
                                    List<string> options = builder.definedBlueprints.Select(b => b.layerName).ToList();
                                    options.Insert(0, "None");
                                    
                                    int cIdx = 0; // Default to None (index 0)
                                    if(options.Contains(iter.stringValue)) cIdx = options.IndexOf(iter.stringValue);
                                    
                                    Rect dropRect = new Rect(rect.x + 20, rect.y, rect.width - 20, h);
                                    int nIdx = EditorGUI.Popup(dropRect, iter.displayName, cIdx, options.ToArray());
                                    
                                    if(nIdx == 0) iter.stringValue = ""; // None
                                    else if(nIdx > 0) iter.stringValue = options[nIdx];
                                }
                                else
                                {
                                    EditorGUI.PropertyField(new Rect(rect.x + 20, rect.y, rect.width - 20, h), iter, true);
                                }
                                rect.y += h + 2;
                            } 
                            while (iter.NextVisible(false));
                        }
                    }
                };
                
                list.elementHeightCallback = (int index) => 
                {
                    if (index >= modifiersProp.arraySize) return EditorGUIUtility.singleLineHeight;
                    
                    SerializedProperty modProp = modifiersProp.GetArrayElementAtIndex(index);
                    float h = EditorGUIUtility.singleLineHeight + 4; // Header line
                    
                    if (modProp.isExpanded)
                    {
                        SerializedProperty endProp = modProp.GetEndProperty();
                        SerializedProperty iter = modProp.Copy();
                        if (iter.NextVisible(true))
                        {
                            do
                            {
                                if (SerializedProperty.EqualContents(iter, endProp)) break;
                                if (iter.name == "m_Script" || iter.name == "active") continue; 
                                h += EditorGUI.GetPropertyHeight(iter, true) + 2;
                            } 
                            while (iter.NextVisible(false));
                        }
                    }
                    return h;
                };

                modifierLists.Add(key, list);
            }
            
            // Draw the list
            if (modifierLists.ContainsKey(key))
            {
                modifierLists[key].serializedProperty = modifiersProp; // Updates prop reference
                modifierLists[key].DoLayoutList();
            }

            // ADD DOT
            if (GUILayout.Button("+ Add Modifier"))
            {
                GenericMenu menu = new GenericMenu();
                
                // Generators
                menu.AddItem(new GUIContent("Generators/Cellular Automata"), false, () => AddModifier(blueprint, typeof(CellularAutomataModifier)));
                menu.AddItem(new GUIContent("Generators/Shapes"), false, () => AddModifier(blueprint, typeof(ShapeGenerator)));
                menu.AddItem(new GUIContent("Generators/Calculated Noise"), false, () => AddModifier(blueprint, typeof(NoiseGenerator)));
                menu.AddItem(new GUIContent("Generators/Dot Grid"), false, () => AddModifier(blueprint, typeof(DotGridGenerator)));
                menu.AddItem(new GUIContent("Generators/Poisson Disc (Scattered)"), false, () => AddModifier(blueprint, typeof(PoissonDiscGenerator)));
                menu.AddItem(new GUIContent("Generators/BSP Dungeon"), false, () => AddModifier(blueprint, typeof(BSPDungeonGenerator)));
                
                // Pathfinding
                menu.AddItem(new GUIContent("Generators/Pathfinding"), false, () => AddModifier(blueprint, typeof(PathfindingGenerator)));

                menu.AddItem(new GUIContent("Generators/Texture Input"), false, () => AddModifier(blueprint, typeof(TextureInputGenerator)));
                
                // Modifiers
                menu.AddItem(new GUIContent("Modifiers/Add (Union)"), false, () => AddModifier(blueprint, typeof(AddModifier)));
                menu.AddItem(new GUIContent("Modifiers/Subtract (Difference)"), false, () => AddModifier(blueprint, typeof(SubtractModifier)));
                menu.AddItem(new GUIContent("Modifiers/Shrink (Erode)"), false, () => AddModifier(blueprint, typeof(ShrinkModifier)));
                menu.AddItem(new GUIContent("Modifiers/Expand (Grow)"), false, () => AddModifier(blueprint, typeof(GrowModifier)));
                menu.AddItem(new GUIContent("Modifiers/Smooth"), false, () => AddModifier(blueprint, typeof(SmoothModifier)));
                menu.AddItem(new GUIContent("Modifiers/Invert"), false, () => AddModifier(blueprint, typeof(InvertModifier)));

                menu.ShowAsContext();
            }

            if (blueprint.outputMap != null)
            {
                EditorGUILayout.Space(5);
                float aspect = (float)blueprint.outputMap.width / blueprint.outputMap.height;
                Rect rect = GUILayoutUtility.GetRect(0, 150); 
                float width = rect.height * aspect;
                Rect centered = new Rect(rect.x + (rect.width - width)/2, rect.y, width, rect.height);
                EditorGUI.DrawPreviewTexture(centered, blueprint.outputMap, null, ScaleMode.ScaleToFit);
            }
        }

        private void AddModifier(WFCBlueprintLayer blueprint, System.Type type, System.Action<WFCModifier> onCreated = null)
        {
            WFCModifier newMod = (WFCModifier)System.Activator.CreateInstance(type);
            newMod.name = type.Name; // Default name
            
            onCreated?.Invoke(newMod); // Apply custom setup
            
            blueprint.modifiers.Add(newMod);
            EditorUtility.SetDirty(target);
        }
    }
}
