using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.CustomUI;
using SolastaUnfinishedBusiness.Properties;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionActionAffinitys;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionPowers;

namespace SolastaUnfinishedBusiness.Subclasses;

[UsedImplicitly]
public sealed class PathOfAncestralGuardians : AbstractSubclass
{
    private const string Name = "PathOfAncestralGuardians";

    public PathOfAncestralGuardians()
    {
        // Lv.3 Ancestral Protectors

        // Lv.6 Spirit Shield (+1 die at 10th, 14th)

        // Lv.10 (Needs a new feature)

        // Lv.14 Vengeful Ancestors

        // MAIN

        Subclass = CharacterSubclassDefinitionBuilder
            .Create(Name)
            .SetGuiPresentation(Category.Subclass, Sprites.GetSprite(Name, Resources.PathOfTheRavager, 256))
            /*.AddFeaturesAtLevel(3, )
            .AddFeaturesAtLevel(6, )
            .AddFeaturesAtLevel(10, )
            .AddFeaturesAtLevel(14, )*/
            .AddToDB();
    }

    internal override CharacterClassDefinition Klass => CharacterClassDefinitions.Barbarian;

    internal override CharacterSubclassDefinition Subclass { get; }

    internal override FeatureDefinitionSubclassChoice SubclassChoice =>
        FeatureDefinitionSubclassChoices.SubclassChoiceBarbarianPrimalPath;

    // ReSharper disable once UnassignedGetOnlyAutoProperty
    internal override DeityDefinition DeityDefinition { get; }
}
