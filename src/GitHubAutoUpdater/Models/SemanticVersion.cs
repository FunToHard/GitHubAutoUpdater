using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GitHubAutoUpdater.Models
{
    /// <summary>
    /// Represents a Semantic Version (SemVer 2.0) with support for standard version numbers,
    /// pre-releases, and build metadata.
    /// </summary>
    public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>, IComparable
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public int? Build { get; }
        public string? PreRelease { get; }
        public string? BuildMetadata { get; }
        public string RawString { get; }

        public bool IsPreRelease => !string.IsNullOrWhiteSpace(PreRelease);

        public SemanticVersion(int major, int minor = 0, int patch = 0, int? build = null, string? preRelease = null, string? buildMetadata = null, string? rawString = null)
        {
            if (major < 0) throw new ArgumentOutOfRangeException(nameof(major), "Major version cannot be negative.");
            if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor), "Minor version cannot be negative.");
            if (patch < 0) throw new ArgumentOutOfRangeException(nameof(patch), "Patch version cannot be negative.");
            if (build.HasValue && build.Value < 0) throw new ArgumentOutOfRangeException(nameof(build), "Build version cannot be negative.");

            Major = major;
            Minor = minor;
            Patch = patch;
            Build = build;
            PreRelease = string.IsNullOrWhiteSpace(preRelease) ? null : preRelease.TrimStart('-');
            BuildMetadata = string.IsNullOrWhiteSpace(buildMetadata) ? null : buildMetadata.TrimStart('+');
            RawString = rawString ?? ToString();
        }

        public static bool TryParse(string? input, out SemanticVersion? version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string clean = input.Trim();
            if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(1).Trim();
            else if (clean.StartsWith("release-", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(8).Trim();
            else if (clean.StartsWith("release/", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(8).Trim();

            // Extract build metadata (+...)
            string? buildMetadata = null;
            int plusIndex = clean.IndexOf('+');
            if (plusIndex >= 0)
            {
                buildMetadata = clean.Substring(plusIndex + 1);
                clean = clean.Substring(0, plusIndex);
            }

            // Extract pre-release (-...)
            string? preRelease = null;
            int hyphenIndex = clean.IndexOf('-');
            if (hyphenIndex >= 0)
            {
                preRelease = clean.Substring(hyphenIndex + 1);
                clean = clean.Substring(0, hyphenIndex);
            }

            // Split numeric version parts
            string[] parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Length > 4)
                return false;

            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major) || major < 0)
                return false;

            int minor = 0;
            if (parts.Length > 1)
            {
                if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor) || minor < 0)
                    return false;
            }

            int patch = 0;
            if (parts.Length > 2)
            {
                if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch) || patch < 0)
                    return false;
            }

            int? build = null;
            if (parts.Length > 3)
            {
                if (!int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int b) || b < 0)
                    return false;
                build = b;
            }

            version = new SemanticVersion(major, minor, patch, build, preRelease, buildMetadata, input);
            return true;
        }

        public static SemanticVersion Parse(string input)
        {
            if (TryParse(input, out var version) && version != null)
                return version;

            throw new FormatException($"String '{input}' was not recognized as a valid SemanticVersion.");
        }

        public static SemanticVersion FromVersion(Version version, string? preRelease = null)
        {
            ArgumentNullException.ThrowIfNull(version);
            return new SemanticVersion(
                version.Major,
                Math.Max(0, version.Minor),
                version.Build >= 0 ? version.Build : 0,
                version.Revision >= 0 ? version.Revision : null,
                preRelease,
                null,
                version.ToString());
        }

        public int CompareTo(SemanticVersion? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (other is null) return 1;

            int majorCmp = Major.CompareTo(other.Major);
            if (majorCmp != 0) return majorCmp;

            int minorCmp = Minor.CompareTo(other.Minor);
            if (minorCmp != 0) return minorCmp;

            int patchCmp = Patch.CompareTo(other.Patch);
            if (patchCmp != 0) return patchCmp;

            int buildCmp = (Build ?? 0).CompareTo(other.Build ?? 0);
            if (buildCmp != 0) return buildCmp;

            // SemVer 2.0: A version without a pre-release has higher precedence than one with a pre-release
            if (string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease))
                return 1;
            if (!string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease))
                return -1;
            if (string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease))
                return 0;

            // Compare pre-release identifiers dot-by-dot
            return ComparePreReleases(PreRelease!, other.PreRelease!);
        }

        public int CompareTo(object? obj)
        {
            if (obj is null) return 1;
            if (obj is SemanticVersion other) return CompareTo(other);
            throw new ArgumentException("Object must be of type SemanticVersion", nameof(obj));
        }

        private static int ComparePreReleases(string pre1, string pre2)
        {
            string[] parts1 = pre1.Split('.');
            string[] parts2 = pre2.Split('.');

            int minLen = Math.Min(parts1.Length, parts2.Length);
            for (int i = 0; i < minLen; i++)
            {
                string p1 = parts1[i];
                string p2 = parts2[i];

                bool isNum1 = int.TryParse(p1, NumberStyles.None, CultureInfo.InvariantCulture, out int num1);
                bool isNum2 = int.TryParse(p2, NumberStyles.None, CultureInfo.InvariantCulture, out int num2);

                if (isNum1 && isNum2)
                {
                    int cmp = num1.CompareTo(num2);
                    if (cmp != 0) return cmp;
                }
                else if (isNum1)
                {
                    return -1; // Numeric identifiers always have lower precedence than non-numeric
                }
                else if (isNum2)
                {
                    return 1;
                }
                else
                {
                    int cmp = string.Compare(p1, p2, StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return cmp;
                }
            }

            return parts1.Length.CompareTo(parts2.Length);
        }

        public bool Equals(SemanticVersion? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return Major == other.Major &&
                   Minor == other.Minor &&
                   Patch == other.Patch &&
                   (Build ?? 0) == (other.Build ?? 0) &&
                   string.Equals(PreRelease, other.PreRelease, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj) => Equals(obj as SemanticVersion);

        public override int GetHashCode()
        {
            return HashCode.Combine(Major, Minor, Patch, Build ?? 0, PreRelease?.ToLowerInvariant());
        }

        public override string ToString()
        {
            string ver = Build.HasValue
                ? $"{Major}.{Minor}.{Patch}.{Build.Value}"
                : $"{Major}.{Minor}.{Patch}";

            if (!string.IsNullOrEmpty(PreRelease))
                ver += $"-{PreRelease}";

            if (!string.IsNullOrEmpty(BuildMetadata))
                ver += $"+{BuildMetadata}";

            return ver;
        }

        public static bool operator ==(SemanticVersion? left, SemanticVersion? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(SemanticVersion? left, SemanticVersion? right) => !(left == right);
        public static bool operator <(SemanticVersion? left, SemanticVersion? right) => left is null ? right is not null : left.CompareTo(right) < 0;
        public static bool operator <=(SemanticVersion? left, SemanticVersion? right) => left is null || left.CompareTo(right) <= 0;
        public static bool operator >(SemanticVersion? left, SemanticVersion? right) => left is not null && left.CompareTo(right) > 0;
        public static bool operator >=(SemanticVersion? left, SemanticVersion? right) => left is null ? right is null : left.CompareTo(right) >= 0;
    }
}
