using System.Collections.Generic;
using System.Linq;
using SolastaUnfinishedBusiness.ItemCrafting;
using SolastaUnfinishedBusiness.Models;
using static RuleDefinitions;

namespace SolastaUnfinishedBusiness.CustomUI;

internal static class GuiItemTweaks
{
    internal static string FormatTitle(ItemDefinition item)
    {
        if (ScrollsData.IsScrollItem(item))
        {
            var spell = ScrollsData.GetScrollSpell(item);
            return Gui.Format(item.GuiPresentation.Title, spell.GuiPresentation.Title);
        }

        if (item.IsWealthPile)
        {
            return item.WealthPileDescription.FormatTitle();
        }

        var label = Gui.Localize(item.GuiPresentation.Title);

        return item.IsDocument ? item.DocumentDescription.FormatTitle(label) : label;
    }

    internal static string FormatDescription(ItemDefinition item)
    {
        if (ScrollsData.IsScrollItem(item))
        {
            var spell = ScrollsData.GetScrollSpell(item);
            return Gui.Format(item.GuiPresentation.Description, spell.GuiPresentation.Title);
        }

        var description = Gui.Localize(item.GuiPresentation.Description);
        return item.IsDocument ? item.DocumentDescription.FormatDescription(description) : description;
    }

    internal static string SubTitleWithoutRarity(ItemDefinition item)
    {
        if (item.ItemPresentation.OverrideSubtype != EquipmentDefinitions.OverrideSubtypeNone)
        {
            return Gui.Localize(string.Format(GuiItemDefinition.itemTypeOverrideTitle,
                item.ItemPresentation.OverrideSubtype));
        }

        List<string> parts = [];
        if (item.IsArmor)
        {
            if (item.ArmorDescription.ArmorTypeDefinition.ArmorCategory == EquipmentDefinitions.NoArmorCategory)
            {
                parts.Add(Gui.Localize(GuiItemDefinition.itemTypeClothTitle));
            }
            else if (item.ArmorDescription.ArmorTypeDefinition.ArmorCategory != EquipmentDefinitions.ShieldCategory)
            {
                parts.Add(Gui.Localize(item.ArmorDescription.ArmorTypeDefinition.ArmorCategoryDefinition
                    .GuiPresentation.Title));
            }
        }

        if (item.IsWeapon)
        {
            var weaponType = item.WeaponDescription.WeaponTypeDefinition;
            if (SettingsContext.GuiModManagerInstance.ShowWeaponTypeInTooltip)
            {
                parts.Add(Gui.Localize(weaponType.GuiPresentation.Title));
            }

            parts.Add(Gui.Localize(weaponType.WeaponCategory == EquipmentDefinitions.MartialWeaponCategory
                ? GuiItemDefinition.itemTypeWeaponMartialTitle
                : GuiItemDefinition.itemTypeWeaponSimpleTitle));
        }

        if (item.IsAmmunition)
        {
            parts.Add(Gui.Localize(GuiItemDefinition.itemTypeAmmunitionTitle));

            var effect = item.AmmunitionDescription.EffectDescription;
            if (effect.HasSavingThrow
                && effect.DifficultyClassComputation == EffectDifficultyClassComputation.FixedValue)
            {
                parts.Add(Gui.FormatDC(effect.FixedSavingThrowDifficultyClass));
            }
        }

        if (item.IsTool)
        {
            parts.Add(Gui.Localize(GuiItemDefinition.itemTypeToolTitle));
        }

        if (item.IsStarterPack)
        {
            parts.Add(Gui.Localize(GuiItemDefinition.itemTypeStarterPackTitle));
        }

        if (item.IsContainerItem)
        {
            parts.Add(Gui.Localize(GuiItemDefinition.itemTypeContainerTitle));
        }

        if (item.IsLightSourceItem)
        {
            parts.Add(Gui.Localize(GuiItemDefinition.itemTypeLightSourceTitle));
        }

        if (item.IsFocusItem)
        {
            parts.Add(Gui.Localize(GuiItemDefinition.itemTypeFocusTitle) + Gui.Format(" ({0})",
                $"Equipment/&ItemTypeSpellFocusSubtype{item.FocusItemDescription.FocusType}Title"));
        }

        if (item.IsUsableDevice)
        {
            var device = item.UsableDeviceDescription;
            if (device.UsableDeviceTags.Contains(GameConstants.TagPoison))
            {
                parts.Add(Gui.Localize("Tooltip/&TagPoisonTitle"));
                var dc = FindPoisonDC(device);
                if (dc > 0) { parts.Add(Gui.FormatDC(dc)); }
            }
        }

        if (item.IsSpellbook)
        {
            parts.Add(Gui.Localize(GuiItemDefinition.itemTypeSpellbookTitle));
        }

        return string.Join(Gui.ListSeparator(), parts);
    }

    private static int FindPoisonDC(UsableDeviceDescription device)
    {
        foreach (var function in device.DeviceFunctions)
        {
            var power = function.featureDefinitionPower;
            if (!power) { continue; }

            foreach (var form in power.EffectDescription.EffectForms)
            {
                if (form.FormType != EffectForm.EffectFormType.ItemProperty)
                {
                    continue;
                }

                foreach (var feature in form.ItemPropertyForm.FeatureBySlotLevel.Select(x => x.FeatureDefinition)
                             .OfType<FeatureDefinitionAdditionalDamage>())
                {
                    if (feature.HasSavingThrow && feature.DcComputation == EffectDifficultyClassComputation.FixedValue)
                    {
                        return feature.SavingThrowDC;
                    }
                }
            }
        }

        return 0;
    }
}
