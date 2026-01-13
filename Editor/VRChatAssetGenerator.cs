using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Soph.AvatarOutfitManager.Editor
{
    /// <summary>
    /// Generates all VRChat-compatible assets for the outfit system.
    /// 
    /// VRChat Constraints:
    /// - No runtime scripts allowed on avatars
    /// - All logic must be in Animator states and transitions
    /// - GameObject toggles via AnimationClip m_IsActive property
    /// - Expression Parameters limited to 256 bits total
    /// - Write Defaults should be OFF for predictable behavior
    /// </summary>
    public static class VRChatAssetGenerator
    {
        private const string LAYER_NAME = "OutfitManager";
        private const string PARAMETER_NAME = "OutfitIndex";
        private const string ICON_RESOURCE_NAME = "OutfitManagerIcon";

        /// <summary>
        /// Generates all VRChat assets for the outfit system.
        /// </summary>
        /// <param name="avatarDescriptor">The VRChat avatar descriptor.</param>
        /// <param name="outfitRoot">The root transform containing all outfit objects.</param>
        /// <param name="slotData">The configured outfit slot data.</param>
        /// <param name="outputFolder">The folder to save generated assets.</param>
        /// <returns>True if generation was successful.</returns>
        public static bool GenerateAllAssets(
            VRCAvatarDescriptor avatarDescriptor,
            Transform outfitRoot,
            OutfitSlotData slotData,
            string outputFolder)
        {
            if (!ValidateInputs(avatarDescriptor, outfitRoot, slotData, out string error))
            {
                EditorUtility.DisplayDialog("Validation Error", error, "OK");
                return false;
            }

            // Ensure output folder exists
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            try
            {
                EditorUtility.DisplayProgressBar("Generating VRChat Assets", "Creating animation clips...", 0.1f);

                // Generate animation clips for each configured slot
                var animationClips = GenerateAnimationClips(avatarDescriptor.transform, outfitRoot, slotData, outputFolder);

                EditorUtility.DisplayProgressBar("Generating VRChat Assets", "Setting up FX layer...", 0.4f);

                // Setup or update FX animator layer
                SetupFXLayer(avatarDescriptor, animationClips, outputFolder);

                EditorUtility.DisplayProgressBar("Generating VRChat Assets", "Creating expression parameters...", 0.6f);

                // Setup expression parameters
                SetupExpressionParameters(avatarDescriptor, outputFolder);

                EditorUtility.DisplayProgressBar("Generating VRChat Assets", "Creating expressions menu...", 0.8f);

                // Setup expressions menu
                SetupExpressionsMenu(avatarDescriptor, slotData, outputFolder);

                EditorUtility.DisplayProgressBar("Generating VRChat Assets", "Finalizing...", 0.95f);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Success", 
                    $"VRChat assets generated successfully!\n\n" +
                    $"Output folder: {outputFolder}\n" +
                    $"Slots configured: {slotData.GetConfiguredSlotCount()}", "OK");

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Outfit Manager] Asset generation failed: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Error", $"Asset generation failed: {ex.Message}", "OK");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Generates an AnimationClip for a single outfit slot.
        /// </summary>
        public static AnimationClip GenerateSingleClip(
            Transform avatarRoot,
            Transform outfitRoot,
            OutfitSlot slot,
            int slotIndex,
            string outputFolder)
        {
            var clip = new AnimationClip();
            clip.name = $"Outfit_{slotIndex}_{SanitizeFileName(slot.slotName)}";

            // Collect all tracked GameObjects under outfit root
            var allObjects = CollectAllGameObjects(outfitRoot);
            
            // Create a set of active paths for quick lookup
            var activePaths = new HashSet<string>();
            foreach (var state in slot.objectStates)
            {
                if (state.isActive)
                {
                    activePaths.Add(state.path);
                }
            }

            // Set animation curves for all objects
            foreach (var obj in allObjects)
            {
                string relativePath = GetRelativePath(avatarRoot, obj.transform);
                string outfitRelativePath = GetRelativePath(outfitRoot, obj.transform);
                
                // Determine if this object should be active based on slot data
                bool shouldBeActive = activePaths.Contains(outfitRelativePath);

                // VRChat-safe GameObject toggle animation
                // Uses m_IsActive property which VRChat supports
                var curve = new AnimationCurve();
                curve.AddKey(0f, shouldBeActive ? 1f : 0f);
                
                clip.SetCurve(relativePath, typeof(GameObject), "m_IsActive", curve);
            }

            // Ensure output folder exists
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            // Save the clip as an asset
            string clipPath = Path.Combine(outputFolder, $"{clip.name}.anim");
            clipPath = AssetDatabase.GenerateUniqueAssetPath(clipPath);
            
            AssetDatabase.CreateAsset(clip, clipPath);
            
            return clip;
        }

        private static bool ValidateInputs(
            VRCAvatarDescriptor avatarDescriptor,
            Transform outfitRoot,
            OutfitSlotData slotData,
            out string error)
        {
            error = string.Empty;

            if (avatarDescriptor == null)
            {
                error = "Avatar Descriptor is not assigned.";
                return false;
            }

            if (outfitRoot == null)
            {
                error = "Outfit Root is not assigned.";
                return false;
            }

            if (!outfitRoot.IsChildOf(avatarDescriptor.transform))
            {
                error = "Outfit Root must be a child of the Avatar.";
                return false;
            }

            if (slotData == null)
            {
                error = "Outfit Slot Data is not assigned.";
                return false;
            }

            if (!slotData.Validate(out string slotError))
            {
                error = slotError;
                return false;
            }

            return true;
        }

        private static List<AnimationClip> GenerateAnimationClips(
            Transform avatarRoot,
            Transform outfitRoot,
            OutfitSlotData slotData,
            string outputFolder)
        {
            var clips = new List<AnimationClip>();
            string clipsFolder = Path.Combine(outputFolder, "Animations");

            if (!Directory.Exists(clipsFolder))
            {
                Directory.CreateDirectory(clipsFolder);
            }

            // Delete existing clips in the folder
            var existingClips = Directory.GetFiles(clipsFolder, "Outfit_*.anim");
            foreach (var existingClip in existingClips)
            {
                AssetDatabase.DeleteAsset(existingClip);
            }

            // Collect all outfit objects once
            var allObjects = CollectAllGameObjects(outfitRoot);

            for (int i = 0; i < OutfitSlotData.SLOT_COUNT; i++)
            {
                var slot = slotData.slots[i];
                
                // Create clip even for unconfigured slots (will disable all objects)
                var clip = new AnimationClip();
                clip.name = $"Outfit_{i}_{SanitizeFileName(slot.slotName)}";

                // Create a set of active paths for this slot
                var activePaths = new HashSet<string>();
                if (slot.isConfigured)
                {
                    foreach (var state in slot.objectStates)
                    {
                        if (state.isActive)
                        {
                            activePaths.Add(state.path);
                        }
                    }
                }

                // Set curves for all tracked objects
                foreach (var obj in allObjects)
                {
                    string avatarRelativePath = GetRelativePath(avatarRoot, obj.transform);
                    string outfitRelativePath = GetRelativePath(outfitRoot, obj.transform);

                    bool shouldBeActive = slot.isConfigured && activePaths.Contains(outfitRelativePath);

                    var curve = new AnimationCurve();
                    curve.AddKey(0f, shouldBeActive ? 1f : 0f);
                    clip.SetCurve(avatarRelativePath, typeof(GameObject), "m_IsActive", curve);
                }

                // Save clip
                string clipPath = Path.Combine(clipsFolder, $"{clip.name}.anim");
                AssetDatabase.CreateAsset(clip, clipPath);
                clips.Add(clip);
            }

            return clips;
        }

        private static void SetupFXLayer(
            VRCAvatarDescriptor avatarDescriptor,
            List<AnimationClip> animationClips,
            string outputFolder)
        {
            // Get or create FX animator controller
            var fxController = GetOrCreateFXController(avatarDescriptor, outputFolder);

            // Remove existing OutfitManager layer if present
            RemoveLayerIfExists(fxController, LAYER_NAME);

            // Add parameter if not exists
            AddParameterIfNotExists(fxController, PARAMETER_NAME, AnimatorControllerParameterType.Int);

            // Create new layer
            var layer = new AnimatorControllerLayer
            {
                name = LAYER_NAME,
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine()
            };

            // Important: Save the state machine as a sub-asset
            layer.stateMachine.name = LAYER_NAME;
            layer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(layer.stateMachine, fxController);

            // Position states in a circle for better visibility
            float radius = 200f;
            Vector3 center = new Vector3(300, 100, 0);

            // Create states for each outfit
            var states = new List<AnimatorState>();
            for (int i = 0; i < animationClips.Count; i++)
            {
                float angle = (i / (float)animationClips.Count) * Mathf.PI * 2f;
                Vector3 position = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;

                var state = layer.stateMachine.AddState(animationClips[i].name, position);
                state.motion = animationClips[i];
                
                // VRChat Best Practice: Write Defaults OFF
                state.writeDefaultValues = false;
                
                states.Add(state);
            }

            // Set first state as default
            if (states.Count > 0)
            {
                layer.stateMachine.defaultState = states[0];
            }

            // Create Any State transitions to each outfit state
            for (int i = 0; i < states.Count; i++)
            {
                var transition = layer.stateMachine.AddAnyStateTransition(states[i]);
                transition.duration = 0f;
                transition.exitTime = 0f;
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                
                // Add condition: OutfitIndex == i
                transition.AddCondition(AnimatorConditionMode.Equals, i, PARAMETER_NAME);
                
                // Prevent self-transition
                transition.canTransitionToSelf = false;
            }

            // Add the layer to the controller
            fxController.AddLayer(layer);

            EditorUtility.SetDirty(fxController);
        }

        private static AnimatorController GetOrCreateFXController(
            VRCAvatarDescriptor avatarDescriptor,
            string outputFolder)
        {
            // Check if avatar already has an FX layer assigned
            var customAnimLayers = avatarDescriptor.baseAnimationLayers;
            AnimatorController fxController = null;

            for (int i = 0; i < customAnimLayers.Length; i++)
            {
                if (customAnimLayers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    fxController = customAnimLayers[i].animatorController as AnimatorController;
                    if (customAnimLayers[i].isDefault || fxController == null)
                    {
                        // Create new FX controller
                        string fxPath = Path.Combine(outputFolder, "FX_OutfitManager.controller");
                        fxController = AnimatorController.CreateAnimatorControllerAtPath(fxPath);
                        
                        customAnimLayers[i].isDefault = false;
                        customAnimLayers[i].animatorController = fxController;
                        avatarDescriptor.baseAnimationLayers = customAnimLayers;
                        EditorUtility.SetDirty(avatarDescriptor);
                    }
                    break;
                }
            }

            return fxController;
        }

        private static void RemoveLayerIfExists(AnimatorController controller, string layerName)
        {
            var layers = new List<AnimatorControllerLayer>(controller.layers);
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (layers[i].name == layerName)
                {
                    // Clean up state machine
                    if (layers[i].stateMachine != null)
                    {
                        Object.DestroyImmediate(layers[i].stateMachine, true);
                    }
                    layers.RemoveAt(i);
                }
            }
            controller.layers = layers.ToArray();
        }

        private static void AddParameterIfNotExists(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType type)
        {
            foreach (var param in controller.parameters)
            {
                if (param.name == parameterName)
                {
                    return;
                }
            }

            controller.AddParameter(parameterName, type);
        }

        private static void SetupExpressionParameters(
            VRCAvatarDescriptor avatarDescriptor,
            string outputFolder)
        {
            var expressionParameters = avatarDescriptor.expressionParameters;

            // Create new parameters asset if needed
            if (expressionParameters == null)
            {
                string paramPath = Path.Combine(outputFolder, "ExpressionParameters_OutfitManager.asset");
                expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                expressionParameters.parameters = new VRCExpressionParameters.Parameter[0];
                AssetDatabase.CreateAsset(expressionParameters, paramPath);
                avatarDescriptor.expressionParameters = expressionParameters;
                EditorUtility.SetDirty(avatarDescriptor);
            }

            // Check if parameter already exists
            var paramList = new List<VRCExpressionParameters.Parameter>(expressionParameters.parameters);
            bool paramExists = false;

            for (int i = 0; i < paramList.Count; i++)
            {
                if (paramList[i].name == PARAMETER_NAME)
                {
                    // Update existing parameter
                    paramList[i].valueType = VRCExpressionParameters.ValueType.Int;
                    paramList[i].defaultValue = 0;
                    paramList[i].saved = true;
                    paramExists = true;
                    break;
                }
            }

            if (!paramExists)
            {
                // Add new parameter
                paramList.Add(new VRCExpressionParameters.Parameter
                {
                    name = PARAMETER_NAME,
                    valueType = VRCExpressionParameters.ValueType.Int,
                    defaultValue = 0,
                    saved = true,
                    networkSynced = true
                });
            }

            expressionParameters.parameters = paramList.ToArray();
            EditorUtility.SetDirty(expressionParameters);
        }

        private static void SetupExpressionsMenu(
            VRCAvatarDescriptor avatarDescriptor,
            OutfitSlotData slotData,
            string outputFolder)
        {
            // Try to load the menu icon from Resources
            Texture2D menuIcon = Resources.Load<Texture2D>(ICON_RESOURCE_NAME);
            if (menuIcon == null)
            {
                Debug.Log("[Outfit Manager] Menu icon not found in Resources. Menu will be created without icon.");
            }

            // Create outfit submenu
            string submenuPath = Path.Combine(outputFolder, "Menu_Outfits.asset");
            var outfitMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            outfitMenu.controls = new List<VRCExpressionsMenu.Control>();

            // Add Radial Puppet control for outfit selection
            var radialControl = new VRCExpressionsMenu.Control
            {
                name = "Select Outfit",
                type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                icon = menuIcon,
                subParameters = new VRCExpressionsMenu.Control.Parameter[]
                {
                    new VRCExpressionsMenu.Control.Parameter { name = PARAMETER_NAME }
                }
            };
            outfitMenu.controls.Add(radialControl);

            // Also add individual buttons for each configured outfit
            for (int i = 0; i < OutfitSlotData.SLOT_COUNT; i++)
            {
                if (slotData.slots[i].isConfigured)
                {
                    // Use slot-specific icon if available, otherwise fall back to global icon
                    Texture2D slotIcon = slotData.slots[i].GetIcon();
                    if (slotIcon == null)
                    {
                        slotIcon = menuIcon;
                    }

                    var buttonControl = new VRCExpressionsMenu.Control
                    {
                        name = string.IsNullOrEmpty(slotData.slots[i].slotName) 
                            ? $"Outfit {i}" 
                            : slotData.slots[i].slotName,
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        icon = slotIcon,
                        parameter = new VRCExpressionsMenu.Control.Parameter { name = PARAMETER_NAME },
                        value = i
                    };
                    outfitMenu.controls.Add(buttonControl);
                }
            }

            // Check if asset exists and delete it
            if (File.Exists(submenuPath))
            {
                AssetDatabase.DeleteAsset(submenuPath);
            }

            AssetDatabase.CreateAsset(outfitMenu, submenuPath);

            // Get or create main menu
            var mainMenu = avatarDescriptor.expressionsMenu;
            if (mainMenu == null)
            {
                string mainMenuPath = Path.Combine(outputFolder, "MainMenu_OutfitManager.asset");
                mainMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                mainMenu.controls = new List<VRCExpressionsMenu.Control>();
                AssetDatabase.CreateAsset(mainMenu, mainMenuPath);
                avatarDescriptor.expressionsMenu = mainMenu;
                EditorUtility.SetDirty(avatarDescriptor);
            }

            // Remove existing Outfits submenu entry if present
            mainMenu.controls.RemoveAll(c => c.name == "Outfits");

            // Add submenu entry to main menu with icon
            var submenuControl = new VRCExpressionsMenu.Control
            {
                name = "Outfits",
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                icon = menuIcon,
                subMenu = outfitMenu
            };
            mainMenu.controls.Add(submenuControl);

            EditorUtility.SetDirty(mainMenu);
            EditorUtility.SetDirty(outfitMenu);
        }

        /// <summary>
        /// Collects all GameObjects under the specified root (excluding the root itself).
        /// </summary>
        private static List<GameObject> CollectAllGameObjects(Transform root)
        {
            var objects = new List<GameObject>();
            CollectChildrenRecursive(root, objects);
            return objects;
        }

        private static void CollectChildrenRecursive(Transform parent, List<GameObject> list)
        {
            foreach (Transform child in parent)
            {
                list.Add(child.gameObject);
                CollectChildrenRecursive(child, list);
            }
        }

        /// <summary>
        /// Gets the path of a transform relative to a root transform.
        /// This path is used in AnimationClips to reference GameObjects.
        /// </summary>
        public static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return string.Empty;

            var path = new List<string>();
            Transform current = target;

            while (current != null && current != root)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", path);
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";

            var invalidChars = Path.GetInvalidFileNameChars();
            var result = name;

            foreach (var c in invalidChars)
            {
                result = result.Replace(c, '_');
            }

            return result;
        }
    }
}
