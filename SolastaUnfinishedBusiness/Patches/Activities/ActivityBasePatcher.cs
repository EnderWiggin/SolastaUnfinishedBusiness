using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using TA.AI;

namespace SolastaUnfinishedBusiness.Patches.Activities;

[UsedImplicitly]
public static class ActivityBasePatcher
{
    [HarmonyPatch(typeof(ActivityBase), nameof(ActivityBase.ExecuteAndWait))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ExecuteAndWait_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(ActivityBase __instance, CharacterActionParams actionParams)
        {
            //PATCH: Fix for UB-introduced cases when enemy with multi-attack loses ability to attack between chained attacks
            //For example - having Topple mastery and using Sentinel's reaction attack when enemy with multi-attack attacks your ally
            return actionParams != null;
        }
    }
}
