using System.Linq;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Interfaces;
using SolastaUnfinishedBusiness.Subclasses.Builders;
using UnityEngine.AddressableAssets;

namespace SolastaUnfinishedBusiness.CustomUI;

internal class PortraitPointEldritchVersatility : ICustomPortraitPointPoolProvider
{
    public static ICustomPortraitPointPoolProvider Instance { get; } = new PortraitPointEldritchVersatility();
    public string Name => "EldritchVersatility";

    public bool IsActive(RulesetCharacter character)
    {
        return true;
    }

    string ICustomPortraitPointPoolProvider.Tooltip(RulesetCharacter character)
    {
        var currentPoints = 0;
        var maxPoints = 0;
        string attribute;
        string priority;

        if (!character.GetVersatilitySupportCondition(out var supportCondition))
        {
            attribute = "Tooltip/&ValueNoneTitle";
            priority = "Tooltip/&ValueNoneTitle";
        }
        else
        {
            currentPoints = supportCondition.CurrentPoints;
            maxPoints = supportCondition.MaxPoints;

            attribute = string.IsNullOrEmpty(supportCondition.ReplacedAbilityScore)
                ? "Feature/&PowerPatronEldritchSurgeVersatilitySwitchNoneTitle"
                : $"Attribute/&{supportCondition.ReplacedAbilityScore}TitleLong";

            priority = supportCondition.StrPowerPriority.Empty()
                ? "Tooltip/&ValueNoneTitle"
                : string.Join(", ", supportCondition.StrPowerPriority.Select(s => s.Localized(Category.Feature)));
        }

        return "EldritchVersatilityPortraitPoolFormat".Formatted(
            Category.Tooltip,
            currentPoints,
            maxPoints,
            attribute,
            priority);
    }

    public AssetReferenceSprite Icon => Sprites.EldritchVersatilityResourceIcon;

    public string GetPoints(RulesetCharacter character)
    {
        var currentPoints = 0;

        if (character.GetVersatilitySupportCondition(out var supportCondition))
        {
            currentPoints = supportCondition.CurrentPoints;
        }

        return $"{currentPoints}";
    }
}
