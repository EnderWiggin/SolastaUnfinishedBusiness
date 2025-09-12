using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Models;
using UnityEngine;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class GraphicsCharacterPatcher
{
    private static bool UseInstrumentAnimation(GraphicsCharacter graphics, ActionDefinitions.Id actionId)
    {
        if (!graphics.CanUseMusicalInstrumentWhenCasting) { return false; }

        return ActionDefinitions.IsSpellAction(actionId)
               || actionId is ActionDefinitions.Id.GrantBardicInspiration;
    }

    [HarmonyPatch(typeof(GraphicsCharacter), nameof(GraphicsCharacter.CastingStart))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class CastingStart_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(GraphicsCharacter __instance, ref ActionDefinitions.MagicEffectCastData spellCastData)
        {
            //PATCH: fixes Bardic Inspiration animation using weapon instead of instrument
            if (UseInstrumentAnimation(__instance, spellCastData.ActionId))
            {
                __instance.SetWieldedItemsActive(false);
                __instance.SetWieldedMusicalInstrumentsActive(true);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(GraphicsCharacter), nameof(GraphicsCharacter.CastingEnd))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class CastingEnd_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(GraphicsCharacter __instance, ActionDefinitions.Id actionId)
        {
            //PATCH: fixes Bardic Inspiration animation using weapon instead of instrument
            if (UseInstrumentAnimation(__instance, actionId))
            {
                __instance.SetWieldedItemsActive(true);
                __instance.SetWieldedMusicalInstrumentsActive(false);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(GraphicsCharacter), nameof(GraphicsCharacter.ResetScale))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ResetScale_Patch
    {
        [UsedImplicitly]
        public static void Postfix(GraphicsCharacter __instance, ref float __result)
        {
            //PATCH: Allows custom races with different scales
            if (__instance.RulesetCharacter is not RulesetCharacterHero rulesetCharacterHero ||
                !RacesContext.RaceScaleMap.TryGetValue(rulesetCharacterHero.RaceDefinition, out var scale))
            {
                return;
            }

            __result *= scale;
            __instance.transform.localScale = new Vector3(__result, __result, __result);
        }
    }
}
