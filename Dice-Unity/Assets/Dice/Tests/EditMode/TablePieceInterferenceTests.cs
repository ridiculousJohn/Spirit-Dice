using System;
using System.Linq;
using System.Text;
using Dice.App;
using Dice.Engine;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Dice.Tests
{
    /** Table pieces, the die interference rule and the bay ward rule, each on a minimal test-only encounter. */
    public sealed class TablePieceInterferenceTests
    {
        private const string PieceId = "piece.test";
        private const string InteractionId = "position";
        private const string HauntingId = "rule.test.haunting";
        private const string WardId = "rule.test.ward";
        private const int AllButFirstBay = 0b11110;

        /** Checks a piece starts at its authored position and moves without touching dice or random streams / fails if a move rolls, disturbs or is lost. */
        [Test]
        public void PieceMovesFromItsAuthoredPositionWithoutChangingDiceOrRandomStreams()
        {
            var session = Session(Catalog(chance: 100));
            Assert.That(Position(session), Is.EqualTo(PiecePosition.Neutral));
            var before = session.CaptureSave();
            var moved = Execute(session, Move(PiecePosition.Bay(2)));
            Assert.That(moved.Events.Single(), Is.InstanceOf<PieceInteractionChangedEvent>(),
                "Moving a piece is not a committed action for interference.");
            Assert.That(Position(session), Is.EqualTo(PiecePosition.Bay(2)));
            var after = session.CaptureSave();
            Assert.That(after.CurrentDice, Is.EqualTo(before.CurrentDice));
            Assert.That(after.DiceRandomState, Is.EqualTo(before.DiceRandomState));
            Assert.That(after.RuleRandomState, Is.EqualTo(before.RuleRandomState));
        }

        /** Checks unknown pieces, disallowed or unchanged positions and missing input are rejected / fails if any of them changes state. */
        [Test]
        public void InvalidPieceRequestsAreRejectedWithoutChangingState()
        {
            var session = Session(Catalog(chance: 0, allowedBays: 0b01111));
            string before = Json(session.CaptureSave());
            long revision = session.Snapshot.Revision;
            Assert.That(session.Execute(new InteractWithPieceAction("piece.missing", InteractionId, new MovePieceInput(PiecePosition.Bay(0))),
                revision).Rejection, Is.EqualTo(CommandRejection.InvalidPiece));
            Assert.That(session.Execute(new InteractWithPieceAction(PieceId, "missing", new MovePieceInput(PiecePosition.Bay(0))),
                revision).Rejection, Is.EqualTo(CommandRejection.InvalidPiece));
            Assert.That(session.Execute(Move(PiecePosition.Neutral), revision).Rejection, Is.EqualTo(CommandRejection.InvalidPieceInput));
            Assert.That(session.Execute(Move(PiecePosition.Bay(4)), revision).Rejection, Is.EqualTo(CommandRejection.InvalidPieceInput));
            Assert.That(session.Execute(new InteractWithPieceAction(PieceId, InteractionId, null), revision).Rejection,
                Is.EqualTo(CommandRejection.InvalidPieceInput));
            Assert.That(Json(session.CaptureSave()), Is.EqualTo(before));
        }

        /** Checks a completed encounter reports its pieces unavailable / fails if a prop stays movable after the encounter ends. */
        [Test]
        public void CompletedEncounterMakesItsPiecesUnavailable()
        {
            var session = Session(Catalog(chance: 0));
            Assert.That(session.Snapshot.PieceInteractions.Single().IsAvailable, Is.True);
            Execute(session, AbandonEncounterAction.Instance);
            Assert.That(session.Snapshot.PieceInteractions.Single().IsAvailable, Is.False);
        }

        /** Checks an until-first-reroll piece locks after a reroll and unlocks next hand while a during-hand piece stays free / fails if timing is ignored. */
        [Test]
        public void MovementTimingIsAuthoredOnTheInteraction()
        {
            var locked = Session(Catalog(chance: 0, timing: PieceMovementTiming.UntilFirstReroll));
            Execute(locked, Move(PiecePosition.Bay(1)));
            Execute(locked, new RerollDiceAction(1));
            Assert.That(locked.Snapshot.PieceInteractions.Single().IsAvailable, Is.False);
            Assert.That(locked.Execute(Move(PiecePosition.Bay(3)), locked.Snapshot.Revision).Rejection,
                Is.EqualTo(CommandRejection.PieceUnavailable));
            Execute(locked, PlayHandAction.Instance);
            Assert.That(locked.Snapshot.PieceInteractions.Single().IsAvailable, Is.True, "Timing resets for each hand.");
            Assert.That(Position(locked), Is.EqualTo(PiecePosition.Bay(1)), "Positions persist between hands.");

            var free = Session(Catalog(chance: 0, timing: PieceMovementTiming.DuringHand));
            Execute(free, new RerollDiceAction(1));
            Execute(free, Move(PiecePosition.Bay(3)));
            Assert.That(Position(free), Is.EqualTo(PiecePosition.Bay(3)));
        }

        /** Checks a moved piece's position survives a JSON save and restore / fails if the restored piece returns to its start. */
        [Test]
        public void PiecePositionSurvivesSaveAndRestore()
        {
            var catalog = Catalog(chance: 0);
            var session = Session(catalog);
            Execute(session, Move(PiecePosition.Bay(3)));
            Assert.That(Position(Restore(catalog, Decode(catalog, EncounterSaveCodec.Serialize(session.CaptureSave())))),
                Is.EqualTo(PiecePosition.Bay(3)));
        }

        /** Checks a save carries the encounter's own pieces and only the rules with saved state / fails if pieces or stateless rules are mis-saved. */
        [Test]
        public void SaveCarriesTheEncountersPiecesAndOnlyRulesWithSavedState()
        {
            var save = Session(Catalog(chance: 0, ward: true)).CaptureSave();
            Assert.That(save.Pieces.Select(value => value.PieceId + "/" + value.InteractionId),
                Is.EqualTo(new[] { PieceId + "/" + InteractionId }));
            Assert.That(save.RuleStates.Select(value => value.RuleId), Is.EqualTo(new[] { HauntingId }));
        }

        /** Checks restore rejects piece records that disagree with the encounter / fails if a missing, foreign or impossible position loads. */
        [Test]
        public void RestoreRejectsPieceStatesThatDisagreeWithTheEncounter()
        {
            var catalog = Catalog(chance: 0);
            var session = Session(catalog);
            AssertInvalid(catalog, session.CaptureSave(), save => save.Pieces = null);
            AssertInvalid(catalog, session.CaptureSave(), save => save.Pieces = Array.Empty<EncounterSavePieceData>());
            AssertInvalid(catalog, session.CaptureSave(), save => save.Pieces[0].PieceId = "piece.other");
            AssertInvalid(catalog, session.CaptureSave(), save => save.Pieces[0].Values = new[] { 1, 5 });
            AssertInvalid(catalog, session.CaptureSave(), save => save.Pieces[0].Values = new[] { 0, 1 });
            AssertInvalid(catalog, session.CaptureSave(), save => save.Pieces[0].Values = new[] { 1 });
            var neutralForbidden = Catalog(chance: 0, allowsNeutral: false, startingBay: 0);
            AssertInvalid(neutralForbidden, Session(neutralForbidden).CaptureSave(), save => save.Pieces[0].Values = new[] { 0, 0 });
        }

        /** Checks piece, interference and ward definitions reject invalid configurations / fails if malformed content constructs. */
        [Test]
        public void DefinitionsValidatePiecesAndWardReferences()
        {
            var position = new PositionInteractionDefinition(InteractionId, PiecePosition.Neutral, PieceMovementTiming.DuringHand);
            Assert.Throws<ArgumentException>(() => new TablePieceDefinition(PieceId, new[] { position, position }));
            Assert.Throws<ArgumentException>(() => new EncounterDefinition(TestContent.EncounterId,
                new[] { new StageDefinition(10, 1, 1) },
                pieces: new[] { new TablePieceDefinition(PieceId, new[] { position }), new TablePieceDefinition(PieceId) }));
            Assert.Throws<ArgumentException>(() => new EncounterDefinition(TestContent.EncounterId,
                new[] { new StageDefinition(10, 1, 1) }, rules: new[] { Ward() }));
            Assert.Throws<ArgumentException>(() => new PositionInteractionDefinition(InteractionId, PiecePosition.Neutral,
                PieceMovementTiming.DuringHand, allowsNeutral: false));
            Assert.Throws<ArgumentException>(() => new DieInterferenceRuleDefinition(HauntingId, 20, 0, 0, true, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DieInterferenceRuleDefinition(HauntingId, 101, 1, 1, true, true));
            Assert.Throws<ArgumentException>(() => new BayWardRuleDefinition(WardId, PieceId, InteractionId,
                Array.Empty<DieInterferenceKind>()));
        }

        /**
         * Checks a reroll disturbance flips a kept die after the dice land and before reroll charms, without marking it rerolled /
         * fails if a rerolled die is targeted, the order changes, or rerolling every die still leaves a target.
         */
        [Test]
        public void RerollInterferenceFlipsAKeptDieBeforeRerollCharms()
        {
            var charm = new ModifierDefinition("test.reroll_charm", "test.reroll_charm", ModifierPhase.PlayerReroll,
                new ModifierCondition(ModifierConditionKind.Always), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 9) },
                ModifierActivationLimit.Unlimited);
            var catalog = Catalog(chance: 100, rerollWeight: 0, flipWeight: 1, modifiers: new[] { charm });

            var result = Execute(Session(catalog), new RerollDiceAction(AllButFirstBay));
            var events = result.Events.ToList();
            var rolled = events.OfType<DiceRolledEvent>().Single();
            var disturbed = events.OfType<DieDisturbedEvent>().Single();
            Assert.That(disturbed.Bay, Is.Zero, "Only the kept die can be disturbed after a reroll.");
            Assert.That(disturbed.RuleId, Is.EqualTo(HauntingId));
            Assert.That(disturbed.Kind, Is.EqualTo(DieInterferenceKind.Flip));
            Assert.That(disturbed.Trigger, Is.EqualTo(InterferenceTrigger.PlayerReroll));
            Assert.That(disturbed.After, Is.EqualTo(7 - disturbed.Before));
            Assert.That(result.Snapshot.CurrentDice[0], Is.EqualTo(disturbed.After));
            Assert.That(events.IndexOf(disturbed), Is.EqualTo(events.IndexOf(rolled) + 1));
            Assert.That(events.IndexOf(events.OfType<ModifierActivatedEvent>().Single()), Is.GreaterThan(events.IndexOf(disturbed)));
            Assert.That(result.Snapshot.Interference().DisturbedMask, Is.EqualTo(1));
            Assert.That(result.Snapshot.EverRerolledMask, Is.EqualTo(AllButFirstBay));

            var rerolledEverything = Execute(Session(catalog), new RerollDiceAction(DiceValues.AllMask));
            Assert.That(rerolledEverything.Events.OfType<DieDisturbedEvent>(), Is.Empty);
            Assert.That(rerolledEverything.Snapshot.Interference().DisturbedMask, Is.Zero);
        }

        /** Checks a zero percent chance never disturbs / fails if interference ignores its chance. */
        [Test]
        public void ZeroChanceNeverDisturbs()
        {
            var session = Session(Catalog(chance: 0));
            Assert.That(Execute(session, new RerollDiceAction(1)).Events.OfType<DieDisturbedEvent>(), Is.Empty);
            Assert.That(Execute(session, PlayHandAction.Instance).Events.OfType<DieDisturbedEvent>(), Is.Empty);
        }

        /** Checks interference only follows the committed actions it is authored for / fails if a disabled trigger still disturbs. */
        [Test]
        public void InterferenceFollowsOnlyItsAuthoredTriggers()
        {
            var playOnly = Session(Catalog(chance: 100, afterReroll: false));
            Assert.That(Execute(playOnly, new RerollDiceAction(1)).Events.OfType<DieDisturbedEvent>(), Is.Empty);
            Assert.That(Execute(playOnly, PlayHandAction.Instance).Events.OfType<DieDisturbedEvent>().Count(), Is.EqualTo(1));
            var rerollOnly = Session(Catalog(chance: 100, afterPlay: false));
            Assert.That(Execute(rerollOnly, PlayHandAction.Instance).Events.OfType<DieDisturbedEvent>(), Is.Empty);
        }

        /**
         * Checks a play disturbance changes the dice before charms and scoring while a preview neither disturbs nor draws from the
         * rule stream / fails if the hand scores the undisturbed dice or a preview changes the outcome.
         */
        [Test]
        public void PlayHandDisturbanceResolvesBeforeCharmsAndScoringAndPreviewsIgnoreIt()
        {
            // Every die shows six, so a flip leaves exactly one one on the table for the charm to find.
            var charm = new ModifierDefinition("test.play_charm", "test.play_charm", ModifierPhase.PlayHand,
                new ModifierCondition(ModifierConditionKind.ContainsFace, 1), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 9) });
            var catalog = Catalog(chance: 100, rerollWeight: 0, flipWeight: 1, modifiers: new[] { charm });
            var session = WithDice(catalog, 6, 6, 6, 6, 6);
            ulong ruleStream = session.CaptureSave().RuleRandomState;

            var preview = session.PreviewHandResolution();
            Assert.That(preview.Events.OfType<DieDisturbedEvent>(), Is.Empty);
            Assert.That(preview.Events.OfType<ModifierActivatedEvent>(), Is.Empty);
            Assert.That(session.CaptureSave().RuleRandomState, Is.EqualTo(ruleStream), "Previews must not consume the rule stream.");

            var result = Execute(session, PlayHandAction.Instance);
            var events = result.Events.ToList();
            var disturbed = events.OfType<DieDisturbedEvent>().Single();
            var scored = events.OfType<HandScoredEvent>().Single();
            Assert.That(disturbed.After, Is.EqualTo(1));
            Assert.That(scored.Dice[disturbed.Bay], Is.EqualTo(1));
            Assert.That(events.IndexOf(events.OfType<ModifierActivatedEvent>().Single()), Is.InRange(events.IndexOf(disturbed),
                events.IndexOf(scored)));
        }

        /** Checks the final hand's disturbance stays recorded and restorable after the encounter ends / fails if a completed save is rejected or loses it. */
        [Test]
        public void PostPlayDisturbanceIsSavedWithTheCompletedHand()
        {
            var catalog = Catalog(chance: 100, stages: new[] { new StageDefinition(1000000, 1, 2) });
            var session = Session(catalog);
            var result = Execute(session, PlayHandAction.Instance);
            var disturbed = result.Events.OfType<DieDisturbedEvent>().Single();
            Assert.That(disturbed.Trigger, Is.EqualTo(InterferenceTrigger.PlayHand));
            Assert.That(result.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Lost));
            Assert.That(result.Snapshot.Interference().DisturbedMask, Is.EqualTo(1 << disturbed.Bay));
            var restored = Restore(catalog, Decode(catalog, EncounterSaveCodec.Serialize(session.CaptureSave())));
            Assert.That(restored.Snapshot.Interference().DisturbedMask, Is.EqualTo(1 << disturbed.Bay));
        }

        /** Checks a ward blocks only effects it names on the bay its piece occupies / fails if it protects from neutral or blocks unnamed effects. */
        [Test]
        public void WardsBlockOnlyTheirOccupiedBayAndNamedEffects()
        {
            var catalog = Catalog(chance: 100, ward: true);
            var session = Session(catalog);
            Execute(session, Move(PiecePosition.Bay(0)));
            var before = session.Snapshot.CurrentDice[0];
            var blocked = Execute(session, new RerollDiceAction(AllButFirstBay));
            var block = blocked.Events.OfType<DisturbanceBlockedEvent>().Single();
            Assert.That(block.Bay, Is.Zero);
            Assert.That(block.RuleId, Is.EqualTo(HauntingId));
            Assert.That(block.ProtectingRuleId, Is.EqualTo(WardId));
            Assert.That(block.Trigger, Is.EqualTo(InterferenceTrigger.PlayerReroll));
            Assert.That(blocked.Events.OfType<DieDisturbedEvent>(), Is.Empty);
            Assert.That(blocked.Snapshot.CurrentDice[0], Is.EqualTo(before));
            Assert.That(blocked.Snapshot.Interference().DisturbedMask, Is.Zero);

            var neutral = Session(catalog);
            Assert.That(Execute(neutral, new RerollDiceAction(AllButFirstBay)).Events.OfType<DieDisturbedEvent>().Single().Bay, Is.Zero,
                "The neutral position protects nothing.");

            var flipOnlyWard = Catalog(chance: 100, ward: true, rerollWeight: 1, flipWeight: 0,
                blocked: new[] { DieInterferenceKind.Flip });
            var unwarded = Session(flipOnlyWard);
            Execute(unwarded, Move(PiecePosition.Bay(0)));
            Assert.That(Execute(unwarded, new RerollDiceAction(AllButFirstBay)).Events.OfType<DieDisturbedEvent>().Single().Kind,
                Is.EqualTo(DieInterferenceKind.ForcedReroll));
        }

        /** Checks a restored session continues the rule stream exactly / fails if restore reseeds or drops interference state. */
        [Test]
        public void RestoredSessionContinuesTheRuleStream()
        {
            var catalog = Catalog(chance: 50);
            var original = Session(catalog);
            Execute(original, new RerollDiceAction(1));
            var restored = Restore(catalog, Decode(catalog, EncounterSaveCodec.Serialize(original.CaptureSave())));
            for (int i = 0; i < 3; ++i)
            {
                Execute(original, new RerollDiceAction(3));
                Execute(restored, new RerollDiceAction(3));
            }
            Assert.That(Json(restored.CaptureSave()), Is.EqualTo(Json(original.CaptureSave())));
        }

        /** Checks restore rejects disturbance records the hand could not have produced / fails if an impossible disturbance loads. */
        [Test]
        public void RestoreRejectsInconsistentDisturbanceRecords()
        {
            var plain = Catalog(chance: 0, haunting: false);
            AssertInvalid(plain, Session(plain).CaptureSave(), save => save.RuleStates = new[]
            {
                new EncounterSaveRuleData { RuleId = HauntingId, State = new DieInterferenceSaveData { DisturbedMask = 1 } }
            });
            var haunted = Catalog(chance: 100);
            var session = Session(haunted);
            Execute(session, new RerollDiceAction(1));
            Assert.That(session.Snapshot.Interference().DisturbedMask, Is.Not.Zero);
            AssertInvalid(haunted, session.CaptureSave(), save =>
                Haunting(save).LatestDisturbedMask = Haunting(save).DisturbedMask == 1 ? 2 : 1);
            AssertInvalid(haunted, session.CaptureSave(), save => Haunting(save).DisturbedMask = DiceValues.AllMask);
            AssertInvalid(haunted, session.CaptureSave(), save => Haunting(save).DisturbedMask = 32);
            AssertInvalid(haunted, session.CaptureSave(), save => save.RuleStates = Array.Empty<EncounterSaveRuleData>());
            AssertInvalid(haunted, session.CaptureSave(), save => save.RuleStates[0].State = new DieBindingSaveData());
            AssertInvalid(haunted, session.CaptureSave(), save =>
            {
                save.LatestRerollMask |= Haunting(save).DisturbedMask;
                save.EverRerolledMask |= Haunting(save).DisturbedMask;
            }, "kept die");
        }

        /** Checks only encounters with an interference rule seed and use the rule stream / fails if a plain encounter draws from or accepts one. */
        [Test]
        public void OnlyInterferenceRulesSeedAndAdvanceTheRuleStream()
        {
            var plain = Catalog(chance: 0, haunting: false);
            var session = Session(plain);
            Execute(session, new RerollDiceAction(3));
            Execute(session, PlayHandAction.Instance);
            Assert.That(session.CaptureSave().RuleRandomState, Is.Zero);
            AssertInvalid(plain, session.CaptureSave(), save => save.RuleRandomState = 1);
            Assert.That(Session(Catalog(chance: 20)).CaptureSave().RuleRandomState, Is.Not.Zero);
        }

        /** Checks current-format JSON requires the piece and rule fields and an earlier format cannot carry pieces / fails if either loads. */
        [Test]
        public void CurrentFormatJsonRequiresTheNewFieldsAndEarlierFormatsRejectPieces()
        {
            var catalog = Catalog(chance: 0);
            var session = Session(catalog);
            foreach (string field in new[] { nameof(EncounterSaveData.Pieces), nameof(EncounterSaveData.RuleRandomState),
                nameof(EncounterSaveData.RuleStates) })
            {
                var json = JObject.Parse(Json(session.CaptureSave()));
                json.Remove(field);
                Assert.Throws<FormatException>(() => Decode(catalog, json), field);
            }
            var downgraded = JObject.Parse(Json(session.CaptureSave()));
            downgraded[nameof(EncounterSaveData.SaveFormatVersion)] = 5;
            downgraded[nameof(EncounterSaveData.RulesVersion)] = 7;
            var result = EncounterSession.RestoreEncounter(catalog, Decode(catalog, downgraded));
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState), result.Detail);
        }

        /** Checks a format five save without pieces or rule state restores and continues identically / fails if the migration loses the dice stream. */
        [Test]
        public void FormatFiveSavesRestoreWithoutPiecesOrDisturbances()
        {
            var catalog = Catalog(chance: 0, haunting: false, pieces: false);
            var original = Session(catalog);
            Execute(original, new RerollDiceAction(3));
            var json = JObject.Parse(Json(original.CaptureSave()));
            json[nameof(EncounterSaveData.SaveFormatVersion)] = 5;
            json[nameof(EncounterSaveData.RulesVersion)] = 7;
            json.Remove(nameof(EncounterSaveData.RuleStates));
            json.Remove(nameof(EncounterSaveData.RuleRandomState));
            json.Remove(nameof(EncounterSaveData.Pieces));
            var restored = Restore(catalog, Decode(catalog, json));
            Execute(original, new RerollDiceAction(5));
            Execute(restored, new RerollDiceAction(5));
            Assert.That(restored.Snapshot.CurrentDice, Is.EqualTo(original.Snapshot.CurrentDice));
            Assert.That(restored.CaptureSave().SaveFormatVersion, Is.EqualTo(EncounterSaveData.CurrentFormatVersion));
        }

        private static MechanicalCatalog Catalog(int chance, int rerollWeight = 1, int flipWeight = 1, bool afterReroll = true,
            bool afterPlay = true, bool ward = false, DieInterferenceKind[] blocked = null,
            PieceMovementTiming timing = PieceMovementTiming.DuringHand, bool allowsNeutral = true, int allowedBays = DiceValues.AllMask,
            int startingBay = -1, bool haunting = true, bool pieces = true, StageDefinition[] stages = null,
            ModifierDefinition[] modifiers = null)
        {
            var rules = new System.Collections.Generic.List<EncounterRuleDefinition>();
            if (haunting) rules.Add(new DieInterferenceRuleDefinition(HauntingId, chance, rerollWeight, flipWeight, afterReroll, afterPlay));
            if (ward) rules.Add(Ward(blocked));
            var start = startingBay < 0 ? PiecePosition.Neutral : PiecePosition.Bay(startingBay);
            var pieceList = pieces
                ? new[] { new TablePieceDefinition(PieceId, new[] { new PositionInteractionDefinition(InteractionId, start, timing,
                    allowsNeutral, allowedBays) }) }
                : Array.Empty<TablePieceDefinition>();
            var encounter = new EncounterDefinition(TestContent.EncounterId,
                stages ?? new[] { new StageDefinition(1000000, 20, 20) }, startingModifiers: modifiers?.Select(value => value.Id),
                rules: rules, pieces: pieceList);
            return new MechanicalCatalog("test.table_pieces", TestContent.Hands(), new[] { encounter }, modifiers);
        }

        private static BayWardRuleDefinition Ward(DieInterferenceKind[] blocked = null) => new BayWardRuleDefinition(WardId, PieceId,
            InteractionId, blocked ?? new[] { DieInterferenceKind.ForcedReroll, DieInterferenceKind.Flip });

        private static InteractWithPieceAction Move(PiecePosition position)
            => new InteractWithPieceAction(PieceId, InteractionId, new MovePieceInput(position));

        private static PiecePosition Position(EncounterSession session)
            => ((PiecePositionState)session.Snapshot.PieceInteractions.Single().State).Position;

        private static EncounterSession Session(MechanicalCatalog catalog) => EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14);

        /** A fresh session whose opening dice are replaced with the given faces. */
        private static EncounterSession WithDice(MechanicalCatalog catalog, params int[] dice)
        {
            var save = Session(catalog).CaptureSave();
            save.CurrentDice = dice;
            save.InitialDice = (int[])dice.Clone();
            return Restore(catalog, save);
        }

        private static Resolution Execute(EncounterSession session, EncounterAction action)
        {
            var result = session.Execute(action, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
            return result;
        }

        private static EncounterSession Restore(MechanicalCatalog catalog, EncounterSaveData save)
        {
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.True, result.Error + ": " + result.Detail);
            return result.Session;
        }

        private static void AssertInvalid(MechanicalCatalog catalog, EncounterSaveData save, Action<EncounterSaveData> mutate,
            string detail = null)
        {
            mutate(save);
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState), result.Detail);
            if (detail != null) Assert.That(result.Detail, Does.Contain(detail));
        }

        private static DieInterferenceSaveData Haunting(EncounterSaveData save)
            => (DieInterferenceSaveData)save.RuleStates.Single(value => value.RuleId == HauntingId).State;

        private static string Json(EncounterSaveData save) => Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(save));

        private static EncounterSaveData Decode(MechanicalCatalog catalog, byte[] bytes) => EncounterSaveCodec.Deserialize(bytes, catalog);
        private static EncounterSaveData Decode(MechanicalCatalog catalog, JObject json) => EncounterSaveCodec.Deserialize(Encoding.UTF8.GetBytes(json.ToString()), catalog);
    }
}
