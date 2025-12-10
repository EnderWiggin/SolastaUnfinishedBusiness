using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Api.ModKit;
using SolastaUnfinishedBusiness.Behaviors;
using SolastaUnfinishedBusiness.Behaviors.Specific;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using SolastaUnfinishedBusiness.CustomUI;
using SolastaUnfinishedBusiness.Feats;
using SolastaUnfinishedBusiness.Interfaces;
using SolastaUnfinishedBusiness.Validators;
using static ActionDefinitions;
using static RuleDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionActionAffinitys;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;
using Resources = SolastaUnfinishedBusiness.Properties.Resources;

namespace SolastaUnfinishedBusiness.Models;

public static partial class Tabletop2024Context
{
    private const string Stage = "WeaponMasteryRelearn";
    private const string IndexUnlearn = "WeaponMasteryUnlearn";
    private const string IndexLearn = "WeaponMasteryLearn";
    private const int StageNotLearned = -1;
    private const int StageLearned = 1;

    private const string WeaponMasteryCleave = "WeaponMasteryCleave";
    internal const string WeaponMasteryNick = "WeaponMasteryNick";
    private const string WeaponMasteryTopple = "WeaponMasteryTopple";
    private static readonly Dictionary<MasteryProperty, FeatureDefinition> MasteryFeatures = [];

    internal static readonly FeatureDefinition FeatureWeaponMasteryBehavior = FeatureDefinitionBuilder
        .Create("FeatureWeaponMasteryBehavior")
        .SetGuiPresentationNoContent(true)
        .AddCustomSubFeatures(new CustomBehaviorWeaponMastery())
        .AddToDB();

    internal static readonly FeatureDefinitionPower PowerWeaponMasteryRelearnPool = FeatureDefinitionPowerBuilder
        .Create("PowerWeaponMasteryRelearnPool")
        .SetGuiPresentationNoContent(true)
        .SetShowCasting(false)
        .SetUsesFixed(ActivationTime.NoCost)
        .SetEffectDescription(
            EffectDescriptionBuilder
                .Create()
                .SetTargetingData(Side.Ally, RangeType.Self, 0, TargetType.Self)
                .Build())
        .AddCustomSubFeatures(new MagicEffectFinishedByMeRelearn())
        .AddToDB();

    private static readonly FeatureDefinitionPower PowerWeaponMasteryRelearn = FeatureDefinitionPowerBuilder
        .Create("PowerWeaponMasteryRelearn")
        .SetGuiPresentation(Category.Feature,
            Sprites.GetSprite("PowerWeaponMasteryRelearn", Resources.PowerWeaponMasteryRelearn, 256, 128))
        .SetShowCasting(false)
        .SetUsesFixed(ActivationTime.NoCost, RechargeRate.LongRest)
        .SetEffectDescription(
            EffectDescriptionBuilder
                .Create()
                .SetTargetingData(Side.Ally, RangeType.Self, 0, TargetType.Self)
                .Build())
        .AddCustomSubFeatures(
            ValidatorsValidatePowerUse.NotInCombat,
            new ValidatorsValidatePowerUse(c => c.GetRemainingPowerUses(PowerWeaponMasteryRelearnPool) > 0),
            new PowerOrSpellFinishedByMeRelearn())
        .AddToDB();

    private static readonly FeatureDefinitionFeatureSet FeatureSetWeaponMasteryLearn1 =
        FeatureDefinitionFeatureSetBuilder
            .Create("FeatureSetWeaponMasteryLearn1")
            .SetGuiPresentation("InvocationPoolWeaponMasteryLearn", Category.Feature)
            .SetFeatureSet(
                PowerWeaponMasteryRelearnPool,
                PowerWeaponMasteryRelearn,
                CustomInvocationPoolDefinitionBuilder
                    .Create("InvocationPoolWeaponMasteryLearn1")
                    .SetGuiPresentation("InvocationPoolWeaponMasteryLearn", Category.Feature)
                    .Setup(InvocationPoolTypeCustom.Pools.WeaponMasterySpecialization)
                    .AddToDB())
            .AddToDB();

    private static readonly FeatureDefinitionFeatureSet FeatureSetWeaponMasteryLearn2 =
        FeatureDefinitionFeatureSetBuilder
            .Create("FeatureSetWeaponMasteryLearn2")
            .SetGuiPresentation("InvocationPoolWeaponMasteryLearn", Category.Feature)
            .SetFeatureSet(
                PowerWeaponMasteryRelearnPool,
                PowerWeaponMasteryRelearn,
                CustomInvocationPoolDefinitionBuilder
                    .Create("InvocationPoolWeaponMasteryLearn2")
                    .SetGuiPresentation("InvocationPoolWeaponMasteryLearn", Category.Feature)
                    .Setup(InvocationPoolTypeCustom.Pools.WeaponMasterySpecialization, 2)
                    .AddToDB())
            .AddToDB();

    private static readonly FeatureDefinitionFeatureSet FeatureSetWeaponMasteryLearn3 =
        FeatureDefinitionFeatureSetBuilder
            .Create("FeatureSetWeaponMasteryLearn3")
            .SetGuiPresentation("InvocationPoolWeaponMasteryLearn", Category.Feature)
            .SetFeatureSet(
                PowerWeaponMasteryRelearnPool,
                PowerWeaponMasteryRelearn,
                CustomInvocationPoolDefinitionBuilder
                    .Create("InvocationPoolWeaponMasteryLearn3")
                    .SetGuiPresentation("InvocationPoolWeaponMasteryLearn", Category.Feature)
                    .Setup(InvocationPoolTypeCustom.Pools.WeaponMasterySpecialization, 3)
                    .AddToDB())
            .AddToDB();

    private static readonly ConditionDefinition ConditionWeaponMasteryCleave =
        ConditionDefinitionBuilder
            .Create("ConditionWeaponMasteryCleave")
            .SetGuiPresentationNoContent(true)
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetFeatures(
                FeatureDefinitionActionAffinityBuilder
                    .Create("ActionAffinityWeaponMasteryCleave")
                    .SetGuiPresentationNoContent(true)
                    .SetAuthorizedActions((Id)ExtraActionId.WeaponMasteryCleave)
                    .AddCustomSubFeatures(
                        new ValidateDefinitionApplication(c => c.GetMainMastery() == MasteryProperty.Cleave))
                    .AddToDB())
            .AddCustomSubFeatures(new CustomBehaviorConditionCleave())
            .AddToDB();

    //Kept for compatibility
    private static readonly ConditionDefinition ConditionWeaponMasteryNick =
        ConditionDefinitionBuilder
            .Create("ConditionWeaponMasteryNick")
            .SetGuiPresentationNoContent(true)
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetFeatures(
                FeatureDefinitionBuilder
                    .Create("AdditionalActionWeaponMasteryNick")
                    .SetGuiPresentation("Feature/&FeatureWeaponMasteryNickTitle",
                        "Feature/&FeatureWeaponMasteryNickDescription", hidden: true)
                    .AddToDB())
            .AddToDB();

    //Kept for compatibility
    private static readonly ConditionDefinition ConditionWeaponMasteryNickDenyAttackOff =
        ConditionDefinitionBuilder
            .Create("ConditionWeaponMasteryNickDenyAttackOff")
            .SetGuiPresentationNoContent(true)
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetFeatures(
                FeatureDefinitionBuilder
                    .Create("ActionAffinityWeaponMasteryNickDenyAttackOff")
                    .SetGuiPresentationNoContent(true)
                    .AddToDB())
            .AddToDB();

    //Kept for compatibility
    private static readonly ConditionDefinition ConditionWeaponMasteryNickDenyAllBonusButAttack =
        ConditionDefinitionBuilder
            .Create("ConditionWeaponMasteryNickDenyAllBonusButAttack")
            .SetGuiPresentationNoContent(true)
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetFeatures(
                FeatureDefinitionBuilder
                    .Create("ActionAffinityWeaponMasteryDenyAllBonusButAttack")
                    .SetGuiPresentationNoContent(true)
                    .AddToDB())
            .AddToDB();

    private static readonly ConditionDefinition ConditionWeaponMasterySap = ConditionDefinitionBuilder
        .Create("ConditionWeaponMasterySap")
        .SetGuiPresentation(Category.Condition, ConditionDefinitions.ConditionCursed)
        .SetConditionType(ConditionType.Detrimental)
        .SetSilent(Silent.None)
        .AddFeatures(
            FeatureDefinitionCombatAffinityBuilder
                .Create("CombatAffinityWeaponMasterySap")
                .SetGuiPresentationNoContent(true)
                .SetMyAttackAdvantage(AdvantageType.Disadvantage)
                .AddToDB())
        .SetSpecialInterruptions(ConditionInterruption.Attacks)
        .AddToDB();

    private static readonly ConditionDefinition ConditionWeaponMasterySlow = ConditionDefinitionBuilder
        .Create("ConditionWeaponMasterySlow")
        .SetGuiPresentation(Category.Condition, ConditionDefinitions.ConditionSlowed)
        .SetConditionType(ConditionType.Detrimental)
        .SetSilent(Silent.None)
        .AddFeatures(
            FeatureDefinitionMovementAffinityBuilder
                .Create("MovementAffinityWeaponMasterySlow")
                .SetGuiPresentationNoContent(true)
                .SetBaseSpeedAdditiveModifier(-2)
                .AddToDB())
        .AddToDB();

    private static readonly ConditionDefinition ConditionWeaponMasteryVexSelf = ConditionDefinitionBuilder
        .Create("ConditionWeaponMasteryVexSelf")
        .SetGuiPresentationNoContent(true)
        .SetSilent(Silent.WhenAddedOrRemoved)
        .AddToDB();

    private static readonly ConditionDefinition ConditionWeaponMasteryVex = ConditionDefinitionBuilder
        .Create("ConditionWeaponMasteryVex")
        .SetGuiPresentation(Category.Condition, ConditionDefinitions.ConditionMarkedByHunter)
        .SetConditionType(ConditionType.Detrimental)
        .SetSilent(Silent.None)
        .AddFeatures(
            FeatureDefinitionCombatAffinityBuilder
                .Create("CombatAffinityWeaponMasteryVex")
                .SetGuiPresentationNoContent(true)
                .SetAttackOnMeAdvantage(AdvantageType.Advantage)
                .SetSituationalContext(SituationalContext.SourceHasCondition, ConditionWeaponMasteryVexSelf)
                .AddToDB())
        .AddCustomSubFeatures(new CustomBehaviorVex())
        .AddToDB();

    private static readonly FeatureDefinitionPower PowerWeaponMasteryPush = FeatureDefinitionPowerBuilder
        .Create("PowerWeaponMasteryPush")
        .SetGuiPresentation("FeatureWeaponMasteryPush", Category.Feature, hidden: true)
        .SetUsesFixed(ActivationTime.NoCost)
        .SetShowCasting(false)
        .SetEffectDescription(
            EffectDescriptionBuilder
                .Create()
                .SetTargetingData(Side.Enemy, RangeType.Distance, 6, TargetType.IndividualsUnique)
                .SetSavingThrowData(false, AttributeDefinitions.Strength, false,
                    EffectDifficultyClassComputation.AbilityScoreAndProficiency, AttributeDefinitions.Strength, 8)
                .SetEffectForms(
                    EffectFormBuilder
                        .Create()
                        .HasSavingThrow(EffectSavingThrowType.Negates)
                        .SetMotionForm(MotionForm.MotionType.PushFromOrigin, 2)
                        .Build())
                .Build())
        .AddCustomSubFeatures(new CustomBehaviorPush())
        .AddToDB();

    private static readonly Dictionary<WeaponTypeDefinition, MasteryProperty> WeaponMasteryTable = new()
    {
        { CustomWeaponsContext.HalberdWeaponType, MasteryProperty.Cleave },
        { CustomWeaponsContext.HandXbowWeaponType, MasteryProperty.Vex },
        { CustomWeaponsContext.KatanaWeaponType, MasteryProperty.Sap },
        { CustomWeaponsContext.LongMaceWeaponType, MasteryProperty.Sap },
        { CustomWeaponsContext.PikeWeaponType, MasteryProperty.Push },
        { WeaponTypeDefinitions.BattleaxeType, MasteryProperty.Topple },
        { WeaponTypeDefinitions.ClubType, MasteryProperty.Slow },
        { WeaponTypeDefinitions.DaggerType, MasteryProperty.Nick },
        { WeaponTypeDefinitions.DartType, MasteryProperty.Vex },
        { WeaponTypeDefinitions.GreataxeType, MasteryProperty.Cleave },
        { WeaponTypeDefinitions.GreatswordType, MasteryProperty.Graze },
        { WeaponTypeDefinitions.HandaxeType, MasteryProperty.Vex },
        { WeaponTypeDefinitions.HeavyCrossbowType, MasteryProperty.Push },
        { WeaponTypeDefinitions.JavelinType, MasteryProperty.Slow },
        { WeaponTypeDefinitions.LightCrossbowType, MasteryProperty.Slow },
        { WeaponTypeDefinitions.LongbowType, MasteryProperty.Slow },
        { WeaponTypeDefinitions.LongswordType, MasteryProperty.Sap },
        { WeaponTypeDefinitions.MaceType, MasteryProperty.Sap },
        { WeaponTypeDefinitions.MaulType, MasteryProperty.Topple },
        { WeaponTypeDefinitions.MorningstarType, MasteryProperty.Sap },
        { WeaponTypeDefinitions.QuarterstaffType, MasteryProperty.Topple },
        { WeaponTypeDefinitions.RapierType, MasteryProperty.Vex },
        { WeaponTypeDefinitions.ScimitarType, MasteryProperty.Nick },
        { WeaponTypeDefinitions.ShortbowType, MasteryProperty.Vex },
        { WeaponTypeDefinitions.ShortswordType, MasteryProperty.Vex },
        { WeaponTypeDefinitions.SpearType, MasteryProperty.Sap },
        { WeaponTypeDefinitions.WarhammerType, MasteryProperty.Push }
    };

    private static void LoadWeaponMastery()
    {
        // Cleave

        var powerCleave = FeatureDefinitionPowerBuilder
            .Create("PowerWeaponMasteryCleave")
            .SetGuiPresentation("FeatureWeaponMasteryCleave", Category.Feature, hidden: true)
            .SetUsesFixed(ActivationTime.NoCost)
            .SetShowCasting(false)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetTargetingData(Side.Enemy, RangeType.Distance, 16, TargetType.IndividualsUnique)
                    .Build())
            .DelegatedToAction()
            .AddCustomSubFeatures(new CustomBehaviorCleave())
            .AddToDB();

        _ = ActionDefinitionBuilder
            .Create("WeaponMasteryCleave")
            .SetGuiPresentation(Category.Action, DatabaseHelper.ActionDefinitions.WhirlwindAttack)
            .SetActionId(ExtraActionId.WeaponMasteryCleave)
            .SetActionType(ActionType.NoCost)
            .SetFormType(ActionFormType.Large)
            .RequiresAuthorization()
            .OverrideClassName("UsePower")
            .SetActivatedPower(powerCleave)
            .AddToDB();

        // create a feature for every mastery property

        foreach (MasteryProperty masteryProperty in Enum.GetValues(typeof(MasteryProperty)))
        {
            if (masteryProperty != MasteryProperty.None)
            {
                MasteryFeatures.Add(masteryProperty, FeatureDefinitionBuilder
                    .Create($"FeatureWeaponMastery{masteryProperty}")
                    .SetGuiPresentation(Category.Feature)
                    .AddToDB());
            }
        }

        // master toggle

        var actionAffinityToggle = FeatureDefinitionActionAffinityBuilder
            .Create(ActionAffinitySorcererMetamagicToggle, "ActionAffinityWeaponMasteryToggle")
            .SetGuiPresentationNoContent(true)
            .SetAuthorizedActions((Id)ExtraActionId.WeaponMasteryToggle)
            .AddCustomSubFeatures(new ValidateDefinitionApplication(
                ShouldDisplayWeaponMasteryToggle,
                c => !c.IsToggleEnabled((Id)ExtraActionId.TacticalMasterToggle)))
            .AddToDB();

        // level up custom invocations and re-learn powers

        var powers = new List<FeatureDefinitionPower>();

        foreach (var kvp in WeaponMasteryTable)
        {
            var weaponTypeDefinition = kvp.Key;
            var weaponTypeName = weaponTypeDefinition.Name;
            var masteryProperty = kvp.Value;
            var featureSpecialization = MasteryFeatures[masteryProperty];
            var featureSet = FeatureDefinitionFeatureSetBuilder
                .Create($"FeatureSetWeaponMastery{weaponTypeName}")
                .SetGuiPresentationNoContent(true)
                .SetFeatureSet(actionAffinityToggle, featureSpecialization)
                .AddToDB();

            _ = CustomInvocationDefinitionBuilder
                .Create($"CustomInvocationWeaponMastery{weaponTypeName}")
                .SetGuiPresentation(
                    weaponTypeDefinition.GuiPresentation.Title,
                    featureSpecialization.GuiPresentation.Description,
                    CustomWeaponsContext.GetStandardWeaponOfType(weaponTypeDefinition.Name))
                .SetPoolType(InvocationPoolTypeCustom.Pools.WeaponMasterySpecialization)
                .SetGrantedFeature(featureSet)
                .AddCustomSubFeatures(ModifyInvocationVisibility.Marker)
                .AddToDB();

            var powerRelearnWeapon = FeatureDefinitionPowerSharedPoolBuilder
                .Create($"PowerWeaponMasteryRelearn{weaponTypeName}")
                .SetGuiPresentation(
                    weaponTypeDefinition.FormatTitle(), featureSpecialization.GuiPresentation.Description)
                .SetShowCasting(false)
                .SetSharedPool(ActivationTime.NoCost, PowerWeaponMasteryRelearnPool)
                .SetEffectDescription(
                    EffectDescriptionBuilder
                        .Create()
                        .SetTargetingData(Side.Ally, RangeType.Self, 0, TargetType.Self)
                        .Build())
                .AddToDB();

            powerRelearnWeapon.GuiPresentation.hidden = true;
            powers.Add(powerRelearnWeapon);
        }

        PowerBundle.RegisterPowerBundle(PowerWeaponMasteryRelearnPool, false, powers);

        foreach (var pair in WeaponMasteryTable)
        {
            var weapon = pair.Key;
            var def = pair.Value;
            var current = weapon.GetMastery();

            if (def == current) { continue; }

            UpdateWeaponMasteryDescriptions(weapon.Name, current);
        }
    }

    internal static void TryAddWeaponMasteryTags(
        RulesetCharacter character,
        RulesetItem item,
        Dictionary<string, TagsDefinitions.Criticity> tags)
    {
        if (!Main.Settings.UseWeaponMasterySystem || !Main.Settings.UseWeaponMasterySystemAddWeaponTag)
        {
            return;
        }

        if (!item.ItemDefinition.IsWeapon) { return; }

        var weapon = item.ItemDefinition.WeaponDescription?.WeaponTypeDefinition;

        if (weapon == null) { return; }

        var mastery = weapon.GetMastery();

        if (mastery == MasteryProperty.None) { return; }

        var known = character != null && WeaponTypesWithLearnedMastery(character).Contains(weapon);
        tags.TryAdd($"{mastery}", known ? TagsDefinitions.Criticity.Important : TagsDefinitions.Criticity.Normal);
    }

    internal static void SwitchWeaponMastery()
    {
        var klasses = new[] { Barbarian, Fighter, Paladin, Ranger, Rogue };

        foreach (var klass in klasses)
        {
            klass.FeatureUnlocks.RemoveAll(x =>
                x.FeatureDefinition == FeatureSetWeaponMasteryLearn1 ||
                x.FeatureDefinition == FeatureSetWeaponMasteryLearn2 ||
                x.FeatureDefinition == FeatureSetWeaponMasteryLearn3);
        }

        if (!Main.Settings.UseWeaponMasterySystem)
        {
            return;
        }

        Barbarian.FeatureUnlocks.AddRange(
            new FeatureUnlockByLevel(FeatureSetWeaponMasteryLearn2, 1),
            new FeatureUnlockByLevel(FeatureSetWeaponMasteryLearn1, 4),
            new FeatureUnlockByLevel(FeatureSetWeaponMasteryLearn1, 10));
        Fighter.FeatureUnlocks.AddRange(
            new FeatureUnlockByLevel(FeatureSetWeaponMasteryLearn3, 1),
            new FeatureUnlockByLevel(FeatureSetWeaponMasteryLearn1, 4),
            new FeatureUnlockByLevel(FeatureSetWeaponMasteryLearn1, 10),
            new FeatureUnlockByLevel(FeatureSetWeaponMasteryLearn1, 16));
        Paladin.FeatureUnlocks.Add(new FeatureUnlockByLevel(FeatureSetWeaponMasteryLearn2, 1));
        Ranger.FeatureUnlocks.Add(new FeatureUnlockByLevel(FeatureSetWeaponMasteryLearn2, 1));
        Rogue.FeatureUnlocks.Add(new FeatureUnlockByLevel(FeatureSetWeaponMasteryLearn2, 1));

        //Sort unlocks
        Barbarian.FeatureUnlocks.Sort(Sorting.CompareFeatureUnlock);
        Fighter.FeatureUnlocks.Sort(Sorting.CompareFeatureUnlock);
        Paladin.FeatureUnlocks.Sort(Sorting.CompareFeatureUnlock);
        Ranger.FeatureUnlocks.Sort(Sorting.CompareFeatureUnlock);
        Rogue.FeatureUnlocks.Sort(Sorting.CompareFeatureUnlock);
    }

    private static WeaponTypeDefinition[] WeaponTypesWithLearnedMastery(RulesetCharacter character)
    {
        const string PREFIX = "CustomInvocationWeaponMastery";

        return character.Invocations
            .Where(x => x.InvocationDefinition.Name.StartsWith(PREFIX))
            .Select(x => GetDefinition<WeaponTypeDefinition>(x.InvocationDefinition.Name.Replace(PREFIX, string.Empty)))
            .ToArray();
    }

    internal static MasteryProperty GetMastery(this WeaponTypeDefinition weapon)
    {
        if (Main.Settings.WeaponMasteryCustom.TryGetValue(weapon.Name, out var mastery))
        {
            if (Masteries.Contains(mastery)) { return mastery; }
        }

        if (WeaponMasteryTable.TryGetValue(weapon, out mastery))
        {
            if (Masteries.Contains(mastery)) { return mastery; }
        }

        return MasteryProperty.None;
    }

    internal static MasteryProperty GetMastery(this RulesetCharacter character, RulesetAttackMode attackMode)
    {
        var attackModeWeaponType = (attackMode.SourceDefinition as ItemDefinition)
            ?.WeaponDescription?.WeaponTypeDefinition;

        if (!attackModeWeaponType)
        {
            return MasteryProperty.None;
        }

        var weaponTypes = WeaponTypesWithLearnedMastery(character);

        return !weaponTypes.Contains(attackModeWeaponType)
            ? MasteryProperty.None
            : attackModeWeaponType.GetMastery();
    }

    private static MasteryProperty GetMastery(this RulesetCharacter character, RulesetItem rulesetItem)
    {
        var rulesetItemWeaponType = rulesetItem?.ItemDefinition?.WeaponDescription?.WeaponTypeDefinition;

        if (!rulesetItemWeaponType)
        {
            return MasteryProperty.None;
        }

        var weaponTypes = WeaponTypesWithLearnedMastery(character);

        return !weaponTypes.Contains(rulesetItemWeaponType)
            ? MasteryProperty.None
            : rulesetItemWeaponType.GetMastery();
    }

    internal static MasteryProperty GetMainMastery(this RulesetCharacter character)
    {
        return character.GetMastery(character.GetMainWeapon());
    }

    private static MasteryProperty GetOffhandMastery(this RulesetCharacter character)
    {
        return character.GetMastery(character.GetOffhandWeapon());
    }

    private static bool ShouldDisplayWeaponMasteryToggle(RulesetCharacter character)
    {
        var mainWeaponMastery = character.GetMainMastery();

        if (mainWeaponMastery != MasteryProperty.None)
        {
            return true;
        }

        var offhandWeaponMastery = character.GetOffhandMastery();

        return offhandWeaponMastery != MasteryProperty.None;
    }

    public enum MasteryProperty
    {
        None = -1,
        Push = 0,
        Sap = 1,
        Slow = 2,
        Cleave,
        Graze,
        Nick,
        Topple,
        Vex
    }

    private static readonly List<MasteryProperty> Masteries =
    [
        MasteryProperty.Push,
        MasteryProperty.Sap,
        MasteryProperty.Slow,
        MasteryProperty.Cleave,
        MasteryProperty.Graze,
        MasteryProperty.Nick,
        MasteryProperty.Topple,
        MasteryProperty.Vex
    ];

    //
    // Weapon Mastery
    //

    private sealed class CustomBehaviorWeaponMastery
        : IPhysicalAttackInitiatedByMe, IPhysicalAttackFinishedByMe, IPhysicalAttackBeforeHitConfirmedOnEnemy,
            IActionFinishedByMe
    {
        public IEnumerator OnPhysicalAttackBeforeHitConfirmedOnEnemy(
            GameLocationBattleManager battleManager,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            ActionModifier actionModifier,
            RulesetAttackMode attackMode,
            bool rangedAttack,
            AdvantageType advantageType,
            List<EffectForm> actualEffectForms,
            bool firstTarget,
            bool criticalHit)
        {
            if (!IsValid(attacker, attackMode))
            {
                yield break;
            }

            var rulesetAttacker = attacker.RulesetCharacter;

            if (!rulesetAttacker.IsToggleEnabled((Id)ExtraActionId.TacticalMasterToggle))
            {
                yield break;
            }

            var masteryToReplace = rulesetAttacker.GetMastery(attackMode);

            if (masteryToReplace is MasteryProperty.None or MasteryProperty.Graze or MasteryProperty.Nick)
            {
                yield break;
            }

            yield return OfferTacticalMasterReplacement(attacker, masteryToReplace);
        }

        public IEnumerator OnPhysicalAttackFinishedByMe(
            GameLocationBattleManager battleManager,
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            RulesetAttackMode attackMode,
            RollOutcome rollOutcome,
            int damageAmount)
        {
            if (!IsValid(attacker, attackMode))
            {
                yield break;
            }

            var rulesetAttacker = attacker.RulesetCharacter;

            if (!rulesetAttacker.IsToggleEnabled((Id)ExtraActionId.WeaponMasteryToggle) &&
                !rulesetAttacker.IsToggleEnabled((Id)ExtraActionId.TacticalMasterToggle))
            {
                yield break;
            }

            var mastery = (MasteryProperty)attacker.GetSpecialFeatureUses(FeatureSetFighterTacticalMaster.Name);

            if (mastery == MasteryProperty.None)
            {
                mastery = rulesetAttacker.GetMastery(attackMode);
            }

            if (rollOutcome is RollOutcome.Success or RollOutcome.CriticalSuccess)
            {
                // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                switch (mastery)
                {
                    case MasteryProperty.Push when defender.RulesetCharacter.WieldingSize <= CreatureSize.Large:
                        DoPush(attacker, defender);
                        break;
                    case MasteryProperty.Sap:
                        DoSap(attacker, defender);
                        break;
                    case MasteryProperty.Slow when damageAmount > 0:
                        DoSlow(attacker, defender);
                        break;
                    case MasteryProperty.Cleave when attacker.OnceInMyTurnIsValid(WeaponMasteryCleave):
                        DoCleave(attacker, defender);
                        break;
                    case MasteryProperty.Topple:
                        DoTopple(attacker, defender, attackMode);
                        break;
                    case MasteryProperty.Vex when damageAmount > 0:
                        DoVex(attacker, defender);
                        break;
                }
            }
            else if (mastery == MasteryProperty.Graze)
            {
                DoGraze(attacker, defender, attackMode);
            }
        }

        public IEnumerator OnPhysicalAttackInitiatedByMe(
            GameLocationBattleManager battleManager,
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            ActionModifier attackModifier,
            RulesetAttackMode attackMode)
        {
            attacker.SetSpecialFeatureUses(FeatureSetFighterTacticalMaster.Name, -1);

            if (!IsValid(attacker, attackMode))
            {
                yield break;
            }

            var rulesetAttacker = attacker.RulesetCharacter;

            if (!rulesetAttacker.IsToggleEnabled((Id)ExtraActionId.TacticalMasterToggle))
            {
                yield break;
            }

            var masteryToReplace = rulesetAttacker.GetMastery(attackMode);

            if (masteryToReplace is not (MasteryProperty.Graze or MasteryProperty.Nick))
            {
                yield break;
            }

            yield return OfferTacticalMasterReplacement(attacker, masteryToReplace);
        }

        private static bool IsValid(GameLocationCharacter attacker, RulesetAttackMode attackMode)
        {
            if (!Main.Settings.UseWeaponMasterySystemFlurryTriggersMastery &&
                attackMode.AttackTags.Contains(TwoWeaponCombatFeats.DualFlurryAttackMark))
            {
                return false;
            }

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (!Main.Settings.UseWeaponMasterySystemNickExtraAttackTriggersMastery &&
                attackMode.AttackTags.Contains(WeaponMasteryNick) &&
                ValidatorsWeapon.IsMelee(attackMode) &&
                attacker.GetSpecialFeatureUses(WeaponMasteryNick) >= 1)
            {
                return false;
            }

            return true;
        }

        private static IEnumerator OfferTacticalMasterReplacement(
            GameLocationCharacter attacker, MasteryProperty masteryToReplace)
        {
            var rulesetAttacker = attacker.RulesetCharacter;
            var usablePower = PowerProvider.Get(PowerFighterTacticalMasterPool, rulesetAttacker);
            var usablePowers = new[] { MasteryProperty.Push, MasteryProperty.Sap, MasteryProperty.Slow }
                .Where(x => x != masteryToReplace)
                .Select(mastery =>
                    PowerProvider.Get(GetDefinition<FeatureDefinitionPower>($"PowerFighterTacticalMaster{mastery}")))
                .ToArray();

            rulesetAttacker.UsablePowers.AddRange(usablePowers);

            yield return attacker.MyReactToSpendPowerBundle(
                usablePower,
                [attacker],
                attacker,
                FeatureSetFighterTacticalMaster.Name,
                $"ReactionSpendPowerBundle{FeatureSetFighterTacticalMaster.Name}Description"
                    .Formatted(Category.Reaction, $"FeatureWeaponMastery{masteryToReplace}Title"
                        .Localized(Category.Feature)),
                ReactionValidated);

            usablePowers.Do(x => rulesetAttacker.UsablePowers.Remove(x));

            yield break;

            void ReactionValidated(ReactionRequestSpendBundlePower reactionRequest)
            {
                attacker.SetSpecialFeatureUses(FeatureSetFighterTacticalMaster.Name, reactionRequest.SelectedSubOption);
            }
        }

        #region Behaviors

        private static void DoCleave(GameLocationCharacter attacker, GameLocationCharacter defender)
        {
            var rulesetAttacker = attacker.RulesetCharacter;
            var rulesetDefender = defender.RulesetCharacter;

            attacker.SetSpecialFeatureUses(WeaponMasteryCleave, 0);
            rulesetAttacker.LogCharacterUsedFeature(GetDefinition<FeatureDefinition>("FeatureWeaponMasteryCleave"));
            rulesetAttacker.InflictCondition(
                ConditionWeaponMasteryCleave.Name,
                DurationType.Round,
                0,
                TurnOccurenceType.EndOfTurn,
                AttributeDefinitions.TagEffect,
                rulesetDefender.guid,
                rulesetDefender.CurrentFaction.Name,
                1,
                ConditionWeaponMasteryCleave.Name,
                0,
                0,
                0);
        }

        private static void DoGraze(
            GameLocationCharacter attacker, GameLocationCharacter defender, RulesetAttackMode attackMode)
        {
            var rulesetAttacker = attacker.RulesetCharacter;
            var rulesetDefender = defender.RulesetCharacter;
            var abilityScore = attackMode.AbilityScore;
            var abilityScoreValue = rulesetAttacker.TryGetAttributeValue(abilityScore);
            var modifier = AttributeDefinitions.ComputeAbilityScoreModifier(abilityScoreValue);

            if (modifier <= 0)
            {
                return;
            }

            var damageForm = attackMode.EffectDescription.EffectForms.FirstOrDefault(x =>
                x.FormType == EffectForm.EffectFormType.Damage)?.DamageForm;

            if (damageForm == null)
            {
                return;
            }

            var effectForm = EffectFormBuilder.DamageForm(damageForm.DamageType, bonusDamage: modifier);
            var applyFormsParams = new RulesetImplementationDefinitions.ApplyFormsParams
            {
                sourceCharacter = rulesetAttacker,
                targetCharacter = rulesetDefender,
                position = defender.LocationPosition
            };

            rulesetAttacker.LogCharacterUsedFeature(GetDefinition<FeatureDefinition>("FeatureWeaponMasteryGraze"));

            RulesetActor.InflictDamage(
                modifier,
                effectForm.DamageForm,
                effectForm.DamageForm.DamageType,
                applyFormsParams,
                rulesetDefender,
                false,
                attacker.Guid,
                false,
                [],
                new RollInfo(DieType.D1, [], modifier),
                false,
                out _);
        }

        private static void DoPush(GameLocationCharacter attacker, GameLocationCharacter defender)
        {
            var rulesetAttacker = attacker.RulesetCharacter;
            var usablePower = PowerProvider.Get(PowerWeaponMasteryPush, rulesetAttacker);

            rulesetAttacker.LogCharacterUsedFeature(GetDefinition<FeatureDefinition>("FeatureWeaponMasteryPush"));
            attacker.MyExecuteActionSpendPower(usablePower, defender);
        }

        private static void DoSap(GameLocationCharacter attacker, GameLocationCharacter defender)
        {
            var rulesetAttacker = attacker.RulesetCharacter;
            var rulesetDefender = defender.RulesetCharacter;

            rulesetDefender.InflictCondition(
                ConditionWeaponMasterySap.Name,
                DurationType.Round,
                1,
                (TurnOccurenceType)ExtraTurnOccurenceType.StartOfSourceTurn,
                AttributeDefinitions.TagEffect,
                rulesetAttacker.guid,
                rulesetAttacker.CurrentFaction.Name,
                1,
                ConditionWeaponMasterySap.Name,
                0,
                0,
                0);
        }

        private static void DoSlow(GameLocationCharacter attacker, GameLocationCharacter defender)
        {
            var rulesetAttacker = attacker.RulesetCharacter;
            var rulesetDefender = defender.RulesetCharacter;

            if (!rulesetDefender.HasConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect, ConditionWeaponMasterySlow.Name))
            {
                rulesetDefender.InflictCondition(
                    ConditionWeaponMasterySlow.Name,
                    DurationType.Round,
                    1,
                    (TurnOccurenceType)ExtraTurnOccurenceType.StartOfSourceTurn,
                    AttributeDefinitions.TagEffect,
                    rulesetAttacker.guid,
                    rulesetAttacker.CurrentFaction.Name,
                    1,
                    ConditionWeaponMasterySlow.Name,
                    0,
                    0,
                    0);
            }
        }

        private static void DoTopple(
            GameLocationCharacter attacker, GameLocationCharacter defender, RulesetAttackMode attackMode)
        {
            var rulesetAttacker = attacker.RulesetCharacter;
            var rulesetDefender = defender.RulesetCharacter;

            var topple = GetDefinition<FeatureDefinition>("FeatureWeaponMasteryTopple");
            rulesetAttacker.LogCharacterUsedFeature(topple);

            var dc = 8 + rulesetAttacker.TryGetProficiencyBonus()
                       + rulesetAttacker.TryGetAbilityModifier(attackMode.AbilityScore);

            var outcome = rulesetDefender.MakeSimpleSavingThrow(AttributeDefinitions.Constitution, dc, topple);

            if (outcome is RollOutcome.Failure or RollOutcome.CriticalFailure)
            {
                MotionContext.ProneTarget(defender);
            }
        }

        private static void DoVex(GameLocationCharacter attacker, GameLocationCharacter defender)
        {
            var rulesetAttacker = attacker.RulesetCharacter;
            var rulesetDefender = defender.RulesetCharacter;

            rulesetAttacker.InflictCondition(
                ConditionWeaponMasteryVexSelf.Name,
                DurationType.Round,
                1,
                TurnOccurenceType.EndOfTurn,
                AttributeDefinitions.TagEffect,
                rulesetAttacker.guid,
                rulesetAttacker.CurrentFaction.Name,
                1,
                ConditionWeaponMasteryVexSelf.Name,
                0,
                0,
                0);
            rulesetDefender.InflictCondition(
                ConditionWeaponMasteryVex.Name,
                DurationType.Round,
                1,
                TurnOccurenceType.EndOfSourceTurn,
                AttributeDefinitions.TagEffect,
                rulesetAttacker.guid,
                rulesetAttacker.CurrentFaction.Name,
                1,
                ConditionWeaponMasteryVex.Name,
                0,
                0,
                0);
        }

        #endregion

        public IEnumerator OnActionFinishedByMe(CharacterAction action)
        {
            var attacker = action.ActingCharacter;

            if (action is not CharacterActionAttack) { yield break; }

            if (action.ActionId == Id.AttackMain)
            {

                if (action.ActionType != ActionType.Main) { yield break; }

                if (attacker.GetSpecialFeatureUses(WeaponMasteryNick) >= 1) { yield break; }
                
                var rulesetAttacker = attacker.RulesetCharacter;
                if (rulesetAttacker.ExecutedBonusAttacks != 0) { yield break; }


                var attackMode = action.ActionParams.AttackMode;
                var mastery = (MasteryProperty)attacker.GetSpecialFeatureUses(FeatureSetFighterTacticalMaster.Name);

                if (mastery == MasteryProperty.None)
                {
                    mastery = rulesetAttacker.GetMastery(attackMode);
                }

                var weapon = rulesetAttacker.GetOffhandWeapon();
                if (weapon == null) { yield break; }

                if (!ValidatorsCharacter.HasMeleeWeaponInMainAndOffhand(rulesetAttacker)) { yield break; }

                if (mastery != MasteryProperty.Nick && rulesetAttacker.GetMastery(weapon) != MasteryProperty.Nick)
                {
                    yield break;
                }

                var nickAttack = rulesetAttacker.AttackModes.FirstOrDefault(m =>
                    m.ActionType == ActionType.Bonus && m.SourceObject == weapon);

                if (nickAttack == null) { yield break; }

                rulesetAttacker.LogCharacterUsedFeature(MasteryFeatures[MasteryProperty.Nick]);
                attacker.SetSpecialFeatureUses(WeaponMasteryNick, 1);
            }
            else if (action.ActionId == (Id)ExtraActionId.NickMasteryAttack)
            {
                if (attacker.GetSpecialFeatureUses(WeaponMasteryNick) == 1)
                {
                    attacker.SetSpecialFeatureUses(WeaponMasteryNick, 2);
                }
            }
        }
    }

    #region Extended Beahaviors

    //
    // Cleave
    //

    private sealed class CustomBehaviorCleave : IPowerOrSpellFinishedByMe, IFilterTargetingCharacter
    {
        public bool EnforceFullSelection => true;

        public bool IsValid(CursorLocationSelectTarget __instance, GameLocationCharacter target)
        {
            var attacker = __instance.ActionParams.ActingCharacter;
            var attackMode = attacker.FindActionAttackMode(Id.AttackMain);

            if (!attacker.RulesetCharacter.TryGetConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect, ConditionWeaponMasteryCleave.Name, out var activeCondition))
            {
                __instance.actionModifier.FailureFlags.Add(Gui.Localize("Failure/&CannotAttackTarget"));
                return false;
            }

            var rulesetFirstTarget = EffectHelpers.GetCharacterByGuid(activeCondition.SourceGuid);
            var firstTarget = GameLocationCharacter.GetFromActor(rulesetFirstTarget);

            if (!attacker.IsWithinRange(target, attackMode.reachRange) || target == firstTarget)
            {
                __instance.actionModifier.FailureFlags.Add(Gui.Localize("Failure/&CannotAttackTarget"));

                return false;
            }

            if (firstTarget.IsWithinRange(target, 1))
            {
                return true;
            }

            __instance.actionModifier.FailureFlags.Add("Failure/&SecondTargetNotWithinRange");

            return false;
        }

        public IEnumerator OnPowerOrSpellFinishedByMe(CharacterActionMagicEffect action, BaseDefinition baseDefinition)
        {
            var attacker = action.ActingCharacter;
            var attackMode = attacker.FindActionAttackMode(Id.AttackMain);
            var target = action.actionParams.TargetCharacters[0];

            if (!Main.Settings.UseWeaponMasterySystemAddCleaveDamage)
            {
                var damageForm = attackMode.EffectDescription.FindFirstDamageForm();
                var attributeDamage =
                    damageForm.DamageBonusTrends.FirstOrDefault(x => x.sourceType == FeatureSourceType.AbilityScore);

                damageForm.BonusDamage -= attributeDamage.value;
                damageForm.DamageBonusTrends.RemoveAll(x => x.sourceType == FeatureSourceType.AbilityScore);
            }

            attacker.MyExecuteActionAttack(Id.AttackFree, target, attackMode, new ActionModifier());

            yield break;
        }
    }

    private sealed class CustomBehaviorConditionCleave : IActionFinishedByMe
    {
        public IEnumerator OnActionFinishedByMe(CharacterAction action)
        {
            // ignore any non cost toggle usage
            if (CustomActionIdContext.ExtraActionIdToggles.Contains(action.ActionId))
            {
                yield break;
            }

            var rulesetCharacter = action.ActingCharacter.RulesetCharacter;

            if (!rulesetCharacter.TryGetConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect, ConditionWeaponMasteryCleave.Name, out var activeCondition))
            {
                yield break;
            }

            // ignore the attack itself
            if (activeCondition.Amount == 1)
            {
                rulesetCharacter.RemoveCondition(activeCondition);
            }

            activeCondition.Amount = 1;
        }
    }

    //
    // Nick
    //

    /**Returns true for attacks that should be removed after Nick was used*/
    internal static void ModifyNickOffHandAttack(RulesetCharacterHero hero, List<RulesetAttackMode> modes)
    {
        var locationCharacter = GameLocationCharacter.GetFromActor(hero);
        if (locationCharacter == null || locationCharacter.GetSpecialFeatureUses(WeaponMasteryNick) < 1) { return; }

        var offHand = hero.GetOffhandWeapon();
        var nickAttack = offHand != null
            ? modes.FirstOrDefault(m => m.ActionType == ActionType.Bonus && m.SourceObject == offHand)
            : null;

        if (nickAttack != null)
        {
            nickAttack.actionType = ActionType.None; //hopefully this hides this attack from the action panel
            nickAttack.AddAttackTagAsNeeded(WeaponMasteryNick);
        }
    }

    internal static RulesetAttackMode FindNickAttackMode(this GameLocationCharacter character)
    {
        return character.RulesetCharacter.FindNickAttackMode();
    }
    
    internal static RulesetAttackMode FindNickAttackMode(this RulesetCharacter character)
    {
        return character.AttackModes
            .FirstOrDefault(mode => mode.AttackTags.Contains(WeaponMasteryNick));
    }


    //
    // Push
    //

    private sealed class CustomBehaviorPush : IModifyEffectDescription
    {
        public bool IsValid(BaseDefinition definition, RulesetCharacter character, EffectDescription effectDescription)
        {
            return definition == PowerWeaponMasteryPush;
        }

        public EffectDescription GetEffectDescription(
            BaseDefinition definition,
            EffectDescription effectDescription,
            RulesetCharacter character,
            RulesetEffect rulesetEffect)
        {
            if (!Main.Settings.UseWeaponMasterySystemPushSave)
            {
                effectDescription.hasSavingThrow = false;
                effectDescription.EffectForms[0].hasSavingThrow = false;

                return effectDescription;
            }

            var glc = GameLocationCharacter.GetFromActor(character);
            var abilityScoreIndex = glc?.GetSpecialFeatureUses(WeaponMasteryTopple) ?? -1;

            if (abilityScoreIndex < 0)
            {
                return effectDescription;
            }

            var abilityScore = AttributeDefinitions.AbilityScoreNames[abilityScoreIndex];

            effectDescription.savingThrowDifficultyAbility = abilityScore;

            return effectDescription;
        }
    }

    //
    // Vex
    //

    private sealed class CustomBehaviorVex : IPhysicalAttackFinishedOnMe
    {
        public IEnumerator OnPhysicalAttackFinishedOnMe(
            GameLocationBattleManager battleManager,
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            RulesetAttackMode attackMode,
            RollOutcome rollOutcome,
            int damageAmount)
        {
            var rulesetDefender = defender.RulesetCharacter;

            if (!rulesetDefender.TryGetConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect, ConditionWeaponMasteryVex.Name, out var activeCondition) ||
                activeCondition.SourceGuid != attacker.Guid)
            {
                yield break;
            }

            rulesetDefender.RemoveCondition(activeCondition);
        }
    }

    #endregion

    #region Relearn

    //
    // Relearn
    //

    private sealed class PowerOrSpellFinishedByMeRelearn : IPowerOrSpellFinishedByMe
    {
        public IEnumerator OnPowerOrSpellFinishedByMe(CharacterActionMagicEffect action, BaseDefinition baseDefinition)
        {
            const string InvocationNamePrefix = "CustomInvocationWeaponMastery";
            var character = action.ActingCharacter;
            var rulesetCharacter = character.RulesetCharacter;
            var aborted = false;
            var usablePowers = new List<RulesetUsablePower>();
            var usablePower = PowerProvider.Get(PowerWeaponMasteryRelearnPool, rulesetCharacter);

            //
            // UNLEARN
            //

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var invocation in rulesetCharacter.Invocations
                         .Where(x => x.InvocationDefinition.Name.StartsWith(InvocationNamePrefix)))
            {
                var weaponTypeName = invocation.InvocationDefinition.Name.Replace(InvocationNamePrefix, string.Empty);
                var power = GetDefinition<FeatureDefinitionPower>($"PowerWeaponMasteryRelearn{weaponTypeName}");

                usablePowers.Add(PowerProvider.Get(power, rulesetCharacter));
            }

            rulesetCharacter.UsablePowers.AddRange(usablePowers);
            character.SetSpecialFeatureUses(Stage, StageNotLearned);

            yield return character.MyReactToSpendPowerBundle(
                usablePower,
                [character],
                character,
                "WeaponMasteryRelearn",
                "ReactionSpendPowerBundleWeaponMasteryUnlearnDescription".Localized(Category.Reaction),
                ReactionValidatedUnlearn,
                ReactionNotValidated);

            usablePowers.Do(x => rulesetCharacter.UsablePowers.Remove(x));

            if (aborted)
            {
                yield break;
            }

            //
            // LEARN
            //

            usablePowers.Clear();

            var weaponTypes = rulesetCharacter.Invocations.Where(x =>
                    x.InvocationDefinition.Name.StartsWith(InvocationNamePrefix)).Select(x =>
                    x.InvocationDefinition.Name.Replace(InvocationNamePrefix, string.Empty))
                .ToList();

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var weaponTypeDefinition in WeaponMasteryTable.Keys
                         .Where(x => !weaponTypes.Contains(x.Name)))
            {
                var weaponTypeName = weaponTypeDefinition.Name;
                var power = GetDefinition<FeatureDefinitionPower>($"PowerWeaponMasteryRelearn{weaponTypeName}");

                usablePowers.Add(PowerProvider.Get(power, rulesetCharacter));
            }

            rulesetCharacter.UsablePowers.AddRange(usablePowers);

            yield return character.MyReactToSpendPowerBundle(
                usablePower,
                [character],
                character,
                "WeaponMasteryRelearn",
                "ReactionSpendPowerBundleWeaponMasteryLearnDescription".Localized(Category.Reaction),
                ReactionValidatedLearn,
                ReactionNotValidated);

            usablePowers.Do(x => rulesetCharacter.UsablePowers.Remove(x));

            if (!aborted)
            {
                character.SetSpecialFeatureUses(Stage, StageLearned);
            }

            yield break;

            void ReactionValidatedUnlearn(ReactionRequestSpendBundlePower reactionRequest)
            {
                character.SetSpecialFeatureUses(IndexUnlearn, reactionRequest.SelectedSubOption);
            }

            void ReactionValidatedLearn(ReactionRequestSpendBundlePower reactionRequest)
            {
                character.SetSpecialFeatureUses(IndexLearn, reactionRequest.SelectedSubOption);
            }

            void ReactionNotValidated(ReactionRequestSpendBundlePower reactionRequest)
            {
                aborted = true;
                usablePower.remainingUses++;
            }
        }
    }

    private sealed class MagicEffectFinishedByMeRelearn : IMagicEffectFinishedByMe
    {
        public IEnumerator OnMagicEffectFinishedByMe(
            CharacterAction action, GameLocationCharacter attacker, List<GameLocationCharacter> targets)
        {
            if (!action.ActionParams.RulesetEffect.SourceDefinition.Name.StartsWith("PowerWeaponMasteryRelearn"))
            {
                yield break;
            }

            if (attacker.GetSpecialFeatureUses(Stage) < 0)
            {
                yield break;
            }

            var indexUnlearn = attacker.GetSpecialFeatureUses(IndexUnlearn);
            var weaponTypeUnlearnName = WeaponMasteryTable.Keys.ToArray()[indexUnlearn].Name;
            var invocationToUnlearn =
                GetDefinition<InvocationDefinition>($"CustomInvocationWeaponMastery{weaponTypeUnlearnName}");

            var indexLearn = attacker.GetSpecialFeatureUses(IndexLearn);
            var weaponTypeLearnName = WeaponMasteryTable.Keys.ToArray()[indexLearn].Name;
            var invocationToLearn =
                GetDefinition<InvocationDefinition>($"CustomInvocationWeaponMastery{weaponTypeLearnName}");

            var hero = attacker.RulesetCharacter.GetOriginalHero();

            hero!.TrainedInvocations.Remove(invocationToUnlearn);
            hero.TrainedInvocations.Add(invocationToLearn);
            hero.GrantInvocations();
        }
    }

    #endregion

    #region Customize

    private static void SetWeaponMastery(WeaponTypeDefinition weapon, MasteryProperty mastery)
    {
        var weaponTypeName = weapon.Name;
        if (!WeaponMasteryTable.TryGetValue(weapon, out var def) || def == mastery)
        {
            mastery = MasteryProperty.None;
        }

        if (mastery == MasteryProperty.None)
        {
            Main.Settings.WeaponMasteryCustom.Remove(weaponTypeName);
        }
        else
        {
            Main.Settings.WeaponMasteryCustom[weaponTypeName] = mastery;
        }

        UpdateWeaponMasteryDescriptions(weaponTypeName, mastery);
    }

    private static void UpdateWeaponMasteryDescriptions(string weaponTypeName, MasteryProperty mastery)
    {
        var description = MasteryFeatures[mastery].GuiPresentation.Description;

        GetDefinition<InvocationDefinition>($"CustomInvocationWeaponMastery{weaponTypeName}")
            .GuiPresentation.Description = description;

        GetDefinition<FeatureDefinitionPower>($"PowerWeaponMasteryRelearn{weaponTypeName}")
            .GuiPresentation.Description = description;
    }

    private static bool _resetState;

    internal static void DisplayWeaponMasteryCustomization()
    {
        var toggle = Main.Settings.WeaponMasterySystemCustomizeToggle;
        if (UI.DisclosureToggle(Gui.Localize("ModUi/&WeaponMasterySystemCustomizeToggle"), ref toggle, 200))
        {
            Main.Settings.WeaponMasterySystemCustomizeToggle = toggle;
        }

        if (!Main.Settings.WeaponMasterySystemCustomizeToggle)
        {
            return;
        }

        UI.DangerousActionButton(Gui.Localize("ModUi/&WeaponMasterySystemCustomizeResetAll"),
            Gui.Localize("ModUi/&WeaponMasterySystemCustomizeResetAllWarning"),
            ref _resetState, () => Main.Settings.WeaponMasteryCustom.Clear());

        using (UI.VerticalScope())
        {
            foreach (var weapon in WeaponMasteryTable.Keys.OrderBy(x => x.FormatTitle()))
            {
                using (UI.HorizontalScope())
                {
                    UI.Label(weapon.FormatTitle(), UI.Width(100));
                    foreach (var mastery in Masteries)
                    {
                        //do not allow Cleave for ranged weapons
                        var disabled = mastery == MasteryProperty.Cleave
                                       && weapon.WeaponProximity == AttackProximity.Range;

                        var title = Gui.Localize($"Tooltip/&Tag{mastery}Title");
                        if (disabled)
                        {
                            title = title.Grey();
                        }
                        else if (mastery == weapon.GetMastery())
                        {
                            title = title.Bold().Orange();
                        }

                        UI.ActionButton(title, () =>
                        {
                            if (disabled) { return; }

                            _resetState = false;
                            SetWeaponMastery(weapon, mastery);
                        });
                    }

                    UI.Space(10f);
                    UI.ActionButton(Gui.Localize("Screen/&ScoreResetButtonTitle"),
                        () => SetWeaponMastery(weapon, MasteryProperty.None));
                }
            }
        }
    }

    #endregion
}
