using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            Transform outfitRoot, // Now optional - if null, uses avatar root
            OutfitSlotData slotData,
            string outputFolder)
        {
            if (!ValidateInputs(avatarDescriptor, outfitRoot, slotData, out string error))
            {
                EditorUtility.DisplayDialog("Validation Error", error, "OK");
                return false;
            }

            // Outfit root is now optional - use avatar root if not specified
            Transform actualOutfitRoot = outfitRoot ?? avatarDescriptor.transform;
            Debug.Log($"[Outfit Manager] Using {(outfitRoot != null ? $"outfit root '{outfitRoot.name}'" : "avatar root (objects stay where they are)")} for asset generation");

            // Ensure output folder exists
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            try
            {
                EditorUtility.DisplayProgressBar("Generating VRChat Assets", "Detecting avatar Write Defaults preference...", 0.05f);

                // Get FX controller first to detect Write Defaults preference
                var fxController = GetOrCreateFXController(avatarDescriptor, outputFolder);
                bool useWriteDefaults = DetectAvatarWriteDefaultsPreference(fxController);

                EditorUtility.DisplayProgressBar("Generating VRChat Assets", "Creating animation clips...", 0.1f);

                // Generate animation clips for each configured slot
                // Note: Objects stay where they are - we collect from avatar root, not outfit root
                // Note: Clips always set all objects explicitly to work with both Write Defaults ON and OFF
                var animationClips = GenerateAnimationClips(avatarDescriptor.transform, actualOutfitRoot, slotData, outputFolder);

                EditorUtility.DisplayProgressBar("Generating VRChat Assets", "Setting up FX layer...", 0.4f);

                // Setup or update FX animator layer (will use detected Write Defaults preference)
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

            // Outfit root is now optional - objects can stay where they are under avatar root
            // If outfitRoot is provided, it must be a child of the avatar
            if (outfitRoot != null && !outfitRoot.IsChildOf(avatarDescriptor.transform))
            {
                error = "Outfit Root must be a child of the Avatar (or leave it empty to use avatar root).";
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

            // Validate that all tracked objects still exist
            List<string> missingObjects = new List<string>();
            Transform avatarRoot = avatarDescriptor.transform;
            
            foreach (var slot in slotData.slots)
            {
                if (slot != null && slot.isConfigured && slot.trackedObjectPaths != null)
                {
                    foreach (string path in slot.trackedObjectPaths)
                    {
                        if (!ValidateTrackedObjectExists(avatarRoot, path))
                        {
                            missingObjects.Add($"{slot.slotName}: {path}");
                        }
                    }
                }
            }
            
            if (missingObjects.Count > 0)
            {
                string missingList = string.Join("\n", missingObjects.Take(10));
                if (missingObjects.Count > 10)
                {
                    missingList += $"\n... and {missingObjects.Count - 10} more";
                }
                
                bool continueAnyway = EditorUtility.DisplayDialog("Missing Tracked Objects", 
                    $"Some tracked objects could not be found:\n\n{missingList}\n\n" +
                    "These objects may have been deleted or moved. Continue anyway?", 
                    "Continue", "Cancel");
                
                if (!continueAnyway)
                {
                    error = "Generation cancelled due to missing tracked objects.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateTrackedObjectExists(Transform avatarRoot, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return false;
            
            string[] pathParts = relativePath.Split('/');
            Transform current = avatarRoot;
            
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
                
                if (found == null) return false;
                current = found;
            }
            
            return current != null;
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

            // Collect all outfit objects from avatar root (objects stay where they are)
            // We collect from avatarRoot, not outfitRoot, since objects can be anywhere in the hierarchy
            var allObjects = CollectAllGameObjects(avatarRoot);
            
            Debug.Log($"[Outfit Manager] Found {allObjects.Count} objects in avatar hierarchy for animation clip generation");

            // Build a set of all unique paths from all configured slots
            // This ensures we track all objects that were ever saved in any outfit
            var allTrackedPaths = new HashSet<string>();
            var allTrackedBlendShapes = new Dictionary<string, HashSet<string>>(); // Path -> Set<ShapeName>

            foreach (var slot in slotData.slots)
            {
                if (slot.isConfigured)
                {
                    foreach (var state in slot.objectStates)
                    {
                        allTrackedPaths.Add(state.path);
                        
                        // Collect blendshapes
                        if (state.blendShapes != null && state.blendShapes.Count > 0)
                        {
                            if (!allTrackedBlendShapes.ContainsKey(state.path))
                            {
                                allTrackedBlendShapes[state.path] = new HashSet<string>();
                            }
                            
                            foreach (var shape in state.blendShapes)
                            {
                                allTrackedBlendShapes[state.path].Add(shape.name);
                            }
                        }
                    }
                }
            }
            
            Debug.Log($"[Outfit Manager] Tracking {allTrackedPaths.Count} objects and blendshapes across all slots");

            for (int i = 0; i < OutfitSlotData.SLOT_COUNT; i++)
            {
                var slot = slotData.slots[i];
                
                // Create clip even for unconfigured slots (will disable all objects)
                var clip = new AnimationClip();
                clip.name = $"Outfit_{i}_{SanitizeFileName(slot.slotName)}";

                // Create a set of active paths for this slot (paths are relative to avatarRoot)
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
                // IMPORTANT: We always set ALL objects explicitly (both active and inactive) to work with both Write Defaults ON and OFF
                // This ensures consistency and avoids mixed write defaults errors
                int activeCount = 0;
                int inactiveCount = 0;
                int errorCount = 0;
                int skippedCount = 0;
                
                foreach (var obj in allObjects)
                {
                    if (obj == null || obj.transform == null) continue;
                    
                    string avatarRelativePath = GetRelativePath(avatarRoot, obj.transform);
                    
                    // Only set curves for objects that were tracked in at least one outfit slot
                    // This avoids animating body parts, armature, etc.
                    if (!allTrackedPaths.Contains(avatarRelativePath))
                    {
                        skippedCount++;
                        continue;
                    }

                    // Validate path
                    if (string.IsNullOrEmpty(avatarRelativePath))
                    {
                        Debug.LogWarning($"[Outfit Manager] Empty path for object '{obj.name}' in clip '{clip.name}'");
                        errorCount++;
                        continue;
                    }

                    // Paths in slot.objectStates are now relative to avatarRoot, not outfitRoot
                    bool shouldBeActive = slot.isConfigured && activePaths.Contains(avatarRelativePath);

                    // Create curve with explicit value (1 = active, 0 = inactive)
                    var curve = new AnimationCurve();
                    float value = shouldBeActive ? 1f : 0f;
                    curve.AddKey(0f, value);
                    
                    try
                    {
                        // Set the curve - this explicitly sets the GameObject's active state
                        clip.SetCurve(avatarRelativePath, typeof(GameObject), "m_IsActive", curve);
                        
                        if (shouldBeActive) activeCount++;
                        else inactiveCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[Outfit Manager] Failed to set curve for '{obj.name}' (path: '{avatarRelativePath}'): {ex.Message}");
                        errorCount++;
                    }

                    // Handle BlendShapes
                    if (allTrackedBlendShapes.TryGetValue(avatarRelativePath, out HashSet<string> shapesToAnimate))
                    {
                        // Find this object's state in the current slot
                        var objState = slot.objectStates.Find(s => s.path == avatarRelativePath);
                        
                        foreach (string shapeName in shapesToAnimate)
                        {
                            float shapeValue = 0f; // Default to 0 (reset)
                            
                            // If this slot has a value for this shape, use it
                            if (objState != null && objState.blendShapes != null)
                            {
                                var shapeState = objState.blendShapes.Find(bs => bs.name == shapeName);
                                if (shapeState != null)
                                {
                                    shapeValue = shapeState.value;
                                }
                            }
                            
                            AnimationCurve shapeCurve = new AnimationCurve();
                            shapeCurve.AddKey(0f, shapeValue);
                            
                            try
                            {
                                clip.SetCurve(avatarRelativePath, typeof(SkinnedMeshRenderer), $"blendShape.{shapeName}", shapeCurve);
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[Outfit Manager] Failed to set blendshape curve '{shapeName}' for '{obj.name}': {ex.Message}");
                                errorCount++;
                            }
                        }
                    }
                }
                
                if (errorCount > 0)
                {
                    Debug.LogWarning($"[Outfit Manager] Clip '{clip.name}' had {errorCount} errors setting curves");
                }
                
                // Animate foot parameter if configured
                if (!string.IsNullOrEmpty(slotData.footParameterName))
                {
                    FootType footType = slot.footType;
                    
                    // Auto-detect from shoe names if set to Auto
                    if (footType == FootType.Auto)
                    {
                        footType = AvatarOutfitManagerWindow.DetectFootTypeFromShoes(slot);
                    }
                    
                    if (footType == FootType.Flat || footType == FootType.Heels)
                    {
                        float footValue = footType == FootType.Heels 
                            ? slotData.footHeelValue 
                            : slotData.footFlatValue;
                        
                        // Find any SkinnedMeshRenderer that might have the blendshape
                        // Foot parameters are typically on the Body mesh
                        foreach (Transform child in avatarRoot.GetComponentsInChildren<Transform>(true))
                        {
                            var smr = child.GetComponent<SkinnedMeshRenderer>();
                            if (smr != null && smr.sharedMesh != null)
                            {
                                int shapeIndex = smr.sharedMesh.GetBlendShapeIndex(slotData.footParameterName);
                                if (shapeIndex != -1)
                                {
                                    string smrPath = GetRelativePath(avatarRoot, child);
                                    AnimationCurve footCurve = new AnimationCurve();
                                    footCurve.AddKey(0f, footValue);
                                    
                                    try
                                    {
                                        clip.SetCurve(smrPath, typeof(SkinnedMeshRenderer), $"blendShape.{slotData.footParameterName}", footCurve);
                                        Debug.Log($"[Outfit Manager] Set foot parameter '{slotData.footParameterName}' = {footValue} for clip '{clip.name}'");
                                    }
                                    catch (System.Exception ex)
                                    {
                                        Debug.LogError($"[Outfit Manager] Failed to set foot curve: {ex.Message}");
                                    }
                                    break; // Only need to set it once
                                }
                            }
                        }
                    }
                }
                
                Debug.Log($"[Outfit Manager] Generated clip '{clip.name}': {activeCount} active, {inactiveCount} inactive objects (errors: {errorCount})");

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

            // Detect avatar's Write Defaults preference to avoid mixed write defaults errors
            bool useWriteDefaults = DetectAvatarWriteDefaultsPreference(fxController);
            Debug.Log($"[Outfit Manager] Using Write Defaults {(useWriteDefaults ? "ON" : "OFF")} for OutfitManager states to match avatar preference.");

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
                
                // Match avatar's Write Defaults preference to avoid mixed write defaults errors
                state.writeDefaultValues = useWriteDefaults;
                
                states.Add(state);
            }

            // Set first configured state as default (or first state if none configured)
            AnimatorState defaultState = null;
            for (int i = 0; i < states.Count; i++)
            {
                // Check if this state has a motion (clip exists)
                if (states[i].motion != null)
                {
                    defaultState = states[i];
                    break;
                }
            }
            
            if (defaultState == null && states.Count > 0)
            {
                defaultState = states[0];
            }

            // Create an "Empty" state for the reset/default case (parameter = -1)
            // This state has no animation, so clothing stays at scene default
            var emptyState = layer.stateMachine.AddState("Default (No Outfit)", new Vector3(center.x, center.y - 150, 0));
            emptyState.writeDefaultValues = useWriteDefaults;
            // No motion assigned - clothing stays at default
            
            // Make the empty state the actual default state
            // This way, avatar spawns with default clothing, not outfit 0
            layer.stateMachine.defaultState = emptyState;
            Debug.Log($"[Outfit Manager] Created 'Default (No Outfit)' state as animator default");

            // Add transition from Any State to Empty state when parameter == -1
            var anyToEmptyTransition = layer.stateMachine.AddAnyStateTransition(emptyState);
            anyToEmptyTransition.duration = 0f;
            anyToEmptyTransition.exitTime = 0f;
            anyToEmptyTransition.hasExitTime = false;
            anyToEmptyTransition.hasFixedDuration = true;
            anyToEmptyTransition.AddCondition(AnimatorConditionMode.Equals, -1, PARAMETER_NAME);
            anyToEmptyTransition.canTransitionToSelf = false;

            if (defaultState != null)
            {
                Debug.Log($"[Outfit Manager] First outfit state is: {defaultState.name}");
            }

            // Create transitions: Any State -> Each Outfit State
            // Also create transitions between states for smooth switching
            for (int i = 0; i < states.Count; i++)
            {
                // Skip if this state has no motion (unconfigured slot)
                if (states[i].motion == null) continue;

                // Transition from Any State to this outfit state
                var anyStateTransition = layer.stateMachine.AddAnyStateTransition(states[i]);
                anyStateTransition.duration = 0f;
                anyStateTransition.exitTime = 0f;
                anyStateTransition.hasExitTime = false;
                anyStateTransition.hasFixedDuration = true;
                
                // Add condition: OutfitIndex == i
                anyStateTransition.AddCondition(AnimatorConditionMode.Equals, i, PARAMETER_NAME);
                
                // Prevent self-transition
                anyStateTransition.canTransitionToSelf = false;

                // Also create transitions from each other state to this one
                // This ensures smooth switching between outfits
                for (int j = 0; j < states.Count; j++)
                {
                    if (i == j || states[j].motion == null) continue;

                    var stateTransition = states[j].AddTransition(states[i]);
                    stateTransition.duration = 0f;
                    stateTransition.exitTime = 0f;
                    stateTransition.hasExitTime = false;
                    stateTransition.hasFixedDuration = true;
                    
                    // Condition: OutfitIndex == i
                    stateTransition.AddCondition(AnimatorConditionMode.Equals, i, PARAMETER_NAME);
                }
            }
            
            Debug.Log($"[Outfit Manager] Created {states.Count} states with transitions in FX Layer");

            // Add the layer to the controller
            fxController.AddLayer(layer);

            EditorUtility.SetDirty(fxController);
        }

        /// <summary>
        /// Detects the avatar's Write Defaults preference by checking existing FX layer states.
        /// Returns true if most states use Write Defaults ON, false if OFF.
        /// Defaults to false (OFF) if no states are found or mixed.
        /// </summary>
        private static bool DetectAvatarWriteDefaultsPreference(AnimatorController fxController)
        {
            if (fxController == null) return false;

            int onCount = 0;
            int offCount = 0;

            // Check all layers in the FX controller
            foreach (var layer in fxController.layers)
            {
                if (layer.stateMachine == null) continue;

                // Check all states in this layer
                foreach (var state in layer.stateMachine.states)
                {
                    if (state.state == null) continue;
                    
                    if (state.state.writeDefaultValues)
                    {
                        onCount++;
                    }
                    else
                    {
                        offCount++;
                    }
                }
            }

            // If we found states, use the majority preference
            if (onCount > 0 || offCount > 0)
            {
                bool preference = onCount > offCount;
                Debug.Log($"[Outfit Manager] Detected Write Defaults: {onCount} ON, {offCount} OFF. Using {(preference ? "ON" : "OFF")}.");
                return preference;
            }

            // Default to OFF if no states found (VRChat best practice)
            Debug.Log("[Outfit Manager] No existing FX states found. Defaulting to Write Defaults OFF.");
            return false;
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
                    paramList[i].defaultValue = -1;
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
                    defaultValue = -1,
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

            // Add "Reset to Default" option at the top
            // This sets the parameter to -1, which doesn't match any outfit transition
            // causing the avatar to stay in its default (scene) state
            var resetControl = new VRCExpressionsMenu.Control
            {
                name = "Reset to Default",
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                icon = menuIcon,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = PARAMETER_NAME },
                value = -1
            };
            outfitMenu.controls.Add(resetControl);

            // Add individual toggles for each configured outfit
            // Toggle type sets and HOLDS the value (unlike Button which is momentary)
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

                    var toggleControl = new VRCExpressionsMenu.Control
                    {
                        name = string.IsNullOrEmpty(slotData.slots[i].slotName) 
                            ? $"Outfit {i}" 
                            : slotData.slots[i].slotName,
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        icon = slotIcon,
                        // Set the parameter and value directly on the control
                        parameter = new VRCExpressionsMenu.Control.Parameter { name = PARAMETER_NAME },
                        value = i
                    };
                    outfitMenu.controls.Add(toggleControl);
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
