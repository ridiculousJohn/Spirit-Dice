using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Binds a discovered clue to its authored card. */
    public sealed class SpiritCompletionClue : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private Text description;

        public void Bind(string titleText, string descriptionText)
        {
            if (title == null || description == null)
                throw new InvalidOperationException("The completion clue requires authored UI references.");
            title.text = titleText;
            description.text = descriptionText;
        }
    }
}
