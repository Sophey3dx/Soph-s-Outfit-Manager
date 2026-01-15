using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using VRC.SDK3.Avatars.Components;
#endif

namespace Soph.AvatarOutfitManager.Editor
{
    /// <summary>
    /// Editor-Only component for the "Soph Outfit Manager" GameObject.
    /// Displays outfit information in the Inspector and provides validation.
    /// This is Editor-only because VRChat does not allow runtime scripts on avatars.
    /// </summary>
    [AddComponentMenu("")] // Hide from Add Component menu - this is auto-added
#if UNITY_EDITOR
    [ExecuteInEditMode]
#endif
    public class OutfitManagerComponent : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private Transform outfitRoot;
        
        [SerializeField, HideInInspector]
        private OutfitSlotData slotData;
        
        [SerializeField, HideInInspector]
        private string avatarGuid;

#if UNITY_EDITOR
        /// <summary>
        /// Gets or sets the outfit root transform.
        /// </summary>
        public Transform OutfitRoot
        {
            get
            {
                if (outfitRoot == null && transform.childCount > 0)
                {
                    // Try to find "Outfits" child
                    Transform outfitsChild = transform.Find("Outfits");
                    if (outfitsChild != null)
                    {
                        outfitRoot = outfitsChild;
                    }
                }
                return outfitRoot;
            }
            set
            {
                outfitRoot = value;
                EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// Gets or sets the slot data asset.
        /// Will auto-load if not set, based on avatar GUID.
        /// </summary>
        public OutfitSlotData SlotData
        {
            get
            {
                if (slotData == null)
                {
                    LoadSlotData();
                }
                return slotData;
            }
            set
            {
                slotData = value;
                if (slotData != null && avatarDescriptor != null)
                {
                    string newGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(avatarDescriptor.gameObject));
                    if (!string.IsNullOrEmpty(newGuid))
                    {
                        avatarGuid = newGuid;
                    }
                }
                EditorUtility.SetDirty(this);
            }
        }

        private VRCAvatarDescriptor avatarDescriptor;

        private void OnEnable()
        {
            // Find avatar descriptor in parent hierarchy
            avatarDescriptor = GetComponentInParent<VRCAvatarDescriptor>();
            
            if (avatarDescriptor != null)
            {
                string newGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(avatarDescriptor.gameObject));
                if (!string.IsNullOrEmpty(newGuid))
                {
                    avatarGuid = newGuid;
                }
            }

            // Auto-load slot data if avatar is found
            if (slotData == null && !string.IsNullOrEmpty(avatarGuid))
            {
                LoadSlotData();
            }
        }

        /// <summary>
        /// Loads slot data based on the avatar GUID.
        /// </summary>
        private void LoadSlotData()
        {
            if (string.IsNullOrEmpty(avatarGuid)) return;

            string[] guids = AssetDatabase.FindAssets("t:OutfitSlotData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<OutfitSlotData>(path);
                
                if (data != null && data.avatarGuid == avatarGuid)
                {
                    slotData = data;
                    EditorUtility.SetDirty(this);
                    return;
                }
            }
        }

        /// <summary>
        /// Gets validation status for this outfit manager.
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();
            
            // Check avatar descriptor
            if (avatarDescriptor == null)
            {
                avatarDescriptor = GetComponentInParent<VRCAvatarDescriptor>();
            }
            
            if (avatarDescriptor == null)
            {
                result.AddError("No VRCAvatarDescriptor found in parent hierarchy.");
                return result;
            }

            // Check outfit root
            if (OutfitRoot == null)
            {
                result.AddWarning("Outfit Root is not set. Drag your clothing items here or create an 'Outfits' child.");
            }
            else
            {
                int childCount = OutfitRoot.childCount;
                if (childCount == 0)
                {
                    result.AddWarning("Outfit Root has no children. Add clothing GameObjects as children.");
                }
            }

            // Check slot data
            if (SlotData == null)
            {
                result.AddWarning("Slot Data not found. It will be created when you save your first outfit.");
            }
            else
            {
                // Validate slot data
                int configuredCount = SlotData.GetConfiguredSlotCount();
                if (configuredCount == 0)
                {
                    result.AddWarning("No outfits have been configured. Save at least one outfit slot.");
                }
                else
                {
                    result.AddInfo($"✓ {configuredCount} outfit(s) configured.");
                    
                    // Check for missing objects in slots
                    if (OutfitRoot != null)
                    {
                        ValidateSlotObjects(result);
                    }
                }
            }

            if (result.errors.Count == 0 && result.warnings.Count == 0)
            {
                result.AddInfo("All validations passed.");
            }

            return result;
        }

        /// <summary>
        /// Validates that all objects referenced in slots still exist.
        /// </summary>
        private void ValidateSlotObjects(ValidationResult result)
        {
            var pathToTransform = new Dictionary<string, Transform>();
            // Use avatar root to collect mapping, as tracked paths are relative to avatar
            CollectPathMappings(avatarDescriptor.transform, avatarDescriptor.transform, pathToTransform);

            for (int i = 0; i < OutfitSlotData.SLOT_COUNT; i++)
            {
                var slot = SlotData.slots[i];
                if (!slot.isConfigured) continue;

                int missingCount = 0;
                foreach (var state in slot.objectStates)
                {
                    if (!pathToTransform.ContainsKey(state.path))
                    {
                        missingCount++;
                    }
                }

                if (missingCount > 0)
                {
                    result.AddWarning($"Slot {i} ({slot.slotName}): {missingCount} object(s) no longer exist in Outfit Root.");
                }
            }
        }

        private void CollectPathMappings(Transform root, Transform current, Dictionary<string, Transform> mappings)
        {
            foreach (Transform child in current)
            {
                string relativePath = GetRelativePath(root, child);
                mappings[relativePath] = child;
                CollectPathMappings(root, child, mappings);
            }
        }

        private static string GetRelativePath(Transform root, Transform target)
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

        /// <summary>
        /// Gets outfit details for a specific slot.
        /// </summary>
        public OutfitSlotInfo GetSlotInfo(int slotIndex)
        {
            var info = new OutfitSlotInfo
            {
                slotIndex = slotIndex,
                isConfigured = false
            };

            if (SlotData == null || slotIndex < 0 || slotIndex >= OutfitSlotData.SLOT_COUNT)
            {
                return info;
            }

            var slot = SlotData.slots[slotIndex];
            info.slotName = slot.slotName;
            info.isConfigured = slot.isConfigured;
            info.objectCount = slot.objectStates?.Count ?? 0;
            info.iconPath = slot.iconPath;
            
            if (slot.objectStates != null)
            {
                info.objectStates = new List<ObjectStateInfo>();
                foreach (var state in slot.objectStates)
                {
                    info.objectStates.Add(new ObjectStateInfo
                    {
                        path = state.path,
                        isActive = state.isActive
                    });
                }
            }

            return info;
        }

        /// <summary>
        /// Validation result container.
        /// </summary>
        [System.Serializable]
        public class ValidationResult
        {
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
            public List<string> infos = new List<string>();

            public void AddError(string message) => errors.Add(message);
            public void AddWarning(string message) => warnings.Add(message);
            public void AddInfo(string message) => infos.Add(message);

            public bool IsValid => errors.Count == 0;
            public bool HasWarnings => warnings.Count > 0;
        }

        /// <summary>
        /// Information about a specific outfit slot.
        /// </summary>
        [System.Serializable]
        public class OutfitSlotInfo
        {
            public int slotIndex;
            public string slotName;
            public bool isConfigured;
            public int objectCount;
            public List<ObjectStateInfo> objectStates;
            public string iconPath;
        }

        /// <summary>
        /// Information about a single object in a slot.
        /// </summary>
        [System.Serializable]
        public class ObjectStateInfo
        {
            public string path;
            public bool isActive;
        }
#endif
    }
}
