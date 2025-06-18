using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Models;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class RulesetItemDevicePatcher
{
    [HarmonyPatch(typeof(RulesetItemDevice), nameof(RulesetItemDevice.IsFunctionAvailable))]
    [UsedImplicitly]
    public static class IsFunctionAvailable_Patch
    {
        [UsedImplicitly]
        public static void Postfix(
            ref bool __result,
            RulesetDeviceFunction function,
            RulesetCharacter character)
        {
            if (!__result)
            {
                return;
            }

            var power = function.DeviceFunctionDescription?.FeatureDefinitionPower;

            if (!power)
            {
                return;
            }

            __result = character.CanUsePower(power, false);
        }
    }

    [HarmonyPatch(typeof(RulesetItemDevice), nameof(RulesetItemDevice.PostLoad))]
    [UsedImplicitly]
    public static class PostLoad_Patch
    {
        [UsedImplicitly]
        public static void Prefix(RulesetItemDevice __instance)
        {
            //PATCH: update availability ofm extra bonus action functions if 2024 item use rules are enabled
            Tabletop2024Context.UpdateDeviceBonusActions(__instance, Tabletop2024Context.Potion);
            Tabletop2024Context.UpdateDeviceBonusActions(__instance, Tabletop2024Context.Poison);
        }
    }
}
