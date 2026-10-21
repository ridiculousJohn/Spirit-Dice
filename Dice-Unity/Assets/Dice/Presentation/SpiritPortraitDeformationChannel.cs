using UnityEngine;

namespace Dice.Presentation
{
    /** A scalar animation binding applies one authored shape without replacing other channel weights. */
    [ExecuteAlways]
    public sealed class SpiritPortraitDeformationChannel : MonoBehaviour
    {
        [SerializeField] private SpiritPortraitImage image;
        [SerializeField] private int shapeIndex;
        [SerializeField] private float weight;

        private void OnEnable() => Apply();
        private void OnDidApplyAnimationProperties() => Apply();

        private void Apply()
        {
            if (image != null) image.SetWeight(shapeIndex, weight);
        }
    }
}
