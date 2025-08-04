using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.ModKit.Utility;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class GuiWrapperManagerPatcher
{
    [HarmonyPatch(typeof(GuiWrapperManager), nameof(GuiWrapperManager.GetGuiPowerDefinition))]
    [UsedImplicitly]
    public static class GetGuiPowerDefinition_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(GuiWrapperManager __instance, out GuiPowerDefinition __result, string powerName)
        {
            //PATCH: in case we remove Power from UB - if it is referenced in Game Console, it may crash
            //with this fix it won't crash, but tooltip would look broken
            __result = __instance.powerDefinitionsMap.GetValueOrDefault(powerName);
            return false;
        }
    }
}
