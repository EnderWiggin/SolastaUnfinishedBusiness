using JetBrains.Annotations;

namespace SolastaUnfinishedBusiness.Api.GameExtensions;

[PublicAPI]
public static class RulesetAttackModeExtensions
{
    internal static RulesetAttackMode Clone(this RulesetAttackMode mode)
    {
        var newMode = RulesetAttackMode.AttackModesPool.Get();
        newMode.Copy(mode);

        return newMode;
    }

    internal static void Return(this RulesetAttackMode mode)
    {
        RulesetAttackMode.AttackModesPool.Return(mode);
    }
}
