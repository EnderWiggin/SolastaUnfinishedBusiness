using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.FightingStyles;

namespace SolastaUnfinishedBusiness.Models;

internal static class FightingStyleContext
{
    private static Dictionary<FightingStyleDefinition, List<FeatureDefinitionFightingStyleChoice>>
        FightingStylesChoiceList { get; } = [];

    internal static HashSet<FightingStyleDefinition> FightingStyles { get; private set; } = [];

    internal static void Load()
    {
        LoadStyle(new AstralReach());
        LoadStyle(new BlessedWarrior());
        LoadStyle(new BlindFighting());
        LoadStyle(new Crippling());
        LoadStyle(new DruidicWarrior());
        LoadStyle(new Executioner());
        LoadStyle(new HandAndAHalf());
        LoadStyle(new Interception());
        LoadStyle(new Lunger());
        LoadStyle(new RemarkableTechnique());
        LoadStyle(new Torchbearer());

        // sorting
        FightingStyles = [.. FightingStyles.OrderBy(x => x.FormatTitle())];

        // settings paring
        foreach (var name in Main.Settings.FightingStyleEnabled
                     .Where(name => FightingStyles.All(x => x.Name != name))
                     .ToArray())
        {
            Main.Settings.FightingStyleEnabled.Remove(name);
        }
    }

    private static void LoadStyle([NotNull] AbstractFightingStyle styleBuilder)
    {
        var style = styleBuilder.FightingStyle;

        if (!FightingStyles.Contains(style))
        {
            FightingStylesChoiceList.TryAdd(style, styleBuilder.FightingStyleChoice);
            FightingStyles.Add(style);
        }

        UpdateStyleVisibility(style);
    }

    private static void UpdateStyleVisibility([NotNull] FightingStyleDefinition fightingStyleDefinition)
    {
        var name = fightingStyleDefinition.Name;
        var choiceLists = FightingStylesChoiceList[fightingStyleDefinition];
        var enabled = Main.Settings.FightingStyleEnabled.Contains(name);
        var hasFeat = DatabaseRepository.GetDatabase<FeatDefinition>().TryGetElement($"Feat{name}", out var feat);

        if (hasFeat) { feat.GuiPresentation.hidden = !enabled; }

        foreach (var fightingStyles in choiceLists.Select(cl => cl.FightingStyles))
        {
            if (enabled)
            {
                fightingStyles.TryAdd(name);
            }
            else
            {
                fightingStyles.Remove(name);
            }
        }
    }

    internal static bool HideFightingStyle(FeatDefinition feat)
    {
        if (!feat.Name.StartsWith("Feat")) { return false; }

        var styleName = feat.Name.Substring(4);

        return FightingStyles.Any(x => x.Name == styleName)
               && !Main.Settings.FightingStyleEnabled.Contains(styleName);
    }

    internal static bool HideFightingStyle(FightingStyleDefinition fightingStyle)
    {
        return FightingStyles.Contains(fightingStyle)
               && !Main.Settings.FightingStyleEnabled.Contains(fightingStyle.Name);
    }

    internal static void Switch(FightingStyleDefinition fightingStyleDefinition, bool active)
    {
        if (!FightingStyles.Contains(fightingStyleDefinition))
        {
            return;
        }

        var name = fightingStyleDefinition.Name;

        if (active)
        {
            Main.Settings.FightingStyleEnabled.TryAdd(name);
        }
        else
        {
            Main.Settings.FightingStyleEnabled.Remove(name);
        }

        // Druidic and Paladin FS don't have a feat
        if (DatabaseRepository.GetDatabase<FeatDefinition>().TryGetElement($"Feat{name}", out var feat))
        {
            feat.GuiPresentation.hidden = !active;
        }

        GuiWrapperContext.RecacheFeats();
        UpdateStyleVisibility(fightingStyleDefinition);
    }

    internal static void RefreshFightingStylesPatch(RulesetCharacterHero hero)
    {
        foreach (var trainedFightingStyle in hero.trainedFightingStyles
                     .Where(x =>
                         // activate all modded fighting styles by default
                         x.contentPack == CeContentPackContext.CeContentPack ||
                         // handles this in a different place [AddCustomWeaponValidatorToFightingStyleArchery()] so always allow here
                         x.Condition == FightingStyleDefinition.TriggerCondition.RangedWeaponAttack))
        {
            hero.activeFightingStyles.TryAdd(trainedFightingStyle);
        }
    }
}
