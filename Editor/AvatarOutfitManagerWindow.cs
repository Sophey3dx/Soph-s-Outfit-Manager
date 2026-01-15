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

        // Auto-detect folder names (Soph Outfit Manager has priority)
        private static readonly string[] OutfitFolderNames = 
        {
            "Soph Outfit Manager",  // Priority: Our own GameObject first
            "Clothing", "Clothes", "Outfits", "Outfit", "Accessories",
            "Toggles", "Toggle", "Wearables", "Apparel", "Garments"
        };

        private static readonly string[] ExcludedNames = 
        {
            "Armature", "Body", "Head", "Hair", "Eyes", "Teeth", "Tongue"
        };

        private static readonly string[] ExcludedSystemKeywords =
        {
            "sps",
            "gogoloco",
            "gesturemanager",
            "vrcfury"
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
            EditorGUILayout.LabelField("Soph Outfit Manager GameObject", wizardHeaderStyle);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Objects stay where they are! The Outfit Manager will collect all clothing/accessory objects directly from your avatar hierarchy.\n\n" +
                "You can optionally organize them under a folder, but it's not required.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (outfitRoot != null)
            {
                EditorGUILayout.HelpBox(
                    $"Optional Outfit Root: '{outfitRoot.name}' (for organization only)",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Continue (Use This Folder)", GUILayout.Height(35)))
                {
                    currentWizardStep = WizardStep.Ready;
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Don't Use Folder (Objects stay where they are)"))
                {
                    outfitRoot = null;
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
                if (GUILayout.Button("Continue (Objects stay where they are)", GUILayout.Height(35)))
                {
                    currentWizardStep = WizardStep.Ready;
                }

                EditorGUILayout.Space(10);

                EditorGUILayout.LabelField("Optional: Create organization folder", EditorStyles.miniLabel);
                if (GUILayout.Button("Create 'Soph Outfit Manager' GameObject", GUILayout.Height(30)))
                {
                    CreateSophOutfitManagerGameObject();
                    if (outfitRoot != null)
                    {
                        currentWizardStep = WizardStep.Ready;
                    }
                }

                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Or select existing GameObject:", centeredLabelStyle);
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

            // Always ensure "Soph Outfit Manager" GameObject exists (like GoGo Loco or SPS)
            CreateSophOutfitManagerGameObject();

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
                        CreateSophOutfitManagerGameObject();
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
                if (outfitRoot != null)
                {
                    EditorGUILayout.LabelField($"Outfit Root (optional): {outfitRoot.name}");
                }
                else
                {
                    EditorGUILayout.LabelField("Outfit Root: Avatar Root (objects stay where they are)");
                }
                
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

                EditorGUILayout.Space(10);

                // Tracked Objects Section
                DrawTrackedObjectsSection(currentSlot);
            }
        }

        private Vector2 trackedObjectsScrollPosition;
        private GameObject objectToAdd;
        private string objectSearchFilter = "";

        private void DrawTrackedObjectsSection(OutfitSlot slot)
        {
            EditorGUILayout.LabelField("Tracked Objects", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Ensure trackedObjectPaths is initialized
            if (slot.trackedObjectPaths == null)
            {
                slot.trackedObjectPaths = new List<string>();
            }

            // Add Object Section
            EditorGUILayout.BeginHorizontal();
            
            // Object Field for drag & drop
            objectToAdd = EditorGUILayout.ObjectField("Add Object", objectToAdd, typeof(GameObject), true) as GameObject;
            
            if (objectToAdd != null)
            {
                if (IsObjectUnderAvatar(objectToAdd.transform))
                {
                    AddTrackedObject(slot, objectToAdd.transform);
                    objectToAdd = null;
                    GUI.FocusControl(null);
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", 
                        "The selected object is not under the avatar hierarchy. Please select an object that is a child of the avatar.", 
                        "OK");
                    objectToAdd = null;
                }
            }

            // Browse Button
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                ShowObjectPicker(slot);
            }

            EditorGUILayout.EndHorizontal();

            // Search Field
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            objectSearchFilter = EditorGUILayout.TextField(objectSearchFilter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Auto-Detect Button
            if (GUILayout.Button("Auto-Detect Objects", GUILayout.Height(25)))
            {
                AutoDetectTrackedObjects(slot);
            }

            EditorGUILayout.Space(5);

            // Tracked Objects List
            if (slot.trackedObjectPaths.Count == 0)
            {
                EditorGUILayout.HelpBox("No objects tracked. Add objects manually or use 'Auto-Detect' to find outfit objects automatically.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Tracked Objects ({slot.trackedObjectPaths.Count}):", EditorStyles.miniLabel);
                
                trackedObjectsScrollPosition = EditorGUILayout.BeginScrollView(trackedObjectsScrollPosition, GUILayout.Height(150));
                
                List<string> pathsToRemove = new List<string>();
                
                foreach (string path in slot.trackedObjectPaths)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    
                    // Find the object
                    Transform obj = FindObjectByPath(path);
                    if (obj != null)
                    {
                        // Checkbox for active state (for preview)
                        bool isActive = obj.gameObject.activeSelf;
                        bool newActive = EditorGUILayout.Toggle(isActive, GUILayout.Width(20));
                        if (newActive != isActive)
                        {
                            Undo.RecordObject(obj.gameObject, "Toggle Object");
                            obj.gameObject.SetActive(newActive);
                        }
                        
                        // Object name and path
                        string displayName = obj.name;
                        if (!string.IsNullOrEmpty(objectSearchFilter) && 
                            !displayName.ToLowerInvariant().Contains(objectSearchFilter.ToLowerInvariant()) &&
                            !path.ToLowerInvariant().Contains(objectSearchFilter.ToLowerInvariant()))
                        {
                            EditorGUILayout.EndHorizontal();
                            continue;
                        }
                        
                        EditorGUILayout.LabelField(displayName, GUILayout.Width(150));
                        EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                        
                        // Remove button
                        if (GUILayout.Button("×", GUILayout.Width(25)))
                        {
                            pathsToRemove.Add(path);
                        }
                    }
                    else
                    {
                        // Object not found - show warning
                        EditorGUILayout.LabelField($"Missing: {path}", new GUIStyle(EditorStyles.miniLabel) 
                        { 
                            normal = { textColor = Color.red } 
                        });
                        
                        if (GUILayout.Button("×", GUILayout.Width(25)))
                        {
                            pathsToRemove.Add(path);
                        }
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                // Remove marked paths
                if (pathsToRemove.Count > 0)
                {
                    Undo.RecordObject(slotData, "Remove Tracked Objects");
                    foreach (string path in pathsToRemove)
                    {
                        slot.trackedObjectPaths.Remove(path);
                    }
                    EditorUtility.SetDirty(slotData);
                }
                
                EditorGUILayout.EndScrollView();
            }
        }

        private bool IsObjectUnderAvatar(Transform obj)
        {
            if (avatarDescriptor == null) return false;
            
            Transform current = obj;
            while (current != null)
            {
                if (current == avatarDescriptor.transform)
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        private void AddTrackedObject(OutfitSlot slot, Transform obj)
        {
            if (avatarDescriptor == null) return;
            
            string relativePath = VRChatAssetGenerator.GetRelativePath(avatarDescriptor.transform, obj);
            
            if (slot.trackedObjectPaths == null)
            {
                slot.trackedObjectPaths = new List<string>();
            }
            
            if (!slot.trackedObjectPaths.Contains(relativePath))
            {
                Undo.RecordObject(slotData, "Add Tracked Object");
                slot.trackedObjectPaths.Add(relativePath);
                EditorUtility.SetDirty(slotData);
                Debug.Log($"[Outfit Manager] Added tracked object: '{relativePath}'");
            }
        }

        private void ShowObjectPicker(OutfitSlot slot)
        {
            if (avatarDescriptor == null)
            {
                EditorUtility.DisplayDialog("Error", "No avatar selected. Please select an avatar first.", "OK");
                return;
            }

            // Create a simple object picker window
            GenericMenu menu = new GenericMenu();
            
            // Collect all objects under avatar
            List<Transform> allObjects = new List<Transform>();
            CollectAllTransforms(avatarDescriptor.transform, allObjects);
            
            foreach (Transform obj in allObjects)
            {
                string path = VRChatAssetGenerator.GetRelativePath(avatarDescriptor.transform, obj);
                bool isAlreadyTracked = slot.trackedObjectPaths != null && slot.trackedObjectPaths.Contains(path);
                
                if (isAlreadyTracked)
                {
                    menu.AddDisabledItem(new GUIContent(path));
                }
                else
                {
                    menu.AddItem(new GUIContent(path), false, () => {
                        AddTrackedObject(slot, obj);
                    });
                }
            }
            
            menu.ShowAsContext();
        }

        private void CollectAllTransforms(Transform root, List<Transform> result)
        {
            result.Add(root);
            foreach (Transform child in root)
            {
                CollectAllTransforms(child, result);
            }
        }

        private void AutoDetectTrackedObjects(OutfitSlot slot)
        {
            if (avatarDescriptor == null) return;
            
            Undo.RecordObject(slotData, "Auto-Detect Tracked Objects");
            
            if (slot.trackedObjectPaths == null)
            {
                slot.trackedObjectPaths = new List<string>();
            }
            
            slot.trackedObjectPaths.Clear();
            
            Transform avatarRoot = avatarDescriptor.transform;
            List<Transform> detectedObjects = new List<Transform>();
            CollectLikelyOutfitObjects(avatarRoot, avatarRoot, detectedObjects);
            
            foreach (Transform obj in detectedObjects)
            {
                string relativePath = VRChatAssetGenerator.GetRelativePath(avatarRoot, obj);
                if (!slot.trackedObjectPaths.Contains(relativePath))
                {
                    slot.trackedObjectPaths.Add(relativePath);
                }
            }
            
            EditorUtility.SetDirty(slotData);
            Debug.Log($"[Outfit Manager] Auto-detected {slot.trackedObjectPaths.Count} objects for slot {selectedSlotIndex}");
        }

        private void CollectLikelyOutfitObjects(Transform root, Transform current, List<Transform> result)
        {
            foreach (Transform child in current)
            {
                if (ShouldSkipCapture(child))
                {
                    CollectLikelyOutfitObjects(root, child, result);
                    continue;
                }
                
                if (IsLikelyOutfitObject(child))
                {
                    result.Add(child);
                }
                
                CollectLikelyOutfitObjects(root, child, result);
            }
        }

        private Transform FindObjectByPath(string relativePath)
        {
            if (avatarDescriptor == null || string.IsNullOrEmpty(relativePath)) return null;
            
            string[] pathParts = relativePath.Split('/');
            Transform current = avatarDescriptor.transform;
            
            foreach (string part in pathParts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                
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
                
                // Update component if it exists
                UpdateComponentSlotData();
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
                
                // Update component if it exists
                UpdateComponentSlotData();
            }
        }
        
        private void MigrateTrackedObjectsFromStates(OutfitSlot slot)
        {
            if (slot.objectStates == null || slot.objectStates.Count == 0) return;
            
            if (slot.trackedObjectPaths == null)
            {
                slot.trackedObjectPaths = new List<string>();
            }
            
            // Migrate: Extract paths from objectStates
            foreach (var state in slot.objectStates)
            {
                if (!string.IsNullOrEmpty(state.path) && !slot.trackedObjectPaths.Contains(state.path))
                {
                    slot.trackedObjectPaths.Add(state.path);
                }
            }
            
            EditorUtility.SetDirty(slotData);
            Debug.Log($"[Outfit Manager] Migrated {slot.trackedObjectPaths.Count} tracked objects from objectStates for slot");
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
                    
                    // Migration: Fill trackedObjectPaths from objectStates if empty
                    if (slotData.slots != null)
                    {
                        bool migrated = false;
                        foreach (var slot in slotData.slots)
                        {
                            if (slot != null && 
                                (slot.trackedObjectPaths == null || slot.trackedObjectPaths.Count == 0) &&
                                slot.objectStates != null && slot.objectStates.Count > 0)
                            {
                                MigrateTrackedObjectsFromStates(slot);
                                migrated = true;
                            }
                        }
                        if (migrated)
                        {
                            EditorUtility.SetDirty(slotData);
                            AssetDatabase.SaveAssets();
                        }
                    }
                    
                    // Try to restore outfit root if path is stored
                    if (!string.IsNullOrEmpty(data.outfitRootPath) && avatarDescriptor != null)
                    {
                        Transform foundRoot = FindTransformByPath(avatarDescriptor.transform, data.outfitRootPath);
                        if (foundRoot != null)
                        {
                            outfitRoot = foundRoot;
                        }
                    }
                    
                    // Update component if it exists
                    UpdateComponentSlotData();
                    
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
                        
                        // Migration: Fill trackedObjectPaths from objectStates if empty
                        if (slotData.slots != null)
                        {
                            bool migrated = false;
                            foreach (var slot in slotData.slots)
                            {
                                if (slot != null && 
                                    (slot.trackedObjectPaths == null || slot.trackedObjectPaths.Count == 0) &&
                                    slot.objectStates != null && slot.objectStates.Count > 0)
                                {
                                    MigrateTrackedObjectsFromStates(slot);
                                    migrated = true;
                                }
                            }
                            if (migrated)
                            {
                                EditorUtility.SetDirty(slotData);
                            }
                        }
                        
                        // Update GUID for future lookups
                        slotData.avatarGuid = avatarGuid;
                        if (outfitRoot != null)
                        {
                            slotData.outfitRootPath = VRChatAssetGenerator.GetRelativePath(avatarDescriptor.transform, outfitRoot);
                        }
                        EditorUtility.SetDirty(slotData);
                        AssetDatabase.SaveAssets();
                        
                        // Update component if it exists
                        UpdateComponentSlotData();
                        
                        Debug.Log($"[Outfit Manager] Loaded slot data by name and updated GUID: {path}");
                        return;
                    }
                }
            }
        }

        private void UpdateComponentSlotData()
        {
            if (avatarDescriptor == null) return;
            
            // Find OutfitManagerComponent in hierarchy
            OutfitManagerComponent component = avatarDescriptor.GetComponentInChildren<OutfitManagerComponent>();
            if (component != null)
            {
                component.SlotData = slotData;
                if (outfitRoot != null)
                {
                    component.OutfitRoot = outfitRoot;
                }
                EditorUtility.SetDirty(component);
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
            if (avatarDescriptor == null)
            {
                EditorUtility.DisplayDialog("Error", "No avatar selected. Please select an avatar first.", "OK");
                return;
            }

            EnsureSlotData();

            var currentSlot = slotData.slots[selectedSlotIndex];
            Undo.RecordObject(slotData, "Save Outfit to Slot");

            // Ensure trackedObjectPaths is initialized
            if (currentSlot.trackedObjectPaths == null)
            {
                currentSlot.trackedObjectPaths = new List<string>();
            }

            // Check if there are tracked objects
            if (currentSlot.trackedObjectPaths.Count == 0)
            {
                bool autoDetect = EditorUtility.DisplayDialog("No Tracked Objects", 
                    "No objects are tracked for this slot. Would you like to auto-detect outfit objects?\n\n" +
                    "Click 'Yes' to auto-detect, or 'No' to manually add objects first.", 
                    "Yes", "No");
                
                if (autoDetect)
                {
                    AutoDetectTrackedObjects(currentSlot);
                }
                else
                {
                    return;
                }
            }

            currentSlot.objectStates.Clear();
            
            // Only save states for tracked objects
            Transform avatarRoot = avatarDescriptor.transform;
            int savedCount = 0;
            int missingCount = 0;
            
            foreach (string path in currentSlot.trackedObjectPaths)
            {
                Transform obj = FindObjectByPath(path);
                if (obj != null)
                {
                    currentSlot.objectStates.Add(new GameObjectState
                    {
                        path = path,
                        isActive = obj.gameObject.activeSelf
                    });
                    savedCount++;
                }
                else
                {
                    Debug.LogWarning($"[Outfit Manager] Tracked object not found: '{path}'");
                    missingCount++;
                }
            }
            
            currentSlot.isConfigured = true;
            
            // Warn if objects are missing
            if (missingCount > 0)
            {
                EditorUtility.DisplayDialog("Warning", 
                    $"{missingCount} tracked object(s) could not be found. They may have been deleted or moved.\n\n" +
                    $"Saved {savedCount} object state(s).", 
                    "OK");
            }
            
            if (savedCount == 0)
            {
                EditorUtility.DisplayDialog("Warning", 
                    "No objects were saved. Make sure tracked objects exist and are under the avatar hierarchy.", 
                    "OK");
                currentSlot.isConfigured = false;
            }
            else
            {
                Debug.Log($"[Outfit Manager] Saved {savedCount} tracked objects to slot {selectedSlotIndex} (Slot name: '{currentSlot.slotName}')");
            }
            
            // Update avatar reference
            if (avatarDescriptor != null)
            {
                slotData.avatarGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(avatarDescriptor.gameObject));
                // Outfit root path is now optional - store empty if not set
                if (outfitRoot != null && outfitRoot != avatarRoot)
                {
                    slotData.outfitRootPath = VRChatAssetGenerator.GetRelativePath(avatarRoot, outfitRoot);
                }
                else
                {
                    slotData.outfitRootPath = string.Empty;
                }
            }

            EditorUtility.SetDirty(slotData);
            AssetDatabase.SaveAssets();
            
            // Update component if it exists
            UpdateComponentSlotData();
        }

        private void CollectObjectStates(Transform root, Transform current, List<GameObjectState> states)
        {
            foreach (Transform child in current)
            {
                if (ShouldSkipCapture(child))
                {
                    // Skip this object but continue recursing (might have valid children)
                    CollectObjectStates(root, child, states);
                    continue;
                }
                
                // Only capture objects that look like clothing/accessories (have toggleable children or are in clothing folders)
                // Skip body parts, armature, etc.
                bool isLikelyOutfitObject = IsLikelyOutfitObject(child);
                
                if (isLikelyOutfitObject)
                {
                    string relativePath = VRChatAssetGenerator.GetRelativePath(root, child);
                    states.Add(new GameObjectState
                    {
                        path = relativePath,
                        isActive = child.gameObject.activeSelf
                    });
                    Debug.Log($"[Outfit Manager] Captured outfit object: '{relativePath}' (Active: {child.gameObject.activeSelf})");
                }
                
                // Always recurse to find nested outfit objects
                CollectObjectStates(root, child, states);
            }
        }

        private bool IsLikelyOutfitObject(Transform obj)
        {
            if (obj == null) return false;
            
            string nameLower = obj.name.ToLowerInvariant();
            
            // Skip body parts, armature, bones, etc.
            if (nameLower.Contains("armature") || 
                nameLower.Contains("body") || 
                nameLower.Contains("head") || 
                nameLower == "hair" || 
                nameLower.Contains("eye") ||
                nameLower.Contains("teeth") ||
                nameLower.Contains("tongue") ||
                nameLower.StartsWith("!!body") ||
                nameLower.StartsWith("!!head"))
            {
                return false;
            }
            
            // Include objects with clothing-related prefixes (A-, B-, H-, S-, etc.)
            if (obj.name.Contains("-") || 
                nameLower.Contains("clothing") ||
                nameLower.Contains("clothes") ||
                nameLower.Contains("outfit") ||
                nameLower.Contains("accessory") ||
                nameLower.Contains("wearable") ||
                nameLower.Contains("hair") ||
                nameLower.Contains("shoes") ||
                nameLower.Contains("shirt") ||
                nameLower.Contains("pants") ||
                nameLower.Contains("skirt") ||
                nameLower.Contains("glasses") ||
                nameLower.Contains("hat") ||
                nameLower.Contains("jewelry") ||
                nameLower.Contains("necklace") ||
                nameLower.Contains("bracelet") ||
                nameLower.Contains("ring") ||
                nameLower.Contains("garter") ||
                nameLower.Contains("piercing") ||
                nameLower.Contains("collar") ||
                nameLower.Contains("choker"))
            {
                return true;
            }
            
            // Include if it has children that look like outfit objects (e.g., folders like "A-Clothing")
            // This catches folder structures
            if (obj.childCount > 0)
            {
                foreach (Transform child in obj)
                {
                    string childNameLower = child.name.ToLowerInvariant();
                    if (childNameLower.Contains("clothing") ||
                        childNameLower.Contains("clothes") ||
                        childNameLower.Contains("accessory") ||
                        child.name.Contains("-"))
                    {
                        return true;
                    }
                }
            }
            
            // Default: include if it's a direct child of avatar (likely clothing folder)
            if (obj.parent != null && obj.parent == avatarDescriptor?.transform)
            {
                // Exclude system objects (already handled by ShouldSkipCapture)
                return true;
            }
            
            // Include if parent is likely an outfit folder
            Transform parent = obj.parent;
            if (parent != null && parent != avatarDescriptor?.transform)
            {
                string parentNameLower = parent.name.ToLowerInvariant();
                if (parentNameLower.Contains("clothing") ||
                    parentNameLower.Contains("clothes") ||
                    parentNameLower.Contains("outfit") ||
                    parentNameLower.Contains("accessory") ||
                    parent.name.Contains("-"))
                {
                    return true;
                }
            }
            
            return false;
        }

        private bool ShouldSkipCapture(Transform target)
        {
            if (target == null) return true;

            // Skip editor-only objects
            if (target.CompareTag("EditorOnly")) return true;

            // Skip hidden or non-editable objects
            if ((target.hideFlags & HideFlags.HideInHierarchy) != 0 ||
                (target.hideFlags & HideFlags.NotEditable) != 0)
            {
                return true;
            }

            // Skip known system roots
            string nameLower = target.name.ToLowerInvariant();
            foreach (string keyword in ExcludedSystemKeywords)
            {
                if (nameLower.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }

        private void LoadSlotInEditor()
        {
            if (slotData == null || avatarDescriptor == null) return;

            var currentSlot = slotData.slots[selectedSlotIndex];
            if (!currentSlot.isConfigured) return;

            // Ensure trackedObjectPaths is initialized
            if (currentSlot.trackedObjectPaths == null || currentSlot.trackedObjectPaths.Count == 0)
            {
                // Migration: If no tracked objects but we have objectStates, use those
                if (currentSlot.objectStates != null && currentSlot.objectStates.Count > 0)
                {
                    MigrateTrackedObjectsFromStates(currentSlot);
                }
                else
                {
                    EditorUtility.DisplayDialog("No Tracked Objects", 
                        "This slot has no tracked objects. Please add objects to track first.", 
                        "OK");
                    return;
                }
            }

            Transform avatarRoot = avatarDescriptor.transform;
            int loadedCount = 0;
            int missingCount = 0;

            // Load only tracked objects
            foreach (string path in currentSlot.trackedObjectPaths)
            {
                Transform obj = FindObjectByPath(path);
                if (obj != null)
                {
                    // Find the state for this object
                    GameObjectState state = currentSlot.objectStates?.Find(s => s.path == path);
                    if (state != null)
                    {
                        Undo.RecordObject(obj.gameObject, "Load Outfit Slot");
                        obj.gameObject.SetActive(state.isActive);
                        loadedCount++;
                    }
                }
                else
                {
                    missingCount++;
                }
            }

            if (missingCount > 0)
            {
                Debug.LogWarning($"[Outfit Manager] {missingCount} tracked object(s) not found when loading slot {selectedSlotIndex}");
            }

            Debug.Log($"[Outfit Manager] Loaded {loadedCount} tracked objects for slot {selectedSlotIndex} in Editor");
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
            if (avatarDescriptor == null || slotData == null) return;
            if (slotData.GetConfiguredSlotCount() == 0) return;

            // Outfit root is now optional - pass null to use avatar root (objects stay where they are)
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

        private void CreateSophOutfitManagerGameObject()
        {
            if (avatarDescriptor == null) return;

            const string GameObjectName = "Soph Outfit Manager";

            // Check if already exists
            foreach (Transform child in avatarDescriptor.transform)
            {
                if (child.name.Equals(GameObjectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    outfitRoot = child;
                    
                    // Ensure component is attached
                    OutfitManagerComponent component = child.GetComponent<OutfitManagerComponent>();
                    if (component == null)
                    {
                        component = child.gameObject.AddComponent<OutfitManagerComponent>();
                        component.OutfitRoot = outfitRoot;
                        if (slotData != null)
                        {
                            component.SlotData = slotData;
                        }
                        EditorUtility.SetDirty(component);
                    }
                    
                    // Select it in hierarchy for visibility
                    Selection.activeGameObject = child.gameObject;
                    EditorGUIUtility.PingObject(child.gameObject);
                    return;
                }
            }

            // Create new GameObject
            GameObject outfitManagerGO = new GameObject(GameObjectName);
            outfitManagerGO.transform.SetParent(avatarDescriptor.transform);
            outfitManagerGO.transform.localPosition = Vector3.zero;
            outfitManagerGO.transform.localRotation = Quaternion.identity;
            outfitManagerGO.transform.localScale = Vector3.one;

            // Add OutfitManagerComponent
            OutfitManagerComponent newComponent = outfitManagerGO.AddComponent<OutfitManagerComponent>();
            newComponent.OutfitRoot = outfitManagerGO.transform;
            if (slotData != null)
            {
                newComponent.SlotData = slotData;
            }

            // Register undo
            Undo.RegisterCreatedObjectUndo(outfitManagerGO, "Create Soph Outfit Manager");

            outfitRoot = outfitManagerGO.transform;

            // Select and ping in hierarchy
            Selection.activeGameObject = outfitManagerGO;
            EditorGUIUtility.PingObject(outfitManagerGO);

            // Show info dialog explaining optional usage
            EditorUtility.DisplayDialog("Soph Outfit Manager Created (Optional)",
                $"Created '{GameObjectName}' GameObject under your avatar.\n\n" +
                $"This folder is OPTIONAL - you can use it to organize your clothing objects, or leave them where they are.\n\n" +
                "The Outfit Manager will automatically find all clothing/accessory objects regardless of their location.\n\n" +
                "Simply save your outfits using the 'Save Current Visibility' button.", 
                "OK");

            Debug.Log($"[Outfit Manager] Created '{GameObjectName}' GameObject in hierarchy with component. Drag your clothing items here!");
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
