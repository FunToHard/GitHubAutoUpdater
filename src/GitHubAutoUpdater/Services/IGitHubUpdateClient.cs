using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitHubAutoUpdater.Models;

namespace GitHubAutoUpdater.Services
{
    public interface IGitHubUpdateClient
    {
        Task<IReadOnlyList<GitHubRelease>> GetReleasesAsync(UpdateCheckOptions options, CancellationToken ct = default);
        Task<GitHubRelease?> GetLatestReleaseAsync(UpdateCheckOptions options, CancellationToken ct = default);
        Task<UpdateInfo> CheckForUpdatesAsync(UpdateCheckOptions options, CancellationToken ct = default);
        Task<string?> FetchChecksumForAssetAsync(GitHubRelease release, string assetName, UpdateCheckOptions options, CancellationToken ct = default);
    }
}
