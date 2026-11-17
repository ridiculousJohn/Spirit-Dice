using System;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using NUnit.Framework;
using static Dice.Tests.TestContent;

namespace Dice.Tests
{
    /** The carry-over rule, provided by an item, and the charms a rule brings to the encounters it is in play in. */
    public sealed class DieCarryOverTests
    {
        private static readonly EncounterRuleDefinition.Id CarryId = TestRules.IdOf("carry_over");
        private static readonly EncounterRuleDefinition.Id RotId = TestRules.IdOf("carry_over.rot");
        private static readonly TablePieceDefinition.Id PieceId = TestPieces.IdOf("piece.carry_over");
        private static readonly ItemDefinition.Id ItemId = new ItemDefinition.Id(new Guid("3b5e0d7a9c1f4e2b8a6d0c4f1e7b9a25"));
        private static readonly ItemDefinition.Id OtherItemId = new ItemDefinition.Id(new Guid("8c2f6a1d4e9b4b7a9f3e5d0c2b1a7e64"));
        private static readonly EncounterRuleDefinition.Id OtherCarryId = TestRules.IdOf("carry_over.other");
        private static readonly TablePieceDefinition.Id OtherPieceId = TestPieces.IdOf("piece.carry_over.other");
        private static readonly EncounterRuleDefinition.Id BindingId = TestRules.IdOf("carry_over.binding");
        // The test piece holds one interaction, its position.
        private const int InteractionIndex = 0;
        private const int CarriedBay = 2;
        private const ulong Seed = 17;

        /**
         * Checks the die under the piece when a hand is played keeps its face and bay through the next opening roll while the
         * others are rolled, and has carried status; the first hand and a hand played with the piece at neutral carry nothing /
         * fails if the held die is rolled, another die is held, or a carry appears without a played hand on a bay.
         */
        [Test]
        public void TheDieUnderThePieceCarriesIntoTheNextOpeningRoll()
        {
            var session = EncounterSession.CreateEncounter(Catalog(), EncounterId, Seed);
            AssertNothingCarried(session.InitialResolution.Events, session.Snapshot);

            var played = session.Snapshot.CurrentDice;
            var result = Execute(session, PlayHandAction.Instance);
            var opening = result.Events.OfType<DiceRolledEvent>().Single();
            Assert.That(opening.Kind, Is.EqualTo(RollKind.Initial));
            Assert.That(opening.SelectedMask, Is.EqualTo(DiceValues.AllMask & ~(1 << CarriedBay)), "The other four dice roll.");
            Assert.That(opening.ChangedMask, Is.EqualTo(opening.SelectedMask));
            var carried = result.Events.OfType<DieCarriedEvent>().Single();
            Assert.That(carried.RuleId, Is.EqualTo(CarryId));
            Assert.That(carried.Bay, Is.EqualTo(CarriedBay));
            Assert.That(carried.Face, Is.EqualTo(played[CarriedBay]));
            Assert.That(result.Events.IndexOf(carried), Is.GreaterThan(result.Events.IndexOf(opening)));
            Assert.That(result.Snapshot.CurrentDice[CarriedBay], Is.EqualTo(played[CarriedBay]));
            Assert.That(Carry(result.Snapshot).CarriedMask, Is.EqualTo(1 << CarriedBay));
            Assert.That(Carry(result.Snapshot).DiagnosticDieLabel(CarriedBay), Is.EqualTo("CARRIED"));

            Execute(session, Move(PiecePosition.Neutral));
            result = Execute(session, PlayHandAction.Instance);
            AssertNothingCarried(result.Events, result.Snapshot);
        }

        /** Checks a player reroll of the carried die ends its status while rerolling another die keeps it / fails if a kept carried die loses its status or a rerolled one keeps it. */
        [Test]
        public void OnlyAPlayerRerollOfTheCarriedDieEndsItsStatus()
        {
            var session = EncounterSession.CreateEncounter(Catalog(), EncounterId, Seed);
            Execute(session, PlayHandAction.Instance);

            var kept = Execute(session, new RerollDiceAction(1));
            Assert.That(kept.Events.OfType<CarriedStatusEndedEvent>(), Is.Empty);
            Assert.That(Carry(kept.Snapshot).CarriedMask, Is.EqualTo(1 << CarriedBay));

            var rerolled = Execute(session, new RerollDiceAction(1 << CarriedBay));
            var ended = rerolled.Events.OfType<CarriedStatusEndedEvent>().Single();
            Assert.That(ended.RuleId, Is.EqualTo(CarryId));
            Assert.That(ended.Bay, Is.EqualTo(CarriedBay));
            Assert.That(Carry(rerolled.Snapshot).CarriedMask, Is.Zero);
        }

        /**
         * Checks the pending carry survives a stage clear and a save and restore taken in the reward phases between the played
         * hand and the next opening roll, and carried status survives a save and restore during the hand / fails if the carry
         * or the status is lost or the stage's first opening roll rolls the carried die.
         */
        [Test]
        public void TheCarrySurvivesAStageClearAndSaves()
        {
            var catalog = Catalog(rewards: true);
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, Seed);
            var played = session.Snapshot.CurrentDice;
            var cleared = Execute(session, PlayHandAction.Instance);
            Assert.That(cleared.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));

            session = RoundTrip(catalog, session);
            Execute(session, new SelectModifierAction(session.Snapshot.PendingReward.OfferId,
                session.Snapshot.PendingReward.Options[0].OptionId));
            var upgrade = session.Snapshot.PendingReward;
            var started = Execute(session, new SelectHandUpgradeAction(upgrade.OfferId, upgrade.Options[0].OptionId));
            Assert.That(started.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(started.Events.OfType<DieCarriedEvent>().Single().Bay, Is.EqualTo(CarriedBay));
            Assert.That(started.Snapshot.CurrentDice[CarriedBay], Is.EqualTo(played[CarriedBay]));

            session = RoundTrip(catalog, session);
            Assert.That(Carry(session.Snapshot).CarriedMask, Is.EqualTo(1 << CarriedBay));
        }

        /** Checks a die withered when the hand is played does not carry / fails if the next opening roll holds an uncounted die. */
        [Test]
        public void ADieThatDoesNotCountDoesNotCarry()
        {
            var catalog = Catalog(rot: true);
            var session = WithDice(catalog, Seed, 3, 4, 1, 5, 6);
            var withered = Execute(session, new RerollDiceAction(1));
            Assert.That(withered.Snapshot.CountedMask & (1 << CarriedBay), Is.Zero, "The kept 1 under the piece withers.");
            var result = Execute(session, PlayHandAction.Instance);
            AssertNothingCarried(result.Events, result.Snapshot);
        }

        /** Checks rot keeps the count of a die the opening roll holds and clears the thrown dice / fails if the held die's rot is cleared or a thrown die keeps its count. */
        [Test]
        public void RotKeepsTheCountOfADieTheOpeningRollHolds()
        {
            var catalog = Catalog(rot: true);
            var session = WithDice(catalog, Seed, 3, 4, 5, 5, 6);
            Execute(session, new RerollDiceAction(1));
            var result = Execute(session, PlayHandAction.Instance);
            var restored = result.Events.OfType<DiceRestoredEvent>().Single();
            Assert.That(restored.Roll, Is.EqualTo(RollKind.Initial));
            Assert.That(restored.ClearedMask, Is.EqualTo(0b11010));
            var rot = result.Snapshot.EncounterRules.OfType<RotRuleSnapshot>().Single();
            Assert.That(rot.Counts.ToArray(), Is.EqualTo(new[] { 0, 0, 1, 0, 0 }));
            Assert.That(result.Snapshot.CurrentDice[CarriedBay], Is.EqualTo(4));
        }

        /**
         * Checks the charms a rule lists follow the encounter's own in its charm additions in play and are offered by the reward
         * generator / fails if a rule's charms are missing from the additions or the offer.
         */
        [Test]
        public void ARulesCharmsAreInTheEncountersAdditionsAndOffered()
        {
            var own = RewardTestContent.Modifier("carry_over.own", "family.carry_over.own");
            var carried = RewardTestContent.Modifier("carry_over.carried", "family.carry_over.carried");
            var plain = RewardTestContent.Modifier("carry_over.plain", "family.carry_over.plain");
            var policy = RewardTestContent.Policy(new[]
                { new ModifierRewardLayout(new[] { new ModifierRewardPoolDefinition("pool", new[] { plain.Identity }) }) });
            var catalog = Catalog(rewards: true, policy: policy, ownCharms: new[] { own.Identity }, ruleCharms: new[] { carried.Identity },
                modifiers: new[] { own, carried, plain });
            var encounter = catalog.GetEncounter(EncounterId);
            Assert.That(encounter.EncounterModifierIds, Is.EqualTo(new[] { own.Identity, carried.Identity }));
            Assert.That(encounter.OwnEncounterModifierIds, Is.EqualTo(new[] { own.Identity }));

            var random = new DeterministicRandom(Seed);
            var offered = RewardGenerator.GenerateModifiers(catalog, policy, 1, new[] { plain.Identity, own.Identity },
                Array.Empty<ModifierFamilyDefinition.Id>(), ref random, new EncounterInPlay(encounter, Array.Empty<ItemDefinition>()));
            Assert.That(offered.ModifierIds, Is.EqualTo(new[] { carried.Identity }));
        }

        /** Checks a charm in play twice fails naming it / fails if an encounter listing a rule's charm again builds. */
        [Test]
        public void ACharmInPlayTwiceFails()
        {
            var charm = TestModifiers.IdOf("carry_over.twice");
            var exception = Assert.Throws<ArgumentException>(() => Encounter(ownCharms: new[] { charm }, ruleCharms: new[] { charm }));
            Assert.That(exception.Message, Does.Contain(charm.ToString()));
        }

        /** Checks the catalog rejects an item whose rule lists a charm the catalog lacks, naming the charm and the rule / fails if the item loads. */
        [Test]
        public void TheCatalogRejectsAnItemWhoseRuleListsAMissingCharm()
        {
            var missing = TestModifiers.IdOf("carry_over.missing");
            var plain = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 20, 20) });
            var exception = Assert.Throws<ArgumentException>(() => new MechanicalCatalog("test.carry_over.items", Hands(), new[] { plain },
                items: new[] { Item(new[] { missing }) }));
            Assert.That(exception.Message, Does.Contain(missing.ToString()));
            Assert.That(exception.Message, Does.Contain(CarryId.ToString()));
        }

        /**
         * Checks the catalog rejects an item whose rule lists a charm that can never activate with the item's own rules in
         * play, naming the charm, the rule and the item / fails if the item loads or the message omits one of them.
         */
        [Test]
        public void TheCatalogRejectsAnItemCharmThatCannotActivateWithTheItemsRules()
        {
            var bound = new ModifierDefinition(TestModifiers.IdOf("carry_over.bound"), TestFamilies.Of("family.carry_over.bound"),
                ModifierPhase.PlayHand, new BoundFaceCountAtLeastCondition(3), new ModifierEffect[] { new AddBaseScoreEffect(10) });
            var plain = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 20, 20) });
            var exception = Assert.Throws<ArgumentException>(() => new MechanicalCatalog("test.carry_over.items", Hands(), new[] { plain },
                new[] { bound }, new[] { Item(new[] { bound.Identity }) }));
            Assert.That(exception.Message, Does.Contain(bound.Identity.ToString()).And.Contain(CarryId.ToString())
                .And.Contain(ItemId.ToString()));
        }

        /**
         * Checks what a run plays puts the carried items' rules and pieces in play after the encounter's own and its lent
         * items', keeps a lent item in play once and out of the carried items, and keeps a carried rule's charms out of the
         * encounter's additions / fails if the order differs, a lent item is carried, a carried charm joins the additions, or
         * a carried charm already in play is accepted.
         */
        [Test]
        public void CarriedItemsAreInPlayAfterTheEncountersOwnAndLentItems()
        {
            var ownCharm = TestModifiers.IdOf("carry_over.own");
            var lentCharm = TestModifiers.IdOf("carry_over.lent");
            var carriedCharm = TestModifiers.IdOf("carry_over.carried");
            var lent = Item(new[] { lentCharm });
            var carried = OtherItem(new[] { carriedCharm });
            var encounter = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                rules: new[] { new RotRuleDefinition(RotId, RotEndpoint.Wither) }, encounterModifierIds: new[] { ownCharm },
                items: new[] { lent });

            var inPlay = new EncounterInPlay(encounter, new[] { carried, lent });
            Assert.That(inPlay.Encounter, Is.SameAs(encounter));
            Assert.That(inPlay.CarriedItems, Is.EqualTo(new[] { carried }), "The lent item is not carried.");
            Assert.That(inPlay.Items, Is.EqualTo(new[] { lent, carried }));
            Assert.That(inPlay.Rules.Select(rule => rule.Identity), Is.EqualTo(new[] { RotId, CarryId, OtherCarryId }));
            Assert.That(inPlay.Pieces.Select(piece => piece.Identity), Is.EqualTo(new[] { PieceId, OtherPieceId }));
            Assert.That(encounter.EncounterModifierIds, Is.EqualTo(new[] { ownCharm, lentCharm }));
            Assert.That(inPlay.CarriedModifierIds, Is.EqualTo(new[] { carriedCharm }));

            var exception = Assert.Throws<ArgumentException>(() => new EncounterInPlay(encounter, new[] { OtherItem(new[] { ownCharm }) }));
            Assert.That(exception.Message, Does.Contain(ownCharm.ToString()));
        }

        /**
         * Checks the catalog rejects an encounter that cannot be played with a catalog item carried, naming the encounter and
         * the cause / fails if the catalog loads or the message omits the encounter or the piece in play twice.
         */
        [Test]
        public void TheCatalogRejectsAnEncounterThatCannotBePlayedWithACatalogItemCarried()
        {
            var clashing = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                pieces: new[] { Piece(PieceId) });
            var exception = Assert.Throws<ArgumentException>(() => new MechanicalCatalog("test.carry_over.carried", Hands(),
                new[] { clashing }, items: new[] { Item() }));
            Assert.That(exception.Message, Does.Contain(EncounterId.ToString()).And.Contain(PieceId.ToString()));
        }

        /**
         * Checks a carried item's charm is a candidate from the first offer while the encounter's own addition waits for the
         * stage its setting names / fails if the carried charm waits or the addition is offered early.
         */
        [Test]
        public void ACarriedItemsCharmIsOfferedFromTheFirstOfferWhileTheEncountersAdditionWaits()
        {
            var own = RewardTestContent.Modifier("carry_over.own", "family.carry_over.own");
            var carried = RewardTestContent.Modifier("carry_over.carried", "family.carry_over.carried");
            var plain = RewardTestContent.Modifier("carry_over.plain", "family.carry_over.plain");
            var policy = RewardTestContent.Policy(new[]
                { new ModifierRewardLayout(new[] { new ModifierRewardPoolDefinition("pool", new[] { plain.Identity }) }) });
            var encounter = new EncounterDefinition(EncounterId, new[]
            {
                new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards), new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards),
                new StageDefinition(1000000, 3, 3)
            }, rewardPolicy: policy, encounterModifierIds: new[] { own.Identity }, encounterModifiersFirstClearedStageNumber: 2);
            var catalog = new MechanicalCatalog("test.carry_over.carried", Hands(), new[] { encounter }, new[] { own, carried, plain },
                new[] { Item(new[] { carried.Identity }) });
            var inPlay = catalog.GetEncounterInPlay(EncounterId, new[] { ItemId });

            Assert.That(Offer(catalog, inPlay, 1, plain.Identity), Is.EqualTo(new[] { carried.Identity }));
            Assert.That(Offer(catalog, inPlay, 1, plain.Identity, carried.Identity), Is.Empty, "The addition waits for its stage.");
            Assert.That(Offer(catalog, inPlay, 2, plain.Identity, carried.Identity), Is.EqualTo(new[] { own.Identity }));
        }

        /**
         * Checks a run with a carried item saves its id and restores what the run plays with the carried rule's state,
         * through capture and restore and through the codec / fails if the item, the piece or the carry is lost.
         */
        [Test]
        public void ARunWithACarriedItemSavesAndRestoresIt()
        {
            var catalog = CarryingCatalog();
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, Seed, carriedItemIds: new[] { ItemId });
            Assert.That(session.InPlay.CarriedItems.Select(item => item.Identity), Is.EqualTo(new[] { ItemId }));
            Execute(session, PlayHandAction.Instance);
            var save = session.CaptureSave();
            Assert.That(save.CarriedItemIds, Is.EqualTo(new[] { ItemId }));
            var decoded = EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(save), catalog);
            var state = decoded.RuleStates.Single(entry => entry.RuleId == CarryId).State;
            Assert.That(state, Is.TypeOf<DieCarryOverSaveData>(), "The carried rule's state is read by its rule's id with its type.");
            Assert.That(((DieCarryOverSaveData)state).CarriedMask, Is.EqualTo(1 << CarriedBay));

            foreach (var restored in new[] { Restore(catalog, save), RoundTrip(catalog, session) })
            {
                Assert.That(restored.InPlay.CarriedItems.Select(item => item.Identity), Is.EqualTo(new[] { ItemId }));
                Assert.That(restored.InPlay.Pieces.Select(piece => piece.Identity), Is.EqualTo(new[] { PieceId }));
                Assert.That(Carry(restored.Snapshot).CarriedMask, Is.EqualTo(1 << CarriedBay));
            }
        }

        /** Checks a save carrying an item the catalog lacks fails as an invalid definition / fails if it restores or fails otherwise. */
        [Test]
        public void ASaveCarryingAnItemTheCatalogLacksFails()
        {
            var catalog = CarryingCatalog();
            var save = EncounterSession.CreateEncounter(catalog, EncounterId, Seed).CaptureSave();
            save.CarriedItemIds = new[] { OtherItemId };
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.InvalidDefinition));
        }

        /**
         * Checks a die bound in the piece's bay when the hand is played carries into the next hand like any counted die, and
         * binding releases it at that hand / fails if the bound die is rolled, does not carry or stays bound.
         */
        [Test]
        public void ABoundDieInThePiecesBayCarriesAndBindingReleasesIt()
        {
            var encounter = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                rules: new[] { new DieBindingRuleDefinition(BindingId, true, false) }, items: new[] { Item() });
            var session = EncounterSession.CreateEncounter(new MechanicalCatalog("test.carry_over.binding", Hands(), new[] { encounter }),
                EncounterId, Seed);
            int marked = Binding(session.Snapshot).MarkedMask;
            Assert.That(marked, Is.Not.Zero);
            // The marked die binds after the next player reroll, here of another die.
            Execute(session, new RerollDiceAction(1 << DiceValues.NthBay(DiceValues.AllMask & ~marked, 0)));
            int bound = Binding(session.Snapshot).BoundMask;
            Assert.That(bound, Is.EqualTo(marked));
            int bay = DiceValues.NthBay(bound, 0);
            Execute(session, Move(PiecePosition.Bay(bay)));
            int face = session.Snapshot.CurrentDice[bay];

            var result = Execute(session, PlayHandAction.Instance);
            var carried = result.Events.OfType<DieCarriedEvent>().Single();
            Assert.That(carried.Bay, Is.EqualTo(bay));
            Assert.That(result.Snapshot.CurrentDice[bay], Is.EqualTo(face));
            Assert.That(Carry(result.Snapshot).CarriedMask, Is.EqualTo(bound));
            Assert.That(Binding(result.Snapshot).BoundMask, Is.Zero, "Binding releases the die at the next hand.");
        }

        /**
         * Checks the catalog rejects two rules of its content that share an id but save different types, naming the id /
         * fails if the catalog loads or the message omits the id.
         */
        [Test]
        public void TheCatalogRejectsRulesSharingAnIdWithDifferentSaveTypes()
        {
            var shared = TestRules.IdOf("carry_over.shared");
            var bound = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                rules: new[] { new DieBindingRuleDefinition(shared, true, false) });
            var rotting = new EncounterDefinition(TestEncounters.IdOf("encounter.carry_over.rot"), new[] { new StageDefinition(1000000, 20, 20) },
                rules: new[] { new RotRuleDefinition(shared, RotEndpoint.Wither) });
            var exception = Assert.Throws<ArgumentException>(() => new MechanicalCatalog("test.carry_over.shared", Hands(),
                new[] { bound, rotting }));
            Assert.That(exception.Message, Does.Contain(shared.ToString()));
        }

        private static ModifierDefinition.Id[] Offer(MechanicalCatalog catalog, EncounterInPlay inPlay, int clearedStage,
            params ModifierDefinition.Id[] owned)
        {
            var random = new DeterministicRandom(Seed);
            return RewardGenerator.GenerateModifiers(catalog, inPlay.Encounter.RewardPolicy, clearedStage, owned,
                Array.Empty<ModifierFamilyDefinition.Id>(), ref random, inPlay).ModifierIds.ToArray();
        }

        private static void AssertNothingCarried(IReadOnlyList<RuleEvent> events, EncounterSnapshot snapshot)
        {
            Assert.That(events.OfType<DiceRolledEvent>().Single().SelectedMask, Is.EqualTo(DiceValues.AllMask));
            Assert.That(events.OfType<DieCarriedEvent>(), Is.Empty);
            Assert.That(Carry(snapshot).CarriedMask, Is.Zero);
        }

        private static DieCarryOverRuleSnapshot Carry(EncounterSnapshot snapshot)
            => snapshot.EncounterRules.OfType<DieCarryOverRuleSnapshot>().Single();

        private static EncounterSession RoundTrip(MechanicalCatalog catalog, EncounterSession session)
            => Restore(catalog, EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(session.CaptureSave()), catalog));

        private static InteractWithPieceAction Move(PiecePosition position)
            => new InteractWithPieceAction(PieceId, InteractionIndex, new MovePieceInput(position));

        private static DieBindingRuleSnapshot Binding(EncounterSnapshot snapshot)
            => snapshot.EncounterRules.OfType<DieBindingRuleSnapshot>().Single();

        /** The carry-over item: the rule and its piece, which starts on the carried bay. */
        private static ItemDefinition Item(IEnumerable<ModifierDefinition.Id> ruleCharms = null)
            => new ItemDefinition(ItemId, new[] { new DieCarryOverRuleDefinition(CarryId, PieceId,
                    ruleCharms ?? Array.Empty<ModifierDefinition.Id>()) }, Piece(PieceId));

        /** A second carry-over item with its own rule and piece. */
        private static ItemDefinition OtherItem(IEnumerable<ModifierDefinition.Id> ruleCharms)
            => new ItemDefinition(OtherItemId, new[] { new DieCarryOverRuleDefinition(OtherCarryId, OtherPieceId, ruleCharms) },
                Piece(OtherPieceId));

        /** A piece whose position starts on the carried bay. */
        private static TablePieceDefinition Piece(TablePieceDefinition.Id id)
            => new TablePieceDefinition(id, new[] { new PositionInteractionDefinition(PiecePosition.Bay(CarriedBay),
                PieceMovementTiming.DuringHand) });

        /** A catalog whose encounter lends nothing and whose item list holds the carry-over item. */
        private static MechanicalCatalog CarryingCatalog()
            => new MechanicalCatalog("test.carry_over.carried", Hands(),
                new[] { new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 20, 20) }) }, items: new[] { Item() });

        private static EncounterDefinition Encounter(bool rot = false, bool rewards = false, RewardPolicyDefinition policy = null,
            IEnumerable<ModifierDefinition.Id> ownCharms = null, IEnumerable<ModifierDefinition.Id> ruleCharms = null)
        {
            var stages = rewards
                ? new[] { new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards), new StageDefinition(1000000, 3, 3) }
                : new[] { new StageDefinition(1000000, 20, 20) };
            var rules = rot ? new EncounterRuleDefinition[] { new RotRuleDefinition(RotId, RotEndpoint.Wither) } : null;
            return new EncounterDefinition(EncounterId, stages, rewardPolicy: rewards ? policy ?? RewardTestContent.Policy() : null,
                rules: rules, encounterModifierIds: ownCharms, items: new[] { Item(ruleCharms) });
        }

        private static MechanicalCatalog Catalog(bool rot = false, bool rewards = false, RewardPolicyDefinition policy = null,
            IEnumerable<ModifierDefinition.Id> ownCharms = null, IEnumerable<ModifierDefinition.Id> ruleCharms = null,
            IEnumerable<ModifierDefinition> modifiers = null)
        {
            // Without a policy, the default reward policy draws from three default charms.
            var charms = modifiers ?? (rewards ? new[] { RewardTestContent.Modifier("first", "family.first"),
                RewardTestContent.Modifier("second", "family.second"), RewardTestContent.Modifier("third", "family.third") }
                : Array.Empty<ModifierDefinition>());
            return new MechanicalCatalog("test.carry_over", Hands(), new[] { Encounter(rot, rewards, policy, ownCharms, ruleCharms) },
                charms);
        }
    }
}
