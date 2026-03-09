using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class CharacterStageBackgroundSelectionPanelPatcher
{
    [HarmonyPatch(typeof(CharacterStageBackgroundSelectionPanel),
        nameof(CharacterStageBackgroundSelectionPanel.OnBeginShow))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class OnBeginShow_Patch
    {
        [UsedImplicitly]
        public static void Prefix([NotNull] CharacterStageBackgroundSelectionPanel __instance)
        {
            //PATCH: avoids a restart when enabling / disabling backgrounds on the Mod UI panel
            __instance.compatibleBackgrounds.Clear();
            __instance.selectedBackgroundPersonalityFlagsMap.Clear();

            foreach (var key in
                     DatabaseRepository.GetDatabase<CharacterBackgroundDefinition>())
            {
                if (key.GuiPresentation.Hidden)
                {
                    continue;
                }

                __instance.compatibleBackgrounds.Add(key);
                __instance.selectedBackgroundPersonalityFlagsMap.Add(key,
                    key.OptionalPersonalityFlags.Count == 2
                        ? [..key.DefaultOptionalPersonalityFlags]
                        : []);
            }

            __instance.compatibleBackgrounds.Sort(__instance);
        }
    }

    //PATCH: allows selecting personality flags after background ability score increases
    [HarmonyPatch(typeof(CharacterStageBackgroundSelectionPanel),
    nameof(CharacterStageBackgroundSelectionPanel.FillBackgroundFeatures))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class FillBackgroundFeatures_Patch
    {
        [UsedImplicitly]

        // Cache a fast field accessor for the private bool newBackgroundSelected
        private static readonly AccessTools.FieldRef<CharacterStageBackgroundSelectionPanel, bool>
        NewBackgroundSelectedRef =
            AccessTools.FieldRefAccess<CharacterStageBackgroundSelectionPanel, bool>("newBackgroundSelected");
        public static bool Prefix(CharacterStageBackgroundSelectionPanel __instance)
        {
            // If no new background was selected, skip the original method entirely
            if (!NewBackgroundSelectedRef(__instance))
            {
                return false;
            }

            // Otherwise, let the game run its normal logic
            return true;
        }
    }
}
