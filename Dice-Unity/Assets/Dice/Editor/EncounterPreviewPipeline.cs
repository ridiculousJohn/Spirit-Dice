using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Dice.Editor
{
    /**
     * The encounter's 3D pipeline, which an encounter preview window makes the editor's pipeline while it shows a stage and
     * restores when it closes or Play Mode starts; it stays active across repaints and stage changes so URP keeps its resources.
     */
    public sealed class EncounterPreviewPipeline
    {
        private const string PipelinePath = "Assets/Settings/SpiritPrototypePipeline.asset";
        private RenderPipelineAsset previous;

        /** The acquired pipeline, or null while the editor's own pipeline is active. */
        public UniversalRenderPipelineAsset Asset { get; private set; }

        internal void Acquire()
        {
            if (Asset != null) return;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null) throw new InvalidOperationException("The encounter preview pipeline is unavailable.");
            previous = QualitySettings.renderPipeline;
            Asset = pipeline;
            QualitySettings.renderPipeline = Asset;
        }

        /** Releases the window's stage, then restores the editor's pipeline even when releasing the stage fails. */
        internal void ReleaseAfter(Action releaseStage)
        {
            try { releaseStage(); }
            finally { Release(); }
        }

        private void Release()
        {
            if (Asset == null) return;
            if (QualitySettings.renderPipeline == Asset) QualitySettings.renderPipeline = previous;
            Asset = null;
            previous = null;
        }

        /** Renders one preview camera into its target with whichever pipeline is active. */
        internal static void Render(Camera camera, RenderTexture target)
        {
            if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
            else
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("The active render pipeline cannot preview this camera.");
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
        }
    }
}
