using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Models;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class GuiCharacterActionPatcher
{
    [HarmonyPatch(typeof(GuiCharacterAction), nameof(GuiCharacterAction.LimitedUses), MethodType.Getter)]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class LimitedUses_Getter_Patch
    {
        [UsedImplicitly]
        public static void Postfix(GuiCharacterAction __instance, ref int __result)
        {
            //PATCH: Get remaining attack uses (ammunition) from forced attack mode
            if (__instance.forcedAttackMode == null)
            {
                return;
            }

            __result = __instance.ActingCharacter.RulesetCharacter.GetRemainingAttackUses(__instance.forcedAttackMode);
        }
    }

    [HarmonyPatch(typeof(GuiCharacterAction), nameof(GuiCharacterAction.SetupSprite))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class SetupSprite_Patch
    {
        private static bool _wasCastQuickened;
        private static bool _wasNickAttack;

        [UsedImplicitly]
        public static void Prefix(GuiCharacterAction __instance)
        {
            //PATCH: make CastQuickened action have larger icon, same as CastBonus or CastMain
            _wasCastQuickened = __instance.actionId == (ActionDefinitions.Id)ExtraActionId.CastQuickened;

            if (_wasCastQuickened)
            {
                __instance.actionId = ActionDefinitions.Id.CastBonus;
            }

            //PATCH: support for Nick weapon Mastery - make action button icon show weapon
            _wasNickAttack = __instance.actionId == (ActionDefinitions.Id)ExtraActionId.NickMasteryAttack;
            if (_wasNickAttack)
            {
                __instance.actionId = ActionDefinitions.Id.AttackOff;
            }
        }

        [UsedImplicitly]
        public static void Postfix(GuiCharacterAction __instance)
        {
            //PATCH: make CastQuickened action have larger icon, same as CastBonus or CastMain
            if (_wasCastQuickened)
            {
                __instance.actionId = (ActionDefinitions.Id)ExtraActionId.CastQuickened;
            }

            //PATCH: support for Nick weapon Mastery - make action button icon show weapon
            if (_wasNickAttack)
            {
                __instance.actionId = (ActionDefinitions.Id)ExtraActionId.NickMasteryAttack;
            }
        }
    }

    [HarmonyPatch(typeof(GuiCharacterAction), nameof(GuiCharacterAction.SetupTooltip))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class SetupTooltip_Patch
    {
        private static bool _wasNickAttack;


        [UsedImplicitly]
        public static void Prefix(GuiCharacterAction __instance)
        {
            //PATCH: support for Nick weapon Mastery - make action button tooltip have weapon info
            _wasNickAttack = __instance.actionId == (ActionDefinitions.Id)ExtraActionId.NickMasteryAttack;
            if (_wasNickAttack)
            {
                __instance.actionId = ActionDefinitions.Id.AttackOff;
            }
        }

        [UsedImplicitly]
        public static void Postfix(GuiCharacterAction __instance,
            ActionDefinitions.ActionStatus actionStatus,
            GuiTooltip guiTooltip,
            RulesetAttackMode currentAttackMode,
            ref int currentEffectFormCount,
            ref string currentFailureString,
            ref string currentTooltip,
            ref ActionDefinitions.ActionStatus currentActionStatus)
        {
            //PATCH: Get custom error message for CastQuickened action
            CustomActionIdContext.CheckQuickenedStatus(__instance, actionStatus, guiTooltip, ref currentFailureString);

            //PATCH: support for Nick weapon Mastery - make action button tooltip have weapon info
            if (_wasNickAttack)
            {
                __instance.actionId = (ActionDefinitions.Id)ExtraActionId.NickMasteryAttack;
            }
        }
    }
}
