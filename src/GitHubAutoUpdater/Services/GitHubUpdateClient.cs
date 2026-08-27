using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GitHubAutoUpdater.Models;

namespace GitHubAutoUpdater.Services
{
    public class GitHubUpdateClient : IGitHubUpdateClient
    {
        private static readonly HttpClient DefaultSharedHttpClient = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<IReadOnlyList<GitHubRelease>> GetReleasesAsync(UpdateCheckOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ValidateOptions(options);

            string url = $"https://api.github.com/repos/{options.RepositoryOwner}/{options.RepositoryName}/releases?per_page=20";
            using var request = CreateRequest(HttpMethod.Get, url, options);
            var client = options.CustomHttpClient ?? DefaultSharedHttpClient;

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            await EnsureSuccessStatusCodeAsync(response).ConfigureAwait(false);

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, JsonOptions, ct).ConfigureAwait(false);

            return releases ?? (IReadOnlyList<GitHubRelease>)Array.Empty<GitHubRelease>();
        }

        public async Task<GitHubRelease?> GetLatestReleaseAsync(UpdateCheckOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ValidateOptions(options);

            if (options.IncludePrereleases)
            {
                var releases = await GetReleasesAsync(options, ct).ConfigureAwait(false);
                return releases
                    .Where(r => !r.Draft)
                    .OrderByDescending(r => r.Version ?? SemanticVersion.Parse("0.0.0"))
                    .FirstOrDefault();
            }

            string url = $"https://api.github.com/repos/{options.RepositoryOwner}/{options.RepositoryName}/releases/latest";
            using var request = CreateRequest(HttpMethod.Get, url, options);
            var client = options.CustomHttpClient ?? DefaultSharedHttpClient;

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            await EnsureSuccessStatusCodeAsync(response).ConfigureAwait(false);

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, ct).ConfigureAwait(false);
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync(UpdateCheckOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ValidateOptions(options);

            var currentVersion = options.CurrentVersion ?? UpdateCheckOptions.GetDefaultCurrentVersion();
            var latestRelease = await GetLatestReleaseAsync(options, ct).ConfigureAwait(false);

            if (latestRelease == null)
            {
                return UpdateInfo.NoUpdate(currentVersion);
            }

            var releaseVersion = latestRelease.Version;
            if (releaseVersion == null)
            {
                return UpdateInfo.NoUpdate(currentVersion, null, latestRelease);
            }

            if (releaseVersion > currentVersion)
            {
                var bestAsset = AssetMatcher.SelectBestAsset(latestRelease.Assets, options.AssetNamePattern, options.AssetSelector);
                string? checksum = null;

                if (bestAsset != null)
                {
                    try
                    {
                        checksum = await FetchChecksumForAssetAsync(latestRelease, bestAsset.Name, options, ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Checksum fetching is non-fatal
                    }
                }

                return UpdateInfo.Available(currentVersion, releaseVersion, latestRelease, bestAsset, checksum);
            }

            return UpdateInfo.NoUpdate(currentVersion, releaseVersion, latestRelease);
        }

        public async Task<string?> FetchChecksumForAssetAsync(GitHubRelease release, string assetName, UpdateCheckOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(release);
            ArgumentException.ThrowIfNullOrWhiteSpace(assetName);

            var checksumAsset = release.Assets.FirstOrDefault(a =>
                a.Name.Equals($"{assetName}.sha256", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals($"{assetName}.sha256sum", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("sha256sums.txt", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("SHA256SUMS", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("hashes.txt", StringComparison.OrdinalIgnoreCase));

            if (checksumAsset != null && !string.IsNullOrWhiteSpace(checksumAsset.BrowserDownloadUrl))
            {
                var client = options.CustomHttpClient ?? DefaultSharedHttpClient;
                using var req = CreateRequest(HttpMethod.Get, checksumAsset.BrowserDownloadUrl, options, acceptJson: false);
                using var res = await client.SendAsync(req, ct).ConfigureAwait(false);
                if (res.IsSuccessStatusCode)
                {
                    string content = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    string? hash = ChecksumVerifier.ExtractChecksumFromText(content, assetName);
                    if (hash != null)
                        return hash;
                }
            }

            if (!string.IsNullOrWhiteSpace(release.Body))
            {
                string? hash = ChecksumVerifier.ExtractChecksumFromText(release.Body, assetName);
                if (hash != null)
                    return hash;
            }

            return null;
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url, UpdateCheckOptions options, bool acceptJson = true)
        {
            var req = new HttpRequestMessage(method, url);
            string userAgent = !string.IsNullOrWhiteSpace(options.UserAgent)
                ? options.UserAgent
                : "GitHubAutoUpdater.NET/1.0";

            req.Headers.UserAgent.ParseAdd(userAgent);

            if (acceptJson)
            {
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            }

            if (!string.IsNullOrWhiteSpace(options.GitHubToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.GitHubToken);
            }

            return req;
        }

        private static async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
            {
                throw new InvalidOperationException("GitHub API rate limit exceeded. Please configure a GitHub personal access token or try again later.");
            }

            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException($"GitHub API returned {response.StatusCode} ({(int)response.StatusCode}): {content}");
        }

        private static void ValidateOptions(UpdateCheckOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.RepositoryOwner))
                throw new ArgumentException("RepositoryOwner is required in UpdateCheckOptions.", nameof(options));

            if (string.IsNullOrWhiteSpace(options.RepositoryName))
                throw new ArgumentException("RepositoryName is required in UpdateCheckOptions.", nameof(options));
        }
    }
}
