using System;
using System.Linq;
using System.Text;
using Dice.App;
using Dice.Engine;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class TablePieceInterferenceTests
    {
        private const string PieceId = "piece.candle";
        private const string InteractionId = "position";
        private const string HauntingId = "rule.test.haunting";
        private const string WardId = "rule.test.ward";

        [Test]
        public void PiecesStartAtTheirAuthoredPositionAndMoveWithoutChangingDiceOrRandomStreams()
        {
            var session = Session(Catalog(chance: 100));
            var initial = session.Snapshot.PieceInteractions.Single();
            Assert.That(initial.PieceId, Is.EqualTo(PieceId));
            Assert.That(initial.InteractionId, Is.EqualTo(InteractionId));
            Assert.That(initial.IsAvailable, Is.True);
            Assert.That(Position(session), Is.EqualTo(PiecePosition.Neutral));
            var before = session.CaptureSave();
            var moved = Execute(session, Move(PiecePosition.Bay(2)));
            var changed = moved.Events.OfType<PieceInteractionChangedEvent>().Single();
            Assert.That(moved.Events.Count, Is.EqualTo(1), "Moving a piece is not a committed action for interference.");
            Assert.That(((PiecePositionState)changed.Before).Position, Is.EqualTo(PiecePosition.Neutral));
            Assert.That(((PiecePositionState)changed.After).Position, Is.EqualTo(PiecePosition.Bay(2)));
            Assert.That(Position(session), Is.EqualTo(PiecePosition.Bay(2)));
            var after = session.CaptureSave();
            Assert.That(after.CurrentDice, Is.EqualTo(before.CurrentDice));
            Assert.That(after.DiceRandomState, Is.EqualTo(before.DiceRandomState));
            Assert.That(after.RewardRandomState, Is.EqualTo(before.RewardRandomState));
            Assert.That(after.RuleRandomState, Is.EqualTo(before.RuleRandomState));
            Assert.That(after.Revision, Is.EqualTo(before.Revision + 1));
            Execute(session, Move(PiecePosition.Neutral));
            Assert.That(Position(session), Is.EqualTo(PiecePosition.Neutral));
        }

        [Test]
        public void InvalidPieceRequestsAreRejectedWithoutChangingState()
        {
            var catalog = Catalog(chance: 0, allowedBays: 0b01111);
            var session = Session(catalog);
            var before = session.CaptureSave();
            long revision = session.Snapshot.Revision;
            Assert.That(session.Execute(new InteractWithPieceAction("piece.missing", InteractionId, new MovePieceInput(PiecePosition.Bay(0))),
                revision).Rejection, Is.EqualTo(CommandRejection.InvalidPiece));
            Assert.That(session.Execute(new InteractWithPieceAction(PieceId, "missing", new MovePieceInput(PiecePosition.Bay(0))),
                revision).Rejection, Is.EqualTo(CommandRejection.InvalidPiece));
            Assert.That(session.Execute(Move(PiecePosition.Neutral), revision).Rejection, Is.EqualTo(CommandRejection.InvalidPieceInput));
            Assert.That(session.Execute(Move(PiecePosition.Bay(4)), revision).Rejection, Is.EqualTo(CommandRejection.InvalidPieceInput));
            Assert.That(session.Execute(new InteractWithPieceAction(PieceId, InteractionId, null), revision).Rejection,
                Is.EqualTo(CommandRejection.InvalidPieceInput));
            EncounterPersistenceTests.AssertGraphEqual(before, session.CaptureSave());
            Execute(session, AbandonEncounterAction.Instance);
            Assert.That(session.Snapshot.PieceInteractions.Single().IsAvailable, Is.False);
            Assert.That(session.Execute(Move(PiecePosition.Bay(0)), session.Snapshot.Revision).Rejection,
                Is.EqualTo(CommandRejection.EncounterCompleted));
        }

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
            Execute(free, new RerollDiceAction(2));
            Execute(free, Move(PiecePosition.Bay(4)));
            Assert.That(Position(free), Is.EqualTo(PiecePosition.Bay(4)));
        }

        [Test]
        public void PiecePositionsSurviveSaveRestoreAndJson()
        {
            var catalog = Catalog(chance: 50);
            var session = Session(catalog);
            Execute(session, Move(PiecePosition.Bay(3)));
            Execute(session, new RerollDiceAction(5));
            var restored = Restore(catalog, Decode(catalog, EncounterSaveCodec.Serialize(session.CaptureSave())));
            Assert.That(Position(restored), Is.EqualTo(PiecePosition.Bay(3)));
            for (int i = 0; i < 6; ++i)
            {
                Execute(session, new RerollDiceAction(1 + i % 31));
                Execute(restored, new RerollDiceAction(1 + i % 31));
            }
            EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), restored.CaptureSave());
        }

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

        [Test]
        public void RerollInterferenceTargetsOneKeptDieBeforeRerollCharmsAndIsNotCountedAsRerolled()
        {
            int disturbances = 0;
            for (ulong seed = 0; seed < 40; ++seed)
            {
                var catalog = Catalog(chance: 100);
                var session = Session(catalog, seed);
                const int rerolled = 0b00011;
                var result = Execute(session, new RerollDiceAction(rerolled));
                var types = result.Events.Select(value => value.GetType()).ToArray();
                int rolledIndex = Array.IndexOf(types, typeof(DiceRolledEvent));
                var disturbed = result.Events.OfType<DieDisturbedEvent>().Single();
                Assert.That(Array.IndexOf(types, typeof(DieDisturbedEvent)), Is.EqualTo(rolledIndex + 1));
                Assert.That(rerolled & (1 << disturbed.Bay), Is.Zero, "Only kept dice can be disturbed after a reroll.");
                Assert.That(disturbed.RuleId, Is.EqualTo(HauntingId));
                Assert.That(result.Snapshot.CurrentDice[disturbed.Bay], Is.EqualTo(disturbed.After));
                Assert.That(result.Snapshot.DisturbedMask, Is.EqualTo(1 << disturbed.Bay));
                Assert.That(result.Snapshot.EverRerolledMask, Is.EqualTo(rerolled));
                Assert.That(result.Snapshot.LatestChangedMask & ~rerolled, Is.Zero);
                if (disturbed.Kind == DieInterferenceKind.Flip) Assert.That(disturbed.After, Is.EqualTo(7 - disturbed.Before));
                Restore(catalog, session.CaptureSave());
                ++disturbances;
            }
            Assert.That(disturbances, Is.EqualTo(40));
        }

        [Test]
        public void RerollingEveryDieLeavesNoInterferenceTarget()
        {
            var session = Session(Catalog(chance: 100));
            var result = Execute(session, new RerollDiceAction(DiceValues.AllMask));
            Assert.That(result.Events.OfType<DieDisturbedEvent>(), Is.Empty);
            Assert.That(result.Events.OfType<DisturbanceBlockedEvent>(), Is.Empty);
            Assert.That(result.Snapshot.DisturbedMask, Is.Zero);
        }

        [Test]
        public void FlipsAlwaysChangeAndForcedRerollsMayKeepTheirFace()
        {
            for (ulong seed = 0; seed < 30; ++seed)
            {
                var flip = Session(Catalog(chance: 100, rerollWeight: 0, flipWeight: 1), seed);
                var flipped = Execute(flip, new RerollDiceAction(1)).Events.OfType<DieDisturbedEvent>().Single();
                Assert.That(flipped.Kind, Is.EqualTo(DieInterferenceKind.Flip));
                Assert.That(flipped.After, Is.EqualTo(7 - flipped.Before));
            }
            bool unchangedReroll = false;
            for (ulong seed = 0; seed < 400 && !unchangedReroll; ++seed)
            {
                var catalog = Catalog(chance: 100, rerollWeight: 1, flipWeight: 0);
                var session = Session(catalog, seed);
                var result = Execute(session, new RerollDiceAction(1));
                var disturbed = result.Events.OfType<DieDisturbedEvent>().Single();
                Assert.That(disturbed.Kind, Is.EqualTo(DieInterferenceKind.ForcedReroll));
                if (disturbed.Before != disturbed.After) continue;
                unchangedReroll = true;
                Assert.That(result.Snapshot.DisturbedMask, Is.EqualTo(1 << disturbed.Bay), "An unchanged forced reroll still counts.");
                Restore(catalog, session.CaptureSave());
            }
            Assert.That(unchangedReroll, Is.True);
        }

        [Test]
        public void ZeroChanceNeverDisturbsAndAuthoredTriggersAreRespected()
        {
            var never = Session(Catalog(chance: 0));
            for (int i = 0; i < 5; ++i) Assert.That(Execute(never, new RerollDiceAction(1)).Events.OfType<DieDisturbedEvent>(), Is.Empty);
            var playOnly = Session(Catalog(chance: 100, afterReroll: false));
            Assert.That(Execute(playOnly, new RerollDiceAction(1)).Events.OfType<DieDisturbedEvent>(), Is.Empty);
            Assert.That(Execute(playOnly, PlayHandAction.Instance).Events.OfType<DieDisturbedEvent>().Count(), Is.EqualTo(1));
            var rerollOnly = Session(Catalog(chance: 100, afterPlay: false));
            Assert.That(Execute(rerollOnly, PlayHandAction.Instance).Events.OfType<DieDisturbedEvent>(), Is.Empty);
        }

        [Test]
        public void PlayHandDisturbanceResolvesBeforeScoringAndPreviewsIgnoreIt()
        {
            for (ulong seed = 0; seed < 20; ++seed)
            {
                var catalog = Catalog(chance: 100);
                var session = Session(catalog, seed);
                var rule = session.CaptureSave().RuleRandomState;
                var preview = session.PreviewHandResolution();
                Assert.That(preview.Events.OfType<DieDisturbedEvent>(), Is.Empty);
                Assert.That(session.CaptureSave().RuleRandomState, Is.EqualTo(rule), "Previews must not consume the rule stream.");
                var result = Execute(session, PlayHandAction.Instance);
                var disturbed = result.Events.OfType<DieDisturbedEvent>().Single();
                var scored = result.Events.OfType<HandScoredEvent>().Single();
                Assert.That(result.Events.IndexOf(disturbed), Is.LessThan(result.Events.IndexOf(scored)));
                Assert.That(scored.Dice[disturbed.Bay], Is.EqualTo(disturbed.After));
                Assert.That(result.Snapshot.PreviousPlayedDice.Value[disturbed.Bay], Is.EqualTo(disturbed.After));
                Assert.That(result.Snapshot.DisturbedMask, Is.Zero, "A new hand clears its disturbance record.");
            }
        }

        [Test]
        public void PostPlayDisturbanceIsSavedWithTheCompletedHand()
        {
            var catalog = Catalog(chance: 100, stages: new[] { new StageDefinition(1000000, 1, 2) });
            var session = Session(catalog, 3);
            var result = Execute(session, PlayHandAction.Instance);
            var disturbed = result.Events.OfType<DieDisturbedEvent>().Single();
            Assert.That(result.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Lost));
            Assert.That(result.Snapshot.DisturbedMask, Is.EqualTo(1 << disturbed.Bay));
            Restore(catalog, Decode(catalog, EncounterSaveCodec.Serialize(session.CaptureSave())));
        }

        [Test]
        public void WardsBlockOnlyTheirOccupiedBayAndNamedEffects()
        {
            var catalog = Catalog(chance: 100, ward: true);
            var session = Session(catalog, 9);
            Execute(session, Move(PiecePosition.Bay(0)));
            var before = session.Snapshot.CurrentDice[0];
            var blocked = Execute(session, new RerollDiceAction(0b11110));
            var block = blocked.Events.OfType<DisturbanceBlockedEvent>().Single();
            Assert.That(block.Bay, Is.Zero);
            Assert.That(block.RuleId, Is.EqualTo(HauntingId));
            Assert.That(block.ProtectingRuleId, Is.EqualTo(WardId));
            Assert.That(blocked.Events.OfType<DieDisturbedEvent>(), Is.Empty);
            Assert.That(blocked.Snapshot.CurrentDice[0], Is.EqualTo(before));
            Assert.That(blocked.Snapshot.DisturbedMask, Is.Zero);
            Restore(catalog, session.CaptureSave());

            var neutral = Session(catalog, 9);
            Assert.That(Execute(neutral, new RerollDiceAction(0b11110)).Events.OfType<DieDisturbedEvent>().Single().Bay, Is.Zero,
                "The neutral position protects nothing.");

            var flipOnlyWard = Catalog(chance: 100, ward: true, rerollWeight: 1, flipWeight: 0,
                blocked: new[] { DieInterferenceKind.Flip });
            var unwarded = Session(flipOnlyWard, 9);
            Execute(unwarded, Move(PiecePosition.Bay(0)));
            Assert.That(Execute(unwarded, new RerollDiceAction(0b11110)).Events.OfType<DieDisturbedEvent>().Single().Kind,
                Is.EqualTo(DieInterferenceKind.ForcedReroll));
        }

        [Test]
        public void InterferenceIsDeterministicForASeedAndAcrossRestore()
        {
            var catalog = Catalog(chance: 40, ward: true);
            var first = Session(catalog, 77);
            var second = Session(catalog, 77);
            EncounterSession restored = null;
            for (int i = 0; i < 12; ++i)
            {
                EncounterAction action = i % 3 == 0 ? Move(i % 2 == 0 ? PiecePosition.Bay(i % 5) : PiecePosition.Bay((i + 2) % 5))
                    : i % 4 == 0 ? (EncounterAction)PlayHandAction.Instance : new RerollDiceAction(1 + i % 31);
                if (first.Snapshot.Phase != EncounterPhase.Playing) break;
                if (first.Execute(action, first.Snapshot.Revision).Rejection != CommandRejection.None) continue;
                Execute(second, action);
                if (restored != null) Execute(restored, action);
                if (i == 5) restored = Restore(catalog, Decode(catalog, EncounterSaveCodec.Serialize(second.CaptureSave())));
                EncounterPersistenceTests.AssertGraphEqual(first.CaptureSave(), second.CaptureSave());
            }
            EncounterPersistenceTests.AssertGraphEqual(first.CaptureSave(), restored.CaptureSave());
        }

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
            Assert.That(session.Snapshot.DisturbedMask, Is.Not.Zero);
            AssertInvalid(haunted, session.CaptureSave(), save =>
                Haunting(save).LatestDisturbedMask = Haunting(save).DisturbedMask == 1 ? 2 : 1);
            AssertInvalid(haunted, session.CaptureSave(), save => Haunting(save).DisturbedMask = DiceValues.AllMask);
            AssertInvalid(haunted, session.CaptureSave(), save => Haunting(save).DisturbedMask = 32);
            AssertInvalid(haunted, Session(haunted).CaptureSave(), save => Haunting(save).DisturbedMask = 1);
            AssertInvalid(haunted, session.CaptureSave(), save => save.RuleStates = Array.Empty<EncounterSaveRuleData>());
            AssertInvalid(haunted, session.CaptureSave(), save => save.RuleStates[0].State = new DieBindingSaveData());
        }

        [Test]
        public void RerollCharmsEvaluateTheDisturbedFace()
        {
            bool found = false;
            for (ulong seed = 0; seed < 400 && !found; ++seed)
            {
                var probe = Execute(Session(Catalog(chance: 100, rerollWeight: 0, flipWeight: 1), seed), new RerollDiceAction(0b11110));
                var flipped = probe.Events.OfType<DieDisturbedEvent>().Single();
                if (CountOf(probe.Events.OfType<DiceRolledEvent>().Single().After, flipped.After) != 0) continue;
                found = true;
                var charm = new ModifierDefinition("charm", "family.charm", ModifierPhase.PlayerReroll,
                    new ModifierCondition(ModifierConditionKind.ContainsFace, flipped.After),
                    new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 9) }, ModifierActivationLimit.Unlimited);
                var catalog = Catalog(chance: 100, rerollWeight: 0, flipWeight: 1, modifiers: new[] { charm });
                var session = Session(catalog, seed);
                var result = Execute(session, new RerollDiceAction(0b11110));
                var disturbed = result.Events.OfType<DieDisturbedEvent>().Single();
                var activated = result.Events.OfType<ModifierActivatedEvent>().Single();
                Assert.That(disturbed.After, Is.EqualTo(flipped.After));
                Assert.That(result.Events.IndexOf(activated), Is.GreaterThan(result.Events.IndexOf(disturbed)),
                    "Reroll charms resolve after the disturbance.");
                Assert.That(result.Snapshot.ModifierBaseScore, Is.EqualTo(9));
                Restore(catalog, session.CaptureSave());
            }
            Assert.That(found, Is.True);
        }

        [Test]
        public void RestoreRejectsARerollDisturbanceOnARerolledDie()
        {
            var catalog = Catalog(chance: 100);
            var session = Session(catalog, 5);
            Execute(session, new RerollDiceAction(1));
            Assert.That(session.Snapshot.DisturbedMask, Is.Not.Zero);
            AssertInvalid(catalog, session.CaptureSave(), save =>
            {
                save.LatestRerollMask |= Haunting(save).DisturbedMask;
                save.EverRerolledMask |= Haunting(save).DisturbedMask;
            }, "kept die");
        }

        [Test]
        public void CurrentFormatJsonRequiresTheNewFieldsAndEarlierFormatsRejectPieces()
        {
            var catalog = Catalog(chance: 0);
            var session = Session(catalog);
            foreach (string field in new[] { nameof(EncounterSaveData.Pieces), nameof(EncounterSaveData.RuleRandomState),
                nameof(EncounterSaveData.RuleStates) })
            {
                var json = JObject.Parse(Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(session.CaptureSave())));
                json.Remove(field);
                Assert.Throws<FormatException>(() => Decode(catalog, json), field);
            }
            var downgraded = JObject.Parse(Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(session.CaptureSave())));
            downgraded[nameof(EncounterSaveData.SaveFormatVersion)] = 5;
            downgraded[nameof(EncounterSaveData.RulesVersion)] = 7;
            var result = EncounterSession.RestoreEncounter(catalog, Decode(catalog, downgraded));
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState), result.Detail);
        }

        [Test]
        public void ExistingEncountersSaveWithoutPiecesOrRuleStream()
        {
            var catalog = TestContent.BuildStandardCatalog();
            foreach (string id in new[] { "encounter.standard", "encounter.fox.introduction", "encounter.owl.watchtower",
                "encounter.moth.crossroads" })
            {
                var session = EncounterSession.CreateEncounter(catalog, id, 44);
                Execute(session, new RerollDiceAction(5));
                Execute(session, PlayHandAction.Instance);
                var save = session.CaptureSave();
                Assert.That(session.Snapshot.PieceInteractions, Is.Empty, id);
                Assert.That(save.Pieces, Is.Empty, id);
                Assert.That(save.RuleRandomState, Is.Zero, id);
                Assert.That(save.RuleStates, Is.Empty, id);
                Restore(catalog, Decode(catalog, EncounterSaveCodec.Serialize(save)));
            }
        }

        [Test]
        public void MerchantContentAuthorsHauntingTheCandleWardAndItsUnlocks()
        {
            var catalog = TestContent.BuildStandardCatalog();
            var merchant = catalog.GetEncounter("encounter.merchant.unquiet_cart");
            var haunting = merchant.Rules.OfType<DieInterferenceRuleDefinition>().Single();
            Assert.That(haunting.Id, Is.EqualTo("rule.merchant.haunting"));
            Assert.That(haunting.ChancePercent, Is.EqualTo(70));
            Assert.That(haunting.ForcedRerollWeight, Is.EqualTo(1));
            Assert.That(haunting.FlipWeight, Is.EqualTo(1));
            Assert.That(haunting.AfterPlayerReroll && haunting.AfterPlayHand, Is.True);
            var ward = merchant.Rules.OfType<BayWardRuleDefinition>().Single();
            Assert.That(ward.Id, Is.EqualTo("rule.merchant.candle_ward"));
            Assert.That(ward.PieceId, Is.EqualTo("piece.merchant.candle"));
            Assert.That(ward.Blocks(DieInterferenceKind.ForcedReroll) && ward.Blocks(DieInterferenceKind.Flip), Is.True);
            var candle = (PositionInteractionDefinition)merchant.Pieces.Single().Interactions.Single();
            Assert.That(merchant.Pieces.Single().Id, Is.EqualTo("piece.merchant.candle"));
            Assert.That(candle.StartingPosition, Is.EqualTo(PiecePosition.Neutral));
            Assert.That(candle.Timing, Is.EqualTo(PieceMovementTiming.DuringHand));
            Assert.That(candle.AllowsNeutral, Is.True);
            Assert.That(candle.AllowedBayMask, Is.EqualTo(DiceValues.AllMask));
            Assert.That(merchant.Stages.Count, Is.EqualTo(10));
            Assert.That(merchant.UnlockMilestones.Select(value => value.StageNumber), Is.EqualTo(new[] { 3, 6, 10 }));
            var families = new[] { "sum_less_than_initial", "rank_equals_previous", "sum_divisible_by" };
            Assert.That(merchant.UnlockRewardModifierIds, Is.EquivalentTo(families.SelectMany(family => new[]
                { "modifier.play_hand." + family + ".base_score", "modifier.play_hand." + family + ".multiplier" })));
            Assert.That(merchant.EncounterModifierIds, Is.Empty);
            foreach (var layout in merchant.RewardPolicy.ModifierLayouts)
                foreach (string poolId in layout.PoolIds)
                    foreach (string family in families)
                        Assert.That(catalog.GetRewardPool(poolId).ModifierIds.Any(id => id.Contains("." + family + ".")), Is.True,
                            poolId + " offers " + family);
            var session = EncounterSession.CreateEncounter(catalog, merchant.Id, 3);
            Assert.That(session.Snapshot.PieceInteractions.Single().IsAvailable, Is.True);
            Assert.That(session.CaptureSave().RuleRandomState, Is.Not.Zero);
        }

        [Test]
        public void OnlyInterferenceRulesSeedAndAdvanceTheRuleStream()
        {
            var plain = Catalog(chance: 0, haunting: false);
            var session = Session(plain, 31);
            Execute(session, new RerollDiceAction(3));
            Execute(session, PlayHandAction.Instance);
            Assert.That(session.CaptureSave().RuleRandomState, Is.Zero);
            AssertInvalid(plain, session.CaptureSave(), save => save.RuleRandomState = 1);
            Assert.That(Session(Catalog(chance: 20), 31).CaptureSave().RuleRandomState, Is.Not.Zero);
        }

        [Test]
        public void FormatFiveSavesRestoreWithoutPiecesOrDisturbances()
        {
            var catalog = Catalog(chance: 0, haunting: false, pieces: false);
            var original = Session(catalog, 12);
            Execute(original, new RerollDiceAction(3));
            var json = JObject.Parse(Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(original.CaptureSave())));
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

        private static EncounterSession Session(MechanicalCatalog catalog, ulong seed = 14)
            => EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, seed);

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

        private static int CountOf(DiceValues dice, int face)
        {
            int count = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if (dice[i] == face) ++count;
            return count;
        }

        private static EncounterSaveData Decode(MechanicalCatalog catalog, byte[] bytes) => EncounterSaveCodec.Deserialize(bytes, catalog);
        private static EncounterSaveData Decode(MechanicalCatalog catalog, JObject json) => EncounterSaveCodec.Deserialize(Encoding.UTF8.GetBytes(json.ToString()), catalog);
    }
}
