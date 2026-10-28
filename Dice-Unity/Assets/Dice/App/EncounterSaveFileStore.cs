using System;
using Dice.Engine;

namespace Dice.App
{
    public static class EncounterSaveFileStore
    {
        /** The run save's file name within a campaign directory. */
        public const string FileName = "spirit-encounter.json";

        public static void Save(string path, EncounterSaveData save)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A save path is required.", nameof(path));
            byte[] bytes = EncounterSaveCodec.Serialize(save);
            SaveFileCommit.Write(path, bytes);
        }

        public static EncounterSaveData Load(string path, MechanicalCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A save path is required.", nameof(path));
            byte[] bytes = SaveFileText.ReadBounded(path, EncounterSaveCodec.MaximumSaveBytes, "The save");
            return EncounterSaveCodec.Deserialize(bytes, catalog);
        }
    }
}
