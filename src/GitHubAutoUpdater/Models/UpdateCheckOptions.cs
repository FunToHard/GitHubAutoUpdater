using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;

namespace GitHubAutoUpdater.Models
{
    public class UpdateCheckOptions
    {
        public string RepositoryOwner { get; set; } = string.Empty;
        public string RepositoryName { get; set; } = string.Empty;
        public SemanticVersion? CurrentVersion { get; set; }
        public bool IncludePrereleases { get; set; } = false;
        public string? GitHubToken { get; set; }
        public string? UserAgent { get; set; }
        public string? AssetNamePattern { get; set; }
        public Func<IReadOnlyList<GitHubReleaseAsset>, GitHubReleaseAsset?>? AssetSelector { get; set; }
        public HttpClient? CustomHttpClient { get; set; }

        public static SemanticVersion GetDefaultCurrentVersion()
        {
            var entryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            
            // Try informational version first (e.g. 1.0.0-preview)
            var infoVersionAttr = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (infoVersionAttr != null && SemanticVersion.TryParse(infoVersionAttr.InformationalVersion, out var infoVer))
            {
                return infoVer!;
            }

            var fileVersionAttr = entryAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            if (fileVersionAttr != null && SemanticVersion.TryParse(fileVersionAttr.Version, out var fileVer))
            {
                return fileVer!;
            }

            var asmVersion = entryAssembly.GetName().Version;
            if (asmVersion != null)
            {
                return SemanticVersion.FromVersion(asmVersion);
            }

            return new SemanticVersion(1, 0, 0);
        }
    }
}
