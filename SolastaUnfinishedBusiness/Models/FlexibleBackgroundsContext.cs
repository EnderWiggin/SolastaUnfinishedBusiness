using System.Collections.Generic;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterBackgroundDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionProficiencys;

namespace SolastaUnfinishedBusiness.Models;

internal static class FlexibleBackgroundsContext
{
    private static readonly FeatureDefinition SkillThree = FeatureDefinitionPointPoolBuilder
        .Create("PointPoolBackgroundSkillSelect3")
        .SetGuiPresentation(Category.Background)
        .SetPool(HeroDefinitions.PointsPoolType.Skill, 3)
        .AddToDB();

    private static readonly FeatureDefinition SkillTwo = FeatureDefinitionPointPoolBuilder
        .Create("PointPoolBackgroundSkillSelect2")
        .SetGuiPresentation(Category.Background)
        .SetPool(HeroDefinitions.PointsPoolType.Skill, 2)
        .AddToDB();

    internal static readonly FeatureDefinition SkillOne = FeatureDefinitionPointPoolBuilder
        .Create("PointPoolBackgroundSkillSelect1")
        .SetGuiPresentation(Category.Background)
        .SetPool(HeroDefinitions.PointsPoolType.Skill, 1)
        .AddToDB();

    internal static readonly FeatureDefinition ToolChoice = FeatureDefinitionPointPoolBuilder
        .Create("PointPoolBackgroundToolSelect")
        .SetGuiPresentation(Category.Background)
        .SetPool(HeroDefinitions.PointsPoolType.Tool, 1)
        .AddToDB();

    private static readonly FeatureDefinition ToolChoiceTwo = FeatureDefinitionPointPoolBuilder
        .Create("PointPoolBackgroundToolSelect2")
        .SetGuiPresentation(Category.Background)
        .SetPool(HeroDefinitions.PointsPoolType.Tool, 2)
        .AddToDB();

    private static readonly Dictionary<CharacterBackgroundDefinition, List<FeatureDefinition>> AddedFeatures = new()
    {
        {
            Academic, [
                SkillThree,
                MakeSuggestedSkillsFor("SuggestedSkillsAcademicBackground", ProficiencyAcademicSkills),
                ToolChoice
            ]
        },
        {
            Acolyte, [
                SkillThree,
                MakeSuggestedSkillsFor("SuggestedSkillsAcolyteBackground", ProficiencyAcolyteSkills),
                ToolChoice
            ]
        },
        {
            Aristocrat, [
                SkillThree,
                MakeSuggestedSkillsFor("SuggestedSkillsAristocratBackground", ProficiencyAristocratSkills)
            ]
        },
        {
            Lawkeeper, [
                SkillTwo,
                MakeSuggestedSkillsFor("SuggestedSkillsLawkeeperBackground", ProficiencyLawkeeperSkills)
            ]
        },
        {
            Lowlife, [
                SkillThree,
                MakeSuggestedSkillsFor("SuggestedSkillsLowlifeBackground", ProficiencyLowlifeSkills),
                ToolChoice
            ]
        },
        {
            Philosopher, [
                SkillTwo,
                MakeSuggestedSkillsFor("SuggestedSkillsPhilosopherBackground", ProficiencyPhilosopherSkills),
                ToolChoice
            ]
        },
        {
            SellSword, [
                SkillTwo,
                MakeSuggestedSkillsFor("SuggestedSkillsSellswordBackground", ProficiencySellSwordSkills),
                ToolChoice
            ]
        },
        {
            Spy, [
                SkillThree,
                MakeSuggestedSkillsFor("SuggestedSkillsSpyBackground", ProficiencySpySkills),
                ToolChoice
            ]
        },
        {
            Wanderer, [
                SkillTwo,
                MakeSuggestedSkillsFor("SuggestedSkillsWandererBackground", ProficiencyWandererSkills),
                ToolChoiceTwo
            ]
        },
        {
            Aescetic_Background, [
                SkillTwo,
                MakeSuggestedSkillsFor("SuggestedSkillsAesceticBackground", ProficiencyAesceticSkills),
                ToolChoice
            ]
        },
        {
            Artist_Background, [
                SkillThree,
                MakeSuggestedSkillsFor("SuggestedSkillsArtistBackground", ProficiencyArtistSkills)
            ]
        },
        {
            Occultist_Background, [
                SkillTwo,
                MakeSuggestedSkillsFor("SuggestedSkillsOccultistBackground", ProficiencyOccultistSkills),
                ToolChoice
            ]
        }
    };

    private static readonly Dictionary<CharacterBackgroundDefinition, List<FeatureDefinition>> RemovedFeatures =
        new()
        {
            { Academic, [ProficiencyAcademicSkills, ProficiencyAcademicSkillsTool] },
            { Acolyte, [ProficiencyAcolyteSkills, ProficiencyAcolyteToolsSkills] },
            { Aristocrat, [ProficiencyAristocratSkills] },
            { Lawkeeper, [ProficiencyLawkeeperSkills] },
            { Lowlife, [ProficiencyLowlifeSkills, ProficiencyLowLifeSkillsTools] },
            { Philosopher, [ProficiencyPhilosopherSkills, ProficiencyPhilosopherTools] },
            { SellSword, [ProficiencySellSwordSkills, ProficiencySmithTools] },
            { Spy, [ProficiencySpySkills, ProficienctSpySkillsTool] },
            { Wanderer, [ProficiencyWandererSkills, ProficiencyWandererTools] },
            { Aescetic_Background, [ProficiencyAesceticSkills, ProficiencyAesceticToolsSkills] },
            { Artist_Background, [ProficiencyArtistSkills] },
            { Occultist_Background, [ProficiencyOccultistSkills, ProficiencyOccultistToolsSkills] }
        };

    internal static void Load()
    {
        var backgrounds = new List<string> { "Devoted", "Farmer", "Militia", "Troublemaker" };

        foreach (var background in backgrounds)
        {
            var backgroundDefinition =
                DatabaseHelper.GetDefinition<CharacterBackgroundDefinition>($"Background{background}");
            var skillsDefinition = DatabaseHelper.GetDefinition<FeatureDefinitionProficiency>(
                $"ProficiencyBackground{background}Skills");

            AddedFeatures.Add(
                backgroundDefinition,
                [
                    SkillThree,
                    MakeSuggestedSkillsFor($"SuggestedSkills{background}Background", skillsDefinition)
                ]);

            RemovedFeatures.Add(
                backgroundDefinition,
                [
                    skillsDefinition
                ]);
        }
    }

    private static FeatureDefinition MakeSuggestedSkillsFor(string name, FeatureDefinitionProficiency skills)
    {
        return FeatureDefinitionBuilder
            .Create(name)
            .SetGuiPresentation("Background/&SuggestedSkillsTitle", skills.EnumerateProficiencies())
            .AddToDB();
    }

    internal static void SwitchFlexibleBackgrounds()
    {
        var enabled = Main.Settings.EnableFlexibleBackgrounds;

        foreach (var keyValuePair in AddedFeatures)
        {
            foreach (var featureDefinition in keyValuePair.Value)
            {
                if (!keyValuePair.Key.Features.Contains(featureDefinition) && enabled)
                {
                    keyValuePair.Key.Features.Add(featureDefinition);
                }
                else if (keyValuePair.Key.Features.Contains(featureDefinition) && !enabled)
                {
                    keyValuePair.Key.Features.Remove(featureDefinition);
                }
            }
        }

        foreach (var keyValuePair in RemovedFeatures)
        {
            foreach (var featureDefinition in keyValuePair.Value)
            {
                if (keyValuePair.Key.Features.Contains(featureDefinition) && enabled)
                {
                    keyValuePair.Key.Features.Remove(featureDefinition);
                }
                else if (!keyValuePair.Key.Features.Contains(featureDefinition) && !enabled)
                {
                    keyValuePair.Key.Features.Add(featureDefinition);
                }
            }
        }
    }
}
