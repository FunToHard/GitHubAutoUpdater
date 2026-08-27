using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GitHubAutoUpdater.Models;
using GitHubAutoUpdater.Services;
using Xunit;

namespace GitHubAutoUpdater.Tests
{
    public class GitHubUpdateClientTests
    {
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }

        [Fact]
        public async Task CheckForUpdatesAsync_NewerVersionAvailable_ReturnsUpdateInfoWithAsset()
        {
            string releaseJson = @"{
                ""tag_name"": ""v1.2.0"",
                ""name"": ""DockAll v1.2.0 Release"",
                ""body"": ""### What's New\n- Added auto update\n- Performance improvements"",
                ""draft"": false,
                ""prerelease"": false,
                ""html_url"": ""https://github.com/FunToHard/DockAll/releases/tag/v1.2.0"",
                ""assets"": [
                    {
                        ""id"": 101,
                        ""name"": ""DockAll-Standalone-win-x64.zip"",
                        ""size"": 52428800,
                        ""browser_download_url"": ""https://github.com/FunToHard/DockAll/releases/download/v1.2.0/DockAll-Standalone-win-x64.zip""
                    },
                    {
                        ""id"": 102,
                        ""name"": ""checksums.txt"",
                        ""size"": 120,
                        ""browser_download_url"": ""https://github.com/FunToHard/DockAll/releases/download/v1.2.0/checksums.txt""
                    }
                ]
            }";

            string checksumsTxt = @"
Algorithm : SHA256
Hash      : 4F53CDA18C2BAA0C0354BB5F9A3ECBE5ED12AB4D8E11BA873C2F11161202B945
Path      : DockAll-Standalone-win-x64.zip
";

            var handler = new MockHttpMessageHandler(req =>
            {
                if (req.RequestUri!.AbsoluteUri.EndsWith("/releases/latest"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(releaseJson, Encoding.UTF8, "application/json")
                    };
                }
                if (req.RequestUri!.AbsoluteUri.EndsWith("checksums.txt"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(checksumsTxt, Encoding.UTF8, "text/plain")
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var httpClient = new HttpClient(handler);
            var client = new GitHubUpdateClient();
            var options = new UpdateCheckOptions
            {
                RepositoryOwner = "FunToHard",
                RepositoryName = "DockAll",
                CurrentVersion = new SemanticVersion(1, 0, 0),
                CustomHttpClient = httpClient
            };

            var info = await client.CheckForUpdatesAsync(options);

            Assert.True(info.IsUpdateAvailable);
            Assert.Equal(new SemanticVersion(1, 0, 0), info.CurrentVersion);
            Assert.Equal(new SemanticVersion(1, 2, 0), info.LatestVersion);
            Assert.NotNull(info.Asset);
            Assert.Equal("DockAll-Standalone-win-x64.zip", info.Asset.Name);
            Assert.Equal("4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945", info.Sha256Checksum);
            Assert.Contains("auto update", info.ReleaseNotes);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_SameOrOlderVersion_ReturnsNoUpdate()
        {
            string releaseJson = @"{
                ""tag_name"": ""v1.0.0"",
                ""name"": ""DockAll v1.0.0"",
                ""draft"": false,
                ""prerelease"": false,
                ""html_url"": ""https://github.com/FunToHard/DockAll/releases/tag/v1.0.0"",
                ""assets"": []
            }";

            var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(releaseJson, Encoding.UTF8, "application/json")
            });

            var httpClient = new HttpClient(handler);
            var client = new GitHubUpdateClient();
            var options = new UpdateCheckOptions
            {
                RepositoryOwner = "FunToHard",
                RepositoryName = "DockAll",
                CurrentVersion = new SemanticVersion(1, 0, 0),
                CustomHttpClient = httpClient
            };

            var info = await client.CheckForUpdatesAsync(options);

            Assert.False(info.IsUpdateAvailable);
            Assert.Equal(new SemanticVersion(1, 0, 0), info.CurrentVersion);
            Assert.Equal(new SemanticVersion(1, 0, 0), info.LatestVersion);
        }
    }
}
