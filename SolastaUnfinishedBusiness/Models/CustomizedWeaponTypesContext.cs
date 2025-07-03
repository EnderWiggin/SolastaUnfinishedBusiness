using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Api.ModKit.Utility;
using SolastaUnfinishedBusiness.CustomUI;
using UnityEngine.AddressableAssets;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.ItemDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static class CustomizedWeaponTypesContext
{
    private static Dictionary<ItemDefinition, List<string>> _available;
    private static Dictionary<ItemDefinition, List<TransformData>> _transforms;

    internal static Dictionary<ItemDefinition, List<string>> AvailableTransforms =>
        _available ??= BuildAvailableTransforms();


    private static Dictionary<ItemDefinition, List<TransformData>> Transformations =>
        _transforms ??= BuildTransforms();

    internal static void LateLoad()
    {
        foreach (var pair in Transformations)
        {
            var item = pair.Key;

            var idx = Main.Settings.WeaponTweakedTypes.GetValueOrDefault(item.Name);
            if (idx != 0) { DoTransform(item, idx); }
        }
    }

    private static Dictionary<ItemDefinition, List<TransformData>> BuildTransforms()
    {
        return new()
        {
            {
                DLC3_Legendary_Battleaxe_Skullcleaver, [
                    TransformData.Base(DLC3_Legendary_Battleaxe_Skullcleaver)
                ]
            },
            {
                DLC3_Legendary_Dagger_SuddenDeath, [
                    TransformData.Base(DLC3_Legendary_Dagger_SuddenDeath)
                ]
            },
            {
                DLC3_Legendary_Greataxe_Imperator, [
                    TransformData.Base(DLC3_Legendary_Greataxe_Imperator)
                ]
            },
            {
                DLC3_Legendary_HeavyCrossbow_Driller, [
                    TransformData.Base(DLC3_Legendary_HeavyCrossbow_Driller),
                    TransformData.From(CustomWeaponsContext.HandXbow)
                ]
            },
            {
                DLC3_Legendary_LightCrossbow_Decadence, [
                    TransformData.Base(DLC3_Legendary_LightCrossbow_Decadence)
                ]
            },
            {
                DLC3_Legendary_Longsword_Anvil, [
                    TransformData.Base(DLC3_Legendary_Longsword_Anvil),
                    TransformData.From(Greatsword, new CustomScale(1.3f, 1f, 1.5f))
                ]
            },
            {
                DLC3_Legendary_Quarterstaff_Purity, [
                    TransformData.Base(DLC3_Legendary_Quarterstaff_Purity)
                ]
            },
            {
                DLC3_Legendary_Shortsword_Grievance, [
                    TransformData.Base(DLC3_Legendary_Shortsword_Grievance)
                ]
            },
            {
                DLC3_Legendary_Warhammer_Glacier, [
                    TransformData.Base(DLC3_Legendary_Warhammer_Glacier)
                ]
            }
        };
    }

    private static Dictionary<ItemDefinition, List<string>> BuildAvailableTransforms()
    {
        return Transformations.OrderBy(x => GuiItemTweaks.FormatTitle(x.Key))
            .ToDictionary(x => x.Key, x => x.Value.Select(t => t.Title).ToList());
    }

    internal static void SetTransform(ItemDefinition item, int index)
    {
        var current = Main.Settings.WeaponTweakedTypes.GetValueOrDefault(item.Name);
        if (current == index) { return; }

        if (index <= 0)
        {
            Main.Settings.WeaponTweakedTypes.Remove(item.Name);
        }
        else
        {
            Main.Settings.WeaponTweakedTypes[item.Name] = index;
        }

        DoTransform(item, index);
    }

    private static void DoTransform(ItemDefinition item, int index)
    {
        if (!Transformations.TryGetValue(item, out var configs) || index < 0 || index >= configs.Count)
        {
            //invalid transform config was passed - clear settings for this item
            Main.Settings.WeaponTweakedTypes.Remove(item.Name);
            return;
        }

        configs[index].Apply(item, configs[0]);
    }

    private class TransformData
    {
        internal readonly string Title;

        private readonly AssetReferenceSprite _sprite;
        private readonly float _weight;
        private readonly string _weaponType;
        private readonly List<string> _weaponTags;

        private readonly string _ammunitionType;
        private readonly int _reachRange;
        private readonly int _closeRange;
        private readonly int _maxRange;

        private readonly RuleDefinitions.DieType _dieType;
        private readonly int _diceNumber;
        private readonly string _damageType;
        private readonly int _bonusDamage;
        private readonly bool _versatile;
        private readonly RuleDefinitions.DieType _versatileDieType;

        [CanBeNull] private readonly CustomScale _scale;
        [CanBeNull] private readonly ItemPresentation _presentation;

        private TransformData(ItemDefinition into,
            [CanBeNull] CustomScale scale,
            [CanBeNull] ItemPresentation presentation,
            [CanBeNull] AssetReferenceSprite sprite)
        {
            var src = into.WeaponDescription;

            Title = Gui.Localize(src.WeaponTypeDefinition.GuiPresentation.Title);

            _weaponType = src.weaponType;
            _weaponTags = [.. src.WeaponTags];

            _ammunitionType = src.ammunitionType;
            _reachRange = src.reachRange;
            _closeRange = src.closeRange;
            _maxRange = src.maxRange;

            _weight = into.weight;

            var newDamage = src.EffectDescription.FindFirstDamageForm();

            _dieType = newDamage.DieType;
            _diceNumber = newDamage.DiceNumber;
            _damageType = newDamage.DamageType;
            _bonusDamage = newDamage.bonusDamage;
            _versatile = newDamage.versatile;
            _versatileDieType = newDamage.versatileDieType;

            _sprite = sprite;
            _scale = scale;
            if (presentation != null)
            {
                _presentation = new ItemPresentation(presentation);
                _presentation.ItemFlags.Clear();
            }
        }

        internal static TransformData Base(ItemDefinition item)
        {
            return new TransformData(item, null, item.ItemPresentation, item.GuiPresentation.SpriteReference);
        }

        internal static TransformData From(ItemDefinition item,
            [CanBeNull] CustomScale scale = null,
            [CanBeNull] ItemDefinition presentation = null)
        {
            return new TransformData(item, scale, presentation?.ItemPresentation,
                presentation?.GuiPresentation.SpriteReference);
        }

        internal void Apply(ItemDefinition item, TransformData def)
        {
            var weapon = item.WeaponDescription;

            weapon.weaponType = _weaponType;
            weapon.weaponTags = [.. _weaponTags];

            weapon.ammunitionType = _ammunitionType;
            weapon.reachRange = _reachRange;
            weapon.closeRange = _closeRange;
            weapon.maxRange = _maxRange;

            item.weight = _weight;

            var damage = weapon.EffectDescription.FindFirstDamageForm();

            damage.dieType = _dieType;
            damage.diceNumber = _diceNumber;
            damage.damageType = _damageType;
            damage.bonusDamage = _bonusDamage;
            damage.versatile = _versatile;
            damage.versatileDieType = _versatileDieType;

            item.SetSubFeatureOfType<CustomScale>(_scale);

            var presentation = _presentation ?? def._presentation;
            if (presentation != null)
            {
                if (item.ItemPresentation.ItemFlags.Count > 0)
                {
                    presentation = new ItemPresentation(presentation);
                    presentation.ItemFlags.SetRange(item.ItemPresentation.ItemFlags);
                }

                item.itemPresentation = presentation;
            }

            item.GuiPresentation.spriteReference = _sprite ?? def._sprite ?? item.GuiPresentation.SpriteReference;
        }
    }
}
