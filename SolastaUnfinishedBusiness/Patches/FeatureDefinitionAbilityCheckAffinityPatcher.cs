using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using static LocationDefinitions;
using static RuleDefinitions;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class FeatureDefinitionAbilityCheckAffinityPatcher
{
    //BUGFIX: vanilla doesn't handle subtract die on ability check affinity tooltip
    [HarmonyPatch(typeof(FeatureDefinitionAbilityCheckAffinity),
        nameof(FeatureDefinitionAbilityCheckAffinity.FormatDescription))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class FormatDescription_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(
            FeatureDefinitionAbilityCheckAffinity __instance,
            ref string __result)
        {
            if (!string.IsNullOrEmpty(__instance.GuiPresentation.Description) &&
                __instance.GuiPresentation.Description != Gui.NoLocalization)
            {
                return true;
            }

            __result = string.Empty;

            foreach (var affinityGroup in __instance.affinityGroups)
            {
                if (!string.IsNullOrEmpty(__result))
                {
                    __result += "\n";
                }

                var formatAbilityScoreAndProficiency = Gui.FormatAbilityScoreAndProficiency(
                    affinityGroup.abilityScoreName, affinityGroup.proficiencyName);

                if (affinityGroup.affinity != CharacterAbilityCheckAffinity.None &&
                    affinityGroup.affinity != CharacterAbilityCheckAffinity.AutomaticSuccess)
                {
                    // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                    switch (affinityGroup.affinity)
                    {
                        case CharacterAbilityCheckAffinity.Advantage:
                            __result += Gui.FormatWithHighlight(FeatureDefinitionAbilityCheckAffinity.advantageFormat,
                                formatAbilityScoreAndProficiency);
                            break;
                        case CharacterAbilityCheckAffinity.Disadvantage:
                            __result += Gui.FormatWithHighlight(
                                FeatureDefinitionAbilityCheckAffinity.disadvantageFormat,
                                formatAbilityScoreAndProficiency);
                            break;
                        case CharacterAbilityCheckAffinity.AutomaticFail:
                            __result += Gui.FormatWithHighlight(
                                FeatureDefinitionAbilityCheckAffinity.automaticallyFailedFormat,
                                formatAbilityScoreAndProficiency);
                            break;
                    }

                    __result = affinityGroup.abilityCheckContext switch
                    {
                        AbilityCheckContext.EscapingPerception =>
                            __result + " " +
                            Gui.Localize(FeatureDefinitionAbilityCheckAffinity.escapingPerceptionSuffix),
                        AbilityCheckContext.ResistingShove =>
                            __result + " " +
                            Gui.Localize(FeatureDefinitionAbilityCheckAffinity.resistingShoveSuffix),
                        _ => __result
                    };
                }

                if (affinityGroup.abilityCheckModifierDiceNumber == 0)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(__result))
                {
                    __result += "\n";
                }

                if (affinityGroup.abilityCheckModifierDieType == DieType.D1)
                {
                    __result += Gui.FormatWithHighlight(
                        FeatureDefinitionAbilityCheckAffinity.bonusFormat,
                        affinityGroup.abilityCheckModifierDiceNumber.ToString("+0;-#"),
                        formatAbilityScoreAndProficiency);
                }
                // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                else
                {
                    var diceNumber = affinityGroup.abilityCheckGroupOperation == AbilityCheckGroupOperation.SubstractDie
                        ? -affinityGroup.abilityCheckModifierDiceNumber
                        : affinityGroup.abilityCheckModifierDiceNumber;

                    __result += Gui.FormatWithHighlight(
                        FeatureDefinitionAbilityCheckAffinity.bonusFormatWithDice,
                        diceNumber.ToString("+0;-#"),
                        Gui.FormatDieTitle(affinityGroup.abilityCheckModifierDieType),
                        formatAbilityScoreAndProficiency);
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(FeatureDefinitionAbilityCheckAffinity),
        nameof(FeatureDefinitionAbilityCheckAffinity.ComputeAbilityCheckAdvantageTrendAndModifier))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ComputeAbilityCheckAdvantageTrendAndModifier_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(FeatureDefinitionAbilityCheckAffinity __instance,
            string abilityScoreName,
            string proficiencyName,
            List<TrendInfo> advantageTrends,
            List<TrendInfo> modifierTrends,
            out int checkModifier,
            FeatureOrigin featureOrigin,
            int contextField,
            LightingState lightingState,
            int bardicDieRoll,
            bool ignoreDieRolls)
        {
            ComputeAbilityCheckAdvantageTrendAndModifier(__instance, abilityScoreName, proficiencyName, advantageTrends,
                modifierTrends, out checkModifier, featureOrigin, contextField, lightingState, bardicDieRoll,
                ignoreDieRolls);
            return false;
        }

        /** This is copied as-is from vanilla, with a small tweak to add the `strictContext` flag to fix a bug */
        private static void ComputeAbilityCheckAdvantageTrendAndModifier(
            FeatureDefinitionAbilityCheckAffinity feature,
            string abilityScoreName,
            string proficiencyName,
            List<TrendInfo> advantageTrends,
            List<TrendInfo> modifierTrends,
            out int checkModifier,
            FeatureOrigin featureOrigin,
            int contextField,
            LightingState lightingState,
            int bardicDieRoll,
            bool ignoreDieRolls = false)
        {
            //BUGFIX: `EscapingPerception` context is used to gather penalty from player to enemy detection rolls
            //like stealth check disadvantage for enemies on Cloak of Elvenkind
            //Unfortunately, this means that ALL WIS (PER) bonuses (like Owl's Wisdom) are also applied in this context to enemies
            //This flag enforces that only buffs that exactly match `EscapingPerception` context are valid
            var strictContext = contextField == (int)AbilityCheckContext.EscapingPerception;

            checkModifier = 0;
            foreach (var group in feature.affinityGroups)
            {
                if (group.abilityScoreName == abilityScoreName)
                {
                    var flag = false;
                    if (string.IsNullOrEmpty(proficiencyName) || string.IsNullOrEmpty(group.proficiencyName))
                    {
                        flag = true;
                    }
                    else if (proficiencyName == group.proficiencyName)
                    {
                        flag = true;
                    }

                    if (flag
                        && (strictContext || group.abilityCheckContext != AbilityCheckContext.None)
                        && (group.abilityCheckContext & (AbilityCheckContext)contextField) == AbilityCheckContext.None)
                    {
                        flag = false;
                    }

                    if (flag && group.lightingContext != LightingContext.Irrelevant)
                    {
                        flag =
                            group.lightingContext == LightingContext.Unlit && lightingState == LightingState.Unlit
                            || group.lightingContext == LightingContext.DimLight && lightingState == LightingState.Dim
                            || group.lightingContext == LightingContext.BrightLight && lightingState == LightingState.Bright;
                    }

                    if (flag)
                    {
                        if (group.affinity == CharacterAbilityCheckAffinity.Advantage)
                        {
                            advantageTrends.Add(new TrendInfo(1, featureOrigin.sourceType,
                                featureOrigin.sourceName, featureOrigin.source));
                        }
                        else if (group.affinity == CharacterAbilityCheckAffinity.Disadvantage)
                        {
                            advantageTrends.Add(new TrendInfo(-1, featureOrigin.sourceType,
                                featureOrigin.sourceName, featureOrigin.source));
                        }

                        var trendInfoDieFlag = group.abilityCheckModifierDiceNumber == 1
                            ? TrendInfoDieFlag.AddDie
                            : TrendInfoDieFlag.None;
                        var num = 0;
                        if (ignoreDieRolls)
                        {
                            num = group.abilityCheckModifierDiceNumber
                                  * DiceMinValue[(int)group.abilityCheckModifierDieType];
                        }
                        else if (group.abilityCheckGroupOperation == AbilityCheckGroupOperation.AddDie)
                        {
                            num = RollStaticDiceAndSum(group.abilityCheckModifierDiceNumber,
                                group.abilityCheckModifierDieType);
                        }
                        else if (group.abilityCheckGroupOperation == AbilityCheckGroupOperation.SubstractDie)
                        {
                            num = -RollStaticDiceAndSum(group.abilityCheckModifierDiceNumber,
                                group.abilityCheckModifierDieType);
                        }

                        checkModifier += num;
                        modifierTrends.Add(new TrendInfo(num, featureOrigin.sourceType, featureOrigin.sourceName,
                            featureOrigin.source)
                        {
                            dieFlag = trendInfoDieFlag,
                            dieType = group.abilityCheckModifierDieType
                        });
                        break;
                    }
                }
            }

            if (!feature.substractBardicDieRoll || bardicDieRoll <= 0)
            {
                return;
            }

            checkModifier -= bardicDieRoll;
            modifierTrends.Add(new TrendInfo(-bardicDieRoll, featureOrigin.sourceType,
                featureOrigin.sourceName, null));
        }
    }
}
