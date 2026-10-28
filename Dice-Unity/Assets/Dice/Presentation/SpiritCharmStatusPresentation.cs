using System;
using Dice.Engine;

namespace Dice.Presentation
{
    public enum SpiritCharmStatusState { Ready, WaitingCondition, Used, OnRoll, Inactive, Kept }

    /** Activation allowance and current eligibility describe separate aspects of an owned charm. */
    public readonly struct SpiritCharmStatusPresentation
    {
        public SpiritCharmStatusState State { get; }
        public int? RemainingUses { get; }
        public ModifierActivationLimit ActivationLimit { get; }

        private SpiritCharmStatusPresentation(SpiritCharmStatusState state, int? remainingUses,
            ModifierActivationLimit activationLimit)
        {
            State = state;
            RemainingUses = remainingUses;
            ActivationLimit = activationLimit;
        }

        public string GetDetailText(ModifierPhase phase, EncounterPhase encounterPhase, SpiritUiWording.CharmStatus wording)
        {
            if (wording == null) throw new ArgumentNullException(nameof(wording));
            switch (State)
            {
                case SpiritCharmStatusState.Ready: return wording.ready;
                case SpiritCharmStatusState.WaitingCondition: return wording.waiting;
                case SpiritCharmStatusState.Used:
                    return ActivationLimit == ModifierActivationLimit.OncePerStage ? wording.activatedThisStage : wording.activatedThisHand;
                case SpiritCharmStatusState.OnRoll:
                    return phase == ModifierPhase.InitialRoll ? wording.onInitialRoll : wording.afterReroll;
                case SpiritCharmStatusState.Inactive:
                    return encounterPhase == EncounterPhase.Completed ? wording.encounterEnded : wording.betweenStages;
                default: return "";
            }
        }

        public static SpiritCharmStatusPresentation Create(ModifierInstanceSnapshot instance,
            ModifierDefinition definition, EncounterPhase phase, ModifierInspection inspection)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            int? remaining = null;
            if (definition.ActivationLimit == ModifierActivationLimit.OncePerHand)
                remaining = instance.ActivationsThisHand == 0 ? 1 : 0;
            else if (definition.ActivationLimit == ModifierActivationLimit.OncePerStage)
                remaining = instance.ActivationsThisStage == 0 ? 1 : 0;

            // Counters remain authoritative between hands, when PlayHand inspection reports only encounter phase.
            SpiritCharmStatusState state;
            if (remaining == 0) state = SpiritCharmStatusState.Used;
            else if (phase != EncounterPhase.Playing) state = SpiritCharmStatusState.Inactive;
            else if (inspection == null || inspection.InstanceId != instance.InstanceId)
                state = SpiritCharmStatusState.Kept;
            else if (inspection.Status == ModifierInspectionStatus.EncounterNotPlaying)
                state = SpiritCharmStatusState.Inactive;
            else if (definition.Phase != ModifierPhase.PlayHand)
                state = SpiritCharmStatusState.OnRoll;
            else
            {
                switch (inspection.Status)
                {
                    case ModifierInspectionStatus.WouldActivate: state = SpiritCharmStatusState.Ready; break;
                    case ModifierInspectionStatus.ConditionNotMet: state = SpiritCharmStatusState.WaitingCondition; break;
                    case ModifierInspectionStatus.ActivationLimitReached: state = SpiritCharmStatusState.Used; break;
                    case ModifierInspectionStatus.DifferentPhase: state = SpiritCharmStatusState.OnRoll; break;
                    default: state = SpiritCharmStatusState.Kept; break;
                }
            }
            return new SpiritCharmStatusPresentation(state, remaining, definition.ActivationLimit);
        }
    }
}
