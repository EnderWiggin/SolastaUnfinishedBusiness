using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Models;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class DeviceFunctionSelectionLinePatcher
{
    [HarmonyPatch(typeof(DeviceFunctionSelectionLine), nameof(DeviceFunctionSelectionLine.FilterRelevantFucntions))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class FilterRelevantFucntions_Patch
    {
        [UsedImplicitly]
        private static void Postfix(DeviceFunctionSelectionLine __instance)
        {
            //PATCH: hide extra functions that were added by Tabletop2024Context.Switch[Poisons/Potions]BonusAction
            __instance.relevantDeviceFunctions.RemoveAll(NeedsRemoval);
        }

        private static bool NeedsRemoval(RulesetDeviceFunction function)
        {
            var power = function.DeviceFunctionDescription?.FeatureDefinitionPower;

            return power != null && Tabletop2024Context.ItemBonusPowers.ContainsValue(power);
        }
    }
}
