using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Behaviors;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using SolastaUnfinishedBusiness.CustomUI;
using SolastaUnfinishedBusiness.Interfaces;
using SolastaUnfinishedBusiness.Properties;
using SolastaUnfinishedBusiness.Validators;
using static RuleDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionSubclassChoices;

namespace SolastaUnfinishedBusiness.Subclasses;

public sealed class RoguishSwashbuckler : AbstractSubclass
{
    internal const string Name = "RoguishSwashbuckler";

    public RoguishSwashbuckler()
    {
        // LEVEL 03 - Rakish Audacity: CHA to Initiative
        var attributeModifierRakishAudacityInitiative = FeatureDefinitionAttributeModifierBuilder
            .Create($"AttributeModifier{Name}RakishAudacityInitiative")
            .SetGuiPresentation(Category.Feature)
            .SetModifierAbilityScore(
                AttributeDefinitions.Initiative,
                AttributeDefinitions.Charisma)
            .AddToDB();

        // LEVEL 03 - Rakish Audacity: 1v1 Sneak Attack
        var additionalDamageRakishAudacity1v1 = FeatureDefinitionAdditionalDamageBuilder
            .Create($"AdditionalDamage{Name}RakishAudacity1v1")
            .SetGuiPresentation(Category.Feature)
            .SetNotificationTag(TagsDefinitions.AdditionalDamageSneakAttackTag)
            .SetDamageDice(DieType.D6, 1)
            .SetAdvancement(AdditionalDamageAdvancement.ClassLevel, 1, 1, 2)
            .SetTriggerCondition(ExtraAdditionalDamageTriggerCondition.TargetIsDuelingWithYou)
            .SetRequiredProperty(RestrictedContextRequiredProperty.FinesseOrRangeWeapon)
            .SetFrequencyLimit(FeatureLimitedUsage.OncePerTurn)
            .AddToDB();

        // LEVEL 03 - Fancy Footwork
        var conditionFancyFootworkMark = ConditionDefinitionBuilder
            .Create($"Condition{Name}FancyFootworkMark")
            .SetGuiPresentationNoContent(true)
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetSpecialDuration(DurationType.Round, 0, TurnOccurenceType.EndOfSourceTurn)
            .AddToDB();

        var conditionFancyFootworkImmune = ConditionDefinitionBuilder
            .Create($"Condition{Name}FancyFootworkImmune")
            .SetGuiPresentationNoContent(true)
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetSpecialDuration(DurationType.Round, 0, TurnOccurenceType.EndOfSourceTurn)
            .AddCustomSubFeatures(new IgnoreAoOOnMeFancyFootwork(conditionFancyFootworkMark))
            .AddToDB();

        var featureFancyFootwork = FeatureDefinitionBuilder
            .Create($"Feature{Name}FancyFootwork")
            .SetGuiPresentation(Category.Feature)
            .AddCustomSubFeatures(new PhysicalAttackFinishedByMeFancyFootwork(
                conditionFancyFootworkMark,
                conditionFancyFootworkImmune))
            .AddToDB();

        var featureSetFancyFootwork = FeatureDefinitionFeatureSetBuilder
            .Create($"FeatureSet{Name}FancyFootwork")
            .SetGuiPresentation(Category.Feature)
            .AddFeatureSet(featureFancyFootwork)
            .AddToDB();

        // LEVEL 09 - Panache
        var conditionPanacheSource = ConditionDefinitionBuilder
            .Create($"Condition{Name}PanacheSource")
            .SetGuiPresentationNoContent(true)
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetSpecialDuration(DurationType.Minute, 1)
            .AddToDB();

        var conditionPanache = ConditionDefinitionBuilder
            .Create($"Condition{Name}Panache")
            .SetGuiPresentation(Category.Condition, ConditionDefinitions.ConditionDistracted)
            .SetConditionType(ConditionType.Detrimental)
            .SetSpecialDuration(DurationType.Minute, 1)
            .AddToDB();

        conditionPanacheSource.AddCustomSubFeatures(
            new PanacheSourceConditionBehavior(conditionPanache, conditionPanacheSource));

        conditionPanache.AddCustomSubFeatures(
            new PanacheConditionBehavior(conditionPanache),
            new ModifyAttackActionModifierPanache(conditionPanache));

        var powerPanache = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}Panache")
            .SetGuiPresentation(Category.Feature)
            .SetUsesFixed(ActivationTime.Action, RechargeRate.AtWill)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetTargetingData(Side.Enemy, RangeType.Distance, 12, TargetType.IndividualsUnique)
                    .Build())
            .AddCustomSubFeatures(
                new CustomBehaviorPanache(conditionPanache, conditionPanacheSource),
                new FilterTargetingCharacterPanache())
            .AddToDB();

        var featureSetPanache = FeatureDefinitionFeatureSetBuilder
            .Create($"FeatureSet{Name}Panache")
            .SetGuiPresentation(powerPanache.GuiPresentation.Title, powerPanache.GuiPresentation.Description)
            .AddFeatureSet(powerPanache)
            .AddToDB();

        // LEVEL 13 - Elegant Maneuver
        var conditionElegantManeuver = ConditionDefinitionBuilder
            .Create($"Condition{Name}ElegantManeuver")
            .SetGuiPresentation(Category.Condition, ConditionDefinitions.ConditionBlessed)
            .SetConditionType(ConditionType.Beneficial)
            .SetSpecialDuration(DurationType.Round, 0, TurnOccurenceType.EndOfTurn)
            .SetFeatures(
                FeatureDefinitionAbilityCheckAffinityBuilder
                    .Create($"AbilityCheckAffinity{Name}ElegantManeuverAcrobatics")
                    .SetGuiPresentationNoContent(true)
                    .BuildAndSetAffinityGroups(
                        CharacterAbilityCheckAffinity.Advantage,
                        abilityProficiencyPairs: (AttributeDefinitions.Dexterity, SkillDefinitions.Acrobatics))
                    .AddToDB(),
                FeatureDefinitionAbilityCheckAffinityBuilder
                    .Create($"AbilityCheckAffinity{Name}ElegantManeuverAthletics")
                    .SetGuiPresentationNoContent(true)
                    .BuildAndSetAffinityGroups(
                        CharacterAbilityCheckAffinity.Advantage,
                        abilityProficiencyPairs: (AttributeDefinitions.Strength, SkillDefinitions.Athletics))
                    .AddToDB())
            .AddSpecialInterruptions(ConditionInterruption.AbilityCheck)
            .AddToDB();

        var powerElegantManeuver = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}ElegantManeuver")
            .SetGuiPresentation(Category.Feature)
            .SetUsesFixed(ActivationTime.BonusAction, RechargeRate.AtWill)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetDurationData(DurationType.Round, 0, TurnOccurenceType.EndOfTurn)
                    .SetTargetingData(Side.Ally, RangeType.Self, 0, TargetType.Self)
                    .SetEffectForms(
                        EffectFormBuilder
                            .Create()
                            .SetConditionForm(conditionElegantManeuver, ConditionForm.ConditionOperation.Add)
                            .Build())
                    .Build())
            .AddToDB();

        // LEVEL 17 - Master Duelist
        var powerMasterDuelist = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}MasterDuelist")
            .SetGuiPresentation(Category.Feature)
            .SetUsesFixed(ActivationTime.Reaction, RechargeRate.ShortRest)
            .SetReactionContext(ReactionTriggerContext.None)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetDurationData(DurationType.Round, 1)
                    .SetTargetingData(Side.Ally, RangeType.Self, 0, TargetType.Self)
                    .Build())
            .AddToDB();

        powerMasterDuelist.AddCustomSubFeatures(new CustomBehaviorMasterDuelist(powerMasterDuelist));

        // MAIN
        Subclass = CharacterSubclassDefinitionBuilder
            .Create(Name)
            .SetGuiPresentation(Category.Subclass, Sprites.GetSprite(Name, Resources.RoguishDuelist, 256))
            .AddFeaturesAtLevel(3,
                attributeModifierRakishAudacityInitiative,
                additionalDamageRakishAudacity1v1,
                featureSetFancyFootwork)
            .AddFeaturesAtLevel(9, featureSetPanache)
            .AddFeaturesAtLevel(13, powerElegantManeuver)
            .AddFeaturesAtLevel(17, powerMasterDuelist)
            .AddToDB();
    }

    internal override CharacterClassDefinition Klass => Rogue;

    internal override CharacterSubclassDefinition Subclass { get; }

    internal override FeatureDefinitionSubclassChoice SubclassChoice => SubclassChoiceRogueRoguishArchetypes;

    internal override DeityDefinition DeityDefinition => null;

    internal static bool IsRakishAudacity1v1Valid(
        GameLocationCharacter attacker,
        GameLocationCharacter defender,
        AdvantageType advantageType)
    {
        return
            advantageType != AdvantageType.Disadvantage &&
            attacker.RulesetCharacter.GetSubclassLevel(Rogue, Name) >= 3 &&
            attacker.IsWithinRange(defender, 1) &&
            Gui.Battle.AllContenders
                .Where(x => x != attacker && x != defender)
                .All(x => !attacker.IsWithinRange(x, 1));
    }

    //
    // Fancy Footwork
    //

    private sealed class IgnoreAoOOnMeFancyFootwork : IIgnoreAoOOnMe
    {
        private readonly ConditionDefinition conditionFancyFootworkMark;

        public IgnoreAoOOnMeFancyFootwork(ConditionDefinition mark)
        {
            conditionFancyFootworkMark = mark;
        }

        public bool CanIgnoreAoOOnSelf(RulesetCharacter defender, RulesetCharacter attacker)
        {
            return attacker.HasConditionOfCategoryAndType(
                AttributeDefinitions.TagEffect, conditionFancyFootworkMark.Name);
        }
    }

    private sealed class PhysicalAttackFinishedByMeFancyFootwork : IPhysicalAttackFinishedByMe
    {
        private readonly ConditionDefinition conditionFancyFootworkMark;
        private readonly ConditionDefinition conditionFancyFootworkImmune;

        public PhysicalAttackFinishedByMeFancyFootwork(
            ConditionDefinition mark,
            ConditionDefinition immune)
        {
            conditionFancyFootworkMark = mark;
            conditionFancyFootworkImmune = immune;
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
            if (!ValidatorsWeapon.IsMeleeOrUnarmed(attackMode))
            {
                yield break;
            }

            var rulesetAttacker = attacker.RulesetCharacter;

            rulesetAttacker.InflictCondition(
                conditionFancyFootworkImmune.Name,
                DurationType.Round,
                0,
                TurnOccurenceType.EndOfTurn,
                AttributeDefinitions.TagEffect,
                rulesetAttacker.guid,
                rulesetAttacker.CurrentFaction.Name,
                1,
                conditionFancyFootworkImmune.Name,
                0,
                0,
                0);

            var rulesetDefender = defender.RulesetCharacter;

            rulesetDefender.InflictCondition(
                conditionFancyFootworkMark.Name,
                DurationType.Round,
                0,
                TurnOccurenceType.EndOfSourceTurn,
                AttributeDefinitions.TagEffect,
                rulesetAttacker.guid,
                rulesetAttacker.CurrentFaction.Name,
                1,
                conditionFancyFootworkMark.Name,
                0,
                0,
                0);
        }
    }

    //
    // Panache
    //
    private sealed class FilterTargetingCharacterPanache : IFilterTargetingCharacter
    {
        public bool EnforceFullSelection => true;

        public bool IsValid(CursorLocationSelectTarget __instance, GameLocationCharacter target)
        {
            var rulesetTarget = target.RulesetCharacter;
            var actingCharacter = __instance.ActionParams.ActingCharacter;

            if (rulesetTarget.HasConditionOfType(ConditionDefinitions.ConditionDeafened.Name))
            {
                __instance.actionModifier.FailureFlags.Add("Failure/&FailureFlagTargetDeafened");
                return false;
            }

            if (!actingCharacter.IsWithinRange(target, 12))
            {
                __instance.actionModifier.FailureFlags.Add("Failure/&FailureFlagTargetTooFar");
                return false;
            }

            return true;
        }
    }

    private sealed class CustomBehaviorPanache : IPowerOrSpellFinishedByMe
    {
        private readonly ConditionDefinition conditionPanache;
        private readonly ConditionDefinition conditionPanacheSource;

        public CustomBehaviorPanache(ConditionDefinition panache, ConditionDefinition source)
        {
            conditionPanache = panache;
            conditionPanacheSource = source;
        }

        public IEnumerator OnPowerOrSpellFinishedByMe(
            CharacterActionMagicEffect action,
            BaseDefinition baseDefinition)
        {
            if (action.Countered || action.ExecutionFailed)
            {
                yield break;
            }

            var attacker = action.ActingCharacter;
            var rulesetAttacker = attacker.RulesetCharacter;
            var defender = action.ActionParams.TargetCharacters[0];
            var rulesetDefender = defender.RulesetCharacter;

            var abilityCheckData = new AbilityCheckData
            {
                AbilityCheckRoll = 0,
                AbilityCheckRollOutcome = RollOutcome.Neutral,
                AbilityCheckSuccessDelta = 0,
                AbilityCheckActionModifier = new ActionModifier(),
                Action = action
            };

            var opponentAbilityCheckData = new AbilityCheckData
            {
                AbilityCheckRoll = 0,
                AbilityCheckRollOutcome = RollOutcome.Neutral,
                AbilityCheckSuccessDelta = 0,
                AbilityCheckActionModifier = new ActionModifier(),
                Action = action
            };

            yield return ResolveContest(
                attacker,
                defender,
                abilityCheckData,
                opponentAbilityCheckData);

            if (abilityCheckData.AbilityCheckRollOutcome is RollOutcome.Success or RollOutcome.CriticalSuccess)
            {
                rulesetDefender.InflictCondition(
                    conditionPanache.Name,
                    DurationType.Minute,
                    1,
                    TurnOccurenceType.EndOfTurn,
                    AttributeDefinitions.TagEffect,
                    rulesetAttacker.guid,
                    rulesetAttacker.CurrentFaction.Name,
                    1,
                    conditionPanache.Name,
                    0,
                    0,
                    0);

                rulesetAttacker.InflictCondition(
                    conditionPanacheSource.Name,
                    DurationType.Minute,
                    1,
                    TurnOccurenceType.EndOfTurn,
                    AttributeDefinitions.TagEffect,
                    rulesetAttacker.guid,
                    rulesetAttacker.CurrentFaction.Name,
                    1,
                    conditionPanacheSource.Name,
                    0,
                    0,
                    0);

                rulesetAttacker.LogCharacterActivatesAbility(
                    $"Feature/&{Name}PanacheTitle",
                    $"Feedback/&{Name}PanacheContestSucceeded");
            }
        }

        private static IEnumerator ResolveContest(
            GameLocationCharacter actor,
            GameLocationCharacter opponent,
            AbilityCheckData abilityCheckData,
            AbilityCheckData opponentAbilityCheckData)
        {
            var rulesetActor = actor.RulesetCharacter;
            var rulesetOpponent = opponent.RulesetCharacter;

            var actorBonus = rulesetActor.ComputeBaseAbilityCheckBonus(
                AttributeDefinitions.Charisma,
                abilityCheckData.AbilityCheckActionModifier.AbilityCheckModifierTrends,
                SkillDefinitions.Persuasion);

            var opponentBonus = rulesetOpponent.ComputeBaseAbilityCheckBonus(
                AttributeDefinitions.Wisdom,
                opponentAbilityCheckData.AbilityCheckActionModifier.AbilityCheckModifierTrends,
                SkillDefinitions.Insight);

            var actorTotal = rulesetActor.RollAbilityCheck(
                actorBonus,
                AttributeDefinitions.Charisma,
                SkillDefinitions.Persuasion,
                abilityCheckData.AbilityCheckActionModifier.AbilityCheckModifierTrends,
                abilityCheckData.AbilityCheckActionModifier.AbilityCheckAdvantageTrends,
                0,
                0,
                false,
                -1,
                out var actorRaw,
                out var actorFirst,
                out var actorSecond,
                out var actorOutcome,
                out var actorDelta,
                true,
                true,
                true);

            var opponentTotal = rulesetOpponent.RollAbilityCheck(
                opponentBonus,
                AttributeDefinitions.Wisdom,
                SkillDefinitions.Insight,
                opponentAbilityCheckData.AbilityCheckActionModifier.AbilityCheckModifierTrends,
                opponentAbilityCheckData.AbilityCheckActionModifier.AbilityCheckAdvantageTrends,
                0,
                0,
                false,
                -1,
                out var opponentRaw,
                out var opponentFirst,
                out var opponentSecond,
                out var opponentOutcome,
                out var opponentDelta,
                true,
                true,
                true);

            if (actorTotal > opponentTotal)
            {
                abilityCheckData.AbilityCheckRollOutcome = RollOutcome.Success;
                abilityCheckData.AbilityCheckSuccessDelta = actorTotal - opponentTotal;
            }
            else
            {
                abilityCheckData.AbilityCheckRollOutcome = RollOutcome.Failure;
                abilityCheckData.AbilityCheckSuccessDelta = opponentTotal - actorTotal;
            }

            abilityCheckData.AbilityCheckRoll = actorRaw;
            opponentAbilityCheckData.AbilityCheckRoll = opponentRaw;

            yield break;
        }    
    }

    private sealed class ModifyAttackActionModifierPanache : IModifyAttackActionModifier
    {
        private readonly ConditionDefinition conditionPanache;

        public ModifyAttackActionModifierPanache(ConditionDefinition condition)
        {
            conditionPanache = condition;
        }

        public void OnAttackComputeModifier(
            RulesetCharacter myself,
            RulesetCharacter defender,
            BattleDefinitions.AttackProximity attackProximity,
            RulesetAttackMode attackMode,
            string effectName,
            ref ActionModifier attackModifier)
        {
            if (!myself.TryGetConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect,
                    conditionPanache.Name,
                    out var activeCondition))
            {
                return;
            }

            if (activeCondition.SourceGuid == defender.Guid)
            {
                return;
            }

            attackModifier.AttackAdvantageTrends.Add(
                new TrendInfo(-1, FeatureSourceType.Condition, conditionPanache.Name, conditionPanache));
        }
    }

    private sealed class PanacheConditionBehavior : 
        IPhysicalAttackFinishedOnMe, 
        IMagicEffectFinishedOnMe,
        IActionFinishedByMe
    {
        private readonly ConditionDefinition conditionPanache;

        public PanacheConditionBehavior(ConditionDefinition condition)
        {
            conditionPanache = condition;
        }

        public IEnumerator OnPhysicalAttackFinishedOnMe(
            GameLocationBattleManager battleManager,
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            RulesetAttackMode attackMode,
            RollOutcome rollOutcome,
            int damageAmount)
        {
            RemoveConditionIfAllyActed(attacker, defender);
            yield break;
        }

        public IEnumerator OnMagicEffectFinishedOnMe(
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            List<GameLocationCharacter> targets)
        {
            if (action is CharacterActionMagicEffect magicEffect &&
                !magicEffect.Countered &&
                !magicEffect.ExecutionFailed)
            {
                RemoveConditionIfAllyActed(attacker, defender);
            }

            yield break;
        }

        public IEnumerator OnActionFinishedByMe(CharacterAction action)
        {
            if (action.ActionType is not (ActionDefinitions.ActionType.Move or 
                                           ActionDefinitions.ActionType.Bonus or 
                                           ActionDefinitions.ActionType.Reaction or 
                                           ActionDefinitions.ActionType.NoCost))
            {
                yield break;
            }

            var actingCharacter = action.ActingCharacter;
            var rulesetCharacter = actingCharacter.RulesetCharacter;

            if (!rulesetCharacter.TryGetConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect,
                    conditionPanache.Name,
                    out var activeCondition))
            {
                yield break;
            }

            var rulesetSource = EffectHelpers.GetCharacterByGuid(activeCondition.SourceGuid);

            if (rulesetSource == null)
            {
                rulesetCharacter.RemoveCondition(activeCondition);
                yield break;
            }

            var source = GameLocationCharacter.GetFromActor(rulesetSource);

            if (source == null)
            {
                rulesetCharacter.RemoveCondition(activeCondition);
                yield break;
            }

            var stillInRange = Gui.Battle
                .GetContenders(actingCharacter, withinRange: 12)
                .Any(x => x == source);

            if (!stillInRange)
            {
                rulesetCharacter.RemoveCondition(activeCondition);
                
                rulesetCharacter.LogCharacterActivatesAbility(
                    $"Feature/&{Name}PanacheTitle",
                    $"Feedback/&{Name}PanacheTooFarApart");
            }
        }

        private void RemoveConditionIfAllyActed(
            GameLocationCharacter attacker,
            GameLocationCharacter defender)
        {
            var rulesetDefender = defender.RulesetCharacter;

            if (!rulesetDefender.TryGetConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect,
                    conditionPanache.Name,
                    out var activeCondition))
            {
                return;
            }

            if (activeCondition.SourceGuid != attacker.Guid)
            {
                rulesetDefender.RemoveCondition(activeCondition);
                
                rulesetDefender.LogCharacterActivatesAbility(
                    $"Feature/&{Name}PanacheTitle",
                    $"Feedback/&{Name}PanacheBrokenByAlly");
            }
        }
    }

    private sealed class PanacheSourceConditionBehavior : IActionFinishedByMe
    {
        private readonly ConditionDefinition conditionPanache;
        private readonly ConditionDefinition conditionPanacheSource;

        public PanacheSourceConditionBehavior(
            ConditionDefinition targetCondition,
            ConditionDefinition sourceCondition)
        {
            conditionPanache = targetCondition;
            conditionPanacheSource = sourceCondition;
        }

        public IEnumerator OnActionFinishedByMe(CharacterAction action)
        {
            if (action.ActionType is not (ActionDefinitions.ActionType.Move or 
                                           ActionDefinitions.ActionType.Bonus or 
                                           ActionDefinitions.ActionType.Reaction or 
                                           ActionDefinitions.ActionType.NoCost))
            {
                yield break;
            }

            var actingCharacter = action.ActingCharacter;
            var rulesetCharacter = actingCharacter.RulesetCharacter;

            if (!rulesetCharacter.TryGetConditionOfCategoryAndType(
                    AttributeDefinitions.TagEffect,
                    conditionPanacheSource.Name,
                    out var activeCondition))
            {
                yield break;
            }

            var stillInRange = Gui.Battle
                .GetContenders(actingCharacter, withinRange: 12)
                .Any(x => 
                    x.RulesetCharacter.TryGetConditionOfCategoryAndType(
                        AttributeDefinitions.TagEffect,
                        conditionPanache.Name,
                        out var targetCondition) &&
                    targetCondition.SourceGuid == rulesetCharacter.Guid);

            if (!stillInRange)
            {
                rulesetCharacter.RemoveCondition(activeCondition);
                
                foreach (var contender in Gui.Battle.AllContenders)
                {
                    if (contender.RulesetCharacter.TryGetConditionOfCategoryAndType(
                            AttributeDefinitions.TagEffect,
                            conditionPanache.Name,
                            out var targetCondition) &&
                        targetCondition.SourceGuid == rulesetCharacter.Guid)
                    {
                        contender.RulesetCharacter.RemoveCondition(targetCondition);
                        
                        rulesetCharacter.LogCharacterActivatesAbility(
                            $"Feature/&{Name}PanacheTitle",
                            $"Feedback/&{Name}PanacheTooFarApart");
                        break;
                    }
                }
            }
        }
    }

    //
    // Master Duelist
    //
    private sealed class CustomBehaviorMasterDuelist : ITryAlterOutcomeAttack
    {
        private readonly FeatureDefinitionPower powerMasterDuelist;

        public CustomBehaviorMasterDuelist(FeatureDefinitionPower power)
        {
            powerMasterDuelist = power;
        }

        public int HandlerPriority => -10;

        public IEnumerator OnTryAlterOutcomeAttack(
            GameLocationBattleManager battleManager,
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            GameLocationCharacter helper,
            ActionModifier actionModifier,
            RulesetAttackMode attackMode,
            RulesetEffect rulesetEffect)
        {
            if (helper != attacker)
                yield break;

            if (action.AttackRollOutcome is not (RollOutcome.Failure or RollOutcome.CriticalFailure))
                yield break;

            var rulesetHelper = helper.RulesetCharacter;
            var usablePower = PowerProvider.Get(powerMasterDuelist, rulesetHelper);
            
            if (rulesetHelper.GetRemainingUsesOfPower(usablePower) == 0)
                yield break;

            yield return helper.MyReactToDoNothing(
                ExtraActionId.DoNothingReaction,
                attacker,
                "MasterDuelist",
                "UseRoguishSwashbucklerMasterDuelistDescription".Localized(Category.Reaction),
                ReactionValidated,
                battleManager: battleManager);

            yield break;

            void ReactionValidated()
            {
                rulesetHelper.UsePower(usablePower);
                
                if (attackMode != null && rulesetEffect == null)
                {
                    // Add advantage to existing trends
                    actionModifier.AttackAdvantageTrends.Add(
                        new TrendInfo(1, FeatureSourceType.Power, powerMasterDuelist.Name, powerMasterDuelist));
                    
                    // Weapon attack - full reroll with advantage
                    var roll = rulesetHelper.RollAttack(
                        attackMode.toHitBonus,
                        defender.RulesetActor,
                        attackMode.sourceDefinition,
                        actionModifier.AttacktoHitTrends,
                        false,
                        actionModifier.AttackAdvantageTrends,
                        attackMode.Ranged,
                        false,
                        actionModifier.AttackRollModifier,
                        out var outcome,
                        out var successDelta,
                        -1,
                        true);

                    action.AttackRollOutcome = outcome;
                    action.AttackSuccessDelta = successDelta;
                    action.AttackRoll = roll;
                }
                else
                {
                    // Spell attack - advantage reroll
                    rulesetHelper.RollDie(
                        DieType.D20,
                        RollContext.AttackRoll,
                        false,
                        AdvantageType.Advantage,
                        out var firstRoll,
                        out var secondRoll);

                    var finalRoll = System.Math.Max(firstRoll, secondRoll);

                    action.AttackSuccessDelta += finalRoll - action.AttackRoll;
                    action.AttackRoll = finalRoll;

                    if (action.AttackSuccessDelta >= 0)
                        action.AttackRollOutcome = finalRoll == 20 ? RollOutcome.CriticalSuccess : RollOutcome.Success;
                }
            }
        }
    }
}
