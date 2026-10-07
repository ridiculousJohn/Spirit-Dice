namespace Dice.Engine
{
    public enum ModifierInspectionStatus
    {
        WouldActivate, ConditionNotMet, ActivationLimitReached, DifferentPhase, EncounterNotPlaying
    }

    /** Eligibility for the current hand's PlayHand phase, with condition and activation limits reported independently. */
    public sealed class ModifierInspection
    {
        public long InstanceId { get; }
        public string DefinitionId { get; }
        public ModifierPhase Phase { get; }
        public ModifierInspectionStatus Status { get; }
        public bool? ConditionMet { get; }
        public bool ActivationLimitReached { get; }

        internal ModifierInspection(ModifierInstanceState instance, ModifierDefinition definition,
            ModifierInspectionStatus status, bool? conditionMet, bool activationLimitReached)
        {
            InstanceId = instance.InstanceId;
            DefinitionId = definition.Id;
            Phase = definition.Phase;
            Status = status;
            ConditionMet = conditionMet;
            ActivationLimitReached = activationLimitReached;
        }
    }
}
