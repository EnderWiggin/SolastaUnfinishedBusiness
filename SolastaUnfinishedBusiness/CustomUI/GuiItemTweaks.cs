using SolastaUnfinishedBusiness.ItemCrafting;

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
}
