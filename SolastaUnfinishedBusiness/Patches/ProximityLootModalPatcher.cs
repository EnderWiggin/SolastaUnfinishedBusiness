using System;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.ItemCrafting;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
internal static class ProximityLootModalPatcher
{
    [HarmonyPatch(typeof(ProximityLootModal), nameof(ProximityLootModal.OnBeginShow))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    internal static class OnBeginShow_Patch
    {
        [UsedImplicitly]
        internal static void Prefix([NotNull] ProximityLootModal __instance)
        {
            InitCustomButtons(__instance);
        }

        private static void InitCustomButtons(ProximityLootModal modal)
        {
            var parent = modal.lootAllButton.transform.parent;

            if (parent.Find("LootMoneyButton") != null) { return; }

            var prefab = modal.lootAllButton.gameObject;

            parent.GetComponent<HorizontalLayoutGroup>().spacing = 10;

            foreach (var element in parent.Find("CloseButton").GetComponents<LayoutElement>())
            {
                if (element.minWidth > 100) { element.minWidth = 100; }
            }

            //Loot all money
            var asset = Object.Instantiate(prefab, parent, false);
            var button = asset.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => OnLootCb(modal, OnlyMoney));
            asset.transform.Find("Label").GetComponent<GuiLabel>().Text = "UI/&LootAllGoldTitle";
            asset.transform.Find("Background").GetComponent<GuiTooltip>().Content = "UI/&LootAllGoldTooltip";
            foreach (var element in asset.GetComponents<LayoutElement>())
            {
                if (element.minWidth > 100) { element.minWidth = 100; }
            }

            asset.name = "LootMoneyButton";

            //Loot all ingredients
            asset = Object.Instantiate(prefab, parent, false);
            button = asset.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => OnLootCb(modal, OnlyLightIngredients));
            asset.transform.Find("Label").GetComponent<GuiLabel>().Text = "UI/&LootAllIngredientsTitle";
            asset.transform.Find("Background").GetComponent<GuiTooltip>().Content = "UI/&LootAllIngredientsTooltip";
            foreach (var element in asset.GetComponents<LayoutElement>())
            {
                if (element.minWidth > 100) { element.minWidth = 100; }
            }

            asset.name = "LootIngredientsButton";

            //Loot all ingredients
            asset = Object.Instantiate(prefab, parent, false);
            button = asset.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => OnLootCb(modal, OnlyScrolls));
            asset.transform.Find("Label").GetComponent<GuiLabel>().Text = "UI/&LootAllScrollsTitle";
            asset.transform.Find("Background").GetComponent<GuiTooltip>().Content = "UI/&LootAllScrollsTooltip";
            foreach (var element in asset.GetComponents<LayoutElement>())
            {
                if (element.minWidth > 100) { element.minWidth = 100; }
            }

            asset.name = "LootScrollsButton";
        }

        private static bool OnlyMoney(ItemDefinition item)
        {
            return item.IsWealthPile;
        }

        private static bool OnlyLightIngredients(ItemDefinition item)
        {
            return item.ItemTags.Contains(TagsDefinitions.ItemTagIngredient) && item.weight <= 0;
        }

        private static bool OnlyScrolls(ItemDefinition item)
        {
            return ScrollsData.IsScrollItem(item);
        }

        private static void OnLootCb(LootEnumerationModal modal, Func<ItemDefinition, bool> filter)
        {
            if (modal.hasLootAllButtonBeenClicked || modal.itemsToLootCache.Count > 0) { return; }

            modal.hasLootAllButtonBeenClicked = true;
            var lootingHero = modal.LootingHero.RulesetCharacterHero;
            lootingHero.CharacterInventory.PersonalContainer.ClearStackedItems();
            var fullSuccess = true;
            var hasLooted = false;
            modal.itemsToLootCache.Clear();

            for (var index = 0; index < modal.groundSlots.Count; ++index)
            {
                var item = modal.groundSlots[index].EquipedItem;
                if (item == null) { continue; }

                var definition = item.ItemDefinition;
                if (!filter.Invoke(definition)) { continue; }

                modal.itemsToLootCache.Add(item);
                if (!modal.TryToLootSlotBox(modal.slotsTable.GetChild(index).GetComponent<InventorySlotBox>()))
                {
                    Main.Log2($"couldn't loot '{definition.FormatTitle()}':{item.guid} ");
                    fullSuccess = false;
                }
                else if (!hasLooted)
                {
                    hasLooted = true;
                }
            }

            ServiceRepository.GetService<ICommandService>()
                .AcknowledgePreviousCommandLocally(() => modal.CheckForPreviousLootAllCommandResult(lootingHero));

            if (!fullSuccess)
            {
                Gui.GuiService.ShowAlert(InventoryPanel.InventoryCannotLootAll, Gui.ColorBrokenWhite);
            }

            if (hasLooted)
            {
                modal.SignalItemInteraction();
            }

            modal.CharacterSelectionChanged();
        }
    }
}
