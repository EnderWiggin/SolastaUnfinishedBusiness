using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Behaviors;
using SolastaUnfinishedBusiness.Models;
using SolastaUnfinishedBusiness.Spells;
using SolastaUnfinishedBusiness.Validators;
using TA;
using static RuleDefinitions;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class RulesetImplementationManagerLocationPatcher
{
    [HarmonyPatch(typeof(RulesetImplementationManagerLocation),
        nameof(RulesetImplementationManagerLocation.InstantiateEffectInvocation))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class InstantiateEffectInvocation_Patch
    {
        [UsedImplicitly]
        public static void Postfix(
            RulesetEffectSpell __result,
            RulesetInvocation invocation)
        {
            //PATCH: setup repertoire for spells cast through invocation 
            __result.spellRepertoire ??= invocation.invocationRepertoire;
        }
    }

    [HarmonyPatch(typeof(RulesetImplementationManagerLocation),
        nameof(RulesetImplementationManagerLocation.IsMetamagicOptionAvailable))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class IsMetamagicOptionAvailable_Patch
    {
        private static int RemainingSorceryPoints(RulesetCharacter caster, RulesetEffectSpell rulesetEffectSpell)
        {
            return Tabletop2024Context.IsArcaneApotheosisValid(caster, rulesetEffectSpell)
                ? 9999
                : caster.RemainingSorceryPoints;
        }

        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> instructions)
        {
            var remainingSorceryPointsMethod = typeof(RulesetCharacter).GetMethod("get_RemainingSorceryPoints");
            var myRemainingSorceryPointsMethod =
                new Func<RulesetCharacter, RulesetEffectSpell, int>(RemainingSorceryPoints).Method;

            return instructions.ReplaceCalls(remainingSorceryPointsMethod,
                "CharacterActionCastSpell.RemoveConcentrationAsNeeded",
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, myRemainingSorceryPointsMethod));
        }

        [UsedImplicitly]
        public static void Postfix(
            ref bool __result,
            RulesetEffectSpell rulesetEffectSpell,
            RulesetCharacter caster,
            MetamagicOptionDefinition metamagicOption,
            ref string failure)
        {
            if (!__result)
            {
                return;
            }

            //PATCH: support for custom metamagic
            foreach (var validator in metamagicOption.GetAllSubFeaturesOfType<ValidateMetamagicApplication>())
            {
                validator.Invoke(caster, rulesetEffectSpell, metamagicOption, ref __result, ref failure);
            }
        }
    }

    [HarmonyPatch(typeof(RulesetImplementationManagerLocation),
        nameof(RulesetImplementationManagerLocation.IsSituationalContextValid))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class IsSituationalContextValid_Patch
    {
        [UsedImplicitly]
        public static void Postfix(
            ref bool __result,
            RulesetImplementationDefinitions.SituationalContextParams contextParams)
        {
            //PATCH: supports custom situational context
            __result = CustomSituationalContext.IsContextValid(contextParams, __result);
        }
    }

    [HarmonyPatch(typeof(RulesetImplementationManagerLocation),
        nameof(RulesetImplementationManagerLocation.InstantiateActiveDeviceFunction))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class InstantiateActiveDeviceFunction_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(
            RulesetImplementationManagerLocation __instance,
            ref RulesetEffect __result,
            RulesetCharacter user,
            RulesetItemDevice usableDevice,
            RulesetDeviceFunction usableDeviceFunction,
            int addedCharges,
            bool delayRegistration)
        {
            //PATCH: support `RulesetEffectPowerWithAdvancement` by creating custom instance when needed
            return RulesetEffectPowerWithAdvancement.InstantiateActiveDeviceFunction(__instance, ref __result, user,
                usableDevice, usableDeviceFunction, addedCharges, delayRegistration);
        }
    }


    [HarmonyPatch(typeof(RulesetImplementationManagerLocation),
        nameof(RulesetImplementationManagerLocation.ApplyMotionForm))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ApplyMotionForm_Patch
    {
        private static void TeleportCharacter(
            IGameLocationPositioningService __instance,
            GameLocationCharacter character,
            int3 newPosition,
            LocationDefinitions.Orientation orientation)
        {
            if (Main.Settings.EnableTeleportToRemoveRestrained)
            {
                var rulesetCharacter = character.RulesetCharacter;
                var conditionsToRemove = rulesetCharacter.ConditionsByCategory
                    .SelectMany(x => x.Value)
                    .Where(x =>
                        x.ConditionDefinition.IsSubtypeOf(ConditionRestrained) &&
                        (character.Side == Side.Ally ||
                         x.ConditionDefinition.Name != SpellBuilders.ConditionTelekinesisRestrainedName))
                    .ToArray();

                foreach (var activeCondition in conditionsToRemove)
                {
                    rulesetCharacter.RemoveCondition(activeCondition);
                }
            }

            __instance.TeleportCharacter(character, newPosition, orientation);
        }

        [NotNull]
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> instructions)
        {
            var teleportCharacterMethod = typeof(IGameLocationPositioningService).GetMethod("TeleportCharacter");
            var myTeleportCharacterMethod =
                new Action<IGameLocationPositioningService, GameLocationCharacter, int3,
                    LocationDefinitions.Orientation>(TeleportCharacter).Method;

            return instructions.ReplaceCalls(teleportCharacterMethod,
                "CharacterStageClassSelectionPanel.Refresh",
                new CodeInstruction(OpCodes.Call, myTeleportCharacterMethod)); // checked for Call vs CallVirtual
        }

        [UsedImplicitly]
        public static bool Prefix(EffectForm effectForm, RulesetImplementationDefinitions.ApplyFormsParams formsParams)
        {
            //PATCH: support for `PushesFromEffectPoint`
            // allows push/grab motion effects to work relative to casting point, instead of caster's position
            // used for Grenadier's force grenades
            // if effect source definition has marker, and forms params have position, will try to push target from that point

            var useDefaultLogic = ForcePushOrDragFromEffectPoint.TryPushFromEffectTargetPoint(effectForm, formsParams);

            if (useDefaultLogic)
            {
                useDefaultLogic = CustomSwap(effectForm, formsParams);
            }

            return useDefaultLogic;
        }

        [UsedImplicitly]
        public static void Postfix(RulesetImplementationDefinitions.ApplyFormsParams formsParams)
        {
            GrappleContext.ValidateGrappleAfterMotion(GameLocationCharacter.GetFromActor(formsParams.sourceCharacter));
            GrappleContext.ValidateGrappleAfterMotion(GameLocationCharacter.GetFromActor(formsParams.targetCharacter));
        }

        private static bool CustomSwap(
            EffectForm effectForm,
            RulesetImplementationDefinitions.ApplyFormsParams formsParams)
        {
            var motionForm = effectForm.MotionForm;

            if (motionForm.Type != (MotionForm.MotionType)ExtraMotionType.CustomSwap)
            {
                return true;
            }

            var actionService = ServiceRepository.GetService<IGameLocationActionService>();
            var attacker = GameLocationCharacter.GetFromActor(formsParams.sourceCharacter);
            var defender = GameLocationCharacter.GetFromActor(formsParams.targetCharacter);

            if (attacker == null || defender == null)
            {
                return true;
            }

            const ActionDefinitions.Id ACTION_ID = (ActionDefinitions.Id)ExtraActionId.PushedCustom;

            actionService.ExecuteAction(
                new CharacterActionParams(attacker, ACTION_ID, defender.LocationPosition)
                {
                    BoolParameter = false, BoolParameter4 = false, CanBeCancelled = false, CanBeAborted = false
                }, null, true);
            actionService.ExecuteAction(
                new CharacterActionParams(defender, ActionDefinitions.Id.Pushed, attacker.LocationPosition)
                {
                    BoolParameter = false, BoolParameter4 = false, CanBeCancelled = false, CanBeAborted = false
                }, null, false);

            return false;
        }
    }

    [HarmonyPatch(typeof(RulesetImplementationManagerLocation),
        nameof(RulesetImplementationManagerLocation.IsAnyMetamagicOptionAvailable))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class IsAnyMetamagicOptionAvailable_Patch
    {
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> instructions)
        {
            //PATCH: support for `ReplaceMetamagicOption`
            return ReplaceMetamagicOption.PatchMetamagicGetter(instructions,
                "RulesetImplementationManagerLocation.IsAnyMetamagicOptionAvailable");
        }
    }

    //PATCH: supports light and obscurement rules
    [HarmonyPatch(typeof(RulesetImplementationManagerLocation),
        nameof(RulesetImplementationManagerLocation.ApplyCounterForm))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ApplyCounterForm_Patch
    {
        [NotNull]
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> instructions)
        {
            var conditionDefinitionMethod = typeof(ConditionForm).GetMethod("get_ConditionDefinition");
            var myConditionDefinitionMethod =
                new Func<ConditionForm, ConditionDefinition>(LightingAndObscurementContext.CheckForDarknessCondition).Method;

            return instructions.ReplaceCalls(conditionDefinitionMethod,
                "RulesetImplementationManagerLocation.ApplyCounterForm",
                new CodeInstruction(OpCodes.Call, myConditionDefinitionMethod));
        }
    }

    [HarmonyPatch(typeof(RulesetImplementationManagerLocation),
        nameof(RulesetImplementationManagerLocation.ApplyShapeChangeForm))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ApplyShapeChangeForm_Patch
    {
        private static readonly List<RulesetEffectPower> PowersUsedByMe = [];
        private static readonly List<RulesetEffectSpell> SpellsCastByMe = [];

        [UsedImplicitly]
        public static bool Prefix(RulesetImplementationManagerLocation __instance, EffectForm effectForm,
            RulesetImplementationDefinitions.ApplyFormsParams formsParams)
        {
            ApplyShapeChangeForm(__instance, effectForm, formsParams);
            return false;
        }

        private static void ApplyShapeChangeForm(RulesetImplementationManagerLocation manager,
            EffectForm effectForm,
            RulesetImplementationDefinitions.ApplyFormsParams formsParams)
        {
            //Mostly original code, except for PATCHES
            
            var targetCharacter = (RulesetCharacter)formsParams.targetCharacter;
            var sourceCharacter = formsParams.sourceCharacter;

            //PATCH: allow Druids to keep concentration on spells / powers with proxy summon forms

            // foreach (var rulesetEffect in targetCharacter.SpellsCastByMe)
            // {
            //     if (rulesetEffect.TrackedLightSourceGuids.Count > 0)
            //     {
            //         rulesetEffect.Terminate(false);
            //     }
            //
            //     if (rulesetEffect.TrackedSummonedItemGuids.Count > 0)
            //     {
            //         rulesetEffect.Terminate(false);
            //     }
            // }
            //
            // foreach (var rulesetEffect in targetCharacter.PowersUsedByMe)
            // {
            //     if (rulesetEffect.TrackedLightSourceGuids.Count > 0)
            //     {
            //         rulesetEffect.Terminate(false);
            //     }
            //
            //     if (rulesetEffect.TrackedSummonedItemGuids.Count > 0)
            //     {
            //         rulesetEffect.Terminate(false);
            //     }
            // }

            //END PATCH

            var service = ServiceRepository.GetService<IGameLocationCharacterService>();
            var entityImplementation = (GameLocationCharacter)targetCharacter.EntityImplementation;
            var targetSubstitute = formsParams.targetSubstitute;
            var creatureSex = targetCharacter.Sex == CreatureSex.Female
                ? GadgetDefinitions.CreatureSex.Female
                : GadgetDefinitions.CreatureSex.Male;
            var spawnOverrides = new SpawnOverrides();
            var rulesetMonster = new RulesetCharacterMonster(targetSubstitute, 0, spawnOverrides,
                creatureSex, targetCharacter, effectForm.ShapeChangeForm.KeepMentalAbilityScores);
            var sourceFaction = sourceCharacter.CurrentFaction.Name ?? string.Empty;
            var sourceAbilityBonus = formsParams.activeEffect.ComputeSourceAbilityBonus(sourceCharacter);
            var proficiencyBonus = formsParams.activeEffect.ComputeSourceProficiencyBonus(sourceCharacter);

            targetCharacter.InflictCondition(ConditionShapeChanged, DurationType.Permanent, 0,
                TurnOccurenceType.EndOfTurn, AttributeDefinitions.TagConjure, sourceCharacter.Guid,
                sourceFaction, formsParams.effectLevel, string.Empty, 0, sourceAbilityBonus, proficiencyBonus);
            var condition = rulesetMonster.InflictCondition(
                effectForm.ShapeChangeForm.SpecialSubstituteDefinition?.Name ?? ConditionSubstituteForm,
                DurationType.Round, formsParams.activeEffect.RemainingRounds, formsParams.endOfEffect,
                AttributeDefinitions.TagConjure, sourceCharacter.Guid, sourceFaction, formsParams.effectLevel,
                string.Empty, 0, sourceAbilityBonus, proficiencyBonus);
            formsParams.activeEffect.TrackCondition(sourceCharacter, sourceCharacter.Guid, rulesetMonster,
                rulesetMonster.Guid, condition, AttributeDefinitions.TagConjure);
            var character = service.CreateCharacter(entityImplementation.ControllerId, rulesetMonster,
                entityImplementation.Side, entityImplementation.BehaviourPackage);
            ServiceRepository.GetService<IGameLocationPositioningService>().PlaceCharacter(character,
                entityImplementation.LocationPosition, entityImplementation.Orientation);
            character.SetupFromShapeChangedCharacter(entityImplementation);
            character.RefreshActionPerformances();
            service.RevealCharacter(character);
            service.ReplaceCharacter(entityImplementation, character);

            //PATCH: enforces concentration on shape change spell
            if (formsParams.activeEffect is RulesetEffectSpell rulesetEffectSpell &&
                rulesetEffectSpell.SpellDefinition.Name == SpellBuilders.ShapechangeName)
            {
                rulesetMonster.concentratedSpell = rulesetEffectSpell;
            }

            //PATCH: allows shape changers to get bonuses effects defined in features / feats / etc.
            sourceAbilityBonus = formsParams.activeEffect.ComputeSourceAbilityBonus(sourceCharacter);
            proficiencyBonus = formsParams.activeEffect.ComputeSourceProficiencyBonus(sourceCharacter);
            var creatureTags = formsParams.targetSubstitute.CreatureTags;

            foreach (var summoningAffinity in sourceCharacter
                         .FeaturesByType<FeatureDefinitionSummoningAffinity>()
                         .Where(x => creatureTags.Contains(x.RequiredMonsterTag)))
            {
                foreach (var addedCondition in summoningAffinity.AddedConditions)
                {
                    var sourceAmount = 0;

                    // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                    switch (addedCondition.AmountOrigin)
                    {
                        case ConditionDefinition.OriginOfAmount.SourceHalfHitPoints:
                            sourceAmount = addedCondition.BaseAmount +
                                           (sourceCharacter.TryGetAttributeValue(AttributeDefinitions.HitPoints) / 2);
                            break;
                        case ConditionDefinition.OriginOfAmount.SourceSpellCastingAbility:
                            sourceAmount = sourceCharacter.SpellRepertoires
                                .Select(spellRepertoire => AttributeDefinitions.ComputeAbilityScoreModifier(
                                    sourceCharacter.TryGetAttributeValue(spellRepertoire.SpellCastingAbility)))
                                .Prepend(0)
                                .Max();
                            break;
                        case ConditionDefinition.OriginOfAmount.SourceSpellAttack:
                            sourceAmount = sourceCharacter.SpellRepertoires
                                .Select(spellRepertoire => spellRepertoire.SpellAttackBonus)
                                .Prepend(0)
                                .Max();
                            break;
                    }

                    rulesetMonster.InflictCondition(
                        addedCondition.Name,
                        formsParams.durationType,
                        formsParams.durationParameter,
                        formsParams.endOfEffect,
                        AttributeDefinitions.TagEffect,
                        sourceCharacter.Guid,
                        sourceCharacter.CurrentFaction.Name,
                        formsParams.effectLevel,
                        string.Empty, sourceAmount,
                        sourceAbilityBonus,
                        proficiencyBonus);

                    // we need to re-assign max hit points as we're on a postfix
                    rulesetMonster.currentHitPoints =
                        rulesetMonster.GetAttribute(AttributeDefinitions.HitPoints).MaxValue;

                    rulesetMonster.RefreshAll();
                }
            }
        }
    }
}
