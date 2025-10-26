using System.Diagnostics.CodeAnalysis;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.CustomUI;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class BaseDefinitionPatcher
{
    [HarmonyPatch(typeof(BaseDefinition), nameof(BaseDefinition.FormatDescription))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class FormatDescription_Patch
    {
        [UsedImplicitly]
        public static void Postfix(BaseDefinition __instance, ref string __result)
        {
            var groupedFeat = __instance.GetFirstSubFeatureOfType<GroupedFeat>();

            if (groupedFeat == null) { return; }

            var titles = string.Join(", ", groupedFeat.GetSubFeats()
                .Select(x => x.FormatTitle())
                .OrderBy(x => x));

            __result = Gui.Format(__instance.guiPresentation.description, titles);
        }
    }
}
