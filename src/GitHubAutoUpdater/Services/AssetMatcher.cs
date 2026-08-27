using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using GitHubAutoUpdater.Models;

namespace GitHubAutoUpdater.Services
{
    public static class AssetMatcher
    {
        public static GitHubReleaseAsset? SelectBestAsset(
            IReadOnlyList<GitHubReleaseAsset> assets,
            string? pattern = null,
            Func<IReadOnlyList<GitHubReleaseAsset>, GitHubReleaseAsset?>? customSelector = null)
        {
            if (assets == null || assets.Count == 0)
                return null;

            // 1. Custom selector
            if (customSelector != null)
            {
                var custom = customSelector(assets);
                if (custom != null)
                    return custom;
            }

            // Filter out checksum files, symbols, signatures, source code zips
            var viableAssets = assets
                .Where(a => !IsAuxiliaryAsset(a.Name))
                .ToList();

            if (viableAssets.Count == 0)
                return assets.FirstOrDefault();

            // 2. Pattern matching (Glob / Regex)
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                var regex = GlobToRegex(pattern);
                var matched = viableAssets.FirstOrDefault(a => regex.IsMatch(a.Name));
                if (matched != null)
                    return matched;
            }

            // 3. Auto-detect based on current OS architecture
            string currentArch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                _ => "x64"
            };

            // Look for windows + architecture match
            var winArchAssets = viableAssets
                .Where(a => MatchesWindowsAndArch(a.Name, currentArch))
                .ToList();

            if (winArchAssets.Count > 0)
            {
                // Prefer installer or standalone zip
                var preferred = winArchAssets.FirstOrDefault(a => a.Name.Contains("Standalone", StringComparison.OrdinalIgnoreCase))
                             ?? winArchAssets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                             ?? winArchAssets.FirstOrDefault(a => a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                             ?? winArchAssets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                             ?? winArchAssets[0];

                return preferred;
            }

            // Look for general Windows or zip/exe
            var generalWinAssets = viableAssets
                .Where(a => a.Name.Contains("win", StringComparison.OrdinalIgnoreCase) ||
                            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                            a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (generalWinAssets.Count > 0)
            {
                return generalWinAssets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    ?? generalWinAssets[0];
            }

            return viableAssets[0];
        }

        private static bool MatchesWindowsAndArch(string fileName, string arch)
        {
            string lower = fileName.ToLowerInvariant();
            bool isWindows = lower.Contains("win") || lower.Contains("windows");
            bool isArch = lower.Contains(arch.ToLowerInvariant());

            if (arch.Equals("x64", StringComparison.OrdinalIgnoreCase))
            {
                isArch = isArch || lower.Contains("win64") || lower.Contains("amd64") || lower.Contains("x86_64");
            }

            return isWindows && isArch;
        }

        private static bool IsAuxiliaryAsset(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower.EndsWith(".sha256") ||
                   lower.EndsWith(".sha256sum") ||
                   lower.EndsWith(".sha512") ||
                   lower.EndsWith(".md5") ||
                   lower.EndsWith(".sig") ||
                   lower.EndsWith(".asc") ||
                   lower.EndsWith(".pdb") ||
                   lower.Contains("checksum") ||
                   lower.Contains("source") ||
                   lower.Equals("checksums.txt");
        }

        public static Regex GlobToRegex(string pattern)
        {
            string escaped = Regex.Escape(pattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".");
            return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
    }
}
