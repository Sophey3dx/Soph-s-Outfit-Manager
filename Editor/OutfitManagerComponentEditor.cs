using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Soph.AvatarOutfitManager.Editor
{
    /// <summary>
    /// Custom Inspector Editor for OutfitManagerComponent.
    /// Provides a detailed view of all outfit slots and validation status.
    /// </summary>
    [CustomEditor(typeof(OutfitManagerComponent))]
    [CanEditMultipleObjects]
    public class OutfitManagerComponentEditor : UnityEditor.Editor
    {
        private Dictionary<int, bool> slotFoldouts = new Dictionary<int, bool>();
        private bool showValidation = true;
        private bool showOutfits = true;
        private Vector2 scrollPosition;

        public override void OnInspectorGUI()
        {
            OutfitManagerComponent component = (OutfitManagerComponent)target;
            
            // Header
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Soph's Outfit Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Open Window Button
            if (GUILayout.Button("Open Outfit Manager Window", GUILayout.Height(30)))
            {
                AvatarOutfitManagerWindow.ShowWindow();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Validation Section
            showValidation = EditorGUILayout.Foldout(showValidation, "Validation & Status", true);
            if (showValidation)
            {
                EditorGUI.indentLevel++;
                DrawValidation(component);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Outfit Root Display
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Outfit Root:", GUILayout.Width(100));
            Transform outfitRoot = component.OutfitRoot;
            EditorGUILayout.ObjectField(outfitRoot, typeof(Transform), true);
            EditorGUILayout.EndHorizontal();

            if (outfitRoot != null)
            {
                int childCount = outfitRoot.childCount;
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Children: {childCount}", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Slot Data Display
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Slot Data:", GUILayout.Width(100));
            OutfitSlotData slotData = component.SlotData;
            EditorGUILayout.ObjectField(slotData, typeof(OutfitSlotData), false);
            if (slotData != null && GUILayout.Button("Ping", GUILayout.Width(50)))
            {
                EditorGUIUtility.PingObject(slotData);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Outfit Slots Section
            showOutfits = EditorGUILayout.Foldout(showOutfits, $"Outfit Slots ({OutfitSlotData.SLOT_COUNT} total)", true);
            if (showOutfits)
            {
                EditorGUI.indentLevel++;
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
                
                if (slotData == null)
                {
                    EditorGUILayout.HelpBox("Slot Data not loaded. It will be created when you save your first outfit.", MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < OutfitSlotData.SLOT_COUNT; i++)
                    {
                        DrawSlotDetails(component, i);
                        EditorGUILayout.Space(3);
                    }
                }
                
                EditorGUILayout.EndScrollView();
                EditorGUI.indentLevel--;
            }

            // Apply changes
            if (GUI.changed)
            {
                EditorUtility.SetDirty(component);
            }
        }

        private void DrawValidation(OutfitManagerComponent component)
        {
            var validation = component.Validate();

            // Errors
            if (validation.errors.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Errors", EditorStyles.boldLabel);
                foreach (string error in validation.errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
                EditorGUILayout.EndVertical();
            }

            // Warnings
            if (validation.warnings.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Warnings", EditorStyles.boldLabel);
                foreach (string warning in validation.warnings)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
                EditorGUILayout.EndVertical();
            }

            // Info messages
            if (validation.infos.Count > 0)
            {
                foreach (string info in validation.infos)
                {
                    EditorGUILayout.LabelField(info, EditorStyles.miniLabel);
                }
            }

            if (validation.errors.Count == 0 && validation.warnings.Count == 0 && validation.infos.Count == 0)
            {
                EditorGUILayout.HelpBox("No validation information available.", MessageType.Info);
            }
        }

        private void DrawSlotDetails(OutfitManagerComponent component, int slotIndex)
        {
            var slotInfo = component.GetSlotInfo(slotIndex);

            // Create foldout key if needed
            if (!slotFoldouts.ContainsKey(slotIndex))
            {
                slotFoldouts[slotIndex] = false;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header with status
            EditorGUILayout.BeginHorizontal();
            
            // Status indicator
            GUIStyle statusStyle = new GUIStyle(EditorStyles.label);
            if (slotInfo.isConfigured)
            {
                statusStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                EditorGUILayout.LabelField("OK", statusStyle, GUILayout.Width(20));
            }
            else
            {
                statusStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                EditorGUILayout.LabelField("--", statusStyle, GUILayout.Width(20));
            }

            // Foldout with slot name
            string slotLabel = $"Slot {slotIndex}: {(string.IsNullOrEmpty(slotInfo.slotName) ? $"Outfit {slotIndex}" : slotInfo.slotName)}";
            slotFoldouts[slotIndex] = EditorGUILayout.Foldout(slotFoldouts[slotIndex], slotLabel, true);

            GUILayout.FlexibleSpace();

            // Object count
            if (slotInfo.isConfigured)
            {
                EditorGUILayout.LabelField($"{slotInfo.objectCount} object(s)", EditorStyles.miniLabel, GUILayout.Width(80));
            }
            else
            {
                EditorGUILayout.LabelField("Not configured", EditorStyles.miniLabel, GUILayout.Width(80));
            }

            EditorGUILayout.EndHorizontal();

            // Expanded details
            if (slotFoldouts[slotIndex])
            {
                EditorGUI.indentLevel++;

                if (slotInfo.isConfigured)
                {
                    // Icon preview
                    if (!string.IsNullOrEmpty(slotInfo.iconPath))
                    {
                        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(slotInfo.iconPath);
                        if (icon != null)
                        {
                            EditorGUILayout.LabelField("Icon:");
                            Rect iconRect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
                            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                        }
                    }

                    // Object list
                    if (slotInfo.objectStates != null && slotInfo.objectStates.Count > 0)
                    {
                        EditorGUILayout.LabelField("Objects:");
                        EditorGUI.indentLevel++;
                        
                        foreach (var state in slotInfo.objectStates)
                        {
                            GUIStyle stateStyle = new GUIStyle(EditorStyles.miniLabel);
                            stateStyle.normal.textColor = state.isActive
                                ? new Color(0.2f, 0.8f, 0.2f)
                                : new Color(0.6f, 0.6f, 0.6f);
                            string label = state.isActive ? "[ON] " : "[OFF] ";
                            EditorGUILayout.LabelField($"{label}{state.path}", stateStyle);
                        }
                        
                        EditorGUI.indentLevel--;
                    }
                    else if (slotInfo.objectCount > 0)
                    {
                        EditorGUILayout.LabelField("No objects recorded.", EditorStyles.miniLabel);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("This slot has not been configured yet.", MessageType.Info);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }
    }
}
