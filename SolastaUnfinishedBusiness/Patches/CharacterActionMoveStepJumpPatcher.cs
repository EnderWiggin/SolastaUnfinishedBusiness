using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Interfaces;
using TA;
using static RuleDefinitions;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class CharacterActionMoveStepJumpPatcher
{
    //PATCH: allow check reactions on jump checks regardless of success / failure
    [HarmonyPatch(typeof(CharacterActionMoveStepJump), nameof(CharacterActionMoveStepJump.RollChecksIfNecessary))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class RollChecksIfNecessary_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(ref IEnumerator __result, CharacterActionMoveStepJump __instance)
        {
            __result = Process(__instance);

            return false;
        }

        private static IEnumerator Process(CharacterActionMoveStepJump action)
        {
            var actingCharacter = action.ActingCharacter;
            var actionModifier = action.ActionParams.ActionModifiers[0];
            RuleDefinitions.AdvantageType BASE_AFFINITY = RuleDefinitions.AdvantageType.None;

            bool isWearingHeavy = actingCharacter.RulesetCharacter.IsWearingHeavyArmor() && Main.Settings.ModifyJumpRulesForArmorAndEncumberance;
            bool isWearingMedium = actingCharacter.RulesetCharacter.IsWearingMediumArmor() && Main.Settings.ModifyJumpRulesForArmorAndEncumberance;
            int distance = (int)int3.Distance(action.jumpPosition, action.landingPosition);

            //adjust for wearing heavy armor                
            if (isWearingHeavy)
                BASE_AFFINITY = RuleDefinitions.AdvantageType.Disadvantage;

            if (CharacterActionMoveStepJump.NeedsAcrobaticsCheck(action.landingPosition))
            {
                const int CHECK_DC = 10;

                var abilityCheckRoll = actingCharacter.RollAbilityCheckEx(
                    AttributeDefinitions.Dexterity,
                    SkillDefinitions.Acrobatics,
                    CHECK_DC,
                    BASE_AFFINITY,
                    actionModifier,
                    false,
                    -1,
                    out var outcome,
                    out var successDelta,
                    out var rawRoll,
                    true);

                var abilityCheckData = new AbilityCheckData
                {
                    AbilityCheckRoll = abilityCheckRoll,
                    AbilityCheckRollOutcome = outcome,
                    AbilityCheckSuccessDelta = successDelta,
                    AbilityCheckActionModifier = actionModifier,
                    Action = action
                };

                yield return TryAlterOutcomeAttributeCheck
                    .HandleITryAlterOutcomeAttributeCheck(actingCharacter, abilityCheckData, rawRoll);

                action.AbilityCheckRoll = abilityCheckData.AbilityCheckRoll;
                action.AbilityCheckRollOutcome = abilityCheckData.AbilityCheckRollOutcome;
                action.AbilityCheckSuccessDelta = abilityCheckData.AbilityCheckSuccessDelta;
            }

            if (action.AbilityCheckRollOutcome != RuleDefinitions.RollOutcome.Failure 
                && (CharacterActionMoveStepJump.NeedsAthleticsCheck(action.ActingCharacter, action.jumpPosition,
                    action.landingPosition) || isWearingHeavy || isWearingMedium))
            {
                int CHECK_DC = Main.Settings.ModifyJumpRulesForArmorAndEncumberance ? distance*5 : 15;

                var abilityCheckRoll = action.ActingCharacter.RollAbilityCheckEx(
                    AttributeDefinitions.Strength,
                    SkillDefinitions.Athletics,
                    CHECK_DC,
                    BASE_AFFINITY,
                    action.ActionParams.ActionModifiers[0],
                    false,
                    -1,
                    out var outcome,
                    out var successDelta,
                    out var rawRoll,
                    true);

                var abilityCheckData = new AbilityCheckData
                {
                    AbilityCheckRoll = abilityCheckRoll,
                    AbilityCheckRollOutcome = outcome,
                    AbilityCheckSuccessDelta = successDelta,
                    AbilityCheckActionModifier = actionModifier,
                    Action = action
                };

                yield return TryAlterOutcomeAttributeCheck
                    .HandleITryAlterOutcomeAttributeCheck(actingCharacter, abilityCheckData, rawRoll);

                action.AbilityCheckRoll = abilityCheckData.AbilityCheckRoll;
                action.AbilityCheckRollOutcome = abilityCheckData.AbilityCheckRollOutcome;
                action.AbilityCheckSuccessDelta = abilityCheckData.AbilityCheckSuccessDelta;
            }
            else if (CharacterActionMoveStepJump.AutomaticPenalty(
                         action.ActingCharacter, action.jumpPosition, action.landingPosition))
            {
                action.AbilityCheckRollOutcome = RuleDefinitions.RollOutcome.Failure;
            }
        }
    }
}
