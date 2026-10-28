using System;
using System.IO;
using System.Text;

namespace Dice.App
{
    /** Reads and decodes the application's bounded UTF-8 save files. */
    internal static class SaveFileText
    {
        /** UTF-8 written without a byte order mark; invalid bytes fail decoding. */
        internal static readonly Encoding Encoding = new UTF8Encoding(false, true);
        private static readonly byte[] ByteOrderMark = { 0xef, 0xbb, 0xbf };

        /** The whole file; the subject names it in the failure when it exceeds maximumBytes or ends early. */
        internal static byte[] ReadBounded(string path, int maximumBytes, string subject)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > maximumBytes) throw new FormatException(subject + " exceeds the size limit.");
                var bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new FormatException(subject + " is truncated.");
                    offset += count;
                }
                return bytes;
            }
        }

        /** The text of the bytes; a leading byte order mark, which other editors may add, is skipped. */
        internal static string Decode(byte[] bytes)
        {
            int offset = StartsWithByteOrderMark(bytes) ? ByteOrderMark.Length : 0;
            return Encoding.GetString(bytes, offset, bytes.Length - offset);
        }

        private static bool StartsWithByteOrderMark(byte[] bytes)
        {
            if (bytes.Length < ByteOrderMark.Length) return false;
            for (int i = 0; i < ByteOrderMark.Length; ++i)
                if (bytes[i] != ByteOrderMark[i]) return false;
            return true;
        }
    }
}
