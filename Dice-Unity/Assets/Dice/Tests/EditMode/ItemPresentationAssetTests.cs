using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /** How an item presentation is checked against its item's definition when the item skin loads. */
    public sealed class ItemPresentationAssetTests
    {
        private static readonly ItemDefinition.Id ItemId = new ItemDefinition.Id(new Guid("5d1a7c3e9b2f4a6c8e0d2b4f6a8c1e3d"));
        private const string PieceName = "piece.test.item";
        private const string RuleName = "rule.test.item";
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; --i)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        /**
         * Checks a presentation with a name, an icon, a card for each rule and a valid piece presenter of a kind its item's
         * piece has is accepted, and one without a name or icon, without a piece presenter for a piece, with one for an item
         * without a piece, with one of a kind the piece has no interaction of, or with a bay indicator presenter without its
         * prefab is rejected / fails if an incomplete presentation loads or a complete one is refused.
         */
        [Test]
        public void ValidateRequiresTheNameIconAndAPiecePresenterExactlyForAPiece()
        {
            var positioned = new TablePieceDefinition(TestPieces.IdOf(PieceName),
                new[] { new PositionInteractionDefinition(PiecePosition.Neutral, PieceMovementTiming.DuringHand) });
            var withPiece = new ItemDefinition(ItemId, piece: positioned);
            var presenter = Create<BayIndicatorPresenterAsset>();
            SyntheticUi.Set(presenter, "indicatorPrefab", IndicatorPrefab());

            var presentation = Presentation("Lantern", Icon(), presenter);
            Assert.DoesNotThrow(() => presentation.Validate(withPiece));

            Assert.Throws<InvalidOperationException>(() => Presentation(" ", Icon(), presenter).Validate(withPiece), "No name.");
            Assert.Throws<InvalidOperationException>(() => Presentation("Lantern", null, presenter).Validate(withPiece), "No icon.");
            Assert.Throws<InvalidOperationException>(() => Presentation("Lantern", Icon(), null).Validate(withPiece),
                "A piece without a piece presenter.");
            var unpositioned = new ItemDefinition(ItemId, piece: new TablePieceDefinition(TestPieces.IdOf(PieceName)));
            Assert.Throws<InvalidOperationException>(() => presentation.Validate(unpositioned),
                "A presenter of a kind the piece has no interaction of.");
            Assert.Throws<InvalidOperationException>(() => Presentation("Lantern", Icon(), Create<BayIndicatorPresenterAsset>())
                .Validate(withPiece), "A bay indicator presenter without its prefab.");

            var ruleId = TestRules.IdOf(RuleName);
            var withoutPiece = new ItemDefinition(ItemId, new[] { new FirstRerollFreeRuleDefinition(ruleId) });
            var card = Create<EncounterRulePresentationAsset>();
            SyntheticUi.Set(card, "rule", Own(TestRules.Asset(ruleId)));
            var ruled = Presentation("Lantern", Icon(), presenter, card);
            Assert.Throws<InvalidOperationException>(() => ruled.Validate(withoutPiece), "A piece presenter without a piece.");
            Assert.DoesNotThrow(() => Presentation("Lantern", Icon(), null, card).Validate(withoutPiece));
        }

        private ItemPresentationAsset Presentation(string displayName, Sprite icon, ItemPiecePresenterAsset piecePresenter,
            params EncounterRulePresentationAsset[] cards)
        {
            var item = Create<ItemAsset>();
            item.name = "Test Item";
            var presentation = Create<ItemPresentationAsset>();
            SyntheticUi.Set(presentation, "item", item);
            SyntheticUi.Set(presentation, "displayName", displayName);
            SyntheticUi.Set(presentation, "icon", icon);
            SyntheticUi.Set(presentation, "piecePresenter", piecePresenter);
            SyntheticUi.Set(presentation, "ruleCards", cards);
            return presentation;
        }

        /** A bay indicator with its glow and emblem sprites on the tray's render layer. */
        private SpiritBayIndicator IndicatorPrefab()
        {
            var root = Own(new GameObject("Bay Indicator") { layer = SpiritDiceTray.RenderLayer });
            var indicator = root.AddComponent<SpiritBayIndicator>();
            var glow = new GameObject("Glow", typeof(SpriteRenderer)) { layer = SpiritDiceTray.RenderLayer };
            glow.transform.SetParent(root.transform, false);
            var emblem = new GameObject("Emblem", typeof(SpriteRenderer)) { layer = SpiritDiceTray.RenderLayer };
            emblem.transform.SetParent(root.transform, false);
            SyntheticUi.Set(indicator, "glow", glow.GetComponent<SpriteRenderer>());
            SyntheticUi.Set(indicator, "emblem", emblem.GetComponent<SpriteRenderer>());
            return indicator;
        }

        private Sprite Icon()
        {
            var texture = Own(new Texture2D(2, 2));
            return Own(Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f)));
        }

        private T Create<T>() where T : ScriptableObject => Own(ScriptableObject.CreateInstance<T>());

        private T Own<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
