using Dice.Engine;

namespace Dice.Presentation
{
    public interface IEncounterPresenter
    {
        void Synchronize(EncounterSnapshot snapshot);
        void Present(Resolution resolution);
    }
}
