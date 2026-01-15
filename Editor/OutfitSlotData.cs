using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soph.AvatarOutfitManager.Editor
{
    /// <summary>
    /// Represents the active state of a single GameObject within an outfit.
    /// Stores the relative path from the outfit root and whether the object should be active.
    /// </summary>
    [Serializable]
    public class GameObjectState
    {
        /// <summary>
        /// Relative path from the avatar root to this GameObject.
        /// Example: "Jacket/Sleeves" for a nested object.
        /// </summary>
        public string path;

        /// <summary>
        /// Whether this GameObject should be active (visible) in this outfit.
        /// </summary>
        public bool isActive;

        public GameObjectState()
        {
            path = string.Empty;
            isActive = false;
        }

        public GameObjectState(string path, bool isActive)
        {
            this.path = path;
            this.isActive = isActive;
        }
    }

    /// <summary>
    /// Represents a single outfit slot containing the visibility states of all tracked GameObjects.
    /// VRChat avatars support toggling GameObject active states via animations.
    /// </summary>
    [Serializable]
    public class OutfitSlot
    {
        /// <summary>
        /// User-friendly name for this outfit slot (e.g., "Casual", "Formal", "Swimwear").
        /// </summary>
        public string slotName;

        /// <summary>
        /// List of all GameObject states for this outfit.
        /// Each entry defines whether a specific GameObject should be active or inactive.
        /// </summary>
        public List<GameObjectState> objectStates;

        /// <summary>
        /// List of relative paths to GameObjects that should be tracked for this outfit slot.
        /// These are manually selected by the user. Only these objects will be saved/loaded.
        /// </summary>
        public List<string> trackedObjectPaths;

        /// <summary>
        /// Indicates whether this slot has been configured with outfit data.
        /// Used to distinguish between empty slots and slots with all objects disabled.
        /// </summary>
        public bool isConfigured;

        /// <summary>
        /// Path to the rendered icon for this outfit slot.
        /// Used in the Editor preview and VRChat Expression Menu.
        /// </summary>
        public string iconPath;

        public OutfitSlot()
        {
            slotName = string.Empty;
            objectStates = new List<GameObjectState>();
            trackedObjectPaths = new List<string>();
            isConfigured = false;
        }

        public OutfitSlot(string name)
        {
            slotName = name;
            objectStates = new List<GameObjectState>();
            trackedObjectPaths = new List<string>();
            isConfigured = false;
        }

        /// <summary>
        /// Clears all outfit data from this slot.
        /// </summary>
        public void Clear()
        {
            objectStates.Clear();
            trackedObjectPaths.Clear();
            isConfigured = false;
            iconPath = null;
        }

        /// <summary>
        /// Gets the icon texture for this slot if one has been rendered.
        /// </summary>
        /// <returns>The icon texture, or null if not available.</returns>
        public UnityEngine.Texture2D GetIcon()
        {
            if (string.IsNullOrEmpty(iconPath)) return null;
            
            #if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(iconPath);
            #else
            return null;
            #endif
        }
    }

    /// <summary>
    /// ScriptableObject that stores all outfit slot data for an avatar.
    /// This is saved as an asset in the project and persists between Unity sessions.
    /// 
    /// VRChat Limitation: Runtime scripts cannot be attached to avatars.
    /// All outfit logic must be baked into AnimationClips and the FX Animator.
    /// This ScriptableObject is only used in the Editor for configuration.
    /// </summary>
    [CreateAssetMenu(fileName = "OutfitManagerData", menuName = "Soph/Outfit Manager Data")]
    public class OutfitSlotData : ScriptableObject
    {
        /// <summary>
        /// Total number of outfit slots available.
        /// This matches the VRChat Expression Parameter range (0-5 for an Int).
        /// </summary>
        public const int SLOT_COUNT = 6;

        /// <summary>
        /// Array of outfit slots. Always contains exactly SLOT_COUNT elements.
        /// </summary>
        public OutfitSlot[] slots;

        /// <summary>
        /// GUID reference to the avatar this data belongs to.
        /// Used to validate that the data matches the current avatar.
        /// </summary>
        public string avatarGuid;

        /// <summary>
        /// Path to the outfit root GameObject relative to the avatar root.
        /// </summary>
        public string outfitRootPath;

        private void OnEnable()
        {
            InitializeSlots();
        }

        /// <summary>
        /// Ensures all slots are properly initialized.
        /// </summary>
        public void InitializeSlots()
        {
            if (slots == null || slots.Length != SLOT_COUNT)
            {
                slots = new OutfitSlot[SLOT_COUNT];
                for (int i = 0; i < SLOT_COUNT; i++)
                {
                    slots[i] = new OutfitSlot($"Outfit {i}");
                }
            }
            else
            {
                // Ensure no null slots and initialize trackedObjectPaths
                for (int i = 0; i < SLOT_COUNT; i++)
                {
                    if (slots[i] == null)
                    {
                        slots[i] = new OutfitSlot($"Outfit {i}");
                    }
                    else
                    {
                        // Ensure trackedObjectPaths is initialized
                        if (slots[i].trackedObjectPaths == null)
                        {
                            slots[i].trackedObjectPaths = new List<string>();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the number of configured slots.
        /// </summary>
        public int GetConfiguredSlotCount()
        {
            if (slots == null) return 0;
            
            int count = 0;
            foreach (var slot in slots)
            {
                if (slot != null && slot.isConfigured)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Validates that the data is properly configured for asset generation.
        /// </summary>
        /// <param name="errorMessage">Output error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (slots == null)
            {
                errorMessage = "Slots array is null. Please reinitialize the data.";
                return false;
            }

            int configuredCount = GetConfiguredSlotCount();
            if (configuredCount == 0)
            {
                errorMessage = "No outfit slots have been configured. Please save at least one outfit.";
                return false;
            }

            return true;
        }
    }
}
