using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Soph.AvatarOutfitManager.Editor
{
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public class DiagnosticItem
    {
        public DiagnosticSeverity severity;
        public string message;

        public DiagnosticItem(DiagnosticSeverity severity, string message)
        {
            this.severity = severity;
            this.message = message;
        }
    }

    public class DiagnosticsResult
    {
        public readonly List<DiagnosticItem> items = new List<DiagnosticItem>();

        public int ErrorCount => items.FindAll(i => i.severity == DiagnosticSeverity.Error).Count;
        public int WarningCount => items.FindAll(i => i.severity == DiagnosticSeverity.Warning).Count;
        public int InfoCount => items.FindAll(i => i.severity == DiagnosticSeverity.Info).Count;

        public void Add(DiagnosticSeverity severity, string message)
        {
            items.Add(new DiagnosticItem(severity, message));
        }
    }

    public static class OutfitDiagnostics
    {
        public const string ParameterName = "OutfitIndex";
        public const string LayerName = "OutfitManager";
        public const string OutfitsSubmenuName = "Outfits";
        public const string DefaultOutputFolder = "Assets/AvatarOutfitManager/Generated";

        private static readonly string[] ExcludedSystemKeywords =
        {
            "sps",
            "gogoloco",
            "gesturemanager",
            "vrcfury"
        };

        public static DiagnosticsResult Run(
            VRCAvatarDescriptor avatarDescriptor,
            Transform outfitRoot,
            OutfitSlotData slotData)
        {
            var result = new DiagnosticsResult();

            if (avatarDescriptor == null)
            {
                result.Add(DiagnosticSeverity.Error, "No VRCAvatarDescriptor found.");
                return result;
            }

            // Expression Parameters
            if (avatarDescriptor.expressionParameters == null)
            {
                result.Add(DiagnosticSeverity.Warning, "Expression Parameters asset is missing.");
            }
            else
            {
                ValidateExpressionParameters(avatarDescriptor.expressionParameters, result);
            }

            // FX Layer / Controller
            AnimatorController fxController = GetFXController(avatarDescriptor);
            if (fxController == null)
            {
                result.Add(DiagnosticSeverity.Warning, "FX Controller is missing or still default.");
            }
            else
            {
                ValidateFXController(fxController, slotData, result);
            }

            // Expressions Menu
            if (avatarDescriptor.expressionsMenu == null)
            {
                result.Add(DiagnosticSeverity.Warning, "Expressions Menu is missing.");
            }
            else
            {
                ValidateExpressionsMenu(avatarDescriptor.expressionsMenu, slotData, result);
            }

            // Outfit Root checks
            if (outfitRoot == null)
            {
                result.Add(DiagnosticSeverity.Warning, "Outfit Root is not set.");
            }
            else
            {
                var excludedFound = FindExcludedSystemRoots(outfitRoot);
                if (excludedFound.Count > 0)
                {
                    result.Add(DiagnosticSeverity.Warning,
                        $"Outfit Root contains excluded system objects: {string.Join(", ", excludedFound)}");
                }
            }

            return result;
        }

        public static bool FixNow(
            VRCAvatarDescriptor avatarDescriptor,
            Transform outfitRoot,
            OutfitSlotData slotData,
            string outputFolder = DefaultOutputFolder)
        {
            if (avatarDescriptor == null || outfitRoot == null || slotData == null)
            {
                return false;
            }

            return VRChatAssetGenerator.GenerateAllAssets(
                avatarDescriptor,
                outfitRoot,
                slotData,
                string.IsNullOrEmpty(outputFolder) ? DefaultOutputFolder : outputFolder);
        }

        private static void ValidateExpressionParameters(
            VRCExpressionParameters parameters,
            DiagnosticsResult result)
        {
            bool found = false;
            foreach (var param in parameters.parameters)
            {
                if (param.name == ParameterName)
                {
                    found = true;
                    if (param.valueType != VRCExpressionParameters.ValueType.Int)
                    {
                        result.Add(DiagnosticSeverity.Error, "OutfitIndex parameter is not Int.");
                    }
                    break;
                }
            }

            if (!found)
            {
                result.Add(DiagnosticSeverity.Error, "OutfitIndex parameter is missing in Expression Parameters.");
            }
        }

        private static void ValidateFXController(
            AnimatorController fxController,
            OutfitSlotData slotData,
            DiagnosticsResult result)
        {
            bool hasParam = false;
            foreach (var param in fxController.parameters)
            {
                if (param.name == ParameterName)
                {
                    hasParam = param.type == AnimatorControllerParameterType.Int;
                    if (!hasParam)
                    {
                        result.Add(DiagnosticSeverity.Error, "FX Controller parameter OutfitIndex is not Int.");
                    }
                    break;
                }
            }

            if (!hasParam)
            {
                result.Add(DiagnosticSeverity.Warning, "FX Controller parameter OutfitIndex is missing.");
            }

            bool layerFound = false;
            foreach (var layer in fxController.layers)
            {
                if (layer.name == LayerName)
                {
                    layerFound = true;
                    int stateCount = layer.stateMachine != null ? layer.stateMachine.states.Length : 0;
                    if (stateCount == 0)
                    {
                        result.Add(DiagnosticSeverity.Warning, "OutfitManager layer has no states.");
                    }

                    if (slotData != null && slotData.GetConfiguredSlotCount() > 0 && stateCount == 0)
                    {
                        result.Add(DiagnosticSeverity.Error, "OutfitManager layer missing states for configured slots.");
                    }
                    break;
                }
            }

            if (!layerFound)
            {
                result.Add(DiagnosticSeverity.Warning, "OutfitManager FX layer is missing.");
            }
        }

        private static void ValidateExpressionsMenu(
            VRCExpressionsMenu mainMenu,
            OutfitSlotData slotData,
            DiagnosticsResult result)
        {
            if (mainMenu == null) return;

            VRCExpressionsMenu outfitsMenu = null;
            foreach (var control in mainMenu.controls)
            {
                if (control.name == OutfitsSubmenuName && control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    outfitsMenu = control.subMenu;
                    break;
                }
            }

            if (outfitsMenu == null)
            {
                result.Add(DiagnosticSeverity.Warning, "Outfits submenu is missing in Expressions Menu.");
                return;
            }

            if (slotData == null)
            {
                result.Add(DiagnosticSeverity.Info, "Slot Data not available to validate menu controls.");
                return;
            }

            for (int i = 0; i < OutfitSlotData.SLOT_COUNT; i++)
            {
                if (!slotData.slots[i].isConfigured) continue;

                string expectedName = string.IsNullOrEmpty(slotData.slots[i].slotName)
                    ? $"Outfit {i}"
                    : slotData.slots[i].slotName;

                var control = outfitsMenu.controls.Find(c => c.name == expectedName);
                if (control == null)
                {
                    result.Add(DiagnosticSeverity.Warning, $"Menu control missing for slot {i} ({expectedName}).");
                    continue;
                }

                if (control.type != VRCExpressionsMenu.Control.ControlType.Button)
                {
                    result.Add(DiagnosticSeverity.Error, $"Menu control for '{expectedName}' is not a Button.");
                }

                if (control.parameter == null || control.parameter.name != ParameterName)
                {
                    result.Add(DiagnosticSeverity.Error, $"Menu control '{expectedName}' has wrong parameter.");
                }
                else if (control.value != i)
                {
                    result.Add(DiagnosticSeverity.Warning, $"Menu control '{expectedName}' has wrong value (expected {i}).");
                }
            }
        }

        private static AnimatorController GetFXController(VRCAvatarDescriptor avatarDescriptor)
        {
            var layers = avatarDescriptor.baseAnimationLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    return layers[i].animatorController as AnimatorController;
                }
            }

            return null;
        }

        public static List<string> FindExcludedSystemRoots(Transform outfitRoot)
        {
            var found = new List<string>();
            if (outfitRoot == null) return found;

            foreach (Transform child in outfitRoot)
            {
                string nameLower = child.name.ToLowerInvariant();
                foreach (string keyword in ExcludedSystemKeywords)
                {
                    if (nameLower.Contains(keyword))
                    {
                        found.Add(child.name);
                        break;
                    }
                }
            }

            return found;
        }
    }
}
