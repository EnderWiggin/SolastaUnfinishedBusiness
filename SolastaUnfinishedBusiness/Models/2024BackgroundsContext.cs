using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using SolastaUnfinishedBusiness.Feats;
using static FeatureDefinitionAttributeModifier;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;
using static SolastaUnfinishedBusiness.Models.FlexibleRacesContext;

namespace SolastaUnfinishedBusiness.Models;

public static partial class Tabletop2024Context
{
    public static class BackgroundASIHelper
    {
        public static FeatureDefinitionFeatureSet BuildBackgroundASI(
            string name,
            string attrA,
            string attrB,
            string attrC)
        {
            // --- Create +1 modifiers ---
            var plus1A = CreatePlus1(name, attrA);
            var plus1B = CreatePlus1(name, attrB);
            var plus1C = CreatePlus1(name, attrC);

            // --- Create +2 modifiers ---
            var plus2A = CreatePlus2(name, attrA);
            var plus2B = CreatePlus2(name, attrB);
            var plus2C = CreatePlus2(name, attrC);

            // --- Create +1/+1/+1 feature set ---
            var featureSet111 = FeatureDefinitionFeatureSetBuilder
                .Create($"FeatureSetBackgroundASI{name}_{attrA}_{attrB}_{attrC}_111")
                .SetGuiPresentation(
                        Gui.Format($"Feature/&FeatureSetBackgroundASI_111_Title",
                            attrA.Substring(0, 3).ToUpper(),
                            attrB.Substring(0, 3).ToUpper(),
                            attrC.Substring(0, 3).ToUpper()),
                        Gui.Format($"Feature/&FeatureSetBackgroundASI_111_Dexcription",
                            attrA.Substring(0, 3).ToUpper(),
                            attrB.Substring(0, 3).ToUpper(),
                            attrC.Substring(0, 3).ToUpper())
                    )
                .SetMode(FeatureDefinitionFeatureSet.FeatureSetMode.Union)
                .AddFeatureSet(plus1A, plus1B, plus1C)
                .AddToDB();

            // --- Create the three “+2 one, +1 one of the others” sets ---
            var setA = CreatePlus2Plus1Set(name, attrA, attrB, attrC, plus2A);
            var setB = CreatePlus2Plus1Set(name, attrB, attrA, attrC, plus2B);
            var setC = CreatePlus2Plus1Set(name, attrC, attrA, attrB, plus2C);

            // --- Final exclusion set ---
            var finalSet = FeatureDefinitionFeatureSetBuilder
                .Create($"FeatureSetBackgroundASI{name}_{attrA}_{attrB}_{attrC}")
                .SetGuiPresentation(
                    Gui.Format($"Feature/&FeatureSetBackgroundASITitle",
                        attrA.Substring(0, 3).ToUpper(),
                        attrB.Substring(0, 3).ToUpper(),
                        attrC.Substring(0, 3).ToUpper()),
                    Gui.Format($"Feature/&FeatureSetBackgroundASIDescription"))
                .SetMode(FeatureDefinitionFeatureSet.FeatureSetMode.Exclusion)
                .AddFeatureSet(setA, setB, setC, featureSet111)
                .AddToDB();

            return finalSet;
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------

        private static FeatureDefinitionAttributeModifier CreatePlus1(string name, string attr)
        {
            return FeatureDefinitionAttributeModifierBuilder
                .Create($"AttributeModifierBackground{name}{attr}1")
                .SetGuiPresentationNoContent(true)
                .SetModifier(AttributeModifierOperation.Additive, attr, 1)
                .AddToDB();
        }

        private static FeatureDefinitionAttributeModifier CreatePlus2(string name, string attr)
        {
            return FeatureDefinitionAttributeModifierBuilder
                .Create($"AttributeModifierBackground{name}{attr}2")
                .SetGuiPresentationNoContent(true)
                .SetModifier(AttributeModifierOperation.Additive, attr, 2)
                .AddToDB();
        }

        private static FeatureDefinitionFeatureSet CreatePlus2Plus1Set(
            string name,
            string plus2Attr,
            string option1,
            string option2,
            FeatureDefinitionAttributeModifier plus2Feature)
        {
            // Create point pool for the +1 choice
            var pool = FeatureDefinitionPointPoolBuilder
                .Create($"PointPoolBackgroundASI{name}_{plus2Attr}_Choose_{option1}_{option2}")
                .SetGuiPresentationNoContent(true)
                .SetPool(HeroDefinitions.PointsPoolType.AbilityScore, 1)
                .AddToDB();

            pool.restrictedChoices = new List<string> { option1, option2 };

            // Combine +2 feature and the point pool
            return FeatureDefinitionFeatureSetBuilder
                .Create($"FeatureSetBackgroundASI{name}_{plus2Attr}_2_{option1}_{option2}_1")
                .SetGuiPresentation(
                    Gui.Format($"Feature/&FeatureSetBackgroundASI_2Choose1_Description",
                        plus2Attr.Substring(0, 3).ToUpper(),
                        option1.Substring(0, 3).ToUpper(),
                        option2.Substring(0, 3).ToUpper()),
                    Gui.Format($"Feature/&FeatureSetBackgroundASI_2Choose1_Title",
                        plus2Attr.Substring(0, 3).ToUpper(),
                        option1.Substring(0, 3).ToUpper(),
                        option2.Substring(0, 3).ToUpper()))
                .SetMode(FeatureDefinitionFeatureSet.FeatureSetMode.Union)
                .AddFeatureSet(plus2Feature, pool)
                .AddToDB();
        }
    }

    private static readonly List<(string Background, string A, string B, string C)> BackgroundAbilitySets =
        new()
        {
            ("Academic", AttributeDefinitions.Dexterity, AttributeDefinitions.Intelligence, AttributeDefinitions.Wisdom),
            ("Acolyte", AttributeDefinitions.Intelligence, AttributeDefinitions.Wisdom, AttributeDefinitions.Charisma),
            ("Aescetic_Background", AttributeDefinitions.Constitution, AttributeDefinitions.Wisdom, AttributeDefinitions.Charisma),
            ("Aristocrat", AttributeDefinitions.Strength, AttributeDefinitions.Intelligence, AttributeDefinitions.Charisma),
            ("Artist_Background", AttributeDefinitions.Strength, AttributeDefinitions.Dexterity, AttributeDefinitions.Charisma),
            ("Lawkeeper", AttributeDefinitions.Strength, AttributeDefinitions.Intelligence, AttributeDefinitions.Wisdom),
            ("Lowlife", AttributeDefinitions.Dexterity, AttributeDefinitions.Constitution, AttributeDefinitions.Intelligence),
            ("Occultist_Background", AttributeDefinitions.Constitution, AttributeDefinitions.Intelligence, AttributeDefinitions.Charisma),
            ("Philosopher", AttributeDefinitions.Constitution, AttributeDefinitions.Intelligence, AttributeDefinitions.Wisdom),
            ("SellSword", AttributeDefinitions.Strength, AttributeDefinitions.Dexterity, AttributeDefinitions.Constitution),
            ("Spy", AttributeDefinitions.Dexterity, AttributeDefinitions.Constitution, AttributeDefinitions.Charisma),
            ("Wanderer", AttributeDefinitions.Dexterity, AttributeDefinitions.Wisdom, AttributeDefinitions.Charisma),
            // UB Backgrounds
            ("Devoted", AttributeDefinitions.Constitution, AttributeDefinitions.Intelligence, AttributeDefinitions.Wisdom),
            ("Farmer", AttributeDefinitions.Strength, AttributeDefinitions.Constitution, AttributeDefinitions.Wisdom),
            ("Militia", AttributeDefinitions.Strength, AttributeDefinitions.Dexterity, AttributeDefinitions.Wisdom),
            ("Troublemaker", AttributeDefinitions.Dexterity, AttributeDefinitions.Constitution, AttributeDefinitions.Charisma)
        };

    private static readonly Dictionary<string, string> BackgroundFeatSets =
        new()
        {
                {"Academic", "FeatSkilled" },
                {"Acolyte", "FeatMagicInitiateCleric" },
                {"Aescetic_Background", "FeatHealer" },
                {"Aristocrat", "FeatSkilled" },
                {"Artist_Background", "FeatMagicInitiateBard" },
                {"Lawkeeper", "FeatAlert" },
                {"Lowlife", "FeatAlert" },
                {"Occultist_Background", "FeatLucky" },
                {"Philosopher", "FeatMagicInitiateWizard" },
                {"SellSword", "FeatSavageAttack" },
                {"Spy", "FeatAlert" },
                {"Wanderer", "FeatLucky" },
                // UB Backgrounds
                {"Devoted", "FeatMagicInitiateCleric" },
                {"Farmer", "FeatTough" },
                {"Militia", "FeatAlert" },
                {"Troublemaker", "FeatSkilled" }
        };

    private static Dictionary<string, FeatureDefinitionFeatureSet> BackgroundASISets = new();
    private static Dictionary<CharacterRaceDefinition, FeatureUnlockByLevel> ASIFeaturesToRemove = new();
    private static readonly List<string> originFeats =
        new()
        {
                "FeatAlert",
                "FeatTough",
                "FeatLucky",
                "FeatHealer",
                "FeatSavageAttack",
                "FeatMagicInitiateCleric",
                "FeatMagicInitiateWizard",
                "FeatMagicInitiateBard"
        };

    internal static void Load2024BackgroundsASIAndFeats()
    {
        // Populate BackgroundASISets
        foreach (var backgroundAbilityScores in BackgroundAbilitySets)
        {
            var featureSet = BackgroundASIHelper.BuildBackgroundASI(
                backgroundAbilityScores.Background,
                backgroundAbilityScores.A,
                backgroundAbilityScores.B,
                backgroundAbilityScores.C);
            BackgroundASISets.TryAdd(backgroundAbilityScores.Background, featureSet);
        }

        // Populate ASIFeaturesToRemove
        var dbCharacterRaceDefinition = DatabaseRepository.GetDatabase<CharacterRaceDefinition>();

        foreach (var characterRaceDefinition in dbCharacterRaceDefinition)
        {
            // Find all ASI features on this race
            foreach (var featureUnlock in characterRaceDefinition.FeatureUnlocks
                 .Where(x =>
                     x.FeatureDefinition.Name.Contains("AbilityScoreIncrease") ||
                     x.FeatureDefinition.Name.Contains("AbilityScoreImprovement")))
            {
                ASIFeaturesToRemove.TryAdd(characterRaceDefinition, featureUnlock);
            }
        }

        // Build point pools
        foreach (var featNameKey in BackgroundFeatSets
                    .Select(kvp => kvp.Value)
                    .Where(v => v != "FeatSkilled")
                    .Distinct())
        {
            var feat = DatabaseRepository.GetDatabase<FeatDefinition>()
                .FirstOrDefault(f => f.Name == featNameKey);

            var featTitle = feat.GuiPresentation.Title;
            var featDescription = feat.GuiPresentation.Description;

            _ = FeatureDefinitionPointPoolBuilder
                    .Create($"PointPool{featNameKey}Feat")
                    .SetGuiPresentation(
                        Gui.Format($"Feature/&BackgroundBonusFeatTitle"),
                        Gui.Format($"Feature/&BackgroundBonusFeatDescription",
                        featTitle))
                    .SetPool(HeroDefinitions.PointsPoolType.Feat, 1)
                    .RestrictChoices(GroupFeats.FeatGroupOrigin.Name, featNameKey)
                    .AddToDB();
        }

        // Skilled doesn't work as a feat in game due to how the game orders learning steps
        // so we are just adding the point pool directly
        _ = FeatureDefinitionPointPoolBuilder
                .Create($"PointPoolFeatSkilledFeat")
                .SetGuiPresentation(
                    "Feature/&PointPoolSkilledTitle",
                    "Feature/&PointPoolSkilledDescription")
                .SetPool(HeroDefinitions.PointsPoolType.Skill, 3)
                .AddToDB();

        SwitchBackgroundBonusFeats();
        SwitchBackgroundASI();

    }

    private static readonly FeatureUnlockByLevel HumanASI =
        new FeatureUnlockByLevel(
            FeatureDefinitionAttributeModifiers.AttributeModifierHumanAbilityScoreIncrease, 1);

    private static readonly FeatureUnlockByLevel HumanASIImprovement =
        new FeatureUnlockByLevel(
            FeatureDefinitionPointPools.PointPoolAbilityScoreImprovement, 1);

    private static void RemoveMatchingFeature([NotNull] List<FeatureUnlockByLevel> unlocks, BaseDefinition toRemove)
    {
        unlocks.RemoveAll(u => u.FeatureDefinition.GUID == toRemove.GUID);
    }

    internal static void SwitchBackgroundASI()
    {
        var dbCharacterRaceDefinition = DatabaseRepository.GetDatabase<CharacterRaceDefinition>();

        // Insert background ASI
        foreach (var keyValuePair in BackgroundASISets)
        {
            foreach (var background in DatabaseRepository.GetDatabase<CharacterBackgroundDefinition>()
                         .Where(b => b.Name.Contains(keyValuePair.Key)))
            {
                if (Main.Settings.EnableBackgroundASI && !background.features.Contains(keyValuePair.Value))
                {
                    background.features.Insert(0, keyValuePair.Value);
                }
                else if (!Main.Settings.EnableBackgroundASI && background.features.Contains(keyValuePair.Value))
                {
                    background.features.Remove(keyValuePair.Value);
                }
            }
        }

        // Remove or restore all race-level ASI features
        foreach (var keyValuePair in publicRemovedFeatures)
        {
            var characterRaceDefinition = dbCharacterRaceDefinition.GetElement(keyValuePair.Key, true);

            if (!characterRaceDefinition)
            {
                continue;
            }

            foreach (var featureDefinitionName in keyValuePair.Value)
            {
                if (!TryGetDefinition<FeatureDefinition>(featureDefinitionName, out var featureDefinition))
                {
                    continue;
                }

                var exists =
                    characterRaceDefinition.FeatureUnlocks.Exists(x => x.FeatureDefinition == featureDefinition);

                switch (exists)
                {
                    case true when Main.Settings.EnableBackgroundASI:
                        RemoveMatchingFeature(characterRaceDefinition.FeatureUnlocks, featureDefinition);
                        break;

                    case false when !Main.Settings.EnableBackgroundASI &&
                            !Main.Settings.EnableFlexibleRaces:
                        characterRaceDefinition.FeatureUnlocks.Add(new FeatureUnlockByLevel(featureDefinition, 1));
                        break;
                }
            }
        }

        // Handle Human separately due to Alternate Human
        var humanRace = dbCharacterRaceDefinition.GetElement("Human", true);

        // Find all ASI-related features
        var toRemoveHuman = humanRace.FeatureUnlocks
            .Where(fu =>
                fu.FeatureDefinition.Name.Contains("AttributeModifierHumanAbilityScoreIncrease") ||
                fu.FeatureDefinition.Name.Contains("PointPoolAbilityScoreImprovement"))
            .ToList();

        foreach (var featureUnlock in toRemoveHuman)
        {
            var exists =
                humanRace.FeatureUnlocks.Contains(featureUnlock);

            switch (exists)
            {
                case true when Main.Settings.EnableBackgroundASI:
                    humanRace.featureUnlocks.Remove(featureUnlock);
                    break;

                case false when !Main.Settings.EnableBackgroundASI &&
                        Main.Settings.EnableAlternateHuman:
                    humanRace.featureUnlocks.Add(HumanASIImprovement);
                    break;

                case false when !Main.Settings.EnableBackgroundASI &&
                        !Main.Settings.EnableAlternateHuman:
                    humanRace.featureUnlocks.Add(HumanASI);
                    break;
            }
        }
    }

    internal static void SwitchAddOriginFeatsToAutoLearn()
    {
        var allClasses = DatabaseRepository.GetDatabase<CharacterClassDefinition>();

        foreach (var characterClass in allClasses)
        {
            if (Main.Settings.EnableBackgroundBonusFeats &&
                    Main.Settings.AddOriginFeatsToAutoLearn)
            {
                characterClass.featAutolearnPreference.AddRange(originFeats);
            }
            else
            {
                foreach (var feat in originFeats)
                {
                    characterClass.featAutolearnPreference.Remove(feat);
                }
            }
        }
    }

    internal static void SwitchBackgroundBonusFeats()
    {
        var dbBackgrounds = DatabaseRepository.GetDatabase<CharacterBackgroundDefinition>();
        var dbFeatures = DatabaseRepository.GetDatabase<FeatureDefinition>();
        var devotedBonus = dbFeatures
            .FirstOrDefault(f => f.Name == "BonusCantripsBackgroundDevoted");

        foreach (var keyValuePair in BackgroundFeatSets)
        {
            var pointPoolName = $"PointPool{keyValuePair.Value}Feat";
            var pointPoolFeature = dbFeatures
                .FirstOrDefault(f => f.Name == pointPoolName);

            foreach (var background in dbBackgrounds.Where(b => b.Name.Contains(keyValuePair.Key)))
            {
                if (Main.Settings.EnableBackgroundBonusFeats)
                {
                    // Add point pool
                    background.features.TryAdd(pointPoolFeature);

                    // Replace Devoted bonus cantrips with a language
                    if (background.Name.Contains("Devoted"))
                    {
                        background.features.Remove(devotedBonus);
                        background.features.TryAdd(FeatureDefinitionPointPools.PointPoolBackgroundLanguageChoice_one);
                    }
                }
                else
                {
                    // Remove point pool
                    background.features.Remove(pointPoolFeature);

                    // Restore Devoted bonus cantrips
                    if (background.Name.Contains("Devoted"))
                    {
                        background.features.TryAdd(devotedBonus);
                        background.features.Remove(FeatureDefinitionPointPools.PointPoolBackgroundLanguageChoice_one);
                    }
                }
            }
        }

        SwitchAddOriginFeatsToAutoLearn();
    }
}
