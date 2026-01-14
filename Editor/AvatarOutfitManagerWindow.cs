using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Soph.AvatarOutfitManager.Editor
{
    /// <summary>
    /// Main Editor Window for the Avatar Outfit Manager.
    /// Features: One-Click Setup, Wizard Mode, Visual Slots, Tooltips, Presets
    /// </summary>
    public class AvatarOutfitManagerWindow : EditorWindow
    {
        #region Fields

        // References
        private VRCAvatarDescriptor avatarDescriptor;
        private Transform outfitRoot;
        private OutfitSlotData slotData;

        // UI State
        private int selectedSlotIndex = 0;
        private Vector2 scrollPosition;
        private string outputFolderPath = "Assets/AvatarOutfitManager/Generated";

        // Wizard State
        private enum WizardStep { None, Avatar, OutfitRoot, Ready }
        private WizardStep currentWizardStep = WizardStep.None;
        private bool showWizard = false;
        private bool isFirstTimeUser = true;

        // Icon Rendering Settings
        private bool showIconSettings = false;
        private IconCameraSettings iconCameraSettings = new IconCameraSettings();
        private int selectedCameraPreset = 0;
        private readonly string[] cameraPresetNames = { "Custom", "Portrait", "Full Body", "3/4 View" };

        // Preset Names
        private static readonly string[] PresetSlotNames = 
        { 
            "Default", "Casual", "Formal", "Sporty", "Beach", "Night Out" 
        };

        // Auto-detect folder names
        private static readonly string[] OutfitFolderNames = 
        {
            "Clothing", "Clothes", "Outfits", "Outfit", "Accessories",
            "Toggles", "Toggle", "Wearables", "Apparel", "Garments"
        };

        private static readonly string[] ExcludedNames = 
        {
            "Armature", "Body", "Head", "Hair", "Eyes", "Teeth", "Tongue"
        };

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle wizardHeaderStyle;
        private GUIStyle slotCardStyle;
        private GUIStyle selectedSlotCardStyle;
        private GUIStyle dropAreaStyle;
        private GUIStyle centeredLabelStyle;
        private bool stylesInitialized = false;

        // Tooltips
        private static class Tips
        {
            public static readonly GUIContent Avatar = new GUIContent(
                "Avatar", "The VRChat avatar you want to add outfits to. Must have a VRC Avatar Descriptor.");
            public static readonly GUIContent OutfitRoot = new GUIContent(
                "Outfit Root", "The folder containing all your clothing/accessory GameObjects that can be toggled.");
            public static readonly GUIContent SlotData = new GUIContent(
                "Slot Data", "Asset file that stores your outfit configurations. Auto-created if needed.");
            public static readonly GUIContent OutputFolder = new GUIContent(
                "Output Folder", "Where generated VRChat assets (animations, menus) will be saved.");
            public static readonly GUIContent SaveSlot = new GUIContent(
                "Save Current Visibility", "Saves which clothing items are currently visible as this outfit.");
            public static readonly GUIContent LoadSlot = new GUIContent(
                "Load in Editor", "Preview this outfit by applying its visibility settings in the Scene.");
            public static readonly GUIContent Generate = new GUIContent(
                "Generate VRChat Assets", "Creates all files needed for in-game outfit switching.");
            public static readonly GUIContent RenderIcon = new GUIContent(
                "Render Icon", "Takes a screenshot of the current outfit for the VRChat menu.");
            public static readonly GUIContent ApplyPresets = new GUIContent(
                "Apply Preset Names", "Fills slot names with: Default, Casual, Formal, Sporty, Beach, Night Out");
        }

        #endregion

        #region Window Setup

        [MenuItem("Tools/Soph's Outfit Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarOutfitManagerWindow>();
            window.titleContent = new GUIContent("Outfit Manager", EditorGUIUtility.IconContent("ClothInspector.SettingsTool").image);
            window.minSize = new Vector2(450, 550);
            window.Show();
        }

        private void OnEnable()
        {
            // Check if first time user
            isFirstTimeUser = !EditorPrefs.HasKey("SophOutfitManager_NotFirstTime");
            if (isFirstTimeUser)
            {
                showWizard = true;
                currentWizardStep = WizardStep.Avatar;
            }
            
            // Try to load saved data if avatar is already assigned
            LoadSavedData();
        }
        
        private void OnDisable()
        {
            // Save current state when window closes
            SaveCurrentState();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized && headerStyle != null) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 10, 5)
            };

            wizardHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 20, 10)
            };

            slotCardStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 90,
                fixedWidth = 90,
                fontSize = 10,
                alignment = TextAnchor.LowerCenter,
                imagePosition = ImagePosition.ImageAbove,
                padding = new RectOffset(5, 5, 5, 5)
            };

            selectedSlotCardStyle = new GUIStyle(slotCardStyle)
            {
                fontStyle = FontStyle.Bold
            };

            dropAreaStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            centeredLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            stylesInitialized = true;
        }

        #endregion

        #region Main GUI

        private void OnGUI()
        {
            InitializeStyles();

            // Handle Drag & Drop anywhere in window
            HandleDragAndDrop();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (showWizard && avatarDescriptor == null)
            {
                DrawWizard();
            }
            else
            {
                DrawMainInterface();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawMainInterface()
        {
            DrawHeader();
            EditorGUILayout.Space(5);

            // Quick Setup Section (if not fully configured)
            if (avatarDescriptor == null || outfitRoot == null)
            {
                DrawQuickSetup();
            }
            else
            {
                // Compact config display
                DrawCompactConfig();
                EditorGUILayout.Space(10);

                // Visual Slot Grid
                DrawVisualSlotGrid();
                EditorGUILayout.Space(10);

                // Selected Slot Details
                DrawSelectedSlotDetails();
                EditorGUILayout.Space(10);

                // Actions
                DrawQuickActions();
                EditorGUILayout.Space(10);

                // Generate Section
                DrawGenerateSection();
            }

            EditorGUILayout.Space(20);
        }

        #endregion

        #region Wizard Mode

        private void DrawWizard()
        {
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.Space(10);

                switch (currentWizardStep)
                {
                    case WizardStep.Avatar:
                        DrawWizardStep1();
                        break;
                    case WizardStep.OutfitRoot:
                        DrawWizardStep2();
                        break;
                    case WizardStep.Ready:
                        DrawWizardStep3();
                        break;
                }

                EditorGUILayout.Space(10);
            }

            GUILayout.FlexibleSpace();

            // Skip wizard button
            EditorGUILayout.Space(10);
            if (GUILayout.Button("Skip Setup Wizard", EditorStyles.miniButton))
            {
                showWizard = false;
                MarkNotFirstTime();
            }
        }

        private void DrawWizardStep1()
        {
            EditorGUILayout.LabelField("Step 1 of 3", centeredLabelStyle);
            EditorGUILayout.LabelField("Select Your Avatar", wizardHeaderStyle);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Drag your avatar from the Hierarchy here:", centeredLabelStyle);
            EditorGUILayout.Space(10);

            // Drop Area
            Rect dropArea = GUILayoutUtility.GetRect(0, 80, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drop Avatar Here\nor click to select", dropAreaStyle);

            // Handle drop in this area
            if (dropArea.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        var go = obj as GameObject;
                        if (go != null)
                        {
                            var descriptor = go.GetComponent<VRCAvatarDescriptor>();
                            if (descriptor != null)
                            {
                                avatarDescriptor = descriptor;
                                currentWizardStep = WizardStep.OutfitRoot;
                                AutoDetectOutfitRoot();
                                Repaint();
                            }
                        }
                    }
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseDown)
                {
                    // Show object picker
                    EditorGUIUtility.ShowObjectPicker<VRCAvatarDescriptor>(null, true, "", 0);
                    Event.current.Use();
                }
            }

            // Handle object picker
            if (Event.current.commandName == "ObjectSelectorUpdated")
            {
                var selected = EditorGUIUtility.GetObjectPickerObject() as VRCAvatarDescriptor;
                if (selected != null)
                {
                    avatarDescriptor = selected;
                    currentWizardStep = WizardStep.OutfitRoot;
                    AutoDetectOutfitRoot();
                    Repaint();
                }
            }

            EditorGUILayout.Space(10);

            // Or select from scene
            EditorGUILayout.LabelField("Or select from scene:", centeredLabelStyle);
            var newDescriptor = EditorGUILayout.ObjectField(avatarDescriptor, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
            if (newDescriptor != null && newDescriptor != avatarDescriptor)
            {
                avatarDescriptor = newDescriptor;
                currentWizardStep = WizardStep.OutfitRoot;
                AutoDetectOutfitRoot();
            }
        }

        private void DrawWizardStep2()
        {
            EditorGUILayout.LabelField("Step 2 of 3", centeredLabelStyle);
            EditorGUILayout.LabelField("Outfit Folder", wizardHeaderStyle);
            EditorGUILayout.Space(10);

            if (outfitRoot != null)
            {
                EditorGUILayout.HelpBox(
                    $"Found: '{outfitRoot.name}' with {CountToggleableObjects(outfitRoot)} items",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Use This Folder", GUILayout.Height(35)))
                {
                    currentWizardStep = WizardStep.Ready;
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Choose Different Folder"))
                {
                    outfitRoot = null;
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No outfit folder found. You can create one or select manually.",
                    MessageType.Warning);

                EditorGUILayout.Space(10);

                if (GUILayout.Button("Create 'Outfits' Folder", GUILayout.Height(35)))
                {
                    CreateOutfitRootFolder();
                    if (outfitRoot != null)
                    {
                        currentWizardStep = WizardStep.Ready;
                    }
                }

                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Or select existing folder:", centeredLabelStyle);
                var newRoot = EditorGUILayout.ObjectField(outfitRoot, typeof(Transform), true) as Transform;
                if (newRoot != null)
                {
                    outfitRoot = newRoot;
                    currentWizardStep = WizardStep.Ready;
                }
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("< Back"))
            {
                currentWizardStep = WizardStep.Avatar;
            }
        }

        private void DrawWizardStep3()
        {
            EditorGUILayout.LabelField("Step 3 of 3", centeredLabelStyle);
            EditorGUILayout.LabelField("Ready!", wizardHeaderStyle);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                $"Avatar: {avatarDescriptor.gameObject.name}\n" +
                $"Outfit Folder: {outfitRoot.name}\n" +
                $"Toggleable Items: {CountToggleableObjects(outfitRoot)}",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Apply preset names option
            EditorGUILayout.LabelField("Would you like preset slot names?", centeredLabelStyle);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes, Apply Presets", GUILayout.Height(30)))
            {
                EnsureSlotData();
                ApplyPresetNames();
                FinishWizard();
            }
            if (GUILayout.Button("No, Custom Names", GUILayout.Height(30)))
            {
                EnsureSlotData();
                FinishWizard();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("< Back"))
            {
                currentWizardStep = WizardStep.OutfitRoot;
            }
        }

        private void FinishWizard()
        {
            showWizard = false;
            MarkNotFirstTime();
            Repaint();
        }

        private void MarkNotFirstTime()
        {
            EditorPrefs.SetBool("SophOutfitManager_NotFirstTime", true);
            isFirstTimeUser = false;
        }

        #endregion

        #region Quick Setup & Drag-Drop

        private void HandleDragAndDrop()
        {
            if (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform)
            {
                // Check if dragging a valid avatar
                bool hasValidAvatar = false;
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    var go = obj as GameObject;
                    if (go != null && go.GetComponent<VRCAvatarDescriptor>() != null)
                    {
                        hasValidAvatar = true;
                        break;
                    }
                }

                if (hasValidAvatar)
                {
                    if (Event.current.type == EventType.DragUpdated)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    }
                    else if (Event.current.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            var go = obj as GameObject;
                            if (go != null)
                            {
                                var descriptor = go.GetComponent<VRCAvatarDescriptor>();
                                if (descriptor != null)
                                {
                                    OneClickSetup(go);
                                    break;
                                }
                            }
                        }
                    }
                    Event.current.Use();
                }
            }
        }

        private void OneClickSetup(GameObject avatarGO)
        {
            avatarDescriptor = avatarGO.GetComponent<VRCAvatarDescriptor>();
            if (avatarDescriptor == null) return;

            // Auto-detect outfit root
            AutoDetectOutfitRoot();

            // If no outfit root found, create one
            if (outfitRoot == null)
            {
                CreateOutfitRootFolder();
            }

            // Try to load existing slot data, otherwise create new
            LoadSlotDataForAvatar();

            showWizard = false;
            MarkNotFirstTime();

            Debug.Log($"[Outfit Manager] One-Click Setup complete for: {avatarGO.name}");
        }

        private void DrawQuickSetup()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Quick Setup", headerStyle);
                EditorGUILayout.Space(5);

                // Drop area
                Rect dropArea = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "Drop Avatar Here", dropAreaStyle);

                EditorGUILayout.Space(10);

                // Manual fields
                var newAvatar = EditorGUILayout.ObjectField(Tips.Avatar, avatarDescriptor, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
                
                if (newAvatar != avatarDescriptor)
                {
                    avatarDescriptor = newAvatar;
                    OnAvatarChanged();
                }

                if (avatarDescriptor != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    outfitRoot = EditorGUILayout.ObjectField(Tips.OutfitRoot, outfitRoot, typeof(Transform), true) as Transform;
                    if (GUILayout.Button("Auto", GUILayout.Width(45)))
                    {
                        AutoDetectOutfitRoot();
                    }
                    if (GUILayout.Button("+", GUILayout.Width(25)))
                    {
                        CreateOutfitRootFolder();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Start Setup Wizard"))
                {
                    showWizard = true;
                    currentWizardStep = WizardStep.Avatar;
                }
            }
        }

        private void DrawCompactConfig()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Avatar: {avatarDescriptor.gameObject.name}", GUILayout.Width(150));
                EditorGUILayout.LabelField($"Outfits: {outfitRoot.name} ({CountToggleableObjects(outfitRoot)} items)");
                
                if (GUILayout.Button("Change", GUILayout.Width(60)))
                {
                    avatarDescriptor = null;
                    outfitRoot = null;
                }
            }
        }

        private void OnAvatarChanged()
        {
            // Auto-detect outfit root
            AutoDetectOutfitRoot();
            
            // Load existing slot data for this avatar
            LoadSlotDataForAvatar();
            
            // If no slot data found, ensure one exists
            if (slotData == null)
            {
                EnsureSlotData();
            }
            
            Repaint();
        }

        #endregion

        #region Visual Slot Grid

        private void DrawVisualSlotGrid()
        {
            EditorGUILayout.LabelField("Outfit Slots", headerStyle);

            // Preset button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Tips.ApplyPresets, GUILayout.Width(130)))
            {
                EnsureSlotData();
                ApplyPresetNames();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EnsureSlotData();

            // Draw 2x3 grid of slot cards
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                for (int row = 0; row < 2; row++)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    
                    for (int col = 0; col < 3; col++)
                    {
                        int slotIndex = row * 3 + col;
                        DrawSlotCard(slotIndex);
                        GUILayout.Space(10);
                    }
                    
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    
                    if (row < 1) GUILayout.Space(10);
                }
            }
        }

        private void DrawSlotCard(int slotIndex)
        {
            if (slotData == null || slotData.slots == null) return;

            var slot = slotData.slots[slotIndex];
            bool isSelected = slotIndex == selectedSlotIndex;
            bool isConfigured = slot != null && slot.isConfigured;

            // Card background color
            Color bgColor = isSelected ? new Color(0.3f, 0.6f, 0.9f, 0.5f) :
                           isConfigured ? new Color(0.3f, 0.8f, 0.3f, 0.3f) :
                           new Color(0.5f, 0.5f, 0.5f, 0.2f);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(100), GUILayout.Height(100)))
            {
                GUI.backgroundColor = oldBg;

                // Icon or placeholder
                Rect iconRect = GUILayoutUtility.GetRect(60, 50);
                iconRect.x += (100 - 60) / 2 - 10;

                Texture2D icon = slot?.GetIcon();
                if (icon != null)
                {
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.LabelField(iconRect, isConfigured ? "✓" : "?", new GUIStyle(EditorStyles.boldLabel) 
                    { 
                        fontSize = 24, 
                        alignment = TextAnchor.MiddleCenter 
                    });
                }

                // Slot name
                string displayName = string.IsNullOrEmpty(slot?.slotName) ? $"Slot {slotIndex}" : slot.slotName;
                if (displayName.Length > 10) displayName = displayName.Substring(0, 10) + "...";
                
                EditorGUILayout.LabelField(displayName, new GUIStyle(EditorStyles.miniLabel) 
                { 
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
                });

                // Status
                string status = isConfigured ? "Configured" : "Empty";
                EditorGUILayout.LabelField(status, new GUIStyle(EditorStyles.miniLabel) 
                { 
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = isConfigured ? Color.green : Color.gray }
                });
            }

            // Handle click
            Rect cardRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
            {
                selectedSlotIndex = slotIndex;
                Event.current.Use();
                Repaint();
            }
        }

        #endregion

        #region Selected Slot Details

        private void DrawSelectedSlotDetails()
        {
            if (slotData == null || slotData.slots == null) return;

            var currentSlot = slotData.slots[selectedSlotIndex];
            if (currentSlot == null) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Slot {selectedSlotIndex} Settings", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                // Slot name with tooltip
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Name:", GUILayout.Width(50));
                string newName = EditorGUILayout.TextField(currentSlot.slotName);
                if (newName != currentSlot.slotName)
                {
                    Undo.RecordObject(slotData, "Change Slot Name");
                    currentSlot.slotName = newName;
                    EditorUtility.SetDirty(slotData);
                }
                EditorGUILayout.EndHorizontal();

                // Status
                EditorGUILayout.LabelField(currentSlot.isConfigured ? 
                    $"Status: Configured ({currentSlot.objectStates?.Count ?? 0} objects)" : 
                    "Status: Not configured");

                EditorGUILayout.Space(10);

                // Action buttons
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button(Tips.SaveSlot, GUILayout.Height(30)))
                {
                    SaveCurrentVisibilityToSlot();
                }

                GUI.backgroundColor = new Color(0.4f, 0.6f, 0.9f);
                GUI.enabled = currentSlot.isConfigured;
                if (GUILayout.Button(Tips.LoadSlot, GUILayout.Height(30)))
                {
                    LoadSlotInEditor();
                }
                GUI.enabled = true;
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // Icon rendering
                EditorGUILayout.BeginHorizontal();
                
                GUI.enabled = currentSlot.isConfigured;
                if (GUILayout.Button(Tips.RenderIcon, GUILayout.Height(25)))
                {
                    RenderSlotIcon(selectedSlotIndex);
                }
                GUI.enabled = true;

                GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
                if (GUILayout.Button("Clear Slot", GUILayout.Height(25)))
                {
                    ClearSlot();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawQuickActions()
        {
            showIconSettings = EditorGUILayout.Foldout(showIconSettings, "Icon Camera Settings", true);
            if (showIconSettings)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    selectedCameraPreset = EditorGUILayout.Popup("Preset", selectedCameraPreset, cameraPresetNames);
                    if (GUI.changed)
                    {
                        ApplyCameraPreset(selectedCameraPreset);
                    }

                    iconCameraSettings.cameraDistance = EditorGUILayout.Slider("Distance", iconCameraSettings.cameraDistance, 0.3f, 5f);
                    iconCameraSettings.cameraHeightOffset = EditorGUILayout.Slider("Height", iconCameraSettings.cameraHeightOffset, -1f, 1f);
                    iconCameraSettings.fieldOfView = EditorGUILayout.Slider("FOV", iconCameraSettings.fieldOfView, 10f, 90f);

                    EditorGUILayout.Space(5);

                    GUI.enabled = slotData != null && slotData.GetConfiguredSlotCount() > 0;
                    if (GUILayout.Button("Render All Icons"))
                    {
                        RenderAllIcons();
                    }
                    GUI.enabled = true;
                }
            }
        }

        #endregion

        #region Generate Section

        private void DrawGenerateSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Generate VRChat Assets", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                int configuredCount = slotData?.GetConfiguredSlotCount() ?? 0;
                EditorGUILayout.LabelField($"Configured: {configuredCount} / {OutfitSlotData.SLOT_COUNT} slots");

                EditorGUILayout.Space(5);

                outputFolderPath = EditorGUILayout.TextField(Tips.OutputFolder, outputFolderPath);

                EditorGUILayout.Space(5);

                GUI.enabled = configuredCount > 0;
                GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f);

                if (GUILayout.Button(Tips.Generate, GUILayout.Height(40)))
                {
                    GenerateVRChatAssets();
                }

                GUI.backgroundColor = Color.white;
                GUI.enabled = true;

                if (configuredCount == 0)
                {
                    EditorGUILayout.HelpBox("Save at least one outfit to generate VRChat assets.", MessageType.Info);
                }
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Soph's Outfit Manager", headerStyle);
        }

        #endregion

        #region Core Functionality

        private void EnsureSlotData()
        {
            if (slotData != null)
            {
                // Update avatar reference if needed
                if (avatarDescriptor != null)
                {
                    string avatarGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(avatarDescriptor.gameObject));
                    if (string.IsNullOrEmpty(slotData.avatarGuid) || slotData.avatarGuid != avatarGuid)
                    {
                        slotData.avatarGuid = avatarGuid;
                        if (outfitRoot != null)
                        {
                            slotData.outfitRootPath = VRChatAssetGenerator.GetRelativePath(avatarDescriptor.transform, outfitRoot);
                        }
                        EditorUtility.SetDirty(slotData);
                    }
                }
                return;
            }

            // Try to load existing slot data first
            LoadSlotDataForAvatar();
            
            // If still no slot data, create new
            if (slotData == null)
            {
                string folderPath = "Assets/AvatarOutfitManager";
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string avatarName = avatarDescriptor != null ? avatarDescriptor.gameObject.name : "Avatar";
                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/OutfitData_{avatarName}.asset");

                slotData = ScriptableObject.CreateInstance<OutfitSlotData>();
                slotData.InitializeSlots();
                
                // Store avatar reference
                if (avatarDescriptor != null)
                {
                    slotData.avatarGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(avatarDescriptor.gameObject));
                    if (outfitRoot != null)
                    {
                        slotData.outfitRootPath = VRChatAssetGenerator.GetRelativePath(avatarDescriptor.transform, outfitRoot);
                    }
                }

                AssetDatabase.CreateAsset(slotData, assetPath);
                AssetDatabase.SaveAssets();
            }
        }
        
        private void LoadSlotDataForAvatar()
        {
            if (avatarDescriptor == null) return;
            
            string avatarGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(avatarDescriptor.gameObject));
            if (string.IsNullOrEmpty(avatarGuid)) return;
            
            // Search for existing slot data
            string[] guids = AssetDatabase.FindAssets("t:OutfitSlotData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<OutfitSlotData>(path);
                
                if (data != null && data.avatarGuid == avatarGuid)
                {
                    slotData = data;
                    
                    // Try to restore outfit root if path is stored
                    if (!string.IsNullOrEmpty(data.outfitRootPath) && avatarDescriptor != null)
                    {
                        Transform foundRoot = FindTransformByPath(avatarDescriptor.transform, data.outfitRootPath);
                        if (foundRoot != null)
                        {
                            outfitRoot = foundRoot;
                        }
                    }
                    
                    Debug.Log($"[Outfit Manager] Loaded existing slot data: {path}");
                    return;
                }
            }
            
            // Also try to find by avatar name (fallback for old data)
            string avatarName = avatarDescriptor.gameObject.name;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains($"OutfitData_{avatarName}"))
                {
                    var data = AssetDatabase.LoadAssetAtPath<OutfitSlotData>(path);
                    if (data != null)
                    {
                        slotData = data;
                        // Update GUID for future lookups
                        slotData.avatarGuid = avatarGuid;
                        if (outfitRoot != null)
                        {
                            slotData.outfitRootPath = VRChatAssetGenerator.GetRelativePath(avatarDescriptor.transform, outfitRoot);
                        }
                        EditorUtility.SetDirty(slotData);
                        AssetDatabase.SaveAssets();
                        Debug.Log($"[Outfit Manager] Loaded slot data by name and updated GUID: {path}");
                        return;
                    }
                }
            }
        }
        
        private Transform FindTransformByPath(Transform root, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return root;
        
        string[] parts = relativePath.Split('/');
        Transform current = root;
        
        foreach (string part in parts)
        {
            if (current == null) return null;
            
            Transform found = null;
            foreach (Transform child in current)
            {
                if (child.name == part)
                {
                    found = child;
                    break;
                }
            }
            
            if (found == null) return null;
            current = found;
        }
        
        return current;
    }
    
    private void LoadSavedData()
    {
        // Try to restore last used avatar from EditorPrefs
        string lastAvatarGuid = EditorPrefs.GetString("SophOutfitManager_LastAvatarGuid", "");
        if (!string.IsNullOrEmpty(lastAvatarGuid))
        {
            string avatarPath = AssetDatabase.GUIDToAssetPath(lastAvatarGuid);
            if (!string.IsNullOrEmpty(avatarPath))
            {
                var avatarGO = AssetDatabase.LoadAssetAtPath<GameObject>(avatarPath);
                if (avatarGO != null)
                {
                    var descriptor = avatarGO.GetComponent<VRCAvatarDescriptor>();
                    if (descriptor != null)
                    {
                        avatarDescriptor = descriptor;
                        AutoDetectOutfitRoot();
                        LoadSlotDataForAvatar();
                    }
                }
            }
        }
    }
    
    private void SaveCurrentState()
    {
        // Save avatar reference for next time
        if (avatarDescriptor != null)
        {
            string avatarGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(avatarDescriptor.gameObject));
            if (!string.IsNullOrEmpty(avatarGuid))
            {
                EditorPrefs.SetString("SophOutfitManager_LastAvatarGuid", avatarGuid);
            }
        }
        
        // Save slot data references
        if (slotData != null && avatarDescriptor != null)
        {
            string avatarGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(avatarDescriptor.gameObject));
            if (!string.IsNullOrEmpty(avatarGuid))
            {
                slotData.avatarGuid = avatarGuid;
                if (outfitRoot != null)
                {
                    slotData.outfitRootPath = VRChatAssetGenerator.GetRelativePath(avatarDescriptor.transform, outfitRoot);
                }
                EditorUtility.SetDirty(slotData);
                AssetDatabase.SaveAssets();
            }
        }
    }

        private void ApplyPresetNames()
        {
            if (slotData == null) return;

            Undo.RecordObject(slotData, "Apply Preset Names");

            for (int i = 0; i < OutfitSlotData.SLOT_COUNT && i < PresetSlotNames.Length; i++)
            {
                slotData.slots[i].slotName = PresetSlotNames[i];
            }

            EditorUtility.SetDirty(slotData);
            AssetDatabase.SaveAssets();
        }

        private void SaveCurrentVisibilityToSlot()
        {
            if (outfitRoot == null) return;

            EnsureSlotData();

            var currentSlot = slotData.slots[selectedSlotIndex];
            Undo.RecordObject(slotData, "Save Outfit to Slot");

            currentSlot.objectStates.Clear();
            CollectObjectStates(outfitRoot, outfitRoot, currentSlot.objectStates);
            currentSlot.isConfigured = true;
            
            // Update avatar reference
            if (avatarDescriptor != null)
            {
                slotData.avatarGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(avatarDescriptor.gameObject));
                slotData.outfitRootPath = VRChatAssetGenerator.GetRelativePath(avatarDescriptor.transform, outfitRoot);
            }

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
                CollectObjectStates(root, child, states);
            }
        }

        private void LoadSlotInEditor()
        {
            if (slotData == null || outfitRoot == null) return;

            var currentSlot = slotData.slots[selectedSlotIndex];
            if (!currentSlot.isConfigured) return;

            var pathToObject = new Dictionary<string, Transform>();
            CollectPathMappings(outfitRoot, outfitRoot, pathToObject);

            foreach (var state in currentSlot.objectStates)
            {
                if (pathToObject.TryGetValue(state.path, out Transform obj))
                {
                    Undo.RecordObject(obj.gameObject, "Load Outfit Slot");
                    obj.gameObject.SetActive(state.isActive);
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

            if (!EditorUtility.DisplayDialog("Clear Slot", $"Clear slot {selectedSlotIndex}?", "Yes", "No"))
                return;

            Undo.RecordObject(slotData, "Clear Outfit Slot");
            slotData.slots[selectedSlotIndex].Clear();
            EditorUtility.SetDirty(slotData);
            AssetDatabase.SaveAssets();
        }

        private void GenerateVRChatAssets()
        {
            if (avatarDescriptor == null || outfitRoot == null || slotData == null) return;
            if (slotData.GetConfiguredSlotCount() == 0) return;

            VRChatAssetGenerator.GenerateAllAssets(avatarDescriptor, outfitRoot, slotData, outputFolderPath);
        }

        #endregion

        #region Icon Rendering

        private void ApplyCameraPreset(int preset)
        {
            iconCameraSettings = preset switch
            {
                1 => IconCameraSettings.Portrait(),
                2 => IconCameraSettings.FullBody(),
                3 => IconCameraSettings.ThreeQuarter(),
                _ => iconCameraSettings
            };
        }

        private void RenderSlotIcon(int slotIndex)
        {
            if (slotData == null || avatarDescriptor == null || outfitRoot == null) return;

            var slot = slotData.slots[slotIndex];
            if (!slot.isConfigured) return;

            var originalStates = StoreCurrentVisibility();

            try
            {
                OutfitIconRenderer.RenderSlotIcon(
                    avatarDescriptor.transform,
                    outfitRoot,
                    slot,
                    slotIndex,
                    outputFolderPath,
                    iconCameraSettings);

                EditorUtility.SetDirty(slotData);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                RestoreVisibility(originalStates);
            }

            Repaint();
        }

        private void RenderAllIcons()
        {
            if (slotData == null || avatarDescriptor == null || outfitRoot == null) return;

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
                    kvp.Key.gameObject.SetActive(kvp.Value);
            }
        }

        #endregion

        #region Auto-Detection

        private void AutoDetectOutfitRoot()
        {
            if (avatarDescriptor == null) return;

            // Look for common folder names
            foreach (string folderName in OutfitFolderNames)
            {
                foreach (Transform child in avatarDescriptor.transform)
                {
                    if (child.name.Equals(folderName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        outfitRoot = child;
                        return;
                    }
                }
            }

            // Find folder with most toggleables
            Transform bestCandidate = null;
            int maxToggleables = 0;

            foreach (Transform child in avatarDescriptor.transform)
            {
                bool isExcluded = false;
                foreach (string excluded in ExcludedNames)
                {
                    if (child.name.IndexOf(excluded, System.StringComparison.OrdinalIgnoreCase) >= 0)
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
            }
        }

        private void CreateOutfitRootFolder()
        {
            if (avatarDescriptor == null) return;

            // Check existing
            foreach (Transform child in avatarDescriptor.transform)
            {
                if (child.name.Equals("Outfits", System.StringComparison.OrdinalIgnoreCase))
                {
                    outfitRoot = child;
                    return;
                }
            }

            // Create new
            GameObject outfitFolder = new GameObject("Outfits");
            outfitFolder.transform.SetParent(avatarDescriptor.transform);
            outfitFolder.transform.localPosition = Vector3.zero;
            outfitFolder.transform.localRotation = Quaternion.identity;
            outfitFolder.transform.localScale = Vector3.one;

            Undo.RegisterCreatedObjectUndo(outfitFolder, "Create Outfit Folder");
            outfitRoot = outfitFolder.transform;

            EditorUtility.DisplayDialog(
                "Folder Created",
                "Created 'Outfits' folder. Drag your clothing items into it!",
                "OK");
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
                if (child.GetComponent<Renderer>() != null || child.childCount > 0)
                {
                    count++;
                }
                CountToggleablesRecursive(child, ref count);
            }
        }

        #endregion
    }
}
