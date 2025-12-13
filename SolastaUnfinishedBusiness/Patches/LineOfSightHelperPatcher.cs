using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Models;
using TA;
using UnityEngine;

namespace SolastaUnfinishedBusiness.Patches;
public static class LineOfSightHelperPatcher
{
    [HarmonyPatch(typeof(LineOfSightHelper), nameof(LineOfSightHelper.ShouldDisplay))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ShouldDisplay_Patch
    {
        [UsedImplicitly]
        public static void Postfix(LineOfSightHelper __instance,
                SectorId hoveredLocationSectorID,
                int3 hoveredLocation,
                bool hoveredGUI,
                ref bool __result
            )
        {
            //PATCH: Out of combat, when targeting, sight lines will appear towards visible enemies
            if (Main.Settings.EnableOutOfCombatTargetingSightLines)
            {
                //PATCH: Don't mess with the normal usage of this function, which isn't supposed to work outside of combat
                if (__instance.gameLocationBattleService == null || __instance.gameLocationBattleService.IsBattleInProgress) return;

                __instance.currentMode = LineOfSightHelper.FeedbackMode.VisibleTargetsFromPosition;
                __result = true;
            }


        }
    }
}
