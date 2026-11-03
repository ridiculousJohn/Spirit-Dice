using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Dice.App
{
    /**
     * Seals, reads and decodes the application's bounded UTF-8 save files. A sealed file is an ASCII first line
     * "sha256 <64 lowercase hexadecimal digits>\n", the SHA-256 checksum of the payload, followed by the exact payload.
     */
    internal static class SaveFileText
    {
        /** UTF-8 written without a byte order mark; invalid bytes fail decoding. */
        internal static readonly Encoding Encoding = new UTF8Encoding(false, true);
        private static readonly byte[] ByteOrderMark = { 0xef, 0xbb, 0xbf };
        private const string SealPrefixText = "sha256 ";
        private static readonly byte[] SealPrefix = Encoding.ASCII.GetBytes(SealPrefixText);
        private const int ChecksumDigits = 64;
        /** The length of the seal line: the prefix, the checksum digits and the newline. */
        internal static readonly int SealLength = SealPrefix.Length + ChecksumDigits + 1;

        /** The file contents that seal the payload. */
        internal static byte[] Seal(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            byte[] header = Encoding.ASCII.GetBytes(SealPrefixText + Checksum(payload) + "\n");
            var bytes = new byte[header.Length + payload.Length];
            Buffer.BlockCopy(header, 0, bytes, 0, header.Length);
            Buffer.BlockCopy(payload, 0, bytes, header.Length, payload.Length);
            return bytes;
        }

        /**
         * The payload of the whole file, at most maximumBytes long; the subject names the file in a failure. A file that
         * starts with the seal prefix must carry a well-formed seal matching its payload; any other file is an unsealed
         * payload, which isSealed reports so its format decides whether it is accepted.
         */
        internal static byte[] ReadBounded(string path, int maximumBytes, string subject, out bool isSealed)
        {
            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > (long)maximumBytes + SealLength) throw new FormatException(subject + " exceeds the size limit.");
                bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new FormatException(subject + " is truncated.");
                    offset += count;
                }
            }
            isSealed = StartsWith(bytes, SealPrefix);
            byte[] payload = isSealed ? Unseal(bytes, subject) : bytes;
            if (payload.Length > maximumBytes) throw new FormatException(subject + " exceeds the size limit.");
            return payload;
        }

        /** The text of the bytes; a leading byte order mark, which other editors may add, is skipped. */
        internal static string Decode(byte[] bytes)
        {
            int offset = StartsWith(bytes, ByteOrderMark) ? ByteOrderMark.Length : 0;
            return Encoding.GetString(bytes, offset, bytes.Length - offset);
        }

        private static byte[] Unseal(byte[] bytes, string subject)
        {
            if (bytes.Length < SealLength || bytes[SealLength - 1] != (byte)'\n')
                throw new FormatException(subject + " has a malformed seal.");
            var payload = new byte[bytes.Length - SealLength];
            Buffer.BlockCopy(bytes, SealLength, payload, 0, payload.Length);
            byte[] expected = Encoding.ASCII.GetBytes(Checksum(payload));
            for (int i = 0; i < ChecksumDigits; ++i)
                if (bytes[SealPrefix.Length + i] != expected[i]) throw new FormatException(subject + " does not match its seal.");
            return payload;
        }

        private static string Checksum(byte[] payload)
        {
            byte[] hash;
            using (var algorithm = SHA256.Create()) hash = algorithm.ComputeHash(payload);
            var text = new StringBuilder(ChecksumDigits);
            foreach (byte value in hash) text.Append(value.ToString("x2"));
            return text.ToString();
        }

        private static bool StartsWith(byte[] bytes, byte[] prefix)
        {
            if (bytes.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; ++i)
                if (bytes[i] != prefix[i]) return false;
            return true;
        }
    }
}
