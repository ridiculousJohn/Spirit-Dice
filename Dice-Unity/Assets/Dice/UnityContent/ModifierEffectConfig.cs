using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    /** A charm's authored effect. Each effect kind is its own config class and builds the engine effect it describes. */
    [Serializable]
    public abstract class ModifierEffectConfig
    {
        [SerializeField] private long amount = 1;

        protected ModifierEffectConfig() { }
        protected ModifierEffectConfig(long amount) => this.amount = amount;

        protected long Amount => amount;

        /** The engine effect with this config's amount; an amount outside its range fails the engine's validation. */
        public abstract ModifierEffect Build();
    }

    [Serializable]
    public sealed class AddBaseScoreEffectConfig : ModifierEffectConfig
    {
        public AddBaseScoreEffectConfig() { }
        public AddBaseScoreEffectConfig(long amount) : base(amount) { }

        public override ModifierEffect Build() => new AddBaseScoreEffect(Amount);
    }

    [Serializable]
    public sealed class AddMultiplierEffectConfig : ModifierEffectConfig
    {
        public AddMultiplierEffectConfig() { }
        public AddMultiplierEffectConfig(long amount) : base(amount) { }

        public override ModifierEffect Build() => new AddMultiplierEffect(Amount);
    }

    [Serializable]
    public sealed class AddRerollsEffectConfig : ModifierEffectConfig
    {
        public AddRerollsEffectConfig() { }
        public AddRerollsEffectConfig(long amount) : base(amount) { }

        public override ModifierEffect Build() => new AddRerollsEffect(Amount);
    }
}
