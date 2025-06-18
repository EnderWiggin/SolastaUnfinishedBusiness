using UnityEngine.AddressableAssets;

namespace SolastaUnfinishedBusiness.Api.GameExtensions;

public static class EffectParticleExtensions
{
    public static AssetReference ParticleEffectReference(this IMagicEffect effect)
    {
        return effect.EffectDescription.EffectParticleParameters.effectParticleReference;
    }

    public static AssetReference ParticleImpactReference(this IMagicEffect effect)
    {
        return effect.EffectDescription.EffectParticleParameters.impactParticleReference;
    }

    //TODO: add extensions for other particle references
}
