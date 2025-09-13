using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Builders;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
internal static class UserEffectFormPatcher
{
    [HarmonyPatch(typeof(UserEffectForm), nameof(UserEffectForm.BuildEffectForm))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class BuildEffectForm_Patch
    {
        [UsedImplicitly]
        public static void Postfix(UserEffectForm __instance, ref EffectForm __result)
        {
            //PATCH: fixes custom effects that add Prone condition
            //just adding a condition does not work properly, so instead create motion form that will make target fall prone properly
            if (__instance.formType != UserContentDefinitions.EffectFormType.Condition
                || __instance.ConditionDefinitionName != RuleDefinitions.ConditionProne)
            {
                return;
            }

            __result = EffectFormBuilder.MotionForm(MotionForm.MotionType.FallProne)
                .WithSavingThrow(__instance.SaveAffinity, canSaveToCancel: __instance.CanSaveToCancel);
        }
    }
}
