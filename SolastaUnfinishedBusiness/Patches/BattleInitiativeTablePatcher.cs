using System;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.Helpers;
using UnityEngine;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
internal class BattleInitiativeTablePatcher
{
    [HarmonyPatch(typeof(BattleInitiativeTable), nameof(BattleInitiativeTable.OnBeginShow))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class OnBeginShow_Patch
    {
        [UsedImplicitly]
        public static void Prefix([NotNull] BattleInitiativeTable __instance)
        {
            if (Main.Settings.WideScreenBattleUI)
            {
                int width = UiHelpers.GetScreenResolution().x;
                if (width > 1600)
                {
                    __instance.RectTransform.SetSizeWithCurrentAnchors(UnityEngine.RectTransform.Axis.Horizontal, width - 700);
                }
            }
        }
    }
}
