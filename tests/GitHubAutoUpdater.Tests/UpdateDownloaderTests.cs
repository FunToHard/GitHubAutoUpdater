using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GitHubAutoUpdater.Models;
using GitHubAutoUpdater.Services;
using Xunit;

namespace GitHubAutoUpdater.Tests
{
    public class UpdateDownloaderTests
    {
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly byte[] _payload;

            public MockHttpMessageHandler(byte[] payload)
            {
                _payload = payload;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var content = new ByteArrayContent(_payload);
                content.Headers.ContentLength = _payload.Length;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                });
            }
        }

        [Fact]
        public async Task DownloadAssetAsync_ReportsProgressAndComputesChecksum()
        {
            byte[] dummyData = Encoding.UTF8.GetBytes(new string('A', 100000));
            using var sha = SHA256.Create();
            string expectedHash = Convert.ToHexString(sha.ComputeHash(dummyData)).ToLowerInvariant();

            var handler = new MockHttpMessageHandler(dummyData);
            var httpClient = new HttpClient(handler);

            var downloader = new UpdateDownloader();
            var asset = new GitHubReleaseAsset
            {
                Id = 999,
                Name = "TestApp.zip",
                Size = dummyData.Length,
                BrowserDownloadUrl = "https://example.com/TestApp.zip"
            };

            var progressReports = new List<UpdateDownloadProgress>();
            var progress = new Progress<UpdateDownloadProgress>(p => progressReports.Add(p));

            string tempFile = Path.Combine(Path.GetTempPath(), $"DownloadTest_{Guid.NewGuid():N}.zip");

            try
            {
                string downloadedPath = await downloader.DownloadAssetAsync(
                    asset,
                    destinationPath: tempFile,
                    expectedSha256: expectedHash,
                    progress: progress,
                    customHttpClient: httpClient);

                Assert.True(File.Exists(downloadedPath));
                Assert.Equal(dummyData.Length, new FileInfo(downloadedPath).Length);

                string actualHash = await ChecksumVerifier.ComputeSha256Async(downloadedPath);
                Assert.Equal(expectedHash, actualHash);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task DownloadAssetAsync_WithInvalidChecksum_ThrowsInvalidDataException()
        {
            byte[] dummyData = Encoding.UTF8.GetBytes("Valid file data");
            var handler = new MockHttpMessageHandler(dummyData);
            var httpClient = new HttpClient(handler);

            var downloader = new UpdateDownloader();
            var asset = new GitHubReleaseAsset
            {
                Id = 998,
                Name = "TestApp.zip",
                Size = dummyData.Length,
                BrowserDownloadUrl = "https://example.com/TestApp.zip"
            };

            string tempFile = Path.Combine(Path.GetTempPath(), $"DownloadTest_{Guid.NewGuid():N}.zip");

            try
            {
                await Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await downloader.DownloadAssetAsync(
                        asset,
                        destinationPath: tempFile,
                        expectedSha256: "0000000000000000000000000000000000000000000000000000000000000000",
                        customHttpClient: httpClient);
                });
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
