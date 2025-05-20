using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Behaviors;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using SolastaUnfinishedBusiness.Interfaces;
using SolastaUnfinishedBusiness.CustomBehaviors;
using SolastaUnfinishedBusiness.CustomInterfaces;
using TA;
using static RuleDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;

namespace SolastaUnfinishedBusiness.Subclasses;

/// <summary>
/// Obiwan's ShadeKnight 
/// </summary>
//TODO: Implement?
[UsedImplicitly]
public sealed class MartialShadeKnight : AbstractSubclass
{
    private const string Name = "ShadeKnight";
    internal const string FullName = $"Martial{Name}";

    public MartialShadeKnight()
    {
        var powerSummonShade = BuildShadePower(
            $"Power{Name}SummonShade", MonsterDefinitions.Adam_The_Twelth, 1, new List<FeatureDefinition>());

        var hpBonus = FeatureDefinitionAttributeModifierBuilder
            .Create($"AttributeModifier{Name}ShadeHitPoints")
            .SetGuiPresentationNoContent(true)
            .SetModifier(FeatureDefinitionAttributeModifier.AttributeModifierOperation.AddConditionAmount,
                AttributeDefinitions.HitPoints)
            .AddToDB();

        var summoningAffinityShade = FeatureDefinitionSummoningAffinityBuilder
            .Create($"SummoningAffinity{Name}Shade")
            .SetGuiPresentationNoContent(true)
            .SetRequiredMonsterTag("Shade")
            .SetAddedConditions(
                ConditionDefinitionBuilder
                    .Create($"Condition{Name}ShadeCopyStrength")
                    .SetGuiPresentationNoContent(true)
                    .SetSilent(Silent.WhenAddedOrRemoved)
                    .SetAmountOrigin(
                        ExtraOriginOfAmount.SourceCopyAttributeFromSummoner, AttributeDefinitions.Strength)
                    .AddToDB(),
                ConditionDefinitionBuilder
                    .Create($"Condition{Name}ShadeCopyDexterity")
                    .SetGuiPresentationNoContent(true)
                    .SetSilent(Silent.WhenAddedOrRemoved)
                    .SetAmountOrigin(
                        ExtraOriginOfAmount.SourceCopyAttributeFromSummoner, AttributeDefinitions.Dexterity)
                    .AddToDB(),
                ConditionDefinitionBuilder
                    .Create($"Condition{Name}ShadeCopyConstitution")
                    .SetGuiPresentationNoContent(true)
                    .SetSilent(Silent.WhenAddedOrRemoved)
                    .SetAmountOrigin(
                        ExtraOriginOfAmount.SourceCopyAttributeFromSummoner, AttributeDefinitions.Constitution)
                    .AddToDB(),
                ConditionDefinitionBuilder
                    .Create($"Condition{Name}ShadeCopyIntelligence")
                    .SetGuiPresentationNoContent(true)
                    .SetSilent(Silent.WhenAddedOrRemoved)
                    .SetAmountOrigin(
                        ExtraOriginOfAmount.SourceCopyAttributeFromSummoner, AttributeDefinitions.Intelligence)
                    .AddToDB(),
                ConditionDefinitionBuilder
                    .Create($"Condition{Name}ShadeCopyWisdom")
                    .SetGuiPresentationNoContent(true)
                    .SetSilent(Silent.WhenAddedOrRemoved)
                    .SetAmountOrigin(
                        ExtraOriginOfAmount.SourceCopyAttributeFromSummoner, AttributeDefinitions.Wisdom)
                    .AddToDB(),
                ConditionDefinitionBuilder
                    .Create($"Condition{Name}ShadeCopyCharisma")
                    .SetGuiPresentationNoContent(true)
                    .SetSilent(Silent.WhenAddedOrRemoved)
                    .SetAmountOrigin(
                        ExtraOriginOfAmount.SourceCopyAttributeFromSummoner, AttributeDefinitions.Charisma)
                    .AddToDB(),
                ConditionDefinitionBuilder
                    .Create($"Condition{Name}ShadeHitPoints")
                    .SetGuiPresentationNoContent(true)
                    .SetSilent(Silent.WhenAddedOrRemoved)
                    .SetAmountOrigin(ExtraOriginOfAmount.SourceCharacterLevel)
                    .SetFeatures(hpBonus)
                    .AddToDB())
            .AddToDB();

        var featureSetShade = FeatureDefinitionFeatureSetBuilder
            .Create($"FeatureSet{Name}SummonShade")
            .SetGuiPresentation(Category.Feature)
            .SetFeatureSet(powerSummonShade, summoningAffinityShade)
            .AddToDB();

        Subclass = CharacterSubclassDefinitionBuilder
            .Create($"Martial{Name}")
            .SetGuiPresentation(Category.Subclass, CharacterSubclassDefinitions.OathOfJugement)
            .AddFeaturesAtLevel(3, featureSetShade)
            .AddFeaturesAtLevel(7)
            .AddFeaturesAtLevel(10)
            .AddFeaturesAtLevel(15)
            .AddFeaturesAtLevel(18)
            .AddToDB();
    }

    internal override CharacterClassDefinition Klass => CharacterClassDefinitions.Fighter;

    internal override CharacterSubclassDefinition Subclass { get; }

    internal override FeatureDefinitionSubclassChoice SubclassChoice =>
        FeatureDefinitionSubclassChoices.SubclassChoiceFighterMartialArchetypes;

    // ReSharper disable once UnassignedGetOnlyAutoProperty
    internal override DeityDefinition DeityDefinition { get; }

    //
    // Shade
    //

    private static FeatureDefinitionPower BuildShadePower(
        string powerName,
        MonsterDefinition monsterDefinition,
        int level,
        IEnumerable<FeatureDefinition> monsterAdditionalFeatures)
    {
        var monster = BuildShadeMonster(powerName, monsterDefinition, level, monsterAdditionalFeatures);

        var powerMoveShade = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}MoveShade")
            .SetGuiPresentation(Category.Feature)
            .SetUsesFixed(ActivationTime.NoCost)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetDurationData(DurationType.Round)
                    .SetTargetingData(Side.Ally, RangeType.Distance, 18, TargetType.Position)
                    .Build())
            .AddCustomSubFeatures(new CustomBehaviorMoveShade())
            .AddToDB();

        var conditionControls = ConditionDefinitionBuilder
            .Create($"Condition{Name}Controls")
            .SetGuiPresentation(Category.Condition)
            .AddFeatures(powerMoveShade)
            .AddCustomSubFeatures(
                new AddUsablePowersFromCondition(),
                new CustomBehaviorControls())
            .AddToDB();

        var power = FeatureDefinitionPowerBuilder
            .Create(powerName + level)
            .SetGuiPresentation(powerName, Category.Feature)
            .SetUsesFixed(ActivationTime.NoCost)
            .SetUniqueInstance()
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetDurationData(DurationType.UntilAnyRest)
                    .SetTargetingData(Side.Ally, RangeType.Distance, 3, TargetType.Position)
                    .SetEffectForms(
                        EffectFormBuilder
                            .Create()
                            .SetSummonCreatureForm(1, monster.Name)
                            .Build(),
                        EffectFormBuilder
                            .Create()
                            .SetConditionForm(conditionControls, ConditionForm.ConditionOperation.Add, true, true)
                            .Build())
                    .SetParticleEffectParameters(SpellDefinitions.ConjureGoblinoids)
                    .Build())
            .AddCustomSubFeatures(
                SkipEffectRemovalOnLocationChange.Always)
            .AddToDB();

        return power;
    }

    private static MonsterDefinition BuildShadeMonster(
        string shadeName,
        MonsterDefinition monsterDefinition,
        int level,
        IEnumerable<FeatureDefinition> monsterAdditionalFeatures)
    {
        var monsterName = $"{Name}{shadeName}{level}";
        var presentationName = $"Power{Name}{shadeName}";

        var monster = MonsterDefinitionBuilder
            .Create(monsterDefinition, monsterName)
            .SetOrUpdateGuiPresentation(presentationName, Category.Feature)
            .SetSizeDefinition(CharacterSizeDefinitions.Small)
            .SetMonsterPresentation(
                MonsterPresentationBuilder
                    .Create()
                    .SetAllPrefab(monsterDefinition.MonsterPresentation)
                    .SetPhantom()
                    .SetModelScale(1f)
                    .SetHasMonsterPortraitBackground(true)
                    .SetCanGeneratePortrait(true)
                    .Build())
            .SetStandardHitPoints(100)
            .SetHeight(6)
            .NoExperienceGain()
            .SetArmorClass(14)
            .SetChallengeRating(0)
            .SetHitDice(DieType.D10, 1)
            .SetAbilityScores(12, 12, 12, 12, 12, 12)
            .SetDefaultFaction(FactionDefinitions.Party)
            .SetCreatureTags("Shade")
            .SetBestiaryEntry(BestiaryDefinitions.BestiaryEntry.None)
            .SetFullyControlledWhenAllied(true)
            .SetDungeonMakerPresence(MonsterDefinition.DungeonMaker.None)
            .ClearAttackIterations()
            .AddFeatures(
                FeatureDefinitionConditionAffinityBuilder
                    .Create($"ConditionAffinity{Name}ShadeNoSurprise")
                    .SetGuiPresentationNoContent(true)
                    .SetConditionAffinityType(ConditionAffinityType.Immunity)
                    .SetConditionType(ConditionDefinitions.ConditionSurprised)
                    .AddCustomSubFeatures(ForceInitiativeToSummoner.Mark)
                    .AddToDB())
            .AddFeatures(monsterAdditionalFeatures.ToArray())
            .AddToDB();

        monster.guiPresentation.description = GuiPresentationBuilder.EmptyString;

        return monster;
    }

    //
    // Shade Controls
    //

    [CanBeNull]
    internal static GameLocationCharacter GetMyShade(GameLocationCharacter summoner)
    {
        var gameLocationCharacterService = ServiceRepository.GetService<IGameLocationCharacterService>();

        var summon = gameLocationCharacterService?.GuestCharacters
            .FirstOrDefault(x =>
                x.RulesetCharacter.Tags.Contains("Shade")
                && x.RulesetCharacter.AllConditions.Any(y =>
                    y.ConditionDefinition == ConditionDefinitions.ConditionConjuredCreature
                    && y.SourceGuid == summoner?.Guid));

        return summon;
    }

    private sealed class CustomBehaviorControls : IFilterTargetingPosition
    {
        public void Filter(CursorLocationSelectPosition __instance)
        {
            var source = __instance.ActionParams.ActingCharacter;
            var shade = GetMyShade(source);

            if (shade == null)
            {
                return;
            }

            var maxShadeMoves = source.UsedSpecialFeatures["MaxShadeMoves"];
            var usedShadeMoves = source.UsedSpecialFeatures["UsedShadeMoves"];
            var remainingMoves = maxShadeMoves - usedShadeMoves;

            var validPositions = new List<int3>();
            var boxInt = new BoxInt(shade.LocationPosition, int3.zero, int3.zero);

            boxInt.Inflate(remainingMoves);

            foreach (var position in boxInt.EnumerateAllPositionsWithin())
            {
                validPositions.Add(position);
            }

            __instance.validPositionsCache.RemoveAll(x => !validPositions.Contains(x));
        }
    }

    //
    // Move Shade
    //

    private sealed class CustomBehaviorMoveShade :
        IMagicEffectFinishedByMe, ICharacterTurnStartListener, IValidatePowerUse
    {
        public void OnCharacterTurnStarted(GameLocationCharacter source)
        {
            // init shade movement tracker
            source.UsedSpecialFeatures.TryAdd("MaxShadeMoves", source.RemainingTacticalMoves);
            source.UsedSpecialFeatures.TryAdd("UsedShadeMoves", 0);
        }

        public IEnumerator OnMagicEffectFinishedByMe(CharacterActionMagicEffect action, BaseDefinition baseDefinition)
        {
            var source = action.ActionParams.ActingCharacter;
            var shade = GetMyShade(source);

            if (shade == null)
            {
                yield break;
            }

            var actionParams = action.ActionParams;

            //TODO: improve distance calculation
            var distance = (int)int3.Distance(source.LocationPosition, actionParams.Positions[0]);

            source.UsedSpecialFeatures["UsedShadeMoves"] += distance;

            // move shad
            var gameLocationActionService = ServiceRepository.GetService<IGameLocationActionService>();
            var newActionParams = new CharacterActionParams(shade, ActionDefinitions.Id.TacticalMove)
            {
                Positions = { action.ActionParams.Positions[0] }
            };

            gameLocationActionService.ExecuteAction(newActionParams, null, false);
        }

        public bool CanUsePower(RulesetCharacter character, FeatureDefinitionPower power)
        {
            // can only have shade tactical control during battles
            return Gui.Battle != null;
        }
    }
}
