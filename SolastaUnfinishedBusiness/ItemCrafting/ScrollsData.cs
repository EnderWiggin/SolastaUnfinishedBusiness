using System.Collections.Generic;
using System.Linq;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.CustomUI;
using SolastaUnfinishedBusiness.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using static RuleDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;
using Resources = SolastaUnfinishedBusiness.Properties.Resources;

namespace SolastaUnfinishedBusiness.ItemCrafting;

internal static class ScrollsData
{
    static readonly Dictionary<int, int> ScrollCostBySpellLevel = new()
    {
        { 1, 30 },
        { 2, 100 },
        { 3, 300 },
        { 4, 600 },
        { 5, 1200 },
        { 6, 2000 },
        { 7, 3500 },
        { 8, 5000 },
        { 9, 8000 }
    };

    static readonly Dictionary<int, ItemRarity> ScrollRarityBySpellLevel = new()
    {
        { 1, ItemRarity.Common },
        { 2, ItemRarity.Common },
        { 3, ItemRarity.Uncommon },
        { 4, ItemRarity.Uncommon },
        { 5, ItemRarity.Rare },
        { 6, ItemRarity.Rare },
        { 7, ItemRarity.VeryRare },
        { 8, ItemRarity.VeryRare },
        { 9, ItemRarity.Legendary }
    };

    static readonly Dictionary<int, AssetReferenceSprite> ScrollIconsBySpellLevel = new()
    {
        { 1, Sprites.GetSprite("ScrollLevel1", Resources.ScrollLevel1, 123) },
        { 2, Sprites.GetSprite("ScrollLevel2", Resources.ScrollLevel2, 123) },
        { 3, Sprites.GetSprite("ScrollLevel3", Resources.ScrollLevel3, 123) },
        { 4, Sprites.GetSprite("ScrollLevel4", Resources.ScrollLevel4, 123) },
        { 5, Sprites.GetSprite("ScrollLevel5", Resources.ScrollLevel5, 123) },
        { 6, Sprites.GetSprite("ScrollLevel6", Resources.ScrollLevel6, 123) },
        { 7, Sprites.GetSprite("ScrollLevel7", Resources.ScrollLevel7, 123) },
        { 8, Sprites.GetSprite("ScrollLevel8", Resources.ScrollLevel8, 123) },
        { 9, Sprites.GetSprite("ScrollLevel9", Resources.ScrollLevel9, 123) }
    };

    private static readonly Dictionary<int, List<ItemDefinition>> ScrollsByLevel = [];

    private static readonly ItemFlagDefinition ScrollSpellFlag = BuildSpellFlag();

    internal static void LateLoad()
    {
        foreach (var spell in SpellsContext.Spells
                     .Where(s => s.SpellLevel > 0 && s.ContentPack == CeContentPackContext.CeContentPack))
        {
            BuildScroll(spell);
        }

        if (Main.Settings.AddNewScrollsToShops)
        {
            AddScrollsToMerchants();
        }

        foreach (var item in DatabaseRepository.GetDatabase<ItemDefinition>())
        {
            if (!IsScrollItem(item)) { continue; }

            item.ItemPresentation.ItemFlags.TryAdd(ScrollSpellFlag);
        }
    }

    private static void AddScrollsToMerchants()
    {
        foreach (var merchant in DatabaseRepository.GetDatabase<MerchantDefinition>())
        {
            AddScrollsToMerchant(merchant);
        }
    }

    internal static bool IsScrollItem(ItemDefinition item)
    {
        return item != null
               && item is { isUsableDevice: true }
               && item.UsableDeviceDescription.UsableDeviceTags.Contains(TagsDefinitions.UsableDeviceTagScroll);
    }

    internal static void AddScrollsToMerchant(MerchantDefinition merchant)
    {
        Dictionary<int, StockUnitDescription> stockByLevel = [];
        foreach (var stock in merchant.stockUnitDescriptions)
        {
            var item = stock.ItemDefinition;
            if (item == null || !IsScrollItem(item))
            {
                continue;
            }

            var spellLevel = GetScrollSpell(item).SpellLevel;
            if (stockByLevel.ContainsKey(spellLevel)) { continue; }

            stockByLevel[spellLevel] = stock;
            
            //Since no vanilla shop has 9th level scrolls, add them alongside 8th level ones
            if (spellLevel == 8) { stockByLevel[9] = stock; }
        }

        foreach (var pair in stockByLevel)
        {
            foreach (var scroll in ScrollsByLevel[pair.Key])
            {
                merchant.StockUnitDescriptions.Add(StockUnitDescriptionBuilder.From(pair.Value)
                    .SetItem(scroll)
                    .Build());
            }
        }
    }

    internal static SpellDefinition GetScrollSpell(ItemDefinition item)
    {
        return IsScrollItem(item)
            ? item.UsableDeviceDescription.deviceFunctions[0].spellDefinition
            : null;
    }

    internal static void SetupScrollSpellImage(Transform t, SpellDefinition spell)
    {
        if (!t) { return; }

        var img = t.Find("IncompatibleImage").GetComponent<Image>();
        var tooltip = t.GetComponent<GuiTooltip>();
        if (spell)
        {
            img.color = Color.white;
            img.sprite = Gui.LoadAssetSync<Sprite>(spell.GuiPresentation.SpriteReference);
            ServiceRepository.GetService<IGuiWrapperService>()
                .GetGuiSpellDefinition(spell.Name)
                .SetupTooltip(tooltip);
        }
        else if (img.sprite)
        {
            Gui.ReleaseAddressableAsset(img.sprite);
            img.sprite = null;
            tooltip.Clear();
        }
    }

    private static void BuildScroll(SpellDefinition spell)
    {
        var spellLevel = spell.SpellLevel;

        if (!ScrollsByLevel.TryGetValue(spellLevel, out var scrolls))
        {
            scrolls = [];
            ScrollsByLevel[spellLevel] = scrolls;
        }

        scrolls.Add(ItemDefinitionBuilder.Create(ItemDefinitions.ScrollFly, $"Scroll_{spell.Name}")
            .SetGuiPresentation("Item/&Scroll_Of_Title", "Item/&Scroll_Of_Description",
                sprite: ScrollIconsBySpellLevel[spellLevel])
            .SetGold(ScrollCostBySpellLevel[spellLevel])
            .SetItemRarity(ScrollRarityBySpellLevel[spellLevel])
            .SetUsableDeviceDescription(new UsableDeviceDescriptionBuilder()
                .SetUsage(EquipmentDefinitions.ItemUsage.Single)
                .SetTags(TagsDefinitions.UsableDeviceTagScroll)
                .AddFunctions(new DeviceFunctionDescriptionBuilder()
                    .SetUsage(EquipmentDefinitions.ItemUsage.Single)
                    .SetSpell(spell)
                    .Build())
                .Build())
            .AddToDB());
    }

    private static ItemFlagDefinition BuildSpellFlag()
    {
        return ItemFlagDefinitionBuilder
            .Create("ItemFlagSpellIcon")
            .SetGuiPresentationNoContent()
            .AddCustomSubFeatures(new RecipeHelper.TooltipModifier<ItemDefinition>(
                (tooltip, img, obj, definition, context) =>
                {
                    if (!SettingsContext.GuiModManagerInstance.ShowSpellIconOnScrolls) { return; }

                    var spell = GetScrollSpell(definition);

                    if (!spell) { return; }


                    if (img)
                    {
                        if (img.sprite)
                        {
                            Gui.ReleaseAddressableAsset(img.sprite);
                            img.sprite = null;
                        }

                        var spriteReference = spell.GuiPresentation.SpriteReference;
                        if (spriteReference != null && spriteReference.RuntimeKeyIsValid())
                        {
                            img.sprite = Gui.LoadAssetSync<Sprite>(spriteReference);
                            if (obj)
                            {
                                obj.gameObject.SetActive(true);
                                obj.localScale = new Vector3(2f, 2f, 1f);
                            }
                        }
                    }

                    if (tooltip)
                    {
                        ServiceRepository.GetService<IGuiWrapperService>()
                            .GetGuiSpellDefinition(spell.Name)
                            .SetupTooltip(tooltip, context);
                    }
                }))
            .AddToDB();
    }
}
