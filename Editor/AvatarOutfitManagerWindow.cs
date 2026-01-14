using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Soph.AvatarOutfitManager.Editor
{
    /// <summary>
    /// Main Editor Window for the Avatar Outfit Manager.
    /// Accessible via Tools > Soph's Outfit Manager.
    /// 
    /// This tool allows users to:
    /// - Save current outfit visibility states to slots
    /// - Preview saved outfits in the Editor
    /// - Generate all required VRChat assets (AnimationClips, FX Layer, Parameters, Menu)
    /// </summary>
    public class AvatarOutfitManagerWindow : EditorWindow
    {
        // References
        private VRCAvatarDescriptor avatarDescriptor;
        private Transform outfitRoot;
        private OutfitSlotData slotData;

        // UI State
        private int selectedSlotIndex = 0;
        private Vector2 scrollPosition;
        private string outputFolderPath = "Assets/AvatarOutfitManager/Generated";

        // Icon Rendering Settings
        private bool showIconSettings = false;
        private IconCameraSettings iconCameraSettings = new IconCameraSettings();
        private int selectedCameraPreset = 0;
        private readonly string[] cameraPresetNames = { "Custom", "Portrait", "Full Body", "3/4 View" };

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle slotButtonStyle;
        private GUIStyle selectedSlotStyle;
        private GUIStyle configuredSlotStyle;
        private bool stylesInitialized = false;

        [MenuItem("Tools/Soph's Outfit Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarOutfitManagerWindow>();
            window.titleContent = new GUIContent("Outfit Manager");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 10)
            };

            slotButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 40,
                fontSize = 12,
                fontStyle = FontStyle.Normal
            };

            selectedSlotStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 40,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            configuredSlotStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 40,
                fontSize = 12,
                fontStyle = FontStyle.Italic
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawAvatarConfiguration();
            EditorGUILayout.Space(10);

            if (avatarDescriptor != null && outfitRoot != null)
            {
                DrawSlotSelector();
                EditorGUILayout.Space(10);

                DrawSlotDetails();
                EditorGUILayout.Space(10);

                DrawSlotActions();
                EditorGUILayout.Space(10);

                DrawIconRendering();
                EditorGUILayout.Space(20);

                DrawAssetGeneration();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Please assign an Avatar with VRC Avatar Descriptor and an Outfit Root to begin.",
                    MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Soph's Outfit Manager", headerStyle);
            EditorGUILayout.LabelField("Save and manage outfit presets for VRChat avatars", EditorStyles.miniLabel);
        }

        private void DrawAvatarConfiguration()
        {
            EditorGUILayout.LabelField("Avatar Configuration", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Avatar Descriptor
                var newDescriptor = EditorGUILayout.ObjectField(
                    "Avatar",
                    avatarDescriptor,
                    typeof(VRCAvatarDescriptor),
                    true) as VRCAvatarDescriptor;

                if (newDescriptor != avatarDescriptor)
                {
                    avatarDescriptor = newDescriptor;
                    OnAvatarChanged();
                }

                // Outfit Root
                EditorGUILayout.BeginHorizontal();
                var newOutfitRoot = EditorGUILayout.ObjectField(
                    "Outfit Root",
                    outfitRoot,
                    typeof(Transform),
                    true) as Transform;

                // Auto-Detect Button
                GUI.enabled = avatarDescriptor != null;
                if (GUILayout.Button("Auto", GUILayout.Width(45)))
                {
                    AutoDetectOutfitRoot();
                }
                if (GUILayout.Button("+", GUILayout.Width(25)))
                {
                    CreateOutfitRootFolder();
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                if (newOutfitRoot != outfitRoot)
                {
                    outfitRoot = newOutfitRoot;
                    ValidateOutfitRoot();
                }

                // Show info about outfit root
                if (outfitRoot == null && avatarDescriptor != null)
                {
                    EditorGUILayout.HelpBox(
                        "Click 'Auto' to auto-detect or '+' to create a new Outfit folder.",
                        MessageType.Info);
                }
                else if (outfitRoot != null && avatarDescriptor != null)
                {
                    if (!outfitRoot.IsChildOf(avatarDescriptor.transform))
                    {
                        EditorGUILayout.HelpBox(
                            "Outfit Root must be a child of the selected Avatar!",
                            MessageType.Error);
                    }
                    else
                    {
                        int childCount = CountToggleableObjects(outfitRoot);
                        EditorGUILayout.HelpBox(
                            $"Found {childCount} toggleable objects under '{outfitRoot.name}'",
                            MessageType.None);
                    }
                }

                // Slot Data
                EditorGUILayout.Space(5);
                slotData = EditorGUILayout.ObjectField(
                    "Slot Data",
                    slotData,
                    typeof(OutfitSlotData),
                    false) as OutfitSlotData;

                if (slotData == null)
                {
                    EditorGUILayout.HelpBox(
                        "No Slot Data assigned. Create one or it will be auto-created when saving.",
                        MessageType.Info);

                    if (GUILayout.Button("Create New Slot Data"))
                    {
                        CreateNewSlotData();
                    }
                }

                // Output Folder
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                outputFolderPath = EditorGUILayout.TextField("Output Folder", outputFolderPath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        // Convert to relative path
                        if (selected.StartsWith(Application.dataPath))
                        {
                            outputFolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSlotSelector()
        {
            EditorGUILayout.LabelField("Outfit Slots", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.BeginHorizontal();

                for (int i = 0; i < OutfitSlotData.SLOT_COUNT; i++)
                {
                    bool isSelected = (i == selectedSlotIndex);
                    bool isConfigured = slotData != null && 
                                       slotData.slots != null && 
                                       slotData.slots[i] != null && 
                                       slotData.slots[i].isConfigured;

                    GUIStyle style = isSelected ? selectedSlotStyle : 
                                    (isConfigured ? configuredSlotStyle : slotButtonStyle);

                    string slotLabel = i.ToString();
                    if (isConfigured && slotData.slots[i] != null)
                    {
                        string name = slotData.slots[i].slotName;
                        if (!string.IsNullOrEmpty(name) && name.Length > 8)
                        {
                            name = name.Substring(0, 8) + "...";
                        }
                        if (!string.IsNullOrEmpty(name))
                        {
                            slotLabel = $"{i}\n{name}";
                        }
                    }

                    GUI.backgroundColor = isSelected ? Color.cyan : 
                                         (isConfigured ? Color.green : Color.white);

                    if (GUILayout.Button(slotLabel, style, GUILayout.Width(60)))
                    {
                        selectedSlotIndex = i;
                    }
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                // Legend
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("● Selected", EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField("● Configured", EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField("○ Empty", EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSlotDetails()
        {
            if (slotData == null || slotData.slots == null) return;

            var currentSlot = slotData.slots[selectedSlotIndex];
            if (currentSlot == null) return;

            EditorGUILayout.LabelField($"Slot {selectedSlotIndex} Details", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Slot name
                string newName = EditorGUILayout.TextField("Slot Name", currentSlot.slotName);
                if (newName != currentSlot.slotName)
                {
                    Undo.RecordObject(slotData, "Change Slot Name");
                    currentSlot.slotName = newName;
                    EditorUtility.SetDirty(slotData);
                }

                // Status
                string statusText = currentSlot.isConfigured ? 
                    $"Configured ({currentSlot.objectStates?.Count ?? 0} objects)" : 
                    "Not configured";
                EditorGUILayout.LabelField("Status", statusText);

                // Show object states if configured
                if (currentSlot.isConfigured && currentSlot.objectStates != null && currentSlot.objectStates.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Saved Objects:", EditorStyles.miniLabel);

                    int activeCount = 0;
                    int inactiveCount = 0;

                    foreach (var state in currentSlot.objectStates)
                    {
                        if (state.isActive) activeCount++;
                        else inactiveCount++;
                    }

                    EditorGUILayout.LabelField($"  Active: {activeCount}  |  Inactive: {inactiveCount}", 
                        EditorStyles.miniLabel);
                }

                // Icon Preview
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Slot Icon:", EditorStyles.miniLabel);
                
                EditorGUILayout.BeginHorizontal();
                
                // Show icon if available
                var icon = currentSlot.GetIcon();
                if (icon != null)
                {
                    GUILayout.Box(icon, GUILayout.Width(64), GUILayout.Height(64));
                }
                else
                {
                    EditorGUILayout.HelpBox("No icon\nrendered", MessageType.None);
                }

                EditorGUILayout.BeginVertical();
                
                // Render icon button
                GUI.enabled = currentSlot.isConfigured && avatarDescriptor != null && outfitRoot != null;
                if (GUILayout.Button("Render Icon", GUILayout.Height(25)))
                {
                    RenderSlotIcon(selectedSlotIndex);
                }
                
                // Clear icon button
                GUI.enabled = icon != null;
                if (GUILayout.Button("Clear Icon", GUILayout.Height(25)))
                {
                    ClearSlotIcon(selectedSlotIndex);
                }
                GUI.enabled = true;
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSlotActions()
        {
            EditorGUILayout.LabelField("Slot Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.BeginHorizontal();

                // Save current visibility
                GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
                if (GUILayout.Button("Save Current Visibility to Slot", GUILayout.Height(30)))
                {
                    SaveCurrentVisibilityToSlot();
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();

                // Load in Editor
                GUI.backgroundColor = new Color(0.5f, 0.7f, 0.9f);
                GUI.enabled = slotData != null && 
                             slotData.slots != null && 
                             slotData.slots[selectedSlotIndex] != null &&
                             slotData.slots[selectedSlotIndex].isConfigured;

                if (GUILayout.Button("Load Slot in Editor", GUILayout.Height(30)))
                {
                    LoadSlotInEditor();
                }

                GUI.enabled = true;

                // Clear slot
                GUI.backgroundColor = new Color(0.9f, 0.5f, 0.5f);
                if (GUILayout.Button("Clear Slot", GUILayout.Height(30)))
                {
                    ClearSlot();
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawIconRendering()
        {
            EditorGUILayout.LabelField("Icon Rendering", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Camera preset selector
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Camera Preset:", GUILayout.Width(100));
                int newPreset = EditorGUILayout.Popup(selectedCameraPreset, cameraPresetNames);
                if (newPreset != selectedCameraPreset)
                {
                    selectedCameraPreset = newPreset;
                    ApplyCameraPreset(newPreset);
                }
                EditorGUILayout.EndHorizontal();

                // Show custom settings foldout
                showIconSettings = EditorGUILayout.Foldout(showIconSettings, "Custom Camera Settings", true);
                if (showIconSettings)
                {
                    EditorGUI.indentLevel++;
                    iconCameraSettings.cameraDistance = EditorGUILayout.Slider("Distance", iconCameraSettings.cameraDistance, 0.3f, 5f);
                    iconCameraSettings.cameraHeightOffset = EditorGUILayout.Slider("Height Offset", iconCameraSettings.cameraHeightOffset, -1f, 1f);
                    iconCameraSettings.cameraRotationOffset = EditorGUILayout.Slider("Rotation", iconCameraSettings.cameraRotationOffset, -180f, 180f);
                    iconCameraSettings.fieldOfView = EditorGUILayout.Slider("FOV", iconCameraSettings.fieldOfView, 10f, 90f);
                    iconCameraSettings.useTransparentBackground = EditorGUILayout.Toggle("Transparent BG", iconCameraSettings.useTransparentBackground);
                    if (!iconCameraSettings.useTransparentBackground)
                    {
                        iconCameraSettings.backgroundColor = EditorGUILayout.ColorField("Background", iconCameraSettings.backgroundColor);
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);

                // Render all icons button
                GUI.enabled = slotData != null && slotData.GetConfiguredSlotCount() > 0 && avatarDescriptor != null && outfitRoot != null;
                GUI.backgroundColor = new Color(0.6f, 0.4f, 0.8f);
                if (GUILayout.Button("Render All Outfit Icons", GUILayout.Height(30)))
                {
                    RenderAllIcons();
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;

                EditorGUILayout.HelpBox(
                    "Icons are used in the VRChat Expression Menu for each outfit.\n" +
                    "Position the avatar in the Scene view before rendering.",
                    MessageType.Info);
            }
        }

        private void DrawAssetGeneration()
        {
            EditorGUILayout.LabelField("VRChat Asset Generation", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Validation info
                int configuredCount = slotData?.GetConfiguredSlotCount() ?? 0;
                EditorGUILayout.LabelField($"Configured Slots: {configuredCount} / {OutfitSlotData.SLOT_COUNT}");

                if (configuredCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        "Please configure at least one outfit slot before generating assets.",
                        MessageType.Warning);
                }

                EditorGUILayout.Space(5);

                // Generate button
                GUI.enabled = configuredCount > 0 && avatarDescriptor != null && outfitRoot != null;
                GUI.backgroundColor = new Color(0.4f, 0.6f, 0.9f);

                if (GUILayout.Button("Generate VRChat Assets", GUILayout.Height(40)))
                {
                    GenerateVRChatAssets();
                }

                GUI.backgroundColor = Color.white;
                GUI.enabled = true;

                EditorGUILayout.Space(5);

                // Info box
                EditorGUILayout.HelpBox(
                    "This will generate:\n" +
                    "• Animation Clips for each outfit\n" +
                    "• FX Animator Layer with outfit states\n" +
                    "• Expression Parameters (OutfitIndex)\n" +
                    "• Expressions Menu with outfit selection",
                    MessageType.Info);
            }
        }

        private void OnAvatarChanged()
        {
            // Try to find existing slot data for this avatar
            if (avatarDescriptor != null)
            {
                string avatarPath = AssetDatabase.GetAssetPath(avatarDescriptor.gameObject);
                if (!string.IsNullOrEmpty(avatarPath))
                {
                    // Could search for existing slot data here
                }
            }
        }

        private void ValidateOutfitRoot()
        {
            if (outfitRoot == null || avatarDescriptor == null) return;

            if (!outfitRoot.IsChildOf(avatarDescriptor.transform))
            {
                Debug.LogWarning("[Outfit Manager] Outfit Root must be a child of the Avatar!");
            }
        }

        private void CreateNewSlotData()
        {
            // Ensure folder exists
            string folderPath = "Assets/AvatarOutfitManager";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string avatarName = avatarDescriptor != null ? avatarDescriptor.gameObject.name : "Avatar";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folderPath}/OutfitData_{avatarName}.asset");

            slotData = ScriptableObject.CreateInstance<OutfitSlotData>();
            slotData.InitializeSlots();

            AssetDatabase.CreateAsset(slotData, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Outfit Manager] Created new slot data at: {assetPath}");
        }

        private void SaveCurrentVisibilityToSlot()
        {
            if (outfitRoot == null)
            {
                EditorUtility.DisplayDialog("Error", "Outfit Root is not assigned!", "OK");
                return;
            }

            // Create slot data if needed
            if (slotData == null)
            {
                CreateNewSlotData();
            }

            slotData.InitializeSlots();

            var currentSlot = slotData.slots[selectedSlotIndex];
            
            Undo.RecordObject(slotData, "Save Outfit to Slot");

            // Clear existing states
            currentSlot.objectStates.Clear();

            // Collect all GameObjects under outfit root
            CollectObjectStates(outfitRoot, outfitRoot, currentSlot.objectStates);

            currentSlot.isConfigured = true;

            EditorUtility.SetDirty(slotData);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Outfit Manager] Saved {currentSlot.objectStates.Count} objects to slot {selectedSlotIndex}");
        }

        private void CollectObjectStates(Transform root, Transform current, List<GameObjectState> states)
        {
            foreach (Transform child in current)
            {
                string relativePath = VRChatAssetGenerator.GetRelativePath(root, child);
                
                states.Add(new GameObjectState
                {
                    path = relativePath,
                    isActive = child.gameObject.activeSelf
                });

                // Recurse into children
                CollectObjectStates(root, child, states);
            }
        }

        private void LoadSlotInEditor()
        {
            if (slotData == null || outfitRoot == null) return;

            var currentSlot = slotData.slots[selectedSlotIndex];
            if (!currentSlot.isConfigured) return;

            Undo.RecordObject(outfitRoot.gameObject, "Load Outfit Slot");

            // Create path to object mapping
            var pathToObject = new Dictionary<string, Transform>();
            CollectPathMappings(outfitRoot, outfitRoot, pathToObject);

            // Apply states
            foreach (var state in currentSlot.objectStates)
            {
                if (pathToObject.TryGetValue(state.path, out Transform obj))
                {
                    Undo.RecordObject(obj.gameObject, "Load Outfit Slot");
                    obj.gameObject.SetActive(state.isActive);
                }
                else
                {
                    Debug.LogWarning($"[Outfit Manager] Object not found: {state.path}");
                }
            }

            Debug.Log($"[Outfit Manager] Loaded slot {selectedSlotIndex} in Editor");
        }

        private void CollectPathMappings(Transform root, Transform current, Dictionary<string, Transform> mappings)
        {
            foreach (Transform child in current)
            {
                string relativePath = VRChatAssetGenerator.GetRelativePath(root, child);
                mappings[relativePath] = child;
                CollectPathMappings(root, child, mappings);
            }
        }

        private void ClearSlot()
        {
            if (slotData == null) return;

            if (!EditorUtility.DisplayDialog(
                "Clear Slot",
                $"Are you sure you want to clear slot {selectedSlotIndex}?",
                "Yes", "No"))
            {
                return;
            }

            Undo.RecordObject(slotData, "Clear Outfit Slot");

            slotData.slots[selectedSlotIndex].Clear();

            EditorUtility.SetDirty(slotData);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Outfit Manager] Cleared slot {selectedSlotIndex}");
        }

        private void GenerateVRChatAssets()
        {
            if (!ValidateForGeneration()) return;

            bool success = VRChatAssetGenerator.GenerateAllAssets(
                avatarDescriptor,
                outfitRoot,
                slotData,
                outputFolderPath);

            if (success)
            {
                // Select the output folder in Project window
                var folder = AssetDatabase.LoadAssetAtPath<Object>(outputFolderPath);
                if (folder != null)
                {
                    Selection.activeObject = folder;
                    EditorGUIUtility.PingObject(folder);
                }
            }
        }

        private bool ValidateForGeneration()
        {
            if (avatarDescriptor == null)
            {
                EditorUtility.DisplayDialog("Error", "Avatar is not assigned!", "OK");
                return false;
            }

            if (outfitRoot == null)
            {
                EditorUtility.DisplayDialog("Error", "Outfit Root is not assigned!", "OK");
                return false;
            }

            if (!outfitRoot.IsChildOf(avatarDescriptor.transform))
            {
                EditorUtility.DisplayDialog("Error", "Outfit Root must be a child of the Avatar!", "OK");
                return false;
            }

            if (slotData == null)
            {
                EditorUtility.DisplayDialog("Error", "Slot Data is not assigned!", "OK");
                return false;
            }

            if (slotData.GetConfiguredSlotCount() == 0)
            {
                EditorUtility.DisplayDialog("Error", "No outfit slots are configured!", "OK");
                return false;
            }

            return true;
        }

        #region Icon Rendering

        private void ApplyCameraPreset(int presetIndex)
        {
            switch (presetIndex)
            {
                case 1: // Portrait
                    iconCameraSettings = IconCameraSettings.Portrait();
                    break;
                case 2: // Full Body
                    iconCameraSettings = IconCameraSettings.FullBody();
                    break;
                case 3: // 3/4 View
                    iconCameraSettings = IconCameraSettings.ThreeQuarter();
                    break;
                default: // Custom - keep current settings
                    break;
            }
        }

        private void RenderSlotIcon(int slotIndex)
        {
            if (slotData == null || avatarDescriptor == null || outfitRoot == null) return;

            var slot = slotData.slots[slotIndex];
            if (!slot.isConfigured)
            {
                EditorUtility.DisplayDialog("Error", "This slot is not configured!", "OK");
                return;
            }

            // Ensure output folder exists
            string iconsFolder = Path.Combine(outputFolderPath, "Icons");
            if (!Directory.Exists(iconsFolder))
            {
                Directory.CreateDirectory(iconsFolder);
            }

            // Store current visibility state to restore later
            var originalStates = StoreCurrentVisibility();

            try
            {
                var icon = OutfitIconRenderer.RenderSlotIcon(
                    avatarDescriptor.transform,
                    outfitRoot,
                    slot,
                    slotIndex,
                    outputFolderPath,
                    iconCameraSettings);

                if (icon != null)
                {
                    EditorUtility.SetDirty(slotData);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[Outfit Manager] Rendered icon for slot {slotIndex}");
                }
            }
            finally
            {
                // Restore original visibility
                RestoreVisibility(originalStates);
            }

            Repaint();
        }

        private void ClearSlotIcon(int slotIndex)
        {
            if (slotData == null) return;

            var slot = slotData.slots[slotIndex];
            if (!string.IsNullOrEmpty(slot.iconPath))
            {
                // Delete the icon file
                if (File.Exists(slot.iconPath))
                {
                    AssetDatabase.DeleteAsset(slot.iconPath);
                }
                
                slot.iconPath = null;
                EditorUtility.SetDirty(slotData);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Outfit Manager] Cleared icon for slot {slotIndex}");
            }

            Repaint();
        }

        private void RenderAllIcons()
        {
            if (slotData == null || avatarDescriptor == null || outfitRoot == null) return;

            // Store current visibility state to restore later
            var originalStates = StoreCurrentVisibility();

            try
            {
                OutfitIconRenderer.RenderAllSlotIcons(
                    avatarDescriptor.transform,
                    outfitRoot,
                    slotData,
                    outputFolderPath);

                EditorUtility.SetDirty(slotData);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                // Restore original visibility
                RestoreVisibility(originalStates);
            }

            Repaint();
        }

        private Dictionary<Transform, bool> StoreCurrentVisibility()
        {
            var states = new Dictionary<Transform, bool>();
            if (outfitRoot == null) return states;

            StoreVisibilityRecursive(outfitRoot, states);
            return states;
        }

        private void StoreVisibilityRecursive(Transform parent, Dictionary<Transform, bool> states)
        {
            foreach (Transform child in parent)
            {
                states[child] = child.gameObject.activeSelf;
                StoreVisibilityRecursive(child, states);
            }
        }

        private void RestoreVisibility(Dictionary<Transform, bool> states)
        {
            foreach (var kvp in states)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.gameObject.SetActive(kvp.Value);
                }
            }
        }

        #endregion

        #region Outfit Root Auto-Detection

        // Common names for outfit/clothing folders
        private static readonly string[] OutfitFolderNames = new string[]
        {
            "Clothing", "Clothes", "Outfits", "Outfit", "Accessories",
            "Toggles", "Toggle", "Wearables", "Apparel", "Garments",
            "clothing", "clothes", "outfits", "outfit", "accessories"
        };

        // Names to exclude (these are usually not outfit containers)
        private static readonly string[] ExcludedNames = new string[]
        {
            "Armature", "Body", "Head", "Hair", "Eyes", "Teeth", "Tongue",
            "armature", "body", "head", "hair", "eyes", "teeth", "tongue"
        };

        private void AutoDetectOutfitRoot()
        {
            if (avatarDescriptor == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign an Avatar first!", "OK");
                return;
            }

            Transform detected = null;

            // Step 1: Look for common folder names
            foreach (string folderName in OutfitFolderNames)
            {
                detected = FindChildByName(avatarDescriptor.transform, folderName);
                if (detected != null)
                {
                    outfitRoot = detected;
                    Debug.Log($"[Outfit Manager] Auto-detected outfit root: '{detected.name}'");
                    return;
                }
            }

            // Step 2: Find folder with most toggleable children (meshes)
            Transform bestCandidate = null;
            int maxToggleables = 0;

            foreach (Transform child in avatarDescriptor.transform)
            {
                // Skip excluded names
                bool isExcluded = false;
                foreach (string excluded in ExcludedNames)
                {
                    if (child.name.Contains(excluded))
                    {
                        isExcluded = true;
                        break;
                    }
                }
                if (isExcluded) continue;

                int toggleables = CountToggleableObjects(child);
                if (toggleables > maxToggleables)
                {
                    maxToggleables = toggleables;
                    bestCandidate = child;
                }
            }

            if (bestCandidate != null && maxToggleables >= 2)
            {
                outfitRoot = bestCandidate;
                Debug.Log($"[Outfit Manager] Auto-detected outfit root by content: '{bestCandidate.name}' ({maxToggleables} toggleables)");
                return;
            }

            // Step 3: No suitable folder found
            if (EditorUtility.DisplayDialog(
                "No Outfit Folder Found",
                "Could not find an outfit folder. Would you like to create one?",
                "Create 'Outfits' Folder", "Cancel"))
            {
                CreateOutfitRootFolder();
            }
        }

        private void CreateOutfitRootFolder()
        {
            if (avatarDescriptor == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign an Avatar first!", "OK");
                return;
            }

            // Check if folder already exists
            Transform existing = FindChildByName(avatarDescriptor.transform, "Outfits");
            if (existing != null)
            {
                outfitRoot = existing;
                Debug.Log("[Outfit Manager] Using existing 'Outfits' folder");
                return;
            }

            // Create new folder
            Undo.RecordObject(avatarDescriptor.gameObject, "Create Outfit Folder");
            
            GameObject outfitFolder = new GameObject("Outfits");
            outfitFolder.transform.SetParent(avatarDescriptor.transform);
            outfitFolder.transform.localPosition = Vector3.zero;
            outfitFolder.transform.localRotation = Quaternion.identity;
            outfitFolder.transform.localScale = Vector3.one;

            Undo.RegisterCreatedObjectUndo(outfitFolder, "Create Outfit Folder");

            outfitRoot = outfitFolder.transform;
            
            Debug.Log("[Outfit Manager] Created new 'Outfits' folder");
            
            EditorUtility.DisplayDialog(
                "Folder Created",
                "Created 'Outfits' folder under your avatar.\n\n" +
                "Now drag your clothing/accessory GameObjects into this folder!",
                "OK");
        }

        private Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
            return null;
        }

        private int CountToggleableObjects(Transform root)
        {
            int count = 0;
            CountToggleablesRecursive(root, ref count);
            return count;
        }

        private void CountToggleablesRecursive(Transform parent, ref int count)
        {
            foreach (Transform child in parent)
            {
                // Count objects that have renderers (meshes) or are likely toggleable
                if (child.GetComponent<Renderer>() != null || 
                    child.GetComponent<SkinnedMeshRenderer>() != null ||
                    child.childCount > 0)
                {
                    count++;
                }
                CountToggleablesRecursive(child, ref count);
            }
        }

        #endregion
    }
}
