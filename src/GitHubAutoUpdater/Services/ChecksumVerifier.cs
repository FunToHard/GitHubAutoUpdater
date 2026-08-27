using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GitHubAutoUpdater.Services
{
    public static class ChecksumVerifier
    {
        public static string ComputeSha256(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static string ComputeSha256(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            using var stream = File.OpenRead(filePath);
            return ComputeSha256(stream);
        }

        public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            using var sha256 = SHA256.Create();
            byte[] hash = await sha256.ComputeHashAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static async Task<bool> VerifyChecksumAsync(string filePath, string expectedSha256, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256) || !File.Exists(filePath))
                return false;

            string actual = await ComputeSha256Async(filePath, ct).ConfigureAwait(false);
            return string.Equals(actual.Trim(), expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Attempts to extract the SHA256 hash for a specific asset name from various checksum file formats:
        /// - GNU sha256sum format: &lt;hash&gt; [* ]&lt;filename&gt;
        /// - PowerShell Format-List: Algorithm : SHA256\nHash : &lt;hash&gt;\nPath : ...
        /// - Simple text or Markdown: &lt;filename&gt; : &lt;hash&gt; or &lt;filename&gt; - &lt;hash&gt;
        /// </summary>
        public static string? ExtractChecksumFromText(string checksumContent, string targetAssetName)
        {
            if (string.IsNullOrWhiteSpace(checksumContent) || string.IsNullOrWhiteSpace(targetAssetName))
                return null;

            string escapedTarget = Regex.Escape(targetAssetName);

            // 1. Single hash content (if the entire checksum file is just one 64-char SHA256 hash)
            var singleHashMatch = Regex.Match(checksumContent.Trim(), @"^[a-fA-F0-9]{64}$");
            if (singleHashMatch.Success)
            {
                return singleHashMatch.Value.ToLowerInvariant();
            }

            // 2. GNU format: "<hash>  filename.zip" or "<hash> *filename.zip"
            var gnuMatch = Regex.Match(checksumContent, @$"\b([a-fA-F0-9]{{64}})\s+\*?.*?\b{escapedTarget}\b", RegexOptions.IgnoreCase);
            if (gnuMatch.Success)
            {
                return gnuMatch.Groups[1].Value.ToLowerInvariant();
            }

            // 3. Reversed GNU / Markdown / colon format: "filename.zip: <hash>" or "filename.zip - <hash>" or "| filename.zip | <hash> |"
            var revMatch = Regex.Match(checksumContent, @$"\b{escapedTarget}\b[^a-fA-F0-9\r\n]*([a-fA-F0-9]{{64}})", RegexOptions.IgnoreCase);
            if (revMatch.Success)
            {
                return revMatch.Groups[1].Value.ToLowerInvariant();
            }

            // 4. PowerShell Format-List format:
            // Algorithm : SHA256
            // Hash      : <hash>
            // Path      : ...\filename.zip
            var psMatch = Regex.Match(checksumContent, @$"Hash\s*:\s*([a-fA-F0-9]{{64}})[\s\S]*?Path\s*:\s*.*?\b{escapedTarget}\b", RegexOptions.IgnoreCase);
            if (psMatch.Success)
            {
                return psMatch.Groups[1].Value.ToLowerInvariant();
            }

            var psMatchRev = Regex.Match(checksumContent, @$"Path\s*:\s*.*?\b{escapedTarget}\b[\s\S]*?Hash\s*:\s*([a-fA-F0-9]{{64}})", RegexOptions.IgnoreCase);
            if (psMatchRev.Success)
            {
                return psMatchRev.Groups[1].Value.ToLowerInvariant();
            }

            return null;
        }
    }
}
