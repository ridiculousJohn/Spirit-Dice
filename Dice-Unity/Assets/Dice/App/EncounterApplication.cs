using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;

namespace Dice.App
{
    public sealed class EncounterApplication
    {
        private readonly EncounterSession session;
        private IEncounterPresenter presenter;

        public EncounterSnapshot Snapshot => session.Snapshot;
        public Resolution InitialResolution => session.InitialResolution;

        public EncounterApplication(MechanicalCatalog catalog, string encounterId, ulong seed,
            IEnumerable<string> unlockedModifierIds = null)
        {
            session = EncounterSession.CreateEncounter(catalog, encounterId, seed, unlockedModifierIds);
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

        public static EncounterApplication Create(MechanicalCatalogAsset asset, IEnumerable<ModifierAsset> modifiers,
            string encounterId, ulong seed)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            return new EncounterApplication(asset.Build(modifiers), encounterId, seed);
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

        public ScoreBreakdown PreviewPlayHand() => session.PreviewPlayHand();
        public HandPreview PreviewHandResolution() => session.PreviewHandResolution();
        public ReadOnlyCollection<ModifierInspection> InspectModifiersForPlayHand() => session.InspectModifiersForPlayHand();
        public EncounterSaveData CaptureSave() => session.CaptureSave();
        public void MergeUnlockedModifiers(IEnumerable<string> modifierIds) => session.AddUnlockedModifiers(modifierIds);
    }
}
