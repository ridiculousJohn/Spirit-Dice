using System;

namespace Dice.Engine
{
    /**
     * What a charm does when it activates. Each effect kind is its own class and applies itself to the activation's
     * totals; effects are defined only in the engine.
     */
    public abstract class ModifierEffect
    {
        public long Amount { get; }

        /** The key the skin words this effect by; the effect's behaviour is its own Apply. */
        public abstract ModifierEffectKind Kind { get; }

        private protected ModifierEffect(long amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Amount = amount;
        }

        /** Adds this effect to the totals of one activation. */
        internal abstract void Apply(ModifierEffectTotals totals);
    }

    public sealed class AddBaseScoreEffect : ModifierEffect
    {
        public AddBaseScoreEffect(long amount) : base(amount) { }

        public override ModifierEffectKind Kind => ModifierEffectKind.AddBaseScore;

        internal override void Apply(ModifierEffectTotals totals) => totals.BaseScore = checked(totals.BaseScore + Amount);
    }

    public sealed class AddMultiplierEffect : ModifierEffect
    {
        public AddMultiplierEffect(long amount) : base(amount) { }

        public override ModifierEffectKind Kind => ModifierEffectKind.AddMultiplier;

        internal override void Apply(ModifierEffectTotals totals) => totals.Multiplier = checked(totals.Multiplier + Amount);
    }

    public sealed class AddRerollsEffect : ModifierEffect
    {
        public AddRerollsEffect(long amount) : base(amount)
        {
            if (amount > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(amount));
        }

        public override ModifierEffectKind Kind => ModifierEffectKind.AddRerolls;

        internal override void Apply(ModifierEffectTotals totals) => totals.Rerolls = checked(totals.Rerolls + (int)Amount);
    }

    /** What one activation of a charm adds: the sum of its effects. */
    internal sealed class ModifierEffectTotals
    {
        internal long BaseScore;
        internal long Multiplier;
        internal int Rerolls;

        internal static ModifierEffectTotals Of(ModifierDefinition modifier)
        {
            var totals = new ModifierEffectTotals();
            foreach (var effect in modifier.Effects) effect.Apply(totals);
            return totals;
        }
    }
}
