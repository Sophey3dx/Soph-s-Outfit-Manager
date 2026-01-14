using System.IO;
using UnityEditor;
using UnityEngine;

namespace Soph.AvatarOutfitManager.Editor
{
    /// <summary>
    /// Renders outfit preview icons using an Editor camera.
    /// Creates thumbnail images for each outfit slot that can be used
    /// in the EditorWindow preview and VRChat Expression Menu.
    /// </summary>
    public static class OutfitIconRenderer
    {
        // Default render settings
        private const int ICON_SIZE = 256;
        private const float CAMERA_DISTANCE = 0.8f;
        private const float CAMERA_HEIGHT_OFFSET = 0.1f; // Offset from avatar center
        
        /// <summary>
        /// Renders an icon for the current outfit state.
        /// </summary>
        /// <param name="avatarRoot">The avatar root transform.</param>
        /// <param name="outputPath">Path to save the icon (including filename.png).</param>
        /// <param name="cameraSettings">Optional custom camera settings.</param>
        /// <returns>The rendered Texture2D, or null if rendering failed.</returns>
        public static Texture2D RenderOutfitIcon(
            Transform avatarRoot,
            string outputPath,
            IconCameraSettings cameraSettings = null)
        {
            if (avatarRoot == null)
            {
                Debug.LogError("[Outfit Icon Renderer] Avatar root is null!");
                return null;
            }

            cameraSettings ??= new IconCameraSettings();

            // Find the avatar's approximate center (chest height)
            Vector3 avatarCenter = CalculateAvatarCenter(avatarRoot);
            
            // Create temporary camera
            var cameraGO = new GameObject("OutfitIconCamera");
            var camera = cameraGO.AddComponent<Camera>();
            
            try
            {
                // Setup camera
                ConfigureCamera(camera, cameraSettings);
                
                // Position camera in front of avatar
                PositionCamera(camera, avatarRoot, avatarCenter, cameraSettings);
                
                // Create render texture
                var renderTexture = new RenderTexture(
                    cameraSettings.iconSize, 
                    cameraSettings.iconSize, 
                    24, 
                    RenderTextureFormat.ARGB32);
                renderTexture.antiAliasing = 4;
                
                camera.targetTexture = renderTexture;
                
                // Render
                camera.Render();
                
                // Read pixels to Texture2D
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(
                    cameraSettings.iconSize, 
                    cameraSettings.iconSize, 
                    TextureFormat.ARGB32, 
                    false);
                texture.ReadPixels(new Rect(0, 0, cameraSettings.iconSize, cameraSettings.iconSize), 0, 0);
                texture.Apply();
                
                RenderTexture.active = null;
                
                // Save to file
                SaveTextureAsPNG(texture, outputPath);
                
                // Cleanup render texture
                camera.targetTexture = null;
                Object.DestroyImmediate(renderTexture);
                
                // Import as asset and return
                AssetDatabase.Refresh();
                var importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
                
                // Cleanup the temporary texture
                Object.DestroyImmediate(texture);
                
                return importedTexture;
            }
            finally
            {
                // Always cleanup camera
                Object.DestroyImmediate(cameraGO);
            }
        }

        /// <summary>
        /// Renders icons for all configured outfit slots.
        /// </summary>
        public static void RenderAllSlotIcons(
            Transform avatarRoot,
            Transform outfitRoot,
            OutfitSlotData slotData,
            string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string iconsFolder = Path.Combine(outputFolder, "Icons");
            if (!Directory.Exists(iconsFolder))
            {
                Directory.CreateDirectory(iconsFolder);
            }

            int renderedCount = 0;
            
            try
            {
                for (int i = 0; i < OutfitSlotData.SLOT_COUNT; i++)
                {
                    var slot = slotData.slots[i];
                    if (!slot.isConfigured) continue;

                    EditorUtility.DisplayProgressBar(
                        "Rendering Outfit Icons",
                        $"Rendering slot {i}: {slot.slotName}",
                        (float)i / OutfitSlotData.SLOT_COUNT);

                    // Apply the outfit visibility in editor
                    ApplyOutfitVisibility(outfitRoot, slot);
                    
                    // Force scene update
                    SceneView.RepaintAll();
                    
                    // Generate filename
                    string safeName = SanitizeFileName(slot.slotName);
                    if (string.IsNullOrEmpty(safeName)) safeName = $"Outfit_{i}";
                    string iconPath = Path.Combine(iconsFolder, $"Icon_{i}_{safeName}.png");

                    // Render the icon
                    var icon = RenderOutfitIcon(avatarRoot, iconPath);
                    
                    if (icon != null)
                    {
                        slot.iconPath = iconPath;
                        renderedCount++;
                    }
                }

                EditorUtility.SetDirty(slotData);
                AssetDatabase.SaveAssets();
                
                Debug.Log($"[Outfit Icon Renderer] Rendered {renderedCount} outfit icons to: {iconsFolder}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Renders a single slot icon with the option to specify camera angle.
        /// </summary>
        public static Texture2D RenderSlotIcon(
            Transform avatarRoot,
            Transform outfitRoot,
            OutfitSlot slot,
            int slotIndex,
            string outputFolder,
            IconCameraSettings cameraSettings = null)
        {
            if (!slot.isConfigured)
            {
                Debug.LogWarning($"[Outfit Icon Renderer] Slot {slotIndex} is not configured!");
                return null;
            }

            string iconsFolder = Path.Combine(outputFolder, "Icons");
            if (!Directory.Exists(iconsFolder))
            {
                Directory.CreateDirectory(iconsFolder);
            }

            // Apply the outfit visibility
            ApplyOutfitVisibility(outfitRoot, slot);
            SceneView.RepaintAll();

            // Generate filename
            string safeName = SanitizeFileName(slot.slotName);
            if (string.IsNullOrEmpty(safeName)) safeName = $"Outfit_{slotIndex}";
            string iconPath = Path.Combine(iconsFolder, $"Icon_{slotIndex}_{safeName}.png");

            // Render
            var icon = RenderOutfitIcon(avatarRoot, iconPath, cameraSettings);
            
            if (icon != null)
            {
                slot.iconPath = iconPath;
            }

            return icon;
        }

        private static Vector3 CalculateAvatarCenter(Transform avatarRoot)
        {
            // Try to find common bone references for better positioning
            var animator = avatarRoot.GetComponent<Animator>();
            
            if (animator != null && animator.isHuman)
            {
                // Use chest bone if available (humanoid rig)
                var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chest != null)
                {
                    return chest.position;
                }
                
                // Fall back to spine
                var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                if (spine != null)
                {
                    return spine.position + Vector3.up * 0.3f;
                }
            }

            // Calculate from bounds as fallback
            var renderers = avatarRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                
                // Return point at ~60% height (chest area)
                return new Vector3(
                    bounds.center.x,
                    bounds.min.y + bounds.size.y * 0.6f,
                    bounds.center.z);
            }

            // Last resort: just use avatar position with offset
            return avatarRoot.position + Vector3.up * 1.2f;
        }

        private static void ConfigureCamera(Camera camera, IconCameraSettings settings)
        {
            camera.clearFlags = settings.useTransparentBackground 
                ? CameraClearFlags.SolidColor 
                : CameraClearFlags.SolidColor;
            
            camera.backgroundColor = settings.useTransparentBackground 
                ? new Color(0, 0, 0, 0) 
                : settings.backgroundColor;
            
            camera.orthographic = settings.useOrthographic;
            camera.orthographicSize = settings.orthographicSize;
            camera.fieldOfView = settings.fieldOfView;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            
            // Only render specific layers if needed
            camera.cullingMask = settings.cullingMask;
        }

        private static void PositionCamera(
            Camera camera, 
            Transform avatarRoot, 
            Vector3 lookAtPoint,
            IconCameraSettings settings)
        {
            // Determine avatar's forward direction by checking where the avatar is actually facing
            // Try to use head/eye position to determine front vs back
            Vector3 avatarForward = DetermineAvatarForward(avatarRoot);
            
            // Position camera in front of avatar (opposite of forward direction)
            Vector3 cameraPosition = lookAtPoint 
                - avatarForward * settings.cameraDistance 
                + Vector3.up * settings.cameraHeightOffset;

            camera.transform.position = cameraPosition;
            camera.transform.LookAt(lookAtPoint);
            
            // Apply rotation offset if specified
            if (settings.cameraRotationOffset != 0)
            {
                camera.transform.RotateAround(lookAtPoint, Vector3.up, settings.cameraRotationOffset);
            }
        }

        private static Vector3 DetermineAvatarForward(Transform avatarRoot)
        {
            // Try to find the head/eyes to determine which way the avatar is facing
            var animator = avatarRoot.GetComponent<Animator>();
            
            if (animator != null && animator.isHuman)
            {
                // Use head bone - it should point forward
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    // Head forward direction
                    Vector3 headForward = head.forward;
                    
                    // Check if head is pointing in a reasonable direction (not straight up/down)
                    if (Mathf.Abs(Vector3.Dot(headForward, Vector3.up)) < 0.9f)
                    {
                        // Project head forward onto horizontal plane (XZ plane)
                        Vector3 horizontalForward = Vector3.ProjectOnPlane(headForward, Vector3.up).normalized;
                        if (horizontalForward.magnitude > 0.1f)
                        {
                            return horizontalForward;
                        }
                    }
                }
                
                // Fallback: use chest/spine to determine facing
                var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chest != null)
                {
                    Vector3 chestForward = Vector3.ProjectOnPlane(chest.forward, Vector3.up).normalized;
                    if (chestForward.magnitude > 0.1f)
                    {
                        return chestForward;
                    }
                }
            }
            
            // Final fallback: use avatar root transform direction
            // In Unity, forward is typically +Z, but many VRChat avatars face -Z
            // Check both directions and use the one that makes more sense
            Vector3 rootForward = avatarRoot.forward;
            Vector3 horizontalRootForward = Vector3.ProjectOnPlane(rootForward, Vector3.up).normalized;
            
            // If avatar forward is pointing mostly backward (negative Z), flip it
            if (Vector3.Dot(horizontalRootForward, Vector3.forward) < -0.5f)
            {
                return -horizontalRootForward;
            }
            
            return horizontalRootForward.magnitude > 0.1f ? horizontalRootForward : -Vector3.forward;
        }

        private static void ApplyOutfitVisibility(Transform outfitRoot, OutfitSlot slot)
        {
            // Build path-to-transform mapping
            var pathToTransform = new System.Collections.Generic.Dictionary<string, Transform>();
            CollectPathMappings(outfitRoot, outfitRoot, pathToTransform);

            // Apply states
            foreach (var state in slot.objectStates)
            {
                if (pathToTransform.TryGetValue(state.path, out Transform obj))
                {
                    obj.gameObject.SetActive(state.isActive);
                }
            }
        }

        private static void CollectPathMappings(
            Transform root, 
            Transform current, 
            System.Collections.Generic.Dictionary<string, Transform> mappings)
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

            var path = new System.Collections.Generic.List<string>();
            Transform current = target;

            while (current != null && current != root)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", path);
        }

        private static void SaveTextureAsPNG(Texture2D texture, string path)
        {
            byte[] pngData = texture.EncodeToPNG();
            
            // Ensure directory exists
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, pngData);
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var invalidChars = Path.GetInvalidFileNameChars();
            var result = name;

            foreach (var c in invalidChars)
            {
                result = result.Replace(c, '_');
            }

            // Also replace spaces
            result = result.Replace(' ', '_');

            return result;
        }
    }

    /// <summary>
    /// Settings for icon camera rendering.
    /// </summary>
    [System.Serializable]
    public class IconCameraSettings
    {
        public int iconSize = 256;
        public float cameraDistance = 0.8f;
        public float cameraHeightOffset = 0.1f;
        public float cameraRotationOffset = 0f; // Degrees around Y axis
        public bool useTransparentBackground = true;
        public Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public bool useOrthographic = false;
        public float orthographicSize = 0.5f;
        public float fieldOfView = 35f;
        public LayerMask cullingMask = -1; // Everything

        public IconCameraSettings()
        {
            // Default values set above
        }

        /// <summary>
        /// Creates settings for a front-facing portrait shot.
        /// </summary>
        public static IconCameraSettings Portrait()
        {
            return new IconCameraSettings
            {
                cameraDistance = 0.6f,
                cameraHeightOffset = 0.2f,
                fieldOfView = 30f
            };
        }

        /// <summary>
        /// Creates settings for a full-body shot.
        /// </summary>
        public static IconCameraSettings FullBody()
        {
            return new IconCameraSettings
            {
                cameraDistance = 2.5f,
                cameraHeightOffset = 0f,
                fieldOfView = 40f
            };
        }

        /// <summary>
        /// Creates settings for an angled 3/4 view.
        /// </summary>
        public static IconCameraSettings ThreeQuarter()
        {
            return new IconCameraSettings
            {
                cameraDistance = 0.8f,
                cameraHeightOffset = 0.1f,
                cameraRotationOffset = 25f,
                fieldOfView = 35f
            };
        }
    }
}
