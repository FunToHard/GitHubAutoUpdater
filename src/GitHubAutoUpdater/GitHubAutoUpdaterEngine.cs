using System;
using System.Threading;
using System.Threading.Tasks;
using GitHubAutoUpdater.Models;
using GitHubAutoUpdater.Services;

namespace GitHubAutoUpdater
{
    public class GitHubAutoUpdaterEngine : IDisposable
    {
        private readonly IGitHubUpdateClient _client;
        private readonly IUpdateDownloader _downloader;
        private readonly IUpdateInstaller _installer;
        private Timer? _periodicTimer;
        private bool _isChecking;
        private bool _disposed;

        public UpdateCheckOptions Options { get; }

        public event EventHandler<UpdateInfo>? UpdateAvailable;
        public event EventHandler<UpdateDownloadProgress>? DownloadProgressChanged;
        public event EventHandler<string>? UpdateDownloaded;
        public event EventHandler<Exception>? ErrorOccurred;

        public GitHubAutoUpdaterEngine(
            UpdateCheckOptions options,
            IGitHubUpdateClient? client = null,
            IUpdateDownloader? downloader = null,
            IUpdateInstaller? installer = null)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _client = client ?? new GitHubUpdateClient();
            _downloader = downloader ?? new UpdateDownloader();
            _installer = installer ?? new UpdateInstaller();
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken ct = default)
        {
            try
            {
                var updateInfo = await _client.CheckForUpdatesAsync(Options, ct).ConfigureAwait(false);
                if (updateInfo.IsUpdateAvailable)
                {
                    UpdateAvailable?.Invoke(this, updateInfo);
                }
                return updateInfo;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        public async Task<string> DownloadUpdateAsync(
            UpdateInfo updateInfo,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(updateInfo);
            if (updateInfo.Asset == null)
            {
                throw new InvalidOperationException("No matching downloadable release asset was found for this update.");
            }

            var progressWrapper = new Progress<UpdateDownloadProgress>(p =>
            {
                progress?.Report(p);
                DownloadProgressChanged?.Invoke(this, p);
            });

            try
            {
                string downloadedPath = await _downloader.DownloadAssetAsync(
                    updateInfo.Asset,
                    destinationPath: null,
                    expectedSha256: updateInfo.Sha256Checksum,
                    progress: progressWrapper,
                    customHttpClient: Options.CustomHttpClient,
                    ct: ct).ConfigureAwait(false);

                UpdateDownloaded?.Invoke(this, downloadedPath);
                return downloadedPath;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        public async Task DownloadAndApplyAsync(
            UpdateInfo updateInfo,
            IProgress<UpdateDownloadProgress>? progress = null,
            UpdateApplyOptions? applyOptions = null,
            CancellationToken ct = default)
        {
            string packagePath = await DownloadUpdateAsync(updateInfo, progress, ct).ConfigureAwait(false);
            await _installer.ApplyUpdateAndRestartAsync(packagePath, applyOptions).ConfigureAwait(false);
        }

        public void StartPeriodicChecks(TimeSpan interval, TimeSpan? initialDelay = null)
        {
            StopPeriodicChecks();

            var dueTime = initialDelay ?? interval;
            _periodicTimer = new Timer(async _ =>
            {
                if (_isChecking || _disposed) return;
                _isChecking = true;

                try
                {
                    await CheckForUpdatesAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                }
                finally
                {
                    _isChecking = false;
                }
            }, null, dueTime, interval);
        }

        public void StopPeriodicChecks()
        {
            _periodicTimer?.Dispose();
            _periodicTimer = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopPeriodicChecks();
            GC.SuppressFinalize(this);
        }
    }
}
