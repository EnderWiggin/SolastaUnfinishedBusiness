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
                float aspectRatio = UiHelpers.GetAspectRatio();
                if (aspectRatio > 1.778f)
                {
                    // The Overlay Canvas may use a higher internal resolution than the actual window size. 
                    //Using it as a reference ensures this works even for unconventional small but wide window sizes.
                    float expanded = UiHelpers.GetOverlayCanvasSize().x - 210;
                    __instance.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, expanded);
                }                
            }
        }
    }
}
