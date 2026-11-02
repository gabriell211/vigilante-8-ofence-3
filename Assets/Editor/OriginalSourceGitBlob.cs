#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Vigilante8.Editor
{
    /// <summary>Checks archived Git blobs across LF and Windows CRLF checkouts.</summary>
    internal static class OriginalSourceGitBlob
    {
        internal static bool Matches(string path, string expectedSha, long expectedBytes = 0)
        {
            if (!File.Exists(path) || string.IsNullOrEmpty(expectedSha)) return false;
            byte[] bytes = File.ReadAllBytes(path);
            if ((expectedBytes == 0 || bytes.Length == expectedBytes) &&
                string.Equals(Hash(bytes), expectedSha, StringComparison.OrdinalIgnoreCase))
                return true;

            // Git's text checkout may insert CR before LF. Only accept the
            // normalized bytes when they match the recorded size and blob SHA.
            byte[] normalized = new byte[bytes.Length];
            int length = 0;
            bool hadCrLf = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == '\r' && i + 1 < bytes.Length && bytes[i + 1] == '\n')
                {
                    hadCrLf = true;
                    continue;
                }
                normalized[length++] = bytes[i];
            }
            if (!hadCrLf || (expectedBytes != 0 && length != expectedBytes)) return false;
            Array.Resize(ref normalized, length);
            return string.Equals(Hash(normalized), expectedSha, StringComparison.OrdinalIgnoreCase);
        }

        private static string Hash(byte[] bytes)
        {
            byte[] header = Encoding.ASCII.GetBytes("blob " + bytes.Length + "\0");
            using (SHA1 sha = SHA1.Create())
            {
                sha.TransformBlock(header, 0, header.Length, header, 0);
                sha.TransformFinalBlock(bytes, 0, bytes.Length);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
#endif
