using UnityEngine;
using UnityEngine.Rendering;

namespace Dice.Presentation
{
    /** Selects the hybrid scene's renderer without changing the project's diagnostic renderer. */
    [DefaultExecutionOrder(-1000)]
    public sealed class SpiritPresentationEnvironment : MonoBehaviour
    {
        [SerializeField] private RenderPipelineAsset pipeline;
        private RenderPipelineAsset previous;
        private ScreenOrientation previousOrientation;

        private void Awake()
        {
            previous = QualitySettings.renderPipeline;
            previousOrientation = Screen.orientation;
            if (pipeline != null) QualitySettings.renderPipeline = pipeline;
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;
        }

        private void OnDestroy()
        {
            if (pipeline != null && QualitySettings.renderPipeline == pipeline)
                QualitySettings.renderPipeline = previous;
            if (Application.isMobilePlatform && Screen.orientation == ScreenOrientation.Portrait)
                Screen.orientation = previousOrientation;
        }
    }
}
