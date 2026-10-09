using System;
using System.IO;

namespace Dice.App
{
    public static class PlayerProfileFileStore
    {
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
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > PlayerProfileCodec.MaximumProfileBytes)
                    throw new FormatException("The profile exceeds the size limit.");
                var bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new FormatException("The profile is truncated.");
                    offset += count;
                }
                return PlayerProfileCodec.Deserialize(bytes);
            }
        }

        public static void Save(string path, PlayerProfile profile)
        {
            ValidatePath(path);
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string fullPath = Path.GetFullPath(path);
            // Read before writing so corrupt profiles remain available for recovery.
            profile.Merge(LoadOrCreate(fullPath));
            byte[] bytes = PlayerProfileCodec.Serialize(profile);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, fullPath + ".bak");
            else File.Move(temporaryPath, fullPath);
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A profile path is required.", nameof(path));
        }
    }
}
