using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Soph.AvatarOutfitManager.Editor
{
    /// <summary>
    /// Shows outfit labels next to clothing items in the Unity Hierarchy.
    /// </summary>
    [InitializeOnLoad]
    public static class HierarchyOverlay
    {
        private static Dictionary<int, string> cachedLabels = new Dictionary<int, string>();
        private static OutfitSlotData cachedSlotData;
        private static VRCAvatarDescriptor cachedAvatar;
        private static float lastCacheTime;
        private const float CACHE_DURATION = 2f; // Refresh every 2 seconds

        static HierarchyOverlay()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            // Get the GameObject for this hierarchy item
            GameObject go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (go == null) return;

            // Check if we need to refresh the cache
            if (Time.realtimeSinceStartup - lastCacheTime > CACHE_DURATION)
            {
                RefreshCache();
            }

            // Look up the label for this object
            if (cachedLabels.TryGetValue(instanceID, out string label))
            {
                // Draw the label on the right side of the hierarchy item
                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.5f, 0.8f, 0.5f, 0.8f) },
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 9
                };

                // Position the label at the right edge
                Rect labelRect = new Rect(selectionRect.xMax - 80, selectionRect.y, 75, selectionRect.height);
                GUI.Label(labelRect, label, labelStyle);
            }
        }

        private static void RefreshCache()
        {
            lastCacheTime = Time.realtimeSinceStartup;
            cachedLabels.Clear();

            // Find all OutfitSlotData assets
            string[] guids = AssetDatabase.FindAssets("t:OutfitSlotData");
            if (guids.Length == 0) return;

            // Find the currently active avatar in the scene
            var avatars = Object.FindObjectsOfType<VRCAvatarDescriptor>();
            if (avatars.Length == 0) return;

            // Use the first active avatar (could be improved to use selected avatar)
            var avatar = avatars[0];
            cachedAvatar = avatar;

            // Find slot data for this avatar
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var slotData = AssetDatabase.LoadAssetAtPath<OutfitSlotData>(path);
                
                if (slotData != null)
                {
                    cachedSlotData = slotData;
                    BuildLabelCache(avatar.transform, slotData);
                    break; // Use first matching slot data
                }
            }
        }

        private static void BuildLabelCache(Transform avatarRoot, OutfitSlotData slotData)
        {
            if (slotData.slots == null) return;

            // Build a mapping of path -> list of outfit names
            var pathToOutfits = new Dictionary<string, List<string>>();

            for (int i = 0; i < slotData.slots.Length; i++)
            {
                var slot = slotData.slots[i];
                if (!slot.isConfigured) continue;

                string outfitName = string.IsNullOrEmpty(slot.slotName) ? $"Outfit {i}" : slot.slotName;

                foreach (var state in slot.objectStates)
                {
                    if (state.isActive) // Only show active items
                    {
                        if (!pathToOutfits.ContainsKey(state.path))
                        {
                            pathToOutfits[state.path] = new List<string>();
                        }
                        pathToOutfits[state.path].Add(outfitName);
                    }
                }
            }

            // Now map paths to GameObjects and cache instanceIDs
            foreach (var kvp in pathToOutfits)
            {
                Transform obj = FindByPath(avatarRoot, kvp.Key);
                if (obj != null)
                {
                    // Create label with outfit names (truncate if too long)
                    string label = string.Join(", ", kvp.Value);
                    if (label.Length > 12) label = label.Substring(0, 12) + "...";
                    
                    cachedLabels[obj.gameObject.GetInstanceID()] = $"[{label}]";
                }
            }
        }

        private static Transform FindByPath(Transform root, string relativePath)
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

        /// <summary>
        /// Force refresh the hierarchy labels (called when outfit data changes)
        /// </summary>
        public static void ForceRefresh()
        {
            lastCacheTime = 0;
            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
