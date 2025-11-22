using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Behaviors;
using SolastaUnfinishedBusiness.Behaviors.Specific;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using SolastaUnfinishedBusiness.CustomUI;
using SolastaUnfinishedBusiness.Interfaces;
using SolastaUnfinishedBusiness.Models;
using SolastaUnfinishedBusiness.Validators;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static RuleDefinitions;
using static FeatureDefinitionAttributeModifier;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.SpellDefinitions;
using static SolastaUnfinishedBusiness.Builders.Features.AutoPreparedSpellsGroupBuilder;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.ConditionDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.WeaponTypeDefinitions;
using Resources = SolastaUnfinishedBusiness.Properties.Resources;

namespace SolastaUnfinishedBusiness.Subclasses;

[UsedImplicitly]
public sealed class OathOfDemonHunter : AbstractSubclass
{
    internal const string Name = "OathOfDemonHunterRemaster";

    internal static int GetSubclassLevel(RulesetCharacter character)
    {
        // compatible with old subclass
        var oldSubclassLevel = character.GetSubclassLevel(
            CharacterClassDefinitions.Paladin, OathOfDemonHunterOld.Name);
        var subClassLevel = character.GetSubclassLevel(
            CharacterClassDefinitions.Paladin, Name);
        
        return Mathf.Max(oldSubclassLevel, subClassLevel);
    }
    
    internal static readonly IsWeaponValidHandler IsOathOfDemonHunterWeapon = (mode, item, character) =>
    {
        var levels = GetSubclassLevel(character);

        return levels switch
        {
            >= 3 => ValidatorsWeapon
                .IsOfWeaponType(LightCrossbowType, HeavyCrossbowType, CustomWeaponsContext.HandXbowWeaponType)
                (mode, item, character),
            _ => false
        };
    };

    internal static FeatureDefinitionPower PowerLightEnergyCrossbowBolt { get; private set; }
    
    internal const string ConditionLightEnergyCrossbowBoltActiveName =
        $"Condition{Name}LightEnergyCrossbowBoltActive";
    
    public OathOfDemonHunter()
    {
        //
        // LEVEL 03
        //

        // auto prepared spells

        var autoPreparedSpells = FeatureDefinitionAutoPreparedSpellsBuilder
            .Create($"AutoPreparedSpells{Name}")
            .SetGuiPresentation(Name, Category.Subclass, "Feature/&DomainSpellsDescription")
            .SetAutoTag("Oath")
            .SetPreparedSpellGroups(
                BuildSpellGroup(2, HuntersMark, ProtectionFromEvilGood),
                BuildSpellGroup(5, MagicWeapon, MistyStep),
                BuildSpellGroup(9, Haste, DispelMagic),
                BuildSpellGroup(13, GuardianOfFaith, GreaterInvisibility),
                BuildSpellGroup(17, HoldMonster, DispelEvilAndGood))
            .SetSpellcastingClass(CharacterClassDefinitions.Paladin)
            .AddToDB();

        // Light Energy Crossbow Bolt - Long Rest Resource with Charisma Modifier
        // Bonus action to activate 1 minute buff: Charisma for attacks, ignore loading, allow smite spells

        const string LightEnergyCrossbowBoltName = $"FeatureSet{Name}LightEnergyCrossbowBolt";
        
        var conditionLightEnergyCrossbowBoltActive = ConditionDefinitionBuilder
            .Create(ConditionLightEnergyCrossbowBoltActiveName)
            .SetGuiPresentation(LightEnergyCrossbowBoltName, Category.Feature, ConditionDefinitions.ConditionBlessed)
            .SetPossessive()
            .SetConditionType(ConditionType.Beneficial)
            .SetFeatures(
                FeatureDefinitionBuilder
                    .Create($"Feature{Name}LightEnergyCrossbowBoltCharismaAttack")
                    .SetGuiPresentationNoContent(true)
                    .AddCustomSubFeatures(new CanUseAttribute(AttributeDefinitions.Charisma, IsOathOfDemonHunterWeapon))
                    .AddToDB(),
                FeatureDefinitionBuilder
                    .Create($"Feature{Name}LightEnergyCrossbowBoltRemoveMeleeDisadvantage")
                    .SetGuiPresentationNoContent(true)
                    .AddCustomSubFeatures(new RemoveRangedAttackInMeleeDisadvantageLevel7(IsOathOfDemonHunterWeapon))
                    .AddToDB(),
                FeatureDefinitionBuilder
                    .Create($"Feature{Name}LightEnergyCrossbowBoltRadiantDamage")
                    .SetGuiPresentationNoContent(true)
                    .AddCustomSubFeatures(new ModifyAttackActionModifierDivineCrossbowLevel7())
                    .AddToDB())
            .AddToDB();

        // Power to activate the condition
        PowerLightEnergyCrossbowBolt = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}LightEnergyCrossbowBolt")
            .SetGuiPresentation(LightEnergyCrossbowBoltName, Category.Feature,
                Sprites.GetSprite("PowerLightEnergyCrossbowBolt", Resources.PowerTrialMark, 256, 128))
            .SetUsesFixed(ActivationTime.BonusAction, RechargeRate.LongRest, 1, 0)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetDurationData(DurationType.Minute, 1)
                    .SetTargetingData(Side.Ally, RangeType.Self, 0, TargetType.Self)
                    .SetEffectForms(
                        EffectFormBuilder
                            .Create()
                            .SetConditionForm(conditionLightEnergyCrossbowBoltActive, ConditionForm.ConditionOperation.Add)
                            .Build())
                    .Build())
            .AddToDB();

        PowerLightEnergyCrossbowBolt.AddCustomSubFeatures(
            HasModifiedUses.Marker,
            new ModifyPowerPoolAmount
            {
                PowerPool = PowerLightEnergyCrossbowBolt,
                Type = PowerPoolBonusCalculationType.AttributeModifier,
                Attribute = AttributeDefinitions.Charisma
            },
            new PowerPortraitPointPool(PowerLightEnergyCrossbowBolt, Sprites.LightEnergyCrossbowBolt));

        // Power to restore all uses by spending Channel Divinity
        var powerRestoreLightEnergyCrossbowBolt = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}RestoreLightEnergyCrossbowBolt")
            .SetGuiPresentation($"PowerRestore{LightEnergyCrossbowBoltName}", Category.Feature, 
                Sprites.GetSprite("PowerLightEnergyCrossbowBoltRecharge", Resources.PowerTrialMarkRecharge, 256, 128))
            .SetUsesFixed(ActivationTime.NoCost, RechargeRate.ChannelDivinity)
            .SetShowCasting(false)
            .SetEffectDescription(EffectDescriptionBuilder.Create().Build())
            .AddCustomSubFeatures(new RestoreLightEnergyCrossbowBoltUses(PowerLightEnergyCrossbowBolt))
            .AddToDB();

        var featureSetLightEnergyCrossbowBolt = FeatureDefinitionFeatureSetBuilder
            .Create(LightEnergyCrossbowBoltName)
            .SetGuiPresentation(LightEnergyCrossbowBoltName, Category.Feature)
            .SetFeatureSet(PowerLightEnergyCrossbowBolt, powerRestoreLightEnergyCrossbowBolt)
            .AddToDB();

        // Trial Mark - Prevents Invisibility and adds Critical Threshold

        const string TrialMarkName = $"FeatureSet{Name}TrialMark";

        var conditionAffinityTrialMarkInvisibilityImmunity = FeatureDefinitionConditionAffinityBuilder
            .Create($"ConditionAffinity{Name}TrialMarkInvisibilityImmunity")
            .SetGuiPresentationNoContent(true)
            .SetConditionAffinityType(ConditionAffinityType.Immunity)
            .SetConditionType(ConditionDefinitions.ConditionInvisible)
            .AddToDB();

        var conditionTrialMark = ConditionDefinitionBuilder
            .Create(ConditionMarkedByHunter, $"Condition{Name}TrialMark")
            .SetOrUpdateGuiPresentation(Category.Condition)
            .SetConditionType(ConditionType.Detrimental)
            .SetPossessive()
            .SetFeatures(conditionAffinityTrialMarkInvisibilityImmunity)
            .AddToDB();

        var featureTrialMarkCritical = FeatureDefinitionBuilder
            .Create($"Feature{Name}TrialMarkCritical")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(new ModifyCriticalThresholdTrialMark(conditionTrialMark))
            .AddToDB();

        var additionalDamageTrialMark = FeatureDefinitionAdditionalDamageBuilder
            .Create($"AdditionalDamage{Name}TrialMark")
            .SetGuiPresentationNoContent(true)
            .SetNotificationTag("TrialMark")
            .SetDamageDice(DieType.D4, 1)
            .SetTargetCondition(conditionTrialMark, AdditionalDamageTriggerCondition.TargetHasConditionCreatedByMe)
            .SetSpecificDamageType(DamageTypeRadiant)
            .SetImpactParticleReference(
                FeatureDefinitionAdditionalDamages.AdditionalDamageBrandingSmite.impactParticleReference)
            .AddCustomSubFeatures(
                new CustomAdditionalDamageTrialMark(conditionTrialMark))
            .AddToDB();

        var powerTrialMark = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}TrialMark")
            .SetGuiPresentationNoContent(hidden: true)
            .SetUsesFixed(ActivationTime.NoCost, RechargeRate.ChannelDivinity)
            .SetShowCasting(false)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetDurationData(DurationType.Minute, 1, TurnOccurenceType.EndOfSourceTurn)
                    .SetTargetingData(Side.Enemy, RangeType.Distance, 12, TargetType.IndividualsUnique)
                    .SetParticleEffectParameters(LightningBolt)
                    .SetEffectForms(
                        EffectFormBuilder
                            .Create()
                            .SetConditionForm(conditionTrialMark, ConditionForm.ConditionOperation.Add)
                            .Build())
                    .Build())
            .AddToDB();

        var powerTrialMarkToggle = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}TrialMarkToggle")
            .SetGuiPresentation(TrialMarkName, Category.Feature)
            .SetUsesFixed(ActivationTime.NoCost)
            .SetShowCasting(false)
            .SetEffectDescription(EffectDescriptionBuilder.Create().Build())
            .AddToDB();

        _ = ActionDefinitionBuilder
            .Create(DatabaseHelper.ActionDefinitions.MetamagicToggle, $"Action{Name}TrialMarkToggle")
            .SetOrUpdateGuiPresentation(TrialMarkName, Category.Feature)
            .RequiresAuthorization()
            .SetActionId(ExtraActionId.OathOfDemonHunterTrialMarkToggle)
            .SetActivatedPower(powerTrialMarkToggle)
            .OverrideClassName("Toggle")
            .AddToDB();

        var actionAffinityTrialMarkToggle = FeatureDefinitionActionAffinityBuilder
            .Create(FeatureDefinitionActionAffinitys.ActionAffinitySorcererMetamagicToggle,
                $"ActionAffinity{Name}TrialMarkToggle")
            .SetGuiPresentationNoContent(true)
            .SetAuthorizedActions((ActionDefinitions.Id)ExtraActionId.OathOfDemonHunterTrialMarkToggle)
            .AddToDB();

        var featureTrialMarkOnHit = FeatureDefinitionBuilder
            .Create($"Feature{Name}TrialMarkOnHit")
            .SetGuiPresentation(TrialMarkName, Category.Feature, Gui.NoLocalization)
            .AddCustomSubFeatures(
                new PhysicalAttackFinishedByMeTrialMarkOnHit(conditionTrialMark, powerTrialMark))
            .AddToDB();

        var featureSetTrialMark = FeatureDefinitionFeatureSetBuilder
            .Create(TrialMarkName)
            .SetGuiPresentation(Category.Feature)
            .SetFeatureSet(
                additionalDamageTrialMark,
                powerTrialMark,
                actionAffinityTrialMarkToggle,
                featureTrialMarkOnHit,
                featureTrialMarkCritical)
            .AddToDB();

        //
        // LEVEL 07
        //

        // Divine Crossbow - Display only feature to show level 7 unlocks
        // (Actual functionality is in Light Energy Crossbow Bolt condition)
        
        var featureSetDivineCrossbow = FeatureDefinitionFeatureSetBuilder
            .Create($"FeatureSet{Name}DivineCrossbow")
            .SetGuiPresentation(Category.Feature)
            .AddToDB();

        // Hunter's Sight

        const string HUNTER_SIGHT_NAME = $"FeatureSet{Name}HunterSight";

        var featureHunterSightPerception = FeatureDefinitionBuilder
            .Create($"Feature{Name}HunterSightPerception")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(new ModifyAbilityCheckHunterSight())
            .AddToDB();

        var combatAffinityHunterSight = FeatureDefinitionCombatAffinityBuilder
            .Create($"CombatAffinity{Name}HunterSight")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(new ModifyAttackActionModifierHunterSight())
            .AddToDB();

        var featureSetHunterSight = FeatureDefinitionFeatureSetBuilder
            .Create(HUNTER_SIGHT_NAME)
            .SetGuiPresentation(Category.Feature)
            .AddFeatureSet(featureHunterSightPerception, combatAffinityHunterSight)
            .AddToDB();
        
        //
        // LEVEL 15
        //
        
        // Demon Hunter

        // Power for Hunter Step teleportation
        var powerHunterStep = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}HunterStep")
            .SetGuiPresentation(Category.Feature, SpellDefinitions.MistyStep)
            .SetUsesFixed(ActivationTime.NoCost, RechargeRate.TurnStart)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetTargetingData(Side.Ally, RangeType.Distance, 6, TargetType.Position)
                    .SetDurationData(DurationType.Round, 0, TurnOccurenceType.StartOfTurn)
                    .SetEffectForms(
                        EffectFormBuilder
                            .Create()
                            .SetMotionForm(MotionForm.MotionType.TeleportToDestination)
                            .Build())
                    .UseQuickAnimations()
                    .SetParticleEffectParameters(SpellDefinitions.MistyStep)
                    .Build())
            .DelegatedToAction()
            .AddToDB();

        // Action affinity to show Hunter Step in action bar
        var actionAffinityHunterStep = FeatureDefinitionActionAffinityBuilder
            .Create($"ActionAffinity{Name}HunterStep")
            .SetGuiPresentationNoContent(true)
            .SetAuthorizedActions((ActionDefinitions.Id)ExtraActionId.OathOfDemonHunterHunterStep)
            .AddToDB();

        // Action definition for Hunter Step
        _ = ActionDefinitionBuilder
            .Create($"Action{Name}HunterStep")
            .SetGuiPresentation(Category.Action, Sprites.GetSprite("HunterStep", Resources.HunterStep, 128, 128))
            .SetActionId(ExtraActionId.OathOfDemonHunterHunterStep)
            .SetActionType(ActionDefinitions.ActionType.NoCost)
            .SetActionScope(ActionDefinitions.ActionScope.Battle)
            .SetFormType(ActionDefinitions.ActionFormType.Large)
            .RequiresAuthorization()
            .OverrideClassName("UsePower")
            .SetActivatedPower(powerHunterStep)
            .AddToDB();

        // Condition that allows using Hunter Step (granted after attacking marked enemy)
        var conditionHunterStepReady = ConditionDefinitionBuilder
            .Create($"Condition{Name}HunterStepReady")
            .SetGuiPresentation(Category.Condition, ConditionDefinitions.ConditionBlessed)
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetSpecialDuration(DurationType.Round, 0, TurnOccurenceType.EndOfTurn)
            .SetConditionType(ConditionType.Beneficial)
            .SetFeatures(actionAffinityHunterStep, powerHunterStep)
            .AddCustomSubFeatures(AddUsablePowersFromCondition.Marker)
            .AddToDB();

        var featureDemonHunterTrigger = FeatureDefinitionBuilder
            .Create($"Feature{Name}DemonHunterTrigger")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(
                new PhysicalAttackFinishedByMeDemonHunter(conditionTrialMark, conditionHunterStepReady),
                new ModifyDamageAffinityDemonHunter(conditionTrialMark))
            .AddToDB();

        var featureSetDemonHunter = FeatureDefinitionFeatureSetBuilder
            .Create($"FeatureSet{Name}DemonHunter")
            .SetGuiPresentation(Category.Feature)
            .AddFeatureSet(featureDemonHunterTrigger)
            .AddToDB();
        
        //
        // LEVEL 20
        //
        
        // Demon Slayer

        var dieRollModifierDemonSlayer = FeatureDefinitionDieRollModifierBuilder
            .Create($"Feature{Name}DemonSlayer")
            .SetGuiPresentationNoContent(true)
            .SetModifiers(RollContext.AttackDamageValueRoll | RollContext.MagicDamageValueRoll, 1, 0, 3,
                "Feedback/&OathOfDemonHunterDemonSlayerReroll")
            .AddCustomSubFeatures(ValidateDieRollModifierDemonSlayerDamageTypeRadiant.Marker)
            .AddToDB();

        var featureRestoreChannelDivinity = FeatureDefinitionBuilder
            .Create($"Feature{Name}DemonSlayerRestoreChannelDivinity")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(new OnReducedToZeroHpByMeOrAllyDemonSlayer(conditionTrialMark, powerTrialMark))
            .AddToDB();

        var featureSetDemonSlayer = FeatureDefinitionFeatureSetBuilder
            .Create($"FeatureSet{Name}DemonSlayer")
            .SetGuiPresentation(Category.Feature)
            .AddFeatureSet(dieRollModifierDemonSlayer, featureRestoreChannelDivinity)
            .AddToDB();

        Subclass = CharacterSubclassDefinitionBuilder
            .Create(Name)
            .SetGuiPresentation(Category.Subclass, Sprites.GetSprite(Name, Resources.OathOfDemonHunter, 256))
            .AddFeaturesAtLevel(3,
                autoPreparedSpells,
                featureSetLightEnergyCrossbowBolt,
                featureSetTrialMark)
            .AddFeaturesAtLevel(7,
                featureSetDivineCrossbow,
                featureSetHunterSight)
            .AddFeaturesAtLevel(15,
                CommonBuilders.AttributeModifierThirdExtraAttack,
                featureSetDemonHunter)
            .AddFeaturesAtLevel(20,
                featureSetDemonSlayer)
            .AddToDB();
    }

    internal override CharacterClassDefinition Klass => CharacterClassDefinitions.Paladin;

    internal override CharacterSubclassDefinition Subclass { get; }

    internal override FeatureDefinitionSubclassChoice SubclassChoice => FeatureDefinitionSubclassChoices
        .SubclassChoicePaladinSacredOaths;


    
    // ReSharper disable once UnassignedGetOnlyAutoProperty
    internal override DeityDefinition DeityDefinition { get; }

    public static bool IsEnergyCrossbowBoltActive(
        RulesetCharacter attacker, 
        RulesetItem rulesetItem,
        RulesetAttackMode attackMode)
    {
        var isDemonHunterWeapon = IsOathOfDemonHunterWeapon(attackMode, rulesetItem, attacker);
        
        if (!isDemonHunterWeapon)
        {
            return false;
        }
        
        var oldSubclassLevel = attacker.GetSubclassLevel(
            CharacterClassDefinitions.Paladin, OathOfDemonHunterOld.Name);
        if (oldSubclassLevel >= 3)
        {
            // Old subclass always has it active
            return true;
        }
        
        if (!attacker.HasConditionOfType(ConditionLightEnergyCrossbowBoltActiveName))
        {
            return false;
        }

        return true;
    }

    // Convert crossbow damage to radiant (Level 7)
    private sealed class ModifyAttackActionModifierDivineCrossbowLevel7 : IModifyAttackActionModifier
    {
        public void OnAttackComputeModifier(
            RulesetCharacter myself,
            RulesetCharacter defender,
            BattleDefinitions.AttackProximity attackProximity,
            RulesetAttackMode attackMode,
            string effectName,
            ref ActionModifier attackModifier)
        {
            if (attackMode == null || !IsOathOfDemonHunterWeapon(attackMode, null, myself))
            {
                return;
            }

            // Only apply at level 7+
            var levels = GetSubclassLevel(myself);
            if (levels < 7)
            {
                return;
            }

            // Convert damage type to Radiant
            var damage = attackMode.EffectDescription?.FindFirstDamageForm();
            if (damage != null)
            {
                damage.DamageType = DamageTypeRadiant;
            }
        }
    }

    // Extend crossbow range (Level 15)
    private sealed class ValidateDieRollModifierDemonSlayerDamageTypeRadiant : IValidateDieRollModifier
    {
        internal static readonly ValidateDieRollModifierDemonSlayerDamageTypeRadiant Marker = new();

        public bool CanModifyRoll(
            RulesetCharacter character, List<FeatureDefinition> features, List<string> damageTypes)
        {
            return damageTypes.Contains(DamageTypeRadiant);
        }
    }
    
    
    // Modify Critical Threshold for Trial Marked targets
    private sealed class ModifyCriticalThresholdTrialMark(ConditionDefinition conditionTrialMark)
        : IModifyAttackCriticalThreshold
    {
        public int GetCriticalThreshold(
            int current, RulesetCharacter me, RulesetCharacter target, BaseDefinition attackMethod)
        {
            if (target == null || !attackMethod)
            {
                return current;
            }
            
            // Only apply if using crossbow weapon
            if (attackMethod is not ItemDefinition item || !IsOathOfDemonHunterWeaponItem(item))
            {
                return current;
            }

            if (target.HasConditionOfType(conditionTrialMark.Name))
            {
                Main.Log("Oath of Demon Hunter: Lowering critical threshold for Trial Marked target");
                return current - 1;
            }

            return current;
        }

        public bool IsOathOfDemonHunterWeaponItem(ItemDefinition item)
        {
            var type = item?.weaponDefinition?.WeaponTypeDefinition;
            if (!type)
                return false;

            if (type    == LightCrossbowType
                || type == HeavyCrossbowType
                || type == CustomWeaponsContext.HandXbowWeaponType)
            {
                return true;
            }

            return false;
        }
    }

    // Custom Additional Damage with dice progression
    private sealed class CustomAdditionalDamageTrialMark(ConditionDefinition conditionTrialMark)
        : IModifyAdditionalDamage
    {
        public void ModifyAdditionalDamage(
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            RulesetAttackMode attackMode,
            FeatureDefinitionAdditionalDamage featureDefinitionAdditionalDamage,
            List<EffectForm> actualEffectForms,
            ref DamageForm damageForm)
        {
            if (!IsOathOfDemonHunterWeapon(attackMode, null, attacker.RulesetCharacter))
            {
                return;
            }

            if (!defender.RulesetActor.HasConditionOfType(conditionTrialMark.Name))
            {
                return;
            }

            var levels = GetSubclassLevel(attacker.RulesetCharacter);

            // 3: 1d4, 6: 1d6, 10: 1d8, 14: 1d10, 18: 1d12
            damageForm.DieType = levels switch
            {
                >= 18 => DieType.D12,
                >= 14 => DieType.D10,
                >= 10 => DieType.D8,
                >= 6 => DieType.D6,
                _ => DieType.D4
            };
            damageForm.DiceNumber = 1;
        }
    }

    // Apply Trial Mark on crossbow hit
    private sealed class PhysicalAttackFinishedByMeTrialMarkOnHit(
        ConditionDefinition conditionTrialMark,
        FeatureDefinitionPower powerTrialMark)
        : IPhysicalAttackFinishedByMe
    {
        public IEnumerator OnPhysicalAttackFinishedByMe(
            GameLocationBattleManager battleManager,
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            RulesetAttackMode attackMode,
            RollOutcome rollOutcome,
            int damageAmount)
        {
            if (rollOutcome is RollOutcome.Failure or RollOutcome.CriticalFailure)
            {
                yield break;
            }

            var rulesetAttacker = attacker.RulesetCharacter;

            if (!IsOathOfDemonHunterWeapon(attackMode, null, rulesetAttacker))
            {
                yield break;
            }

            var rulesetDefender = defender.RulesetActor;

            if (rulesetDefender is not { IsDeadOrDyingOrUnconscious: false } ||
                rulesetDefender.HasConditionOfType(conditionTrialMark.Name))
            {
                yield break;
            }

            var usablePower = PowerProvider.Get(powerTrialMark, rulesetAttacker);

            if (rulesetAttacker.GetRemainingUsesOfPower(usablePower) == 0)
            {
                yield break;
            }

            // Check toggle state - if enabled, use reaction; otherwise auto-apply
            var toggleEnabled = rulesetAttacker.IsToggleEnabled(
                (ActionDefinitions.Id)ExtraActionId.OathOfDemonHunterTrialMarkToggle);

            if (toggleEnabled)
            {
                yield return attacker.MyReactToUsePower(
                    ActionDefinitions.Id.PowerNoCost,
                    usablePower,
                    [defender],
                    attacker,
                    "TrialMark");
            }
        }
    }

    // Hunter's Sight - Remove Disadvantage in Darkness
    private sealed class ModifyAttackActionModifierHunterSight : IModifyAttackActionModifier
    {
        public void OnAttackComputeModifier(
            RulesetCharacter myself,
            RulesetCharacter defender,
            BattleDefinitions.AttackProximity attackProximity,
            RulesetAttackMode attackMode,
            string effectName,
            ref ActionModifier attackModifier)
        {
            if (myself is not { IsDeadOrDyingOrUnconscious: false } ||
                defender is not { IsDeadOrDyingOrUnconscious: false })
            {
                return;
            }

            // Only apply if defender is not in bright light (i.e., in darkness or dim light)
            if (!ValidatorsCharacter.IsNotInBrightLight(defender))
            {
                return;
            }

            // Remove disadvantage trends from attacking in darkness
            attackModifier.AttackAdvantageTrends.RemoveAll(t =>
                t.value == -1
                && t is
                {
                    sourceType: FeatureSourceType.Lighting
                });
        }
    }

    // Demon Hunter - Reveal Monster Knowledge and Ignore Resistances
    private sealed class PhysicalAttackFinishedByMeDemonHunter(
        ConditionDefinition conditionTrialMark,
        ConditionDefinition conditionHunterStepReady)
        : IPhysicalAttackFinishedByMe
    {
        public IEnumerator OnPhysicalAttackFinishedByMe(
            GameLocationBattleManager battleManager,
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            RulesetAttackMode attackMode,
            RollOutcome rollOutcome,
            int damageAmount)
        {
            var rulesetAttacker = attacker.RulesetCharacter;
            var rulesetDefender = defender.RulesetCharacter;

            // Grant Hunter Step ready condition (once per round)
            // Allow to get hunter step after attack without trial mark
            if (!rulesetAttacker.HasConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect, conditionHunterStepReady.Name))
            {
                rulesetAttacker.InflictCondition(
                    conditionHunterStepReady.Name,
                    DurationType.Round,
                    0,
                    TurnOccurenceType.EndOfTurn,
                    AttributeDefinitions.TagEffect,
                    rulesetAttacker.Guid,
                    rulesetAttacker.CurrentFaction.Name,
                    1,
                    conditionHunterStepReady.Name,
                    0,
                    0,
                    0);
            }
            
            // Check if defender has Trial Mark condition
            if (!rulesetDefender.HasConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect, conditionTrialMark.Name))
            {   
                yield break;
            }

            // Reveal full monster knowledge if defender is a monster
            if (rulesetDefender is RulesetCharacterMonster monster)
            {
                var gameLoreService = ServiceRepository.GetService<IGameLoreService>();
                gameLoreService.LearnMonsterKnowledge(
                    monster.MonsterDefinition, GetDefinition<KnowledgeLevelDefinition>("Mastered4"));
            }
        }
    }

    // Demon Hunter - Bypass Resistances
    private sealed class ModifyDamageAffinityDemonHunter(ConditionDefinition conditionTrialMark)
        : IModifyDamageAffinity
    {
        public void ModifyDamageAffinity(RulesetActor defender, RulesetActor attacker, List<FeatureDefinition> features)
        {
            if (defender == null || attacker == null)
            {
                return;
            }

            // Check if defender has Trial Mark from this attacker
            if (!defender.HasConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect, conditionTrialMark.Name))
            {
                return;
            }

            // Remove all radiant resistance features
            features.RemoveAll(f => f is FeatureDefinitionDamageAffinity damageAffinity && 
                                    damageAffinity == FeatureDefinitionDamageAffinitys.DamageAffinityRadiantResistance);
        }
    }

    // Demon Slayer - Restore Channel Divinity when marked enemy dies
    private sealed class OnReducedToZeroHpByMeOrAllyDemonSlayer(
        ConditionDefinition conditionTrialMark,
        FeatureDefinitionPower powerTrialMark) : IOnReducedToZeroHpByMeOrAlly
    {
        public IEnumerator HandleReducedToZeroHpByMeOrAlly(
            GameLocationCharacter attacker,
            GameLocationCharacter downedCreature,
            GameLocationCharacter ally,
            RulesetAttackMode attackMode,
            RulesetEffect activeEffect)
        {
            var rulesetAlly = ally.RulesetCharacter;
            var rulesetDowned = downedCreature.RulesetCharacter;

            // Check if ally has this feature (level 20 Oath of Demon Hunter)
            if (rulesetAlly is not { IsDeadOrDyingOrUnconscious: false })
            {
                yield break;
            }

            // Check if downed creature had Trial Mark condition
            if (!rulesetDowned.HasConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect, conditionTrialMark.Name))
            {
                yield break;
            }

            // Restore one use of Channel Divinity (Trial Mark power)
            var usablePower = PowerProvider.Get(powerTrialMark, rulesetAlly);
            rulesetAlly.UpdateUsageForPowerPool(-1, usablePower);
        }
    }

    // Ignore Loading Property for Crossbows
    internal sealed class IgnoreCrossbowLoadingProperty
    {
        internal static void ModifyTags(RulesetItem item, RulesetCharacter character, Dictionary<string, TagsDefinitions.Criticity> tags)
        {
            if (character == null || item == null)
            {
                return;
            }

            var levels = GetSubclassLevel(character);

            if (levels < 3)
            {
                return;
            }

            if (!ValidatorsWeapon.IsOfWeaponType(
                LightCrossbowType, HeavyCrossbowType, CustomWeaponsContext.HandXbowWeaponType)
                (null, item, character))
            {
                return;
            }

            tags.Remove(TagsDefinitions.WeaponTagLoading);
        }
    }

    // Restore 1 Light Energy Crossbow Bolt use by spending Channel Divinity
    private sealed class RestoreLightEnergyCrossbowBoltUses(FeatureDefinitionPower powerToRestore)
        : IPowerOrSpellFinishedByMe
    {
        public IEnumerator OnPowerOrSpellFinishedByMe(CharacterActionMagicEffect action, BaseDefinition baseDefinition)
        {
            var character = action.ActingCharacter.RulesetCharacter;
            
            if (!action.Countered && !action.ExecutionFailed)
            {
                var usablePower = PowerProvider.Get(powerToRestore, character);
                var currentUses = character.GetRemainingUsesOfPower(usablePower);
                var maxUses = character.GetMaxUsesForPool(usablePower.PowerDefinition);
                
                // Only restore 1 use, and only if not already at max
                if (currentUses < maxUses)
                {
                    character.UpdateUsageForPowerPool(-1, usablePower);
                }
            }

            yield break;
        }
    }

    // Remove ranged attack in melee disadvantage (Level 7+)
    private sealed class RemoveRangedAttackInMeleeDisadvantageLevel7(IsWeaponValidHandler isWeaponValid)
        : RemoveRangedAttackInMeleeDisadvantage(isWeaponValid, HasLevel7)
    {
        private static bool HasLevel7(RulesetCharacter character)
        {
            return GetSubclassLevel(character) >= 7;
        }
    }

    // Hunter's Sight - Add Charisma modifier to Perception checks
    private sealed class ModifyAbilityCheckHunterSight : IModifyAbilityCheck
    {
        public void MinRoll(
            RulesetCharacter character,
            int baseBonus,
            string abilityScoreName,
            string proficiencyName,
            List<TrendInfo> advantageTrends,
            List<TrendInfo> modifierTrends,
            ref int rollModifier,
            ref int minRoll)
        {
            if (proficiencyName != SkillDefinitions.Perception)
            {
                return;
            }

            var charisma = character.TryGetAttributeValue(AttributeDefinitions.Charisma);
            var chaMod = AttributeDefinitions.ComputeAbilityScoreModifier(charisma);

            if (chaMod <= 0)
            {
                return;
            }

            rollModifier += chaMod;

            modifierTrends.Add(new TrendInfo(chaMod, FeatureSourceType.CharacterFeature,
                "FeatureSetOathOfDemonHunterHunterSight", null));
        }
    }
    
    internal sealed class ValidateSmiteDamageForDemonHunter : IValidateContextInsteadOfRestrictedProperty
    {
        public (OperationType, bool) ValidateContext(
            BaseDefinition             definition,
            IRestrictedContextProvider provider,
            RulesetCharacter           character,
            ItemDefinition             itemDefinition,
            bool                       rangedAttack,
            RulesetAttackMode          attackMode,
            RulesetEffect              rulesetEffect)
        {
            if (IsOathOfDemonHunterWeapon(attackMode, attackMode.sourceObject as RulesetItem, character))
            {
                var oldSubclassLevel = character.GetSubclassLevel(
                    CharacterClassDefinitions.Paladin, OathOfDemonHunterOld.Name);
                if (oldSubclassLevel >= 3)
                {
                    // Old subclass always allows to smite with crossbow
                    return (OperationType.Set, true);
                }
                
                // Check if Light Energy Crossbow Bolt condition is active
                var conditionName = $"Condition{Name}LightEnergyCrossbowBoltActive";
            
                if (character.HasConditionOfType(conditionName))
                {
                    return (OperationType.Set, true);
                }
            
                // Condition not active, don't allow to smite with crossbow
                return (OperationType.Set, false);
            }

            return (OperationType.Ignore, false);
        }
    }
}
