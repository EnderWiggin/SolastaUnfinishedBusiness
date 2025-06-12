using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Models;
using UnityEngine;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class SlotRecoveryModalPatcher
{
    [HarmonyPatch(typeof(SlotRecoveryModal), nameof(SlotRecoveryModal.ShowSlotRecovery))]
    internal static class ShowSlotRecovery_Patch
    {
        [UsedImplicitly]
        public static void Prefix(SlotRecoveryModal __instance,
            RulesetCharacter caster,
            string effectName,
            RulesetSpellRepertoire repertoire,
            int slotsCapital,
            ref int maxSlotLevel)
        {
            if (caster.IsSpellPointsEnabled())
            {
                maxSlotLevel = 1;
            }
        }
    }

    [HarmonyPatch(typeof(SlotRecoveryModal), nameof(SlotRecoveryModal.Refresh))]
    internal static class Refresh_Patch
    {
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> code)
        {
            var oldMethod = typeof(SlotRecoveryItem).GetMethod(nameof(SlotRecoveryItem.Bind));
            var newMethod =
                typeof(Refresh_Patch).GetMethod(nameof(BindX), BindingFlags.NonPublic | BindingFlags.Static);


            return code.ReplaceCalls(oldMethod, "SlotRecoveryModal.Refresh",
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, newMethod));
        }

        private static void BindX(SlotRecoveryItem item,
            int slotLevel,
            int remainingSlots,
            int maxSlots,
            bool available,
            int recoveredSlots,
            string failure,
            SlotRecoveryItem.SlotRecoveredHandler slotSelected,
            RectTransform tooltipAnchor,
            SlotRecoveryModal modal
        )
        {
            var caster = modal.caster;
            if (!caster.IsSpellPointsEnabled())
            {
                item.Bind(slotLevel, remainingSlots, maxSlots, available, recoveredSlots, failure, slotSelected,
                    tooltipAnchor);
                return;
            }

            var maxPoints = caster.GetMaxSpellPoints();
            var remainingPoints = caster.GetRemainingSpellPoints();
            var cost = SpellPointsContext.SpellCostByLevel[slotLevel];
            maxSlots = modal.slotsCapital;
            remainingSlots = 0;

            available = false;
            if (remainingPoints + recoveredSlots * cost >= maxPoints)
                failure = "Failure/&FailureSlotsOfLevelFull";
            else if (modal.currentCapital == 0)
                failure = "Failure/&FailureSpentCapital";
            else
                available = true;

            item.Bind(slotLevel, remainingSlots, maxSlots, available, recoveredSlots, failure, slotSelected,
                tooltipAnchor);
        }
    }
}
