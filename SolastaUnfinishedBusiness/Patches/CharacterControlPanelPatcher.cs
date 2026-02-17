using System;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.Helpers;
using UnityEngine;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class CharacterControlPanelPatcher
{
    [HarmonyPatch(typeof(CharacterControlPanel), nameof(CharacterControlPanel.OnBeginShow))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class OnBeginShow_Patch
    {
        [UsedImplicitly]
        public static void Prefix([NotNull] CharacterControlPanel __instance)
        {
            if (Main.Settings.WideScreenBattleUI)
            {
                int width = UiHelpers.GetScreenResolution().x;
                if (width > 1600)
                {
                    int expansion = width - 400;
                    __instance.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, expansion);
                }                
            }
        }
    }
}
