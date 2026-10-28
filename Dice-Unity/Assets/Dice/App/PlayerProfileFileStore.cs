using System;
using System.IO;

namespace Dice.App
{
    public static class PlayerProfileFileStore
    {
        /** The profile's file name within a campaign directory. */
        public const string FileName = "spirit-profile.json";

        public static PlayerProfile LoadOrCreate(string path)
        {
            ValidatePath(path);
            try { return Load(path); }
            catch (FileNotFoundException) { return new PlayerProfile(); }
            catch (DirectoryNotFoundException) { return new PlayerProfile(); }
        }

        public static PlayerProfile Load(string path)
        {
            ValidatePath(path);
            byte[] bytes = SaveFileText.ReadBounded(path, PlayerProfileCodec.MaximumProfileBytes, "The profile");
            return PlayerProfileCodec.Deserialize(bytes);
        }

        public static void Save(string path, PlayerProfile profile)
        {
            ValidatePath(path);
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string fullPath = Path.GetFullPath(path);
            // Read before writing so corrupt profiles remain available for recovery.
            profile.Merge(LoadOrCreate(fullPath));
            byte[] bytes = PlayerProfileCodec.Serialize(profile);
            SaveFileCommit.Write(fullPath, bytes);
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A profile path is required.", nameof(path));
        }
    }
}
