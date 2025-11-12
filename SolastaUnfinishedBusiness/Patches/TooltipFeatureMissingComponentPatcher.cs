using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]

public class TooltipFeatureMissingComponentPatcher
{
    [HarmonyPatch(typeof(TooltipFeatureMissingComponent), nameof(TooltipFeatureMissingComponent.Bind))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class Bind_Patch
    {
        [UsedImplicitly]
        public static void Postfix(ref TooltipFeatureMissingComponent __instance)
        {
            __instance.gameObject.GetComponentInChildren<GuiLabel>().Text = "Tooltip/&InvalidComponentRequirementTitle";
        }
    }
}
