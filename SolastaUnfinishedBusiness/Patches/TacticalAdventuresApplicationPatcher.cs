using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Models;
using UnityEngine;
using static SolastaUnfinishedBusiness.Models.SaveByLocationContext;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class TacticalAdventuresApplicationPatcher
{
    private static bool EnableSaveByLocation(ref string __result)
    {
        //PATCH: EnableSaveByLocation
        if (!SettingsContext.GuiModManagerInstance.EnableSaveByLocation)
        {
            return true;
        }

        // Modify the value returned by TacticalAdventuresApplication.SaveGameDirectory so that saves
        // end up where we want them (by location/campaign)
        var selectedCampaignService = ServiceRepository.GetService<SelectedCampaignService>();

        // handle exception when saving from world map or encounters on a user campaign
        if (Gui.GameCampaign?.campaignDefinition?.IsUserCampaign == true &&
            selectedCampaignService is { LocationType: LocationType.StandardCampaign })
        {
            __result = GetMostRecentPlace().Path;

            return false;
        }

        __result = selectedCampaignService?.SaveGameDirectory ?? DefaultSaveGameDirectory;

        return false;
    }

    [HarmonyPatch(typeof(TacticalAdventuresApplication), nameof(TacticalAdventuresApplication.SaveGameDirectory),
        MethodType.Getter)]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class SaveGameDirectory_Getter_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(ref string __result)
        {
            return EnableSaveByLocation(ref __result);
        }
    }

    [HarmonyPatch(typeof(TacticalAdventuresApplication), nameof(TacticalAdventuresApplication.HandleLogModException))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleLogModException_Patch
    {
        [UsedImplicitly]
        private static bool Prefix(TacticalAdventuresApplication __instance, string logString, string stackTrace,
            LogType type)
        {
            if (type != LogType.Exception || Gui.GuiService == null)
                return true;
            Gui.GuiService.ShowMessage(MessageModal.Severity.Serious3, Gui.Localize("Message/&ModErrorWarningTitle"),
                Gui.Localize("Message/&ModErrorWarningDescription"), "Message/&ModErrorWarningShowLogs", "Screen/&CloseTitle",
                OpenLogsFolder, null);
            Application.logMessageReceived -= __instance.HandleLogModException;


            return false;
        }

        private static void OpenLogsFolder()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = TacticalAdventuresApplication.GameDirectory,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }
}
