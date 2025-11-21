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
using SolastaUnfinishedBusiness.Properties;
using SolastaUnfinishedBusiness.Validators;
using UnityEngine.AddressableAssets;
using static RuleDefinitions;
using static FeatureDefinitionAttributeModifier;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.SpellDefinitions;
using static SolastaUnfinishedBusiness.Builders.Features.AutoPreparedSpellsGroupBuilder;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.ConditionDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.WeaponTypeDefinitions;

namespace SolastaUnfinishedBusiness.Subclasses;

[UsedImplicitly]
public sealed class OathOfDemonHunter : AbstractSubclass
{
    internal const string Name = "OathOfDemonHunter";

    internal static readonly IsWeaponValidHandler IsOathOfDemonHunterWeapon = (mode, item, character) =>
    {
        var levels = character.GetSubclassLevel(CharacterClassDefinitions.Paladin, Name);

        return levels switch
        {
            >= 3 => ValidatorsWeapon
                .IsOfWeaponType(LightCrossbowType, HeavyCrossbowType, CustomWeaponsContext.HandXbowWeaponType)
                (mode, item, character),
            _ => false
        };
    };

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

        // Light Energy Crossbow Bolt - Short Rest Resource with Charisma Modifier

        const string LightEnergyCrossbowBoltName = $"FeatureSet{Name}LightEnergyCrossbowBolt";

        var powerLightEnergyCrossbowBolt = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}LightEnergyCrossbowBolt")
            .SetGuiPresentationNoContent(true)
            .SetUsesFixed(ActivationTime.OnAttackHitAuto, RechargeRate.ShortRest, 1, 0)
            .SetShowCasting(false)
            .SetEffectDescription(EffectDescriptionBuilder.Create().Build())
            .AddToDB();

        powerLightEnergyCrossbowBolt.AddCustomSubFeatures(
            HasModifiedUses.Marker,
            new ModifyPowerPoolAmount
            {
                PowerPool = powerLightEnergyCrossbowBolt,
                Type = PowerPoolBonusCalculationType.AttributeModifier,
                Attribute = AttributeDefinitions.Charisma
            },
            new PowerPortraitPointPool(powerLightEnergyCrossbowBolt, Sprites.LightEnergyCrossbowBolt));

        var featureIgnoreCrossbowLoading = FeatureDefinitionBuilder
            .Create($"Feature{Name}IgnoreCrossbowLoading")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(new IgnoreCrossbowLoadingProperty())
            .AddToDB();

        var featureSetLightEnergyCrossbowBolt = FeatureDefinitionFeatureSetBuilder
            .Create(LightEnergyCrossbowBoltName)
            .SetGuiPresentation(LightEnergyCrossbowBoltName, Category.Feature)
            .SetFeatureSet(powerLightEnergyCrossbowBolt, featureIgnoreCrossbowLoading)
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
            .SetSpecialInterruptions(ConditionInterruption.Attacks)
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
            .SetGuiPresentation(TrialMarkName, Category.Feature,
                Sprites.GetSprite("PowerTrialMark", Resources.PowerTrialMark, 256, 128))
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

        // Divine Crossbow

        const string DivineCrossbowName = $"FeatureSet{Name}DivineCrossbow";

        var featureRemoveCrossbowMeleeDisadvantage = FeatureDefinitionBuilder
            .Create($"Feature{Name}RemoveCrossbowMeleeDisadvantage")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(new RemoveRangedAttackInMeleeDisadvantage(IsOathOfDemonHunterWeapon))
            .AddToDB();

        var featureConvertCrossbowDamageToRadiant = FeatureDefinitionBuilder
            .Create($"Feature{Name}ConvertCrossbowDamageToRadiant")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(new ModifyAttackActionModifierDivineCrossbow())
            .AddToDB();

        var featureLightEnergyCrossbowBolt = FeatureDefinitionBuilder
            .Create($"Feature{Name}LightEnergyCrossbowBolt")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(
                new PhysicalAttackFinishedByMeLightEnergyCrossbowBolt(conditionTrialMark, powerTrialMark))
            .AddToDB();

        var featureSetDivineCrossbow = FeatureDefinitionFeatureSetBuilder
            .Create(DivineCrossbowName)
            .SetGuiPresentation(Category.Feature)
            .SetFeatureSet(
                featureRemoveCrossbowMeleeDisadvantage,
                featureConvertCrossbowDamageToRadiant,
                featureLightEnergyCrossbowBolt)
            .AddToDB();

        // Hunter's Sight

        const string HUNTER_SIGHT_NAME = $"FeatureSet{Name}HunterSight";

        var attributeModifierHunterSight = FeatureDefinitionAttributeModifierBuilder
            .Create($"AttributeModifier{Name}HunterSightPerception")
            .SetGuiPresentationNoContent(true)
            .SetModifierAbilityScore(SkillDefinitions.Perception, AttributeDefinitions.Charisma)
            .AddToDB();

        var combatAffinityHunterSight = FeatureDefinitionCombatAffinityBuilder
            .Create($"CombatAffinity{Name}HunterSight")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(new ModifyAttackActionModifierHunterSight())
            .AddToDB();

        var featureSetHunterSight = FeatureDefinitionFeatureSetBuilder
            .Create(HUNTER_SIGHT_NAME)
            .SetGuiPresentation(Category.Feature)
            .AddFeatureSet(attributeModifierHunterSight, combatAffinityHunterSight)
            .AddToDB();
        
        //
        // LEVEL 15
        //
        
        // Demon Hunter
        
        const string DEMON_HUNTER_NAME = $"FeatureSet{Name}DemonHunter";

        var conditionHunterStepUsed = ConditionDefinitionBuilder
            .Create($"Condition{Name}HunterStepUsed")
            .SetGuiPresentationNoContent(true)
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetSpecialDuration(DurationType.Round, 1)
            .AddToDB();

        var powerHunterStep = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}HunterStep")
            .SetGuiPresentation(Category.Feature, SpellDefinitions.MistyStep)
            .SetUsesFixed(ActivationTime.NoCost, RechargeRate.AtWill)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetTargetingData(Side.Ally, RangeType.Self, 0, TargetType.Self)
                    .SetDurationData(DurationType.Instantaneous)
                    .SetEffectForms(
                        EffectFormBuilder
                            .Create()
                            .SetMotionForm(MotionForm.MotionType.TeleportToDestination, 6)
                            .Build())
                    .SetParticleEffectParameters(SpellDefinitions.MistyStep)
                    .Build())
            .AddCustomSubFeatures(new ValidatePowerUseHunterStep(conditionHunterStepUsed))
            .AddToDB();

        var featureSetDemonHunter = FeatureDefinitionFeatureSetBuilder
            .Create(DEMON_HUNTER_NAME)
            .SetGuiPresentation(Category.Feature)
            .AddFeatureSet(powerHunterStep)
            .AddCustomSubFeatures(
                new PhysicalAttackFinishedByMeDemonHunter(conditionTrialMark, conditionHunterStepUsed),
                new ModifyDamageAffinityDemonHunter(conditionTrialMark))
            .AddToDB();
        
        //
        // LEVEL 20
        //
        
        // Demon Slayer

        const string DEMON_SLAYER_NAME = $"FeatureSet{Name}DemonSlayer";

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
            .Create(DEMON_SLAYER_NAME)
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

    private sealed class PhysicalAttackFinishedByMeLightEnergyCrossbowBolt(
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
                rulesetDefender.HasConditionOfType(conditionTrialMark))
            {
                yield break;
            }

            var usablePower = PowerProvider.Get(powerTrialMark, rulesetAttacker);

            if (rulesetAttacker.GetRemainingUsesOfPower(usablePower) == 0)
            {
                yield break;
            }

            yield return attacker.MyReactToUsePower(
                ActionDefinitions.Id.PowerNoCost,
                usablePower,
                [defender],
                attacker,
                "TrialMark");
        }
    }

    // Convert crossbow damage to radiant (Level 7)
    private sealed class ModifyAttackActionModifierDivineCrossbow : IModifyAttackActionModifier
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

            // Convert damage type to Radiant
            var damage = attackMode.EffectDescription?.FindFirstDamageForm();
            if (damage != null)
            {
                damage.DamageType = DamageTypeRadiant;
            }
        }
    }

    // Extend crossbow range (Level 15)
    private sealed class ModifyCrossbowAttackModeDivineCrossbow : IModifyWeaponAttackMode
    {
        public void ModifyWeaponAttackMode(
            RulesetCharacter character,
            RulesetAttackMode attackMode,
            RulesetItem weapon,
            bool canAddAbilityDamageBonus)
        {
            if (!IsOathOfDemonHunterWeapon(attackMode, null, character))
            {
                return;
            }

            attackMode.maxRange += 6;
        }
    }

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

            if (!defender.RulesetActor.HasConditionOfType(conditionTrialMark))
            {
                return;
            }

            var levels = attacker.RulesetCharacter.GetSubclassLevel(CharacterClassDefinitions.Paladin, Name);

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
                rulesetDefender.HasConditionOfType(conditionTrialMark))
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
        ConditionDefinition conditionHunterStepUsed)
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

            // Grant Hunter Step usage if available and not used this round
            if (!rulesetAttacker.HasConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect, conditionHunterStepUsed.Name))
            {
                rulesetAttacker.InflictCondition(
                    conditionHunterStepUsed.Name,
                    DurationType.Round,
                    1,
                    TurnOccurenceType.EndOfTurn,
                    AttributeDefinitions.TagEffect,
                    rulesetAttacker.Guid,
                    rulesetAttacker.CurrentFaction.Name,
                    1,
                    conditionHunterStepUsed.Name,
                    0,
                    0,
                    0);
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

    // Hunter Step - Validate Power Use
    private sealed class ValidatePowerUseHunterStep(ConditionDefinition conditionHunterStepUsed)
        : IPowerOrSpellFinishedByMe
    {
        public IEnumerator OnPowerOrSpellFinishedByMe(CharacterActionMagicEffect action, BaseDefinition baseDefinition)
        {
            // Mark that Hunter Step was used this round
            if (!action.Countered && !action.ExecutionFailed)
            {
                var character = action.ActingCharacter.RulesetCharacter;
                
                if (!character.HasConditionOfCategoryAndType(
                        AttributeDefinitions.TagEffect, conditionHunterStepUsed.Name))
                {
                    character.InflictCondition(
                        conditionHunterStepUsed.Name,
                        DurationType.Round,
                        1,
                        TurnOccurenceType.EndOfTurn,
                        AttributeDefinitions.TagEffect,
                        character.Guid,
                        character.CurrentFaction.Name,
                        1,
                        conditionHunterStepUsed.Name,
                        0,
                        0,
                        0);
                }
            }

            yield break;
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

            var levels = character.GetSubclassLevel(CharacterClassDefinitions.Paladin, Name);

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
}
