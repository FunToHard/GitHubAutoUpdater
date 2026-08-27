using System;
using System.Threading;
using System.Threading.Tasks;
using GitHubAutoUpdater.Models;

namespace GitHubAutoUpdater.Services
{
    public interface IUpdateDownloader
    {
        Task<string> DownloadAssetAsync(
            GitHubReleaseAsset asset,
            string? destinationPath = null,
            string? expectedSha256 = null,
            IProgress<UpdateDownloadProgress>? progress = null,
            HttpClient? customHttpClient = null,
            CancellationToken ct = default);
    }
}
