using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Behaviors;
using SolastaUnfinishedBusiness.Behaviors.Specific;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Interfaces;
using SolastaUnfinishedBusiness.Spells;
using SolastaUnfinishedBusiness.Subclasses;
using static ActionDefinitions;
using static RuleDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;

namespace SolastaUnfinishedBusiness.Models;

public static class SmiteSpells2024Context
{
    private static readonly List<SpellDefinition> AlLSmiteSpells = [];

    internal static readonly List<SpellDefinition> SmiteSpells =
    [
        SpellDefinitions.BrandingSmite
    ];

    internal static readonly List<FeatureDefinitionAdditionalDamage> SmiteDamages =
    [
        FeatureDefinitionAdditionalDamages.AdditionalDamageBrandingSmite
    ];

    internal static readonly List<ConditionDefinition> SmiteConditions = [];

    private static readonly ConditionDefinition ConditionMarkUsedFreeSmite = ConditionDefinitionBuilder
        .Create("ConditionMarkUsedFreeSmite")
        .SetGuiPresentationNoContent(true)
        .SetSilent(Silent.Always)
        .SetSpecialDuration(DurationType.UntilLongRest)
        .AddToDB();

    internal static void LateLoad()
    {
        AlLSmiteSpells.Add(Tabletop2024Context.DivineSmiteSpell);
        AlLSmiteSpells.Add(SpellDefinitions.BrandingSmite);
        AlLSmiteSpells.Add(SpellsContext.WrathfulSmite);
        AlLSmiteSpells.Add(SpellsContext.ThunderousSmite);
        AlLSmiteSpells.Add(SpellsContext.SearingSmite);
        AlLSmiteSpells.Add(SpellsContext.StaggeringSmite);
        AlLSmiteSpells.Add(SpellsContext.BanishingSmite);
        AlLSmiteSpells.Add(SpellsContext.BlindingSmite);

        SwitchSmiteSpells();
    }

    internal static void SwitchSmiteSpells()
    {
        if (Main.Settings.EnableSmiteSpells2024)
        {
            SmiteSpells.ForEach(SwitchSmiteSpellOn);
            SmiteDamages.ForEach(SwitchSmiteDamageOn);
            SmiteConditions.ForEach(SwitchSmiteConditionOn);

            SpellBuilders.PowerThunderousSmite.activationTime = ActivationTime.OnAttackHitAuto;
            SpellBuilders.AdditionalDamageWrathfulSmite.specificDamageType = DamageTypeNecrotic;
            SpellsContext.WrathfulSmite.schoolOfMagic = SchoolNecromancy;
        }
        else
        {
            SmiteSpells.ForEach(SwitchSmiteSpellOff);
            SmiteDamages.ForEach(SwitchSmiteDamageOff);
            SmiteConditions.ForEach(SwitchSmiteConditionOff);

            SpellBuilders.PowerThunderousSmite.activationTime = ActivationTime.OnAttackHitMeleeAuto;
            SpellBuilders.AdditionalDamageWrathfulSmite.specificDamageType = DamageTypePsychic;
            SpellsContext.WrathfulSmite.schoolOfMagic = SchoolEvocation;
        }

        Global.RefreshControlledCharacter();

        return;

        static void SwitchSmiteSpellOn(SpellDefinition spell)
        {
            spell.castingTime = ActivationTime.OnAttackHit;
            spell.requiresConcentration = false;
            spell.effectDescription.speedParameter = -1;
        }

        static void SwitchSmiteSpellOff(SpellDefinition spell)
        {
            spell.castingTime = ActivationTime.BonusAction;
            spell.requiresConcentration = true;
            spell.effectDescription.speedParameter = 4.5f;
        }

        static void SwitchSmiteDamageOn(FeatureDefinitionAdditionalDamage damage)
        {
            damage.requiredProperty = RestrictedContextRequiredProperty.None;

            damage.SetSubFeatureOfType<ValidateSmiteDamageForDemonHunter>(null);
        }

        static void SwitchSmiteDamageOff(FeatureDefinitionAdditionalDamage damage)
        {
            damage.requiredProperty = RestrictedContextRequiredProperty.MeleeWeapon;
            
            // Add custom validator to allow Oath of Demon Hunter to use crossbows in non-2024 mode
            damage.SetSubFeatureOfType<ValidateSmiteDamageForDemonHunter>(new ValidateSmiteDamageForDemonHunter());
        }


        static void SwitchSmiteConditionOn(ConditionDefinition condition)
        {
            condition.silentWhenAdded = true;
            condition.silentWhenRemoved = true;
        }

        static void SwitchSmiteConditionOff(ConditionDefinition condition)
        {
            condition.silentWhenAdded = false;
            condition.silentWhenRemoved = false;
        }
    }

    internal static IEnumerator OnAttackHitConfirmed(GameLocationBattleManager battleManager,
        GameLocationCharacter attacker,
        GameLocationCharacter defender,
        RulesetAttackMode attackMode,
        bool criticalHit,
        bool rangedAttack)
    {
        if (attackMode == null) { yield break; }

        var weapon = attackMode.SourceDefinition as ItemDefinition;
        
        //Only attacks with melee weapons or unarmed are supported (can be thrown melee weapon)
        if (rangedAttack
            && weapon != null
            && weapon.WeaponDescription?.WeaponTypeDefinition?.WeaponProximity != AttackProximity.Melee)
        {
            //Demon Hunter can use Smite spells on crossbows
            var isDemonHunterWeapon = OathOfDemonHunter.IsOathOfDemonHunterWeapon(attackMode, null, attacker.RulesetCharacter);
            if (!isDemonHunterWeapon)
            {
                yield break;
            }
        }

        var attackerCharacter = attacker.RulesetCharacter;

        //Not a crit and smite toggle is enabled but turned off - skip
        if (!criticalHit && Main.Settings.AddPaladinSmiteToggle &&
            !attackerCharacter.IsToggleEnabled((Id)ExtraActionId.PaladinSmiteToggle))
        {
            yield break;
        }

        //Cannot cast or is silenced - skip
        if (!attacker.RulesetCharacter.IsComponentVerbalValid(Tabletop2024Context.DivineSmiteSpell, out _)
            || !attacker.RulesetCharacter.CanCastSpells())
        {
            yield break;
        }

        //Check for available BA cast spell
        if (attacker.GetActionStatus(Id.CastBonus, ActionScope.Battle) != ActionStatus.Available)
        {
            yield break;
        }

        var ruleService = ServiceRepository.GetService<IRulesetImplementationService>();
        var actionService = ServiceRepository.GetService<IGameLocationActionService>();

        //Wrong service, can't properly add custom reactions - should never trigger
        if (actionService is not GameLocationActionManager actionManager) { yield break; }

        var spellPoints = attackerCharacter.IsSpellPointsEnabled();
        var hasFreeUseDivineSmite = attackerCharacter.HasAnyFeature(Tabletop2024Context.DivineSmite2024AutoSpell);
        var freeUseDivineSmiteAvailable = !attackerCharacter.HasAnyConditionOfType(ConditionMarkUsedFreeSmite.Name);
        var smites = GetSmiteOptions(attackerCharacter, false, spellPoints,
            hasFreeUseDivineSmite && freeUseDivineSmiteAvailable);

        var availableSmites = smites.Where(x => x.Available).ToList();
        //No smites
        if (availableSmites.Count == 0) { yield break; }

        SpellDefinition smite;
        RulesetSpellRepertoire repertoire;

        CharacterActionParams reactionParams;
        int pendingRequests;
        //Need to choose smite
        if (availableSmites.Count > 1)
        {
            pendingRequests = actionManager.PendingReactionRequestGroups.Count;
            reactionParams = new CharacterActionParams(attacker, (Id)ExtraActionId.DoNothingFree)
            {
                IsReactionEffect = true
            };

            var selectRequest = new ReactionRequestSelectSmiteSpell(smites, reactionParams, attacker, defender);
            actionManager.AddInterruptRequest(selectRequest);

            yield return battleManager.WaitForReactions(attacker, actionManager, pendingRequests);

            //didn't confirm spell selection
            if (!reactionParams.ReactionValidated) { yield break; }

            var option = selectRequest.SelectedSubOption;
            smite = smites[option].Spell;
            repertoire = smites[option].Repertoire;
        }
        else
        {
            smite = availableSmites[0].Spell;
            repertoire = availableSmites[0].Repertoire;
        }

        pendingRequests = actionManager.PendingReactionRequestGroups.Count;

        reactionParams = battleManager.PrepareReactionParams(attacker, attacker, Id.CastBonus);
        reactionParams.IntParameter = 0;
        reactionParams.SpellRepertoire = repertoire;
        reactionParams.RulesetEffect =
            ruleService.InstantiateEffectSpell(attackerCharacter, repertoire, smite, 0, false);
        reactionParams.IsReactionEffect = true;

        hasFreeUseDivineSmite = hasFreeUseDivineSmite && smite == Tabletop2024Context.DivineSmiteSpell;
        var slotRequest = new ReactionRequestSelectSmiteSlot(reactionParams, hasFreeUseDivineSmite,
            freeUseDivineSmiteAvailable);
        actionManager.AddInterruptRequest(slotRequest);

        yield return battleManager.WaitForReactions(attacker, actionManager, pendingRequests);

        //didn't confirm slot selection
        if (!reactionParams.ReactionValidated || slotRequest.SelectedSubOption > 0) { yield break; }

        attackerCharacter.InflictCondition(ConditionMarkUsedFreeSmite.Name, DurationType.UntilLongRest, 0,
            TurnOccurenceType.EndOfTurn, AttributeDefinitions.TagEffect, attackerCharacter.Guid,
            attackerCharacter.CurrentFaction.Name, 1, attackerCharacter.Name, 0, 0, 0);
    }

    private static readonly HashSet<SpellDefinition> SpellsToBrowse = [];

    private static List<SmiteOption> GetSmiteOptions(RulesetCharacter character, bool divineSmiteOnly, bool spellPoints,
        bool canFreeUseDivineSmite)
    {
        List<SmiteOption> options = [];

        foreach (var repertoire in character.SpellRepertoires)
        {
            SpellsToBrowse.Clear();

            if (repertoire.SpellCastingFeature == null)
            {
                SpellsToBrowse.AddRange(repertoire.KnownSpells);
            }
            else if (repertoire.SpellCastingFeature.SpellReadyness == SpellReadyness.Prepared)
            {
                SpellsToBrowse.AddRange(repertoire.PreparedSpells);
            }
            else if (repertoire.SpellCastingFeature.SpellReadyness == SpellReadyness.AllKnown)
            {
                SpellsToBrowse.AddRange(repertoire.KnownSpells);
                SpellsToBrowse.AddRange(repertoire.AutoPreparedSpells);
            }

            SpellsToBrowse.AddRange(repertoire.ExtraSpellsByTag.SelectMany(x => x.Value));

            //Find smite spells
            foreach (var spell in SpellsToBrowse)
            {
                if (spell.ActivationTime == ActivationTime.OnAttackHit
                    && (!divineSmiteOnly || spell == Tabletop2024Context.DivineSmiteSpell))
                {
                    options.Add(new SmiteOption(spell, repertoire, CanCast(repertoire, spell)));
                }
            }
        }

        options.Sort((a, b) => a.Spell.spellLevel == b.Spell.spellLevel
            ? string.Compare(a.Spell.FormatTitle(), b.Spell.FormatTitle(), StringComparison.CurrentCulture)
            : a.Spell.spellLevel.CompareTo(b.Spell.spellLevel));

        return options;

        bool CanCast(RulesetSpellRepertoire repertoire, SpellDefinition spell)
        {
            return character.AreSpellComponentsValid(spell)
                   && ((canFreeUseDivineSmite && spell == Tabletop2024Context.DivineSmiteSpell)
                       || (spellPoints
                           ? SpellPointsContext.CanCastSpellOfLevel(character, spell.SpellLevel)
                           : repertoire.CanCastSpellOfLevel(spell.SpellLevel)));
        }
    }

    internal static bool HasSmites(this RulesetCharacter character)
    {
        if (character is not RulesetCharacterHero hero) { return false; }

        if (!Main.Settings.AddPaladinSmiteToggle) { return false; }

        if (hero.ClassesHistory.Contains(CharacterClassDefinitions.Paladin)) { return true; }

        if (!Main.Settings.EnableSmiteSpells2024) { return false; }

        return hero.SpellRepertoires.Any(repertoire =>
            AlLSmiteSpells.Any(spell => repertoire.HasKnowledgeOfSpell(spell) && repertoire.IsSpellReady(spell)));
    }
}

internal readonly struct SmiteOption(SpellDefinition spell, RulesetSpellRepertoire repertoire, bool available)
{
    public SpellDefinition Spell { get; } = spell;
    public RulesetSpellRepertoire Repertoire { get; } = repertoire;
    public bool Available { get; } = available;
}

internal class ReactionRequestSelectSmiteSpell : ReactionRequest
{
    internal const string Name = "SmiteSpellSelect";

    internal readonly List<SmiteOption> Smites;
    private readonly GuiCharacter _attacker;
    private readonly GuiCharacter _defender;
    private int _selectedOption;

    public ReactionRequestSelectSmiteSpell(List<SmiteOption> smites, CharacterActionParams reactionParams,
        GameLocationCharacter attacker, GameLocationCharacter defender) : base(Name, reactionParams)
    {
        Smites = smites;
        _attacker = new GuiCharacter(attacker);
        _defender = new GuiCharacter(defender);
        BuildSuboptions();
    }

    private void BuildSuboptions()
    {
        SubOptionsAvailability.Clear();

        reactionParams.SpellRepertoire = new RulesetSpellRepertoire();

        for (var index = 0; index < Smites.Count; index++)
        {
            var smite = Smites[index];
            SubOptionsAvailability.Add(index, smite.Available);
        }

        foreach (var pair in SubOptionsAvailability.Where(pair => pair.Value))
        {
            SelectSubOption(pair.Key);
            break;
        }
    }

    public override void SelectSubOption(int option)
    {
        ReactionParams.RulesetEffect?.Terminate(false);
        _selectedOption = option;
    }

    public override int SelectedSubOption => _selectedOption;

    public override string SuboptionTag => "DivineSmiteSelect";

    public override string FormatTitle()
    {
        return Gui.Localize("Reaction/&SpendSpellSlotDivineSmiteReactTitle");
    }

    public override string FormatDescription()
    {
        return Gui.Format("Reaction/&ReactionDivineSmite2024SelectDescription", _attacker.Name, _defender.Name);
    }

    public override string FormatReactTitle()
    {
        return Gui.Localize("Feedback/&AdditionalDamageDivineSmiteFormat");
    }

    public override string FormatReactDescription()
    {
        return Gui.Localize("Reaction/&ReactionDivineSmite2024SelectReactDescription");
    }
}

internal class ReactionRequestSelectSmiteSlot : ReactionRequestCastSpell
{
    private readonly bool _hasFreeUse;
    private readonly bool _freeUseAvailable;
    public const string Name = "SmiteSlotSelect";
    private readonly string _spellName;
    private int _spellLevel;
    private int _selectedOption = -1;

    public ReactionRequestSelectSmiteSlot(CharacterActionParams actionParams, bool hasFreeUse, bool freeUseAvailable)
        : base(Name, actionParams)
    {
        _hasFreeUse = hasFreeUse;
        _freeUseAvailable = freeUseAvailable;
        var spellEffect = (ReactionParams.RulesetEffect as RulesetEffectSpell)!;
        _spellName = spellEffect.SpellDefinition.GuiPresentation.Title;

        BuildSlotSubOptions();
    }

    private new void BuildSlotSubOptions()
    {
        _selectedOption = -1;
        SubOptionsAvailability.Clear();
        if (reactionParams.RulesetEffect is not RulesetEffectSpell spellEffect) { return; }

        var spellRepertoire = spellEffect.SpellRepertoire;
        var maxSpellLevel = spellRepertoire.MaxSpellLevelOfSpellCastingLevel;
        var spell = spellEffect.SpellDefinition;
        _spellLevel = spell.SpellLevel;

        var preSelected = -1;
        var index = 0;
        if (_hasFreeUse)
        {
            index++;
            SubOptionsAvailability.Add(0, _freeUseAvailable);
            if (_freeUseAvailable) { preSelected = 0; }
        }

        for (var slotLevel = _spellLevel; slotLevel <= maxSpellLevel; ++slotLevel, ++index)
        {
            spellRepertoire.GetSlotsNumber(slotLevel, out var remaining, out _);
            var hasSlots = remaining > 0;
            SubOptionsAvailability.Add(slotLevel, hasSlots);
            if (preSelected < 0 && hasSlots) { preSelected = index; }
        }

        if (preSelected >= 0) { SelectSubOption(preSelected); }
    }

    public override void SelectSubOption(int option)
    {
        if (reactionParams.RulesetEffect is not RulesetEffectSpell spellEffect) { return; }

        _selectedOption = option;
        var slotLevel = _spellLevel + option;
        if (_hasFreeUse && option == 0)
        {
            slotLevel += 1;
        }

        spellEffect.SlotLevel = slotLevel;
    }

    public override int SelectedSubOption => _selectedOption;
    public override string SuboptionTag => "DivineSmite";

    public override string FormatTitle()
    {
        return Gui.Localize(_spellName);
    }

    public override string FormatDescription()
    {
        return "";
    }

    public override string FormatReactTitle()
    {
        return Gui.Localize("Feedback/&AdditionalDamageDivineSmiteFormat");
    }

    public override string FormatReactDescription()
    {
        return Gui.Format("Reaction/&ReactionDivineSmite2024SlotReactDescription", _spellName);
    }
}

internal sealed class ValidateSmiteDamageForDemonHunter : IValidateContextInsteadOfRestrictedProperty
{
    public (OperationType, bool) ValidateContext(
        BaseDefinition definition,
        IRestrictedContextProvider provider,
        RulesetCharacter character,
        ItemDefinition itemDefinition,
        bool rangedAttack,
        RulesetAttackMode attackMode,
        RulesetEffect rulesetEffect)
    {
        if (OathOfDemonHunter.IsOathOfDemonHunterWeapon(attackMode, attackMode.sourceObject as RulesetItem, character))
        {
            // Check if Light Energy Crossbow Bolt condition is active
            var conditionName = $"Condition{OathOfDemonHunter.Name}LightEnergyCrossbowBoltActive";
            
            if (character.HasConditionOfType(conditionName))
            {
                return (OperationType.Set, true);
            }
            
            // Condition not active, don't allow Smite with crossbow
            return (OperationType.Set, false);
        }

        return (OperationType.Ignore, false);
    }
}
