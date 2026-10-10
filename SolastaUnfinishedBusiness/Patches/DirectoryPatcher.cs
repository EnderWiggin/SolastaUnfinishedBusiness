using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Models;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class DirectoryPatcher
{
    //PATCH: keep the Steam Cloud settings mirror out of the save list
    [HarmonyPatch(typeof(Directory), nameof(Directory.GetFiles), typeof(string), typeof(string))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class GetFiles_Patch
    {
        [UsedImplicitly]
        public static void Postfix(ref string[] __result)
        {
            if (__result == null || !__result.Any(SettingsCloudSync.IsMirrorFile))
            {
                return;
            }

            __result = __result.Where(path => !SettingsCloudSync.IsMirrorFile(path)).ToArray();
        }
    }
}
