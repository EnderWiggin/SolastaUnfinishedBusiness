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
using SolastaUnfinishedBusiness.Behaviors;
using SolastaUnfinishedBusiness.Behaviors.Specific;
using SolastaUnfinishedBusiness.CustomUI;
using SolastaUnfinishedBusiness.Models;
using SolastaUnfinishedBusiness.Subclasses;
using UnityEngine;
using UnityEngine.UI;
using static ActionDefinitions;
using static RuleDefinitions;
using Object = UnityEngine.Object;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class CharacterActionPanelPatcher
{
    private static bool HasShapeChangeForm(RulesetEffectSpell rulesetEffectSpell)
    {
        return rulesetEffectSpell.SpellDefinition.EffectDescription.EffectForms.Any(x =>
            x.FormType == EffectForm.EffectFormType.ShapeChange);
    }

    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.ReadyActionEngaged))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ReadyActionEngaged_Patch
    {
        [UsedImplicitly]
        public static void Prefix(CharacterActionPanel __instance, ReadyActionType readyActionType)
        {
            //PATCH: used for `force preferred cantrip` option
            CustomReactionsContext.SaveReadyActionPreferredCantrip(__instance.actionParams, readyActionType);
        }
    }

    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.BindCharacterActionItem))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class BindCharacterActionItem_Patch
    {
        [UsedImplicitly]
        public static void Prefix(CharacterActionPanel __instance, GuiCharacterAction guiCharacterAction)
        {
            //PATCH: allow cast Quickened and Bonus spell to be small if both present
            CustomActionIdContext.UpdateCastActionForm(guiCharacterAction, __instance.filteredActions);
        }

        [UsedImplicitly]
        public static void Postfix(CharacterActionPanel __instance, GuiCharacterAction guiCharacterAction,
            int itemIndex)
        {
            //PATCH: Customize name on the Attack button
            if (__instance.actionItems.Count <= itemIndex) { return; }

            if (guiCharacterAction.actionId is not Id.AttackOff) { return; }

            var component = __instance.actionItems[itemIndex];

            component.currentItemForm.captionLabel.Text = GetName(guiCharacterAction.ForcedAttackMode?.AttackTags)
                                                          ?? component.currentItemForm.captionLabel.Text;
        }

        private static string GetName([CanBeNull] List<string> tags)
        {
            if (tags is null || tags.Empty()) { return null; }

            foreach (var tag in tags)
            {
                switch (tag)
                {
                    case TagsDefinitions.FlurryOfBlows:
                        return "Action/&FlurryOfBlowsTitle";
                    case WayOfBlade.OneWithTheBlade:
                        return "Feature/&AttributeModifierWayOfBladeOneWithTheBladeTitle";
                }
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.ComputeMultipleGuiCharacterActions))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ComputeMultipleGuiCharacterActions_Patch
    {
        [UsedImplicitly]
        public static void Postfix(CharacterActionPanel __instance, ref int __result, Id actionId)
        {
            //PATCH: Support for ExtraAttacksOnActionPanel
            //Allows multiple actions on panel for off-hand attacks and main attacks for non-guests
            __result = ExtraAttacksOnActionPanel.ComputeMultipleGuiCharacterActions(__instance, actionId, __result);

            if(__instance.guiActionById.TryGetValue((Id)ExtraActionId.NickMasteryAttack, out var guiAction))
            {
                guiAction.ForcedAttackMode = __instance.GuiCharacter.RulesetCharacter.FindNickAttackMode();
            }
        }
    }

    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.RefreshActions))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class RefreshActions_Patch
    {
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> instructions)
        {
            //PATCH: hide power button on action panel if no valid powers to use or see
            var method = new Func<
                List<Id>,
                CharacterActionPanel,
                int
            >(FilterActions).Method;

            //TODO: it replaces first call to get_Count, find a better way to find proper place
            return instructions.ReplaceCode(
                code => code.opcode == OpCodes.Callvirt && $"{code.operand}".Contains("get_Count"), 1,
                "CharacterActionPanel.RefreshActions",
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, method));
        }

        private static int FilterActions(List<Id> actions, CharacterActionPanel panel)
        {
            var character = panel.GuiCharacter.RulesetCharacter;
            var inBattle = Gui.Battle != null;

            //PATCH: reorder the actions panel in case we have custom toggles
            CustomActionIdContext.ReorderToggles(actions);

            //PATCH: hide power button on action panel if no valid powers to use or see
            actions.RemoveAll(id => ActionIsInvalid(id, character, inBattle));

            return actions.Count;
        }

        private static bool ActionIsInvalid(Id id, RulesetCharacter character, bool battle)
        {
            return id switch
            {
                Id.PowerMain => !character.CanSeeAndUseAtLeastOnePower(ActionType.Main, battle),
                Id.PowerBonus => !character.CanSeeAndUseAtLeastOnePower(ActionType.Bonus, battle),
                Id.PowerNoCost => !character.CanSeeAndUseAtLeastOnePower(ActionType.NoCost, battle),
                _ => false
            };
        }
    }

    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.ActionStarted))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ActionStarted_Patch
    {
        [UsedImplicitly]
        public static void Postfix(CharacterActionPanel __instance, CharacterAction characterAction)
        {
            if (characterAction.ActingCharacter != __instance.GuiCharacter.GameLocationCharacter) { return; }

            if (__instance.cursorCaptionScreen != null && __instance.cursorCaptionScreen.Visible)
            {
                //PATCH: fixes action callback triggering on wrong character when Disengage used by movement, not Confirm button
                __instance.SetDisengageModeInCursor(Id.NoAction);
                __instance.actionId = Id.NoAction;
            }
        }
    }

    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.OnActivateAction))]
    [HarmonyPatch([typeof(Id), typeof(GuiCharacterAction)])]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class OnActivateAction_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(CharacterActionPanel __instance,
            Id actionId, GuiCharacterAction guiCharacterAction)
        {
            OnActivateAction(__instance, actionId, guiCharacterAction);
            return false;
        }

        private static void OnActivateAction(CharacterActionPanel panel, Id actionId,
            GuiCharacterAction guiCharacterAction)
        {
            panel.ClearActionParams();
            panel.actionId = actionId;
            var actingCharacter = panel.GuiCharacter.GameLocationCharacter;
            panel.actionParams = new CharacterActionParams(actingCharacter, panel.actionId);
            var actionDefinition = panel.actionParams.ActionDefinition;
            var service1 = ServiceRepository.GetService<ICursorService>();
            var service2 = ServiceRepository.GetService<ICommandService>();
            if (panel.SpellSelectionPanel.Visible
                && (actionDefinition.Parameter != ActionParameter.SelectSpell
                    || panel.SpellSelectionPanel.ActionType != ActionType.None
                    && panel.SpellSelectionPanel.ActionType != actionDefinition.ActionType))
            {
                panel.SpellSelectionPanel.Hide(true);
            }

            if (panel.RitualSelectionPanel != null
                && panel.RitualSelectionPanel.Visible
                && actionDefinition.Parameter != ActionParameter.SelectRitual)
            {
                panel.RitualSelectionPanel.Hide(true);
            }

            if (panel.PowerSelectionPanel.Visible
                && (actionDefinition.Parameter != ActionParameter.SelectPower
                    || panel.SpellSelectionPanel.ActionType != ActionType.None
                    && panel.PowerSelectionPanel.ActionType != actionDefinition.ActionType))
            {
                panel.PowerSelectionPanel.Hide(true);
            }

            if (panel.DeviceSelectionPanel.Visible
                && (actionDefinition.Parameter != ActionParameter.SelectDeviceFunction
                    || panel.SpellSelectionPanel.ActionType != ActionType.None
                    && panel.DeviceSelectionPanel.ActionType != actionDefinition.ActionType))
            {
                panel.DeviceSelectionPanel.Hide(true);
            }

            if (panel.ReadyActionSelectionPanel != null
                && panel.ReadyActionSelectionPanel.Visible && actionDefinition.Parameter
                != ActionParameter.SelectActionToReady)
            {
                panel.ReadyActionSelectionPanel.Hide(true);
            }

            if (panel.ShoveModeSelectionPanel != null
                && panel.ShoveModeSelectionPanel.Visible
                && actionDefinition.Parameter != ActionParameter.Shove)
            {
                panel.ShoveModeSelectionPanel.Hide(true);
            }

            if (panel.BreakFreeModeSelectionPanel != null
                && panel.BreakFreeModeSelectionPanel.Visible
                && actionDefinition.Parameter != ActionParameter.BreakFreeMode)
            {
                panel.BreakFreeModeSelectionPanel.Hide(true);
            }

            if (panel.DashModeSelectionPanel != null
                && panel.DashModeSelectionPanel.Visible
                && actionDefinition.Parameter != ActionParameter.DashConfirmation)
            {
                panel.DisableDashMode();
            }

            if (panel.MetamagicSelectionPanel != null
                && panel.MetamagicSelectionPanel.Visible)
            {
                panel.MetamagicSelectionPanel.Hide(true);
            }

            if (panel.ShapeSelectionPanel != null
                && panel.ShapeSelectionPanel.Visible && actionDefinition.Id != Id.WildShape)
            {
                panel.ShapeSelectionPanel.Hide(true);
            }

            if (panel.FlurryOfBlowsModeSelectionPanel != null
                && panel.FlurryOfBlowsModeSelectionPanel.Visible && actionDefinition.Parameter
                != ActionParameter.OpenHandTechnique)
            {
                panel.FlurryOfBlowsModeSelectionPanel.Hide(true);
            }

            if (panel.InvocationSelectionPanel != null
                && panel.InvocationSelectionPanel.Visible
                && actionDefinition.Parameter != ActionParameter.SelectInvocation)
            {
                panel.InvocationSelectionPanel.Hide(true);
            }

            if (panel.cursorCaptionScreen.Visible)
            {
                CursorLocation.CaptionLineDismissed();
                panel.SetDisengageModeInCursor(Id.NoAction);
            }

            switch (actionDefinition.Parameter)
            {
                case ActionParameter.None:
                    if (Gui.Battle != null && panel.DefaultCursorType == typeof(CursorLocationBattleFriendlyTurn))
                    {
                        var cursor = service1.GetCursor<CursorLocationBattleActionExecuting>();
                        service1.ActivateCursor<CursorLocationBattleActionExecuting>();
                        service2.ExecuteAction(panel.actionParams.Clone(), cursor.ActionExecuted, false);
                        break;
                    }

                    service2.ExecuteAction(panel.actionParams.Clone(), panel.ActionExecuted, false);
                    panel.RestoreDefaultCursor();
                    break;
                case ActionParameter.Destination:
                    panel.ToggleConstrainedMoveMode();
                    break;
                case ActionParameter.Target:
                    if (service1.CurrentCursor is CursorLocationSelectTarget)
                    {
                        panel.RestoreDefaultCursor();
                        panel.actionId = Id.NoAction;
                        panel.actionParams = null;
                        break;
                    }

                    if (panel.actionId is Id.AttackMain or Id.AttackOff)
                    {
                        //PATCH: Support for ExtraAttacksOnActionPanel - try using ForcedAttackMode for off-hand attacks too
                        panel.actionParams.AttackMode =  guiCharacterAction.ForcedAttackMode
                              ?? actingCharacter.FindActionAttackMode(panel.actionId);
                    }
                    //PATCH: support for Nick weapon mastery
                    else if (panel.actionId is (Id)ExtraActionId.NickMasteryAttack)
                    {
                        panel.actionParams.AttackMode = guiCharacterAction.ForcedAttackMode
                                                        ?? actingCharacter.FindNickAttackMode();
                    }
                    else if (panel.actionId is Id.AssignTargetMain or Id.AssignTargetBonus)
                    {
                        panel.actionParams.RulesetEffect =
                            panel.GuiCharacter.RulesetCharacter.FindFirstRetargetableEffect();
                    }

                    service1.ActivateCursor<CursorLocationSelectTarget>(panel.actionParams);
                    break;
                case ActionParameter.SelectSpell:
                    panel.SpellSelectionPanel.ActionType =
                        panel.ActionScope != ActionScope.Exploration
                            ? actionDefinition.ActionType
                            : ActionType.None;
                    panel.SelectSpell();
                    break;
                case ActionParameter.SelectPower:
                    panel.PowerSelectionPanel.ActionType =
                        panel.ActionScope != ActionScope.Exploration
                            ? actionDefinition.ActionType
                            : ActionType.None;
                    panel.SelectPower();
                    break;
                case ActionParameter.Shove:
                    panel.SelectShoveMode();
                    break;
                case ActionParameter.Levitate:
                    service1.ActivateCursor<CursorLocationLevitate>(panel.actionParams);
                    break;
                case ActionParameter.InstantSingleAction:
                    service2.ExecuteInstantSingleAction(panel.actionParams.Clone());
                    panel.RestoreDefaultCursor();
                    break;
                case ActionParameter.SelectDeviceFunction:
                    panel.SelectDeviceFunction();
                    break;
                case ActionParameter.SelectActionToReady:
                    panel.SelectActionToReady();
                    break;
                case ActionParameter.SelectRitual:
                    panel.SelectRitual();
                    break;
                case ActionParameter.ActivatePower:
                    if (!(panel.actionParams.ActionDefinition.ActivatedPower != null))
                    {
                        break;
                    }

                    var service3 =
                        ServiceRepository.GetService<IRulesetImplementationService>();
                    var usablePower =
                        panel.GuiCharacter.RulesetCharacter.GetPowerFromDefinition(panel.actionParams.ActionDefinition
                            .ActivatedPower);
                    if (usablePower == null)
                    {
                        usablePower = new RulesetUsablePower(panel.actionParams.ActionDefinition.ActivatedPower,
                            null, null);
                        if (panel.actionParams.ActingCharacter is { RulesetCharacter: not null })
                        {
                            var rulesetCharacter = panel.actionParams.ActingCharacter.RulesetCharacter;
                            var effectDescription =
                                panel.actionParams.ActionDefinition.ActivatedPower.EffectDescription;
                            if (effectDescription.DifficultyClassComputation
                                == EffectDifficultyClassComputation.SpellCastingFeature)
                            {
                                RulesetSpellRepertoire rulesetSpellRepertoire = null;
                                foreach (var spellRepertoire in rulesetCharacter.SpellRepertoires)
                                {
                                    if (spellRepertoire.SpellCastingClass != null)
                                    {
                                        rulesetSpellRepertoire = spellRepertoire;
                                        break;
                                    }

                                    if (spellRepertoire.SpellCastingSubclass != null)
                                    {
                                        rulesetSpellRepertoire = spellRepertoire;
                                        break;
                                    }
                                }

                                if (rulesetSpellRepertoire != null)
                                {
                                    usablePower.SaveDC = rulesetSpellRepertoire.SaveDC;
                                }
                            }
                            else if (effectDescription.DifficultyClassComputation == EffectDifficultyClassComputation.AbilityScoreAndProficiency)
                            {
                                usablePower.SaveDC = ComputeAbilityScoreBasedDC(
                                    rulesetCharacter.TryGetAttributeValue(
                                        effectDescription.SavingThrowDifficultyAbility),
                                    rulesetCharacter.TryGetAttributeValue("ProficiencyBonus"));
                            }
                            else if (effectDescription.DifficultyClassComputation
                                     == EffectDifficultyClassComputation.FixedValue)
                            {
                                usablePower.SaveDC = effectDescription.FixedSavingThrowDifficultyClass;
                            }
                            else if (effectDescription.DifficultyClassComputation
                                     == EffectDifficultyClassComputation.Ki)
                            {
                                usablePower.SaveDC = ComputeAbilityScoreBasedDC(
                                    rulesetCharacter.TryGetAttributeValue("Wisdom"),
                                    rulesetCharacter.TryGetAttributeValue("ProficiencyBonus"));
                            }
                            else if (effectDescription.DifficultyClassComputation
                                     == EffectDifficultyClassComputation.BreathWeapon)
                            {
                                usablePower.SaveDC = ComputeAbilityScoreBasedDC(
                                    rulesetCharacter.TryGetAttributeValue("Constitution"),
                                    rulesetCharacter.TryGetAttributeValue("ProficiencyBonus"));
                            }
                        }
                    }

                    panel.actionParams.RulesetEffect =
                        service3.InstantiateEffectPower(panel.GuiCharacter.RulesetCharacter, usablePower,
                            true);
                    panel.actionId = Id.PowerNoCost;
                    if (panel.actionParams.ActionDefinition.ActivatedPower.EffectDescription.HasShapeChangeForm())
                    {
                        panel.SelectShape(panel.actionParams.RulesetEffect);
                        break;
                    }

                    panel.ExecuteEffectOfAction();
                    break;
                case ActionParameter.DashConfirmation:
                    panel.ToggleDashMode();
                    break;
                case ActionParameter.DodgeConfirmation:
                    panel.ToggleDodgeMode();
                    break;
                case ActionParameter.DisengageConfirmation:
                    panel.ToggleDisengageMode();
                    break;
                case ActionParameter.BreakFreeMode:
                    panel.SelectBreakFreeMode();
                    break;
                case ActionParameter.AreaForTargets:
                    switch (actionDefinition.TargetType)
                    {
                        case TargetType.Line:
                        case TargetType.Cone:
                        case TargetType.Cube:
                        case TargetType.Sphere:
                        case TargetType.PerceivingWithinDistance:
                        case TargetType.Cylinder:
                        case TargetType.WallLine:
                        case TargetType.WallRing:
                        case TargetType.CubeWithOffset:
                        case TargetType.CylinderWithDiameter:
                            service1.ActivateCursor<CursorLocationGeometricShape>(panel.actionParams);
                            return;
                        default:
                            panel.HandleInput(InputCommands.Id.Cancel);
                            return;
                    }
                case ActionParameter.InstantSingleActionSelectionAsTargets:
                    panel.actionParams.TargetCharacters.AddRange(ServiceRepository
                        .GetService<IGameLocationSelectionService>().SelectedCharacters);
                    service2.ExecuteInstantSingleAction(panel.actionParams.Clone());
                    panel.RestoreDefaultCursor();
                    break;
                case ActionParameter.SelectInvocation:
                    panel.SelectInvocation();
                    break;
                case ActionParameter.OpenHandTechnique:
                    panel.SelectFlurryOfBlowsMode();
                    break;
                case ActionParameter.TogglePower:
                    if (!(panel.actionParams.ActionDefinition.ActivatedPower != null))
                    {
                        break;
                    }

                    panel.actionParams.RulesetEffect = ServiceRepository
                        .GetService<IRulesetImplementationService>().InstantiateEffectPower(
                            panel.GuiCharacter.RulesetCharacter,
                            panel.GuiCharacter.RulesetCharacter.GetPowerFromDefinition(panel.actionParams.ActionDefinition
                                .ActivatedPower)
                            ?? new RulesetUsablePower(panel.actionParams.ActionDefinition.ActivatedPower,
                                null, null), true);
                    panel.actionId = Id.PowerNoCost;
                    panel.RestoreDefaultCursor();
                    service2.ExecuteAction(panel.actionParams.Clone(), panel.ActionExecuted, false);
                    break;
            }
        }
    }

    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.SelectSpell))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class SelectSpell_Patch
    {
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> instructions)
        {
            //PATCH: Support for Quickened Spell action
            //replaces calls to ActionType to custom method which returns Main for Quickened action
            var getActionType = typeof(CharacterActionPanel).GetProperty(nameof(CharacterActionPanel.ActionType))!
                .GetGetMethod();
            var method = new Func<
                CharacterActionPanel,
                ActionType
            >(GetActionType).Method;

            return instructions.ReplaceCalls(getActionType, "CharacterActionPanel.SelectSpell",
                new CodeInstruction(OpCodes.Call, method));
        }

        private static ActionType GetActionType(CharacterActionPanel panel)
        {
            return panel.actionId == (Id)ExtraActionId.CastQuickened
                ? ActionType.Main
                : panel.ActionType;
        }
    }

    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.SpellCastConfirmed))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class SpellCastConfirmed_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(CharacterActionPanel __instance)
        {
            //PATCH: Support for Quickened Spell action
            if (__instance.actionId != (Id)ExtraActionId.CastQuickened)
            {
                return true;
            }

            __instance.actionId = Id.CastBonus;
            __instance.MetamagicSelected(
                __instance.GuiCharacter.GameLocationCharacter,
                (RulesetEffectSpell)__instance.actionParams.activeEffect,
                DatabaseHelper.MetamagicOptionDefinitions.MetamagicQuickenedSpell
            );
            return false;
        }
    }

    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.InvocationCastEngaged))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class InvocationCastEngaged_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(CharacterActionPanel __instance, RulesetInvocation invocation, int subspellIndex)
        {
            var definition = invocation.InvocationDefinition;
            var power = definition.GetPower();
            var actionDefinitions =
                ServiceRepository.GetService<IGameLocationActionService>().AllActionDefinitions;

            if (power)
            {
                var bundle = power.GetBundle();

                if (bundle != null)
                {
                    if (subspellIndex >= 0 && bundle.SubPowers.Count > subspellIndex)
                    {
                        power = bundle.SubPowers[subspellIndex];
                    }
                    else
                    {
                        Main.Error($"Wrong index for power bundle in '{definition.Name}' invocation: {subspellIndex}");
                    }
                }

                __instance.actionId = power.BattleActionId;
                __instance.actionParams.actionDefinition = actionDefinitions[__instance.actionId];
                __instance.PowerEngaged(PowerProvider.Get(power, __instance.GuiCharacter.RulesetCharacter));

                return false;
            }

            if (definition.GrantedSpell)
            {
                if (__instance.actionId == Id.CastInvocation)
                {
                    return true;
                }

                __instance.actionId = definition.GrantedSpell.BattleActionId;
                __instance.actionParams.actionDefinition = actionDefinitions[__instance.actionId];

                return true;
            }

            //Shouldn't happen - it should return from earlier, but just in case, to prevent crash
            Main.Error("InvocationCastEngaged with null spell and no power feature");
            return false;
        }
    }

    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.SelectInvocation))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class SelectInvocation_Patch
    {
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> instructions)
        {
            //PATCH: Support for bonus action invocations
            //replaces calls to InvocationSelectionPanel.Bind to custom method which supports filtering bonus and main action invocations
            var bindInvocationSelectionPanel = typeof(InvocationSelectionPanel).GetMethod("Bind");
            var method = new Action<
                InvocationSelectionPanel,
                GameLocationCharacter, // caster,
                InvocationSelectionPanel.InvocationSelectedHandler, // selected,
                InvocationSelectionPanel.InvocationCancelledHandler, // canceled,
                CharacterActionPanel // panel
            >(InvocationSelectionPanelExtensions.CustomBind).Method;

            return instructions.ReplaceCalls(bindInvocationSelectionPanel, "CharacterActionPanel.SelectInvocation",
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, method));
        }
    }

    //PATCH: don't display the break free selection panel if restrained by web or or ice bound
    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.SelectBreakFreeMode))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class SelectBreakFreeMode_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(CharacterActionPanel __instance)
        {
            var rulesetCharacter = __instance.GuiCharacter.RulesetCharacter;
            var restrainingCondition = AiHelpers.GetRestrainingCondition(rulesetCharacter);

            // if not a modded strength check condition let vanilla handle
            // this works as so far there is no way an ally should be forced to do a DoWisdomCheckAgainstCasterDC
            if (restrainingCondition?.Amount != (int)AiHelpers.BreakFreeType.DoStrengthCheckAgainstCasterDC)
            {
                return true;
            }

            __instance.actionParams.BreakFreeMode = BreakFreeMode.Athletics;

            ServiceRepository.GetService<IGameLocationActionService>()
                .ExecuteAction(__instance.actionParams.Clone(), __instance.ActionExecuted, false);

            return false;
        }
    }

    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.RefreshActionPerformances))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class RefreshActionPerformances_Patch
    {
        [UsedImplicitly]
        public static void Postfix(CharacterActionPanel __instance)
        {
            if (!Main.Settings.EnableActionSwitching)
            {
                return;
            }

            var table = __instance.actionPerformanceTable;

            if (!table)
            {
                return;
            }

            if (!table.gameObject.activeSelf)
            {
                return;
            }

            var filters = __instance.GuiCharacter.GameLocationCharacter.ActionPerformancesByType[__instance.ActionType];

            if (table.gameObject.TryGetComponent<HorizontalLayoutGroup>(out var group))
            {
                Object.DestroyImmediate(group);
            }

            if (!table.gameObject.TryGetComponent<GridLayoutGroup>(out var grid))
            {
                grid = table.gameObject.AddComponent<GridLayoutGroup>();
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.childAlignment = TextAnchor.MiddleCenter;
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.cellSize = new Vector2(32, 10);
                grid.spacing = new Vector2(3, 5);
            }

            if (grid)
            {
                var width = (int)__instance.RectTransform.rect.width;
                var constraint = width / 35;

                if (constraint > filters.Count)
                {
                    constraint = filters.Count;
                }

                grid.constraintCount = constraint;
            }

            var activeCount = 0;

            for (var i = 0; i < table.childCount; i++)
            {
                var child = table.GetChild(i);

                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                var item = child.GetComponent<ActionTypePerformanceItem>();

                if (!item)
                {
                    continue;
                }

                activeCount++;

                var k = child.GetSiblingIndex();
                var f = k >= 0 && k < filters.Count
                    ? PerformanceFilterExtraData.GetData(filters[k])
                    : null;

                var featureName = f?.FormatTitle();

                if (!string.IsNullOrEmpty(featureName))
                {
                    item.Tooltip.Content += $"\n{featureName}";
                }


                var btn = item.GetComponent<Button>();

                if (btn)
                {
                    continue;
                }

                btn = item.gameObject.AddComponent<Button>();
                btn.enabled = true;
                btn.interactable = true;
                btn.onClick.AddListener(() =>
                {
                    if (!Main.Settings.EnableActionSwitching)
                    {
                        return;
                    }

                    var panel = item.GetComponentInParent<CharacterActionPanel>();

                    if (item.availableSymbol.IsActive())
                    {
                        ActionSwitching.PrioritizeAction(
                            panel.GuiCharacter.GameLocationCharacter, panel.ActionType,
                            item.transform.GetSiblingIndex());
                    }
                });
            }

            var rank = __instance.GuiCharacter.GameLocationCharacter.CurrentActionRankByType[__instance.ActionType];

            if (activeCount - rank >= 2) //at least 2 non-spent actions
            {
                ServiceRepository.GetService<IGuiService>()
                    .ShowTutorial(ActionSwitching.Tutorial);
            }
        }
    }

    //BUGFIX: allows shape change spells to correctly interact with metamagic
    //it displays a shape prompt and avoid the delegate to call ExecuteEffectOfAction
    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.MetamagicIgnored))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class MetamagicIgnored_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(CharacterActionPanel __instance)
        {
            var hasShapeChangeForm = HasShapeChangeForm(__instance.actionParams.activeEffect as RulesetEffectSpell);

            if (!hasShapeChangeForm)
            {
                return true;
            }

            // SelectShape will call ExecuteEffectOfAction
            __instance.SelectShape(__instance.actionParams.RulesetEffect);

            return false;
        }
    }

    //BUGFIX: allows shape change spells to correctly interact with metamagic
    //it displays a shape prompt and avoid the delegate to call ExecuteEffectOfAction
    [HarmonyPatch(typeof(CharacterActionPanel), nameof(CharacterActionPanel.MetamagicSelected))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class MetamagicSelected_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(
            CharacterActionPanel __instance,
            RulesetEffectSpell spellEffect,
            MetamagicOptionDefinition metamagicOption)
        {
            var hasShapeChangeForm = HasShapeChangeForm(spellEffect);

            if (!hasShapeChangeForm)
            {
                return true;
            }

            // BEGIN VANILLA CODE
            spellEffect.MetamagicOption = metamagicOption;

            if (metamagicOption.Type == MetamagicType.QuickenedSpell)
            {
                __instance.actionParams.ActionDefinition = ServiceRepository.GetService<IGameLocationActionService>()
                    .AllActionDefinitions[Id.CastBonus];
            }
            // END VANILLA CODE

            // SelectShape will call ExecuteEffectOfAction
            __instance.SelectShape(spellEffect);

            return false;
        }
    }
}
