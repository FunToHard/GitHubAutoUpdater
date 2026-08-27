using System;

namespace GitHubAutoUpdater.Models
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; init; }
        public SemanticVersion CurrentVersion { get; init; } = new(1, 0, 0);
        public SemanticVersion? LatestVersion { get; init; }
        public GitHubRelease? Release { get; init; }
        public GitHubReleaseAsset? Asset { get; init; }
        public string? ReleaseNotes => Release?.Body;
        public string? ReleaseUrl => Release?.HtmlUrl;
        public string? Sha256Checksum { get; set; }

        public static UpdateInfo NoUpdate(SemanticVersion currentVersion, SemanticVersion? latestVersion = null, GitHubRelease? release = null)
        {
            return new UpdateInfo
            {
                IsUpdateAvailable = false,
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion ?? currentVersion,
                Release = release
            };
        }

        public static UpdateInfo Available(
            SemanticVersion currentVersion,
            SemanticVersion latestVersion,
            GitHubRelease release,
            GitHubReleaseAsset? asset,
            string? checksum = null)
        {
            return new UpdateInfo
            {
                IsUpdateAvailable = true,
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                Release = release,
                Asset = asset,
                Sha256Checksum = checksum
            };
        }
    }
}
