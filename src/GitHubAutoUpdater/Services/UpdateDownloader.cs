using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using GitHubAutoUpdater.Models;

namespace GitHubAutoUpdater.Services
{
    public class UpdateDownloader : IUpdateDownloader
    {
        private static readonly HttpClient DefaultSharedHttpClient = new();

        public async Task<string> DownloadAssetAsync(
            GitHubReleaseAsset asset,
            string? destinationPath = null,
            string? expectedSha256 = null,
            IProgress<UpdateDownloadProgress>? progress = null,
            HttpClient? customHttpClient = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(asset);
            if (string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
                throw new ArgumentException("Asset BrowserDownloadUrl cannot be null or empty.", nameof(asset));

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "GitHubAutoUpdater", asset.Id.ToString());
                Directory.CreateDirectory(tempDir);
                destinationPath = Path.Combine(tempDir, asset.Name);
            }
            else
            {
                string? parent = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }
            }

            string tempFilePath = destinationPath + ".tmp";
            var client = customHttpClient ?? DefaultSharedHttpClient;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, asset.BrowserDownloadUrl);
                request.Headers.UserAgent.ParseAdd("GitHubAutoUpdater.NET/1.0");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength ?? (asset.Size > 0 ? asset.Size : null);

                using (var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    byte[] buffer = new byte[81920];
                    long totalBytesRead = 0;
                    int bytesRead;

                    var stopwatch = Stopwatch.StartNew();
                    long lastBytesReported = 0;
                    long lastReportTime = 0;

                    while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                        totalBytesRead += bytesRead;

                        long elapsedMs = stopwatch.ElapsedMilliseconds;
                        if (progress != null && (elapsedMs - lastReportTime >= 100 || (totalBytes.HasValue && totalBytesRead == totalBytes.Value)))
                        {
                            double timeDeltaSec = (elapsedMs - lastReportTime) / 1000.0;
                            double speed = timeDeltaSec > 0 ? (totalBytesRead - lastBytesReported) / timeDeltaSec : 0;

                            double percent = totalBytes.HasValue && totalBytes.Value > 0
                                ? (double)totalBytesRead / totalBytes.Value * 100.0
                                : 0.0;

                            TimeSpan? eta = null;
                            if (totalBytes.HasValue && speed > 0)
                            {
                                long remainingBytes = Math.Max(0, totalBytes.Value - totalBytesRead);
                                eta = TimeSpan.FromSeconds(remainingBytes / speed);
                            }

                            progress.Report(new UpdateDownloadProgress
                            {
                                BytesReceived = totalBytesRead,
                                TotalBytesToReceive = totalBytes,
                                ProgressPercentage = Math.Min(100.0, Math.Max(0.0, percent)),
                                BytesPerSecond = speed,
                                EstimatedTimeRemaining = eta
                            });

                            lastBytesReported = totalBytesRead;
                            lastReportTime = elapsedMs;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    bool isValid = await ChecksumVerifier.VerifyChecksumAsync(tempFilePath, expectedSha256, ct).ConfigureAwait(false);
                    if (!isValid)
                    {
                        string computed = await ChecksumVerifier.ComputeSha256Async(tempFilePath, ct).ConfigureAwait(false);
                        throw new InvalidDataException($"Checksum verification failed! Expected SHA256 '{expectedSha256}', but computed '{computed}'.");
                    }
                }

                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(tempFilePath, destinationPath);

                return destinationPath;
            }
            catch
            {
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { }
                }
                throw;
            }
        }
    }
}
