using System;
using System.IO;
using Dice.Engine;

namespace Dice.App
{
    public static class EncounterSaveFileStore
    {
        public static void Save(string path, EncounterSaveData save)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A save path is required.", nameof(path));
            byte[] bytes = EncounterSaveCodec.Serialize(save);
            SaveFileCommit.Write(path, bytes);
        }

        public static EncounterSaveData Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A save path is required.", nameof(path));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > EncounterSaveCodec.MaximumSaveBytes)
                    throw new FormatException("The save exceeds the size limit.");
                var bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new FormatException("The save is truncated.");
                    offset += count;
                }
                return EncounterSaveCodec.Deserialize(bytes);
            }
        }
    }
}
