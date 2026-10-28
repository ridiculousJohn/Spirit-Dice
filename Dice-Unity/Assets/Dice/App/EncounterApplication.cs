using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dice.Engine;
using Dice.Presentation;

namespace Dice.App
{
    public sealed class EncounterApplication
    {
        /** The seed a fixed-seed run starts from until a configuration names another. */
        public const ulong DefaultSeed = 60;
        private readonly EncounterSession session;
        private IEncounterPresenter presenter;

        public EncounterSnapshot Snapshot => session.Snapshot;
        public Resolution InitialResolution => session.InitialResolution;

        public EncounterApplication(MechanicalCatalog catalog, string encounterId, ulong seed,
            IEnumerable<string> unlockedModifierIds = null, IEnumerable<string> discoveredFamilyIds = null)
        {
            session = EncounterSession.CreateEncounter(catalog, encounterId, seed, unlockedModifierIds, discoveredFamilyIds);
        }

        private EncounterApplication(EncounterSession restoredSession)
        {
            session = restoredSession;
        }

        public static bool TryRestore(MechanicalCatalog catalog, EncounterSaveData save,
            out EncounterApplication application, out RestoreEncounterResult result)
        {
            result = EncounterSession.RestoreEncounter(catalog, save);
            application = result.Success ? new EncounterApplication(result.Session) : null;
            return result.Success;
        }

        public void AttachPresenter(IEncounterPresenter value)
        {
            presenter = value;
            presenter?.Synchronize(session.Snapshot);
        }

        public Resolution Submit(EncounterAction action, long expectedRevision)
        {
            Resolution result = session.Execute(action, expectedRevision);
            if (result.Accepted) presenter?.Present(result);
            return result;
        }

        public HandPreview PreviewHandResolution() => session.PreviewHandResolution();
        public ReadOnlyCollection<ModifierInspection> InspectModifiersForPlayHand() => session.InspectModifiersForPlayHand();

        /** The hand preview and charm inspections a view shows; a failure the caller expects is reported, not thrown. */
        public EncounterDisplayPreview PreviewForDisplay(Func<Exception, bool> isExpectedFailure)
        {
            if (isExpectedFailure == null) throw new ArgumentNullException(nameof(isExpectedFailure));
            HandPreview preview = null;
            IReadOnlyList<ModifierInspection> inspections = Array.Empty<ModifierInspection>();
            string previewFailure = null;
            string inspectionFailure = null;
            // Display evaluation cannot invalidate a successfully synchronized engine state.
            try
            {
                if (session.Snapshot.Phase == EncounterPhase.Playing) preview = session.PreviewHandResolution();
            }
            catch (Exception exception) when (isExpectedFailure(exception))
            {
                previewFailure = exception.Message;
            }
            try { inspections = session.InspectModifiersForPlayHand(); }
            catch (Exception exception) when (isExpectedFailure(exception))
            {
                inspectionFailure = exception.Message;
            }
            return new EncounterDisplayPreview(preview, inspections, previewFailure, inspectionFailure);
        }

        public EncounterSaveData CaptureSave() => session.CaptureSave();
        public void MergeUnlockedModifiers(IEnumerable<string> modifierIds) => session.AddUnlockedModifiers(modifierIds);
        public void MergeDiscoveredFamilies(IEnumerable<string> familyIds) => session.AddDiscoveredFamilies(familyIds);
    }

    /** What a view shows of the hand to be played; the failures are developer detail of an unavailable part, or null. */
    public sealed class EncounterDisplayPreview
    {
        public HandPreview HandPreview { get; }
        public IReadOnlyList<ModifierInspection> ModifierInspections { get; }
        public string HandPreviewFailure { get; }
        public string ModifierInspectionFailure { get; }

        internal EncounterDisplayPreview(HandPreview handPreview, IReadOnlyList<ModifierInspection> modifierInspections,
            string handPreviewFailure, string modifierInspectionFailure)
        {
            HandPreview = handPreview;
            ModifierInspections = modifierInspections;
            HandPreviewFailure = handPreviewFailure;
            ModifierInspectionFailure = modifierInspectionFailure;
        }
    }
}
