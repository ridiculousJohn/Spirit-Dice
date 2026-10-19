using System;
using System.IO;
using System.Threading;

namespace Dice.App
{
    internal static class SaveFileCommit
    {
        private const int UnableToRemoveReplacedHResult = unchecked((int)0x80070497);

        internal static void Write(string path, byte[] bytes)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            // Commit only a complete file, retaining the previous successful save for recovery.
            if (File.Exists(fullPath)) ReplaceWithRetry(temporaryPath, fullPath, fullPath + ".bak", File.Replace);
            else File.Move(temporaryPath, fullPath);
        }

        internal static void ReplaceWithRetry(string sourcePath, string destinationPath, string backupPath,
            Action<string, string, string> replace)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    replace(sourcePath, destinationPath, backupPath);
                    return;
                }
                catch (IOException exception) when (exception.HResult == UnableToRemoveReplacedHResult && attempt < 3)
                {
                    // This Windows replacement failure leaves the source and destination filenames intact.
                    Thread.Sleep(25 << attempt);
                }
            }
        }
    }
}
