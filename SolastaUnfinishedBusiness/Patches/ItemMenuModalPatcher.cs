using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Models;
using static ActionDefinitions;
using static RuleDefinitions;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class ItemMenuModalPatcher
{
    [HarmonyPatch(typeof(ItemMenuModal), nameof(ItemMenuModal.ActivateFunction))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ActivateFunction_Patch
    {
        [NotNull]
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> instructions)
        {
            //PATCH: do not use Bonus Action to use Potion/Poison if 2024 BA item use is active
            //usually if character has ability to use BA for item manipulation, game will try to use BA first
            //But with this option poisons and potions get separate menu action to use BA

            var oldMethod = AccessTools.Method(typeof(CharacterActionPanel),
                nameof(CharacterActionPanel.OnActivateActionExternal));
            var newMethod = new Action<
                CharacterActionPanel,
                Id,
                GameLocationCharacter,
                RulesetItemDevice,
                RulesetDeviceFunction
            >(PatchedOnActivateActionExternal).Method;

            return instructions.ReplaceCalls(oldMethod, "ItemMenuModal.ActivateFunction",
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldarg_3),
                new CodeInstruction(OpCodes.Call, newMethod));
        }

        private static void PatchedOnActivateActionExternal(
            CharacterActionPanel panel,
            Id actionId,
            GameLocationCharacter externalCharacter,
            RulesetItemDevice device,
            RulesetDeviceFunction function)
        {
            if (Gui.Battle != null)
            {
                var tags = device.UsableDeviceDescription.usableDeviceTags;
                if (Main.Settings.EnablePotionsBonusAction2024 && tags.Contains(GameConstants.TagPotion)
                    || Main.Settings.EnablePoisonsBonusAction2024 && tags.Contains(GameConstants.TagPoison))
                {
                    var power = function.DeviceFunctionDescription.FeatureDefinitionPower;
                    actionId = power.ActivationTime == ActivationTime.BonusAction
                        ? Id.UseItemBonus
                        : Id.UseItemMain;
                }
            }

            panel.OnActivateActionExternal(actionId, externalCharacter);
        }
    }

    [HarmonyPatch(typeof(ItemMenuModal), nameof(ItemMenuModal.SetupFromItem))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class SetupFromItem_Patch
    {
#if false
        private static bool IsSpellDefinitionOnRepertoire(
            RulesetActor rulesetActor,
            RulesetSpellRepertoire spellRepertoire,
            SpellDefinition spellDefinition)
        {
            if (spellRepertoire.SpellCastingFeature.HasAccessToSpell(spellDefinition) ||
                spellRepertoire.AutoPreparedSpells.Contains(spellDefinition) ||
                spellRepertoire.IsSpellDefinitionInExtraSpells(spellDefinition))
            {
                return true;
            }

            // only exceptional case a Wizard in game can get additional spells
            return rulesetActor.HasAnyFeature(MagicAffinityGreenmageGreenMagicList) &&
                   MagicAffinityGreenmageGreenMagicList.ExtendedSpellList.ContainsSpell(spellDefinition);
        }
#endif

        //PATCH: allows mark deity to work with MC heroes (Multiclass)
        private static bool RequiresDeity(ItemMenuModal itemMenuModal)
        {
            return itemMenuModal.GuiCharacter.RulesetCharacterHero.ClassesHistory.Exists(x => x.RequiresDeity);
        }

        //PATCH: only allow to scribe spells the scriber class can do
        private static List<RulesetSpellRepertoire> SpellRepertoires(
            RulesetCharacterHero rulesetCharacterHero,
            GuiEquipmentItem guiEquipmentItem)
        {
            if (guiEquipmentItem.EquipementItem is not RulesetItemDevice rulesetItemDevice ||
                rulesetItemDevice.UsableFunctions[0] is null)
            {
                return rulesetCharacterHero.SpellRepertoires;
            }

            return rulesetCharacterHero.SpellRepertoires
                .Where(x => x.SpellCastingFeature.SpellKnowledge == SpellKnowledge.Spellbook)
                .ToList();
        }

        private static ActionStatus PatchedActionStatus(GameLocationCharacter character,
            ActionType actionType, ActionScope actionScope, bool ignoreMovePoints,
            RulesetDeviceFunction usableFunction)
        {
            //PATCH: allow action if this is extra bonus function enabled from 2024 item usage rules
            var power = usableFunction.DeviceFunctionDescription?.featureDefinitionPower;
            if (power != null && Tabletop2024Context.ItemBonusPowers.ContainsValue(power))
            {
                return ActionStatus.Available;
            }

            return character.GetActionTypeStatus(actionType, actionScope, ignoreMovePoints);
        }

        [NotNull]
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> instructions)
        {
            var requiresDeityMethod = typeof(CharacterClassDefinition).GetMethod("get_RequiresDeity");
            var myRequiresDeityMethod = new Func<ItemMenuModal, bool>(RequiresDeity).Method;

            var spellRepertoiresMethod = typeof(RulesetCharacter).GetMethod("get_SpellRepertoires");
            var mySpellRepertoiresMethod =
                new Func<RulesetCharacterHero, GuiEquipmentItem, List<RulesetSpellRepertoire>>(SpellRepertoires).Method;

            var oldActionStatus = AccessTools.Method(typeof(GameLocationCharacter),
                nameof(GameLocationCharacter.GetActionTypeStatus));
            var newActionStatus = typeof(SetupFromItem_Patch).GetMethod(nameof(PatchedActionStatus),
                BindingFlags.Static | BindingFlags.NonPublic);

            return instructions
                .ReplaceCalls(requiresDeityMethod, "ItemMenuModal.SetupFromItem1",
                    new CodeInstruction(OpCodes.Pop),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, myRequiresDeityMethod))
                .ReplaceCalls(spellRepertoiresMethod, "ItemMenuModal.SetupFromItem2",
                    new CodeInstruction(OpCodes.Ldarg_3),
                    new CodeInstruction(OpCodes.Call, mySpellRepertoiresMethod))
                .ReplaceCall(oldActionStatus, 1, "ItemMenuModal.SetupFromItem3",
                    new CodeInstruction(OpCodes.Ldloc_S, 7),
                    new CodeInstruction(OpCodes.Call, newActionStatus));
        }
    }
}
