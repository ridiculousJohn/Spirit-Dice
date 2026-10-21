namespace Dice.Presentation
{
    /** Scene lighting an ambient activity dims through per-light multipliers of the authored intensities, as on SceneLightingPresentation. */
    public interface IAmbientLightingTarget
    {
        int LightCount { get; }
        float GetLightIntensity(int index);
        void SetLightIntensity(int index, float multiplier);
    }
}
