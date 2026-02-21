namespace SolastaUnfinishedBusiness.Interfaces;

public interface IFilterRulesetEffectTargets
{
    bool CanAffectTarget(RulesetEffect rulesetEffect, GameLocationCharacter caster, GameLocationCharacter target);
}
