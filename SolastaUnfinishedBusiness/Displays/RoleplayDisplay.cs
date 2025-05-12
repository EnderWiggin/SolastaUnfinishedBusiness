using SolastaUnfinishedBusiness.Api.ModKit;
using SolastaUnfinishedBusiness.Models;

namespace SolastaUnfinishedBusiness.Displays;
/// <summary>
/// TODO: Make this its own tab for rules. I have a dayjob :(
/// </summary>
internal static class RoleplayDisplay
{
    internal static void DisplayRoleplaySettings()
    {
        bool toggle = false;
        UI.Label();
        UI.Label(Gui.Localize("ModUI/&RoleplaySettingsDescription"));
        UI.Label();
        toggle = Main.Settings.EnableCriticalHitsMissesAt10;
        if (UI.Toggle(Gui.Localize("ModUI/&EnableCriticalHitsMissesAt10"), ref toggle,
                UI.AutoWidth()))
        {
            Main.Settings.EnableCriticalHitsMissesAt10 = toggle;
        }

        toggle = Main.Settings.ModifyJumpRulesForArmorAndEncumberance;
        if (UI.Toggle(Gui.Localize("ModUI/&ModifyJumpRulesForArmorAndEncumberance"), ref toggle,
                UI.AutoWidth()))
        {
            Main.Settings.ModifyJumpRulesForArmorAndEncumberance = toggle;
        }

        toggle = Main.Settings.ModifyThrowingRulesForStrength;
        if (UI.Toggle(Gui.Localize("ModUI/&ModifyThrowingRulesForStrength"), ref toggle,
                UI.AutoWidth()))
        {
            Main.Settings.ModifyThrowingRulesForStrength = toggle;
        }

        toggle = Main.Settings.StealthBreaksWhenMoving;
        if (UI.Toggle(Gui.Localize("ModUi/&StealthBreaksWhenMoving"), ref toggle, UI.AutoWidth()))
        {
            Main.Settings.StealthBreaksWhenMoving = toggle;
        }

        toggle = Main.Settings.StealthRollForBreak;
        if (UI.Toggle(Gui.Localize("ModUi/&StealthRollForBreak"), ref toggle, UI.AutoWidth()))
        {
            Main.Settings.StealthRollForBreak = toggle;
        }

        toggle = Main.Settings.EnableShotInDarknessPenalties;
        if (UI.Toggle(Gui.Localize("ModUI/&EnableShotInDarknessPenalties"), ref toggle, UI.AutoWidth()))
        {
            Main.Settings.EnableShotInDarknessPenalties = toggle;
        }

        toggle = Main.Settings.EnableChanceToPerceiveCloseRange;
        if (UI.Toggle(Gui.Localize("ModUI/&EnableChanceToPerceiveCloseRange"), ref toggle, UI.AutoWidth()))
        {
            Main.Settings.EnableChanceToPerceiveCloseRange = toggle;
        }
    }
}
