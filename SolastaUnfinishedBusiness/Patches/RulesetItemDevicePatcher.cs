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

    [HarmonyPatch(typeof(RulesetItemDevice), nameof(RulesetItemDevice.SerializeElements))]
    [UsedImplicitly]
    public static class SerializeElements_Patch
    {
        [UsedImplicitly]
        public static void Postfix(RulesetItemDevice __instance,  IElementsSerializer serializer)
        {
            if(serializer.Mode != Serializer.SerializationMode.Read) { return; }
            
            //PATCH: update availability of extra bonus action functions if 2024 item use rules are enabled
            Tabletop2024Context.UpdateDeviceBonusActions(__instance, GameConstants.TagPotion);
            Tabletop2024Context.UpdateDeviceBonusActions(__instance, GameConstants.TagPoison);
        }
    }
}
