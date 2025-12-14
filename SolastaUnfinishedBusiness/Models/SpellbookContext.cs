using System.Collections.Generic;
using System.Linq;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;

namespace SolastaUnfinishedBusiness.Models;

internal static class SpellbookContext
{
    private static readonly Dictionary<string, SpellDefinition> WizardSubspellParent = [];

    private static readonly Dictionary<string, List<SpellDefinition>> MonsterSpellCache = [];

    private static readonly ItemDefinition SpellbookDefinition = GetDefinition<ItemDefinition>("Spellbook");

    private static void InitializeWizardSubspellParents()
    {
        if (WizardSubspellParent.Count != 0) { return; }

        foreach (var duplet in SpellListDefinitions.SpellListWizard.SpellsByLevel)
        {
            var spells = duplet.spells;
            foreach (var spell in spells)
            {
                if (!WizardSubspellParent.ContainsKey(spell.Name))
                {
                    WizardSubspellParent[spell.Name] = spell;
                    foreach (var subspell in spell.SubspellsList)
                    {
                        WizardSubspellParent[subspell.Name] = spell;
                    }
                }
            }
        }
    }

    internal static SpellDefinition GetSpellRoot(SpellDefinition spell)
    {
        InitializeWizardSubspellParents();

        return WizardSubspellParent.TryGetValue(spell.Name, out var root) ? root : spell;
    }

    internal static List<SpellDefinition> GetWizardSpells(MonsterDefinition monsterDef)
    {
        var result = new List<SpellDefinition>();

        if (monsterDef == null || monsterDef.Name == null) return result;
        if (MonsterSpellCache.TryGetValue(monsterDef.Name, out var spells)) { return spells; }

        if (monsterDef.features != null)
        {
            foreach (var feature in monsterDef.Features)
            {
                if (feature is FeatureDefinitionCastSpell featureCastSpell)
                {
                    if (featureCastSpell.SpellListDefinition != null)
                    {
                        foreach (var spellDuplet in featureCastSpell.SpellListDefinition.SpellsByLevel)
                        {
                            var level = spellDuplet.Level;
                            foreach (var spell in spellDuplet.Spells)
                            {
                                if (level > 0)
                                {
                                    var s = GetSpellRoot(spell);
                                    if (!result.Contains(s) && SpellListDefinitions.SpellListWizard.ContainsSpell(s))
                                        result.Add(s);
                                }
                            }
                        }
                    }
                }
            }
        }

        MonsterSpellCache[monsterDef.Name] = result;

        return result;
    }

    private static RulesetItemSpellbook MakeBlankSpellbook()
    {
        var service = ServiceRepository.GetService<IRulesetItemFactoryService>();
        var item = service.CreateStandardItem(SpellbookDefinition);
        if (item is RulesetItemSpellbook result) return result;

        return null;
    }

    internal static RulesetItem TryDropSpellbook(RulesetCharacterMonster monster)
    {
        if (!Main.Settings.EnemySpellcastersDropScribedSpellbooks) { return null;}

        if (monster?.Name == null || monster.MonsterDefinition == null || monster.droppedItems == null) { return null; }

        var spellList = GetWizardSpells(monster.MonsterDefinition);
        if (spellList.Count <= 0) { return null; }

        var spellbook = MakeBlankSpellbook();
        if (spellbook == null) { return null; }

        spellbook.ScribedSpells = spellList;
        spellbook.ScribedSpells.Sort(spellbook);
        spellbook.OwnerName = monster.Name;

        var pages = spellbook.GetAttribute(AttributeDefinitions.ItemSpellbookPages);
        if (pages != null)
        {
            pages.BaseValue -= spellList.Sum(s => s.SpellLevel);
            pages.Refresh();
        }

        return spellbook;
    }
}
