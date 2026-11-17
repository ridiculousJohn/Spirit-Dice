using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The bay indicators of one tray binding: each made from its prefab under a bay's root, which shows and hides it with
     * the tray's content, scaled so its authored offsets are in die sizes, shown on one bay at a time or hidden, and
     * destroyed when released or when the tray releases its presentation. The emblems of the indicators shown on one bay lie
     * side by side in creation order about the bay's label anchor, laid out again only when an indicator is shown, hidden
     * or released.
     */
    internal sealed class SpiritBayIndicators
    {
        private readonly IReadOnlyList<Transform> bayRoots;
        private readonly IReadOnlyList<Transform> labelAnchors;
        private readonly float dieScale;
        // In creation order, which orders the emblems sharing a bay.
        private readonly List<Indicator> indicators = new List<Indicator>();

        /**
         * bayRoots are the roots of the five bays' contents, labelAnchors the tray geometry's anchors at each bay's front lip,
         * where the bay's emblems sit, and dieScale the size of a die in the bay roots' space.
         */
        internal SpiritBayIndicators(IReadOnlyList<Transform> bayRoots, IReadOnlyList<Transform> labelAnchors, float dieScale)
        {
            this.bayRoots = bayRoots ?? throw new ArgumentNullException(nameof(bayRoots));
            this.labelAnchors = labelAnchors ?? throw new ArgumentNullException(nameof(labelAnchors));
            if (bayRoots.Count != DiceValues.Count) throw new ArgumentException("A tray has five bay roots.", nameof(bayRoots));
            if (labelAnchors.Count != DiceValues.Count) throw new ArgumentException("A tray has five label anchors.", nameof(labelAnchors));
            this.dieScale = dieScale;
        }

        /** An indicator made from the prefab, showing the icon and glow colour, hidden under the first bay's root. */
        internal BayIndicator Create(SpiritBayIndicator prefab, Sprite icon, Color glowColor)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (icon == null) throw new ArgumentNullException(nameof(icon));
            SpiritBayIndicator view = UnityEngine.Object.Instantiate(prefab, bayRoots[0], false);
            view.gameObject.SetActive(false);
            view.transform.localScale = prefab.transform.localScale * dieScale;
            view.Initialize(icon, glowColor);
            var indicator = new Indicator(this, view);
            indicators.Add(indicator);
            return indicator;
        }

        /** Destroys every indicator; one a presenter releases afterwards stays released. */
        internal void ReleaseAll()
        {
            foreach (var indicator in indicators) indicator.Destroy();
            indicators.Clear();
        }

        private void Show(Indicator indicator, int bay)
        {
            if ((uint)bay >= DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(bay));
            if (indicator.Released) throw new InvalidOperationException("A released bay indicator cannot be shown.");
            if (indicator.Bay == bay) return;
            int previous = indicator.Bay;
            indicator.View.transform.SetParent(bayRoots[bay], false);
            indicator.Bay = bay;
            indicator.View.gameObject.SetActive(true);
            if (previous >= 0) LayOut(previous);
            LayOut(bay);
        }

        private void Hide(Indicator indicator)
        {
            if (indicator.Bay < 0) return;
            int previous = indicator.Bay;
            indicator.Bay = -1;
            indicator.View.gameObject.SetActive(false);
            LayOut(previous);
        }

        private void Release(Indicator indicator)
        {
            if (indicator.Released) return;
            int previous = indicator.Bay;
            indicators.Remove(indicator);
            indicator.Destroy();
            if (previous >= 0) LayOut(previous);
        }

        /** Lays the emblems of the indicators shown on the bay side by side in creation order, about the bay's label anchor. */
        private void LayOut(int bay)
        {
            int count = 0;
            foreach (var indicator in indicators)
                if (indicator.Bay == bay) ++count;
            int place = 0;
            foreach (var indicator in indicators)
                if (indicator.Bay == bay) indicator.View.PlaceEmblem(labelAnchors[bay].position, place++, count);
        }

        private sealed class Indicator : BayIndicator
        {
            private readonly SpiritBayIndicators owner;

            internal Indicator(SpiritBayIndicators owner, SpiritBayIndicator view)
            {
                this.owner = owner;
                View = view;
            }

            internal SpiritBayIndicator View { get; }
            /** The bay the indicator is shown on, or -1 while hidden or released. */
            internal int Bay { get; set; } = -1;
            internal bool Released { get; private set; }

            public override void ShowOn(int bay) => owner.Show(this, bay);
            public override void Hide() => owner.Hide(this);
            public override void Release() => owner.Release(this);

            internal void Destroy()
            {
                Released = true;
                Bay = -1;
                OwnedObject.Release(View.gameObject);
            }
        }
    }
}
