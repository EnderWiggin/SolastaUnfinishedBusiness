using System.Collections.Generic;
using System.Linq;
using SolastaUnfinishedBusiness.Behaviors;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using SolastaUnfinishedBusiness.Validators;
using static ActionDefinitions;
using static RuleDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static partial class Tabletop2024Context
{
    private static readonly FeatureDefinitionActionAffinity ActionAffinityPotionBonusAction =
        FeatureDefinitionActionAffinityBuilder
            .Create("ActionAffinityPotionBonusAction")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(
                new ValidateDeviceFunctionUse((_, device, f) =>
                    device.UsableDeviceDescription.UsableDeviceTags.Contains(GameConstants.TagPotion)
                    && f.DeviceFunctionDescription.FeatureDefinitionPower.ActivationTime == ActivationTime.Action))
            .SetAuthorizedActions(Id.UseItemBonus)
            .AddToDB();

    private static readonly FeatureDefinitionActionAffinity ActionAffinityPoisonBonusAction =
        FeatureDefinitionActionAffinityBuilder
            .Create("ActionAffinityPoisonBonusAction")
            .SetGuiPresentationNoContent(true)
            .AddCustomSubFeatures(
                new ValidateDeviceFunctionUse((_, device, f) =>
                    device.UsableDeviceDescription.UsableDeviceTags.Contains(GameConstants.TagPoison)
                    && f.DeviceFunctionDescription.FeatureDefinitionPower.ActivationTime == ActivationTime.Action))
            .SetAuthorizedActions(Id.UseItemBonus)
            .AddToDB();

    private static readonly ItemPropertyDescription ItemPropertyPotionBonusAction = ItemPropertyDescriptionBuilder
        .From(ActionAffinityPotionBonusAction, false).Build();

    private static readonly ItemPropertyDescription ItemPropertyPoisonBonusAction = ItemPropertyDescriptionBuilder
        .From(ActionAffinityPoisonBonusAction, false).Build();

    internal static void SwitchPotionsBonusAction()
    {
        SwitchItemBonusActions(GameConstants.TagPotion, ItemPropertyPotionBonusAction, Main.Settings.EnablePotionsBonusAction2024);
    }

    internal static void SwitchPoisonsBonusAction()
    {
        SwitchItemBonusActions(GameConstants.TagPoison, ItemPropertyPoisonBonusAction, Main.Settings.EnablePoisonsBonusAction2024);
    }

    private static void SwitchItemBonusActions(string tag, ItemPropertyDescription property, bool enabled)
    {
        List<DeviceFunctionDescription> functions = [];
        foreach (var item in DatabaseRepository.GetDatabase<ItemDefinition>()
                     .Where(a => a.UsableDeviceDescription != null
                                 && a.UsableDeviceDescription.usableDeviceTags.Contains(tag)))
        {
            var device = item.UsableDeviceDescription;
            if (enabled)
            {
                functions.Clear();
                foreach (var function in device.DeviceFunctions)
                {
                    var power = function.Type == DeviceFunctionDescription.FunctionType.Power
                        ? function.featureDefinitionPower
                        : null;
                    if (power is not { ActivationTime: ActivationTime.Action }) { continue; }

                    functions.Add(new DeviceFunctionDescription(function)
                    {
                        featureDefinitionPower = GetBonusPower(power)
                    });
                }

                item.StaticProperties.TryAdd(property);
                device.DeviceFunctions.AddRange(functions);
            }
            else
            {
                device.DeviceFunctions.RemoveAll(f => ItemBonusPowers.ContainsValue(f.featureDefinitionPower));
                item.StaticProperties.Remove(property);
            }
        }

        var service = ServiceRepository.GetService<IRulesetEntityService>();

        if (service is not null)
        {
            foreach (var device in service.RulesetEntities.Select(x => x.Value).OfType<RulesetItemDevice>())
            {
                UpdateDeviceBonusActions(device, tag);
            }
        }
    }

    internal static void UpdateDeviceBonusActions(RulesetItemDevice device, string tag)
    {
        var tags = device.UsableDeviceDescription?.usableDeviceTags;
        if (tags == null || !tags.Contains(tag)) { return; }

        var functionsCount = device.ItemDefinition.UsableDeviceDescription.DeviceFunctions.Count;

        device.usableFunctions.RemoveAll(f => f.functionIndex >= functionsCount);

        for (var i = 0; i < functionsCount; i++)
        {
            if (device.usableFunctions.Any(f => f.functionIndex == i)) { continue; }

            var function = new RulesetDeviceFunction(device.itemDefinition, i);
            device.usableFunctions.Add(function);
            function.RegisterAttributes();
        }
    }

    internal static readonly Dictionary<FeatureDefinitionPower, FeatureDefinitionPower> ItemBonusPowers = [];

    private static FeatureDefinitionPower GetBonusPower(FeatureDefinitionPower power)
    {
        if (ItemBonusPowers.TryGetValue(power, out var bonusPower))
        {
            return bonusPower;
        }

        bonusPower = FeatureDefinitionPowerBuilder
            .Create(power, $"{power.Name}_2024BA")
            .AddCustomSubFeatures(ModifyPowerVisibility.Hidden)
            .AddToDB();
        bonusPower.GuiPresentation.Title =
            Gui.Format("{0} ({1})", bonusPower.GuiPresentation.Title, "Action/&ActionTypeBonusTitle");
        bonusPower.activationTime = ActivationTime.BonusAction;

        ItemBonusPowers.Add(power, bonusPower);
        return bonusPower;
    }
}
