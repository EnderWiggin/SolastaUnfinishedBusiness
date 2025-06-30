using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.CustomUI;
using static ConsoleStyleDuplet;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class GameConsolePatcher
{
    [HarmonyPatch(typeof(GameConsole), nameof(GameConsole.AttackRolled))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class AttackRolled_Patch
    {
        [NotNull]
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> instructions)
        {
            //PATCH: setup tooltip of a power passed to `GameConsole.AttackRolled`
            var method = new Action<GameRecordTable, GameRecordEntry, int, BaseDefinition>(AddEntry).Method;
            return instructions.ReplaceCall("AddEntry", -1, "GameConsole.AttackRolled",
                new CodeInstruction(OpCodes.Ldarg_3),
                new CodeInstruction(OpCodes.Call, method));
        }

        private static void AddEntry(GameRecordTable console, GameRecordEntry entry, int insertionIndex,
            BaseDefinition definition)
        {
            if (definition is FeatureDefinitionPower)
            {
                foreach (var parameter in entry.Parameters
                             .Where(parameter =>
                                 parameter.parameterType == (int)ParameterType.AttackSpellPower)
                             .Where(parameter =>
                                 string.IsNullOrEmpty(parameter.tooltipContent) &&
                                 string.IsNullOrEmpty(parameter.tooltipClass))
                             .Where(parameter => parameter.contentValue == definition.GuiPresentation.Title))
                {
                    parameter.tooltipContent = definition.Name;
                    parameter.tooltipClass = GuiPowerDefinition.TooltipClassPowerDefinition;
                }
            }

            console.AddEntry(entry, insertionIndex);
        }
    }

    [HarmonyPatch(typeof(GameConsole), nameof(GameConsole.DamageReduced))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class DamageReduced_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(GameConsole __instance,
            RulesetActor character,
            FeatureDefinition feature,
            int reductionAmount)
        {
            //PATCH: allow damage reduction log to show damage types and show feature description on tooltip
            var prompt = "Feedback/&DamageReducedLine";
            var types = "";
            var typeNames = "";

            if (feature is FeatureDefinitionReduceDamage { DamageTypes.Count: > 0 } reduce)
            {
                prompt = Gui.Localize("Feedback/&DamageReducedLine").Replace("{2}", "{2}{3}");
                types = string.Join("", reduce.DamageTypes.Select(x => Gui.FormatDamageType(x)));
                typeNames = string.Join("\n", reduce.DamageTypes.Select(x => Gui.FormatDamageType(x, true)));
            }

            var entry = new GameConsoleEntry(prompt, __instance.consoleTableDefinition) { Indent = true };

            entry.AddParameter(ParameterType.AttackSpellPower,
                Gui.Localize(feature.GuiPresentation.Title), tooltipContent: feature.guiPresentation.Description);
            __instance.AddCharacterEntry(character, entry);
            entry.AddParameter(ParameterType.Positive, reductionAmount.ToString());
            entry.AddParameter(ParameterType.Initiative, types, tooltipContent: typeNames);

            __instance.AddEntry(entry);

            return false;
        }
    }


    [HarmonyPatch(typeof(GameConsole), nameof(GameConsole.ItemUsed))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ItemUsed_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(GameConsole __instance,
            RulesetCharacter character,
            RulesetItemDevice usableDevice,
            RulesetDeviceFunction deviceFunction,
            int additionalCharges)
        {
            var itemTitle = GuiItemTweaks.FormatTitle(usableDevice.ItemDefinition);
            if (deviceFunction.DeviceFunctionDescription.Type == DeviceFunctionDescription.FunctionType.Spell)
            {
                var entry = new GameConsoleEntry(GameConsole.ItemUsedSpellCastLine, __instance.consoleTableDefinition);
                __instance.AddCharacterEntry(character, entry);
                var spell = deviceFunction.DeviceFunctionDescription.SpellDefinition;

                entry.AddParameter(ParameterType.AttackSpellPower, itemTitle,
                    tooltipContent: usableDevice.ItemDefinition.Name,
                    tooltipClass: GuiItemDefinition.TooltipClassItemDefinition);
                entry.AddParameter(ParameterType.AttackSpellPower, spell.GuiPresentation.Title,
                    tooltipContent: spell.Name, tooltipClass: GuiSpellDefinition.TooltipClassSpellDefinition);
                __instance.AddEntry(entry);
            }
            else
            {
                var entry = new GameConsoleEntry(GameConsole.ItemUsedLine, __instance.consoleTableDefinition);
                __instance.AddCharacterEntry(character, entry);
                entry.AddParameter(ParameterType.AttackSpellPower, itemTitle,
                    tooltipContent: usableDevice.ItemDefinition.Name,
                    tooltipClass: GuiItemDefinition.TooltipClassItemDefinition);
                __instance.AddEntry(entry);
            }

            return false;
        }
    }
}
