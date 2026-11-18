using System;
using System.Collections.Generic;
using Dice.Engine;

namespace Dice.Presentation
{
    /** What giving rewards would add that the player lacks: whether anything, and the items of it, each once. */
    public readonly struct RewardLack
    {
        public static RewardLack None => new RewardLack(false, Array.Empty<ItemDefinition.Id>());

        /** Whether giving the rewards would add anything: an item, a charm or a family. */
        public bool Any { get; }
        /** The items the rewards hold that the player does not own. */
        public IReadOnlyList<ItemDefinition.Id> Items { get; }

        public RewardLack(bool any, IReadOnlyList<ItemDefinition.Id> items)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            Any = any;
        }
    }
}
