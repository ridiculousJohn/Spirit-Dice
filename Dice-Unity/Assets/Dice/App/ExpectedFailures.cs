using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;

namespace Dice.App
{
    /** The failures of file access, content and seed generation that the encounter controller and progression report as a status. */
    internal static class ExpectedFailures
    {
        internal static bool Includes(Exception exception)
            => exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException
                || exception is CryptographicException
                || exception is FormatException || exception is NotSupportedException || exception is ArgumentException
                || exception is InvalidOperationException || exception is OverflowException || exception is KeyNotFoundException;
    }
}
