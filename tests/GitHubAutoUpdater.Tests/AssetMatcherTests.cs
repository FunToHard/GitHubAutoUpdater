using System.Collections.Generic;
using GitHubAutoUpdater.Models;
using GitHubAutoUpdater.Services;
using Xunit;

namespace GitHubAutoUpdater.Tests
{
    public class AssetMatcherTests
    {
        [Fact]
        public void SelectBestAsset_WithPattern_MatchesCorrectAsset()
        {
            var assets = new List<GitHubReleaseAsset>
            {
                new() { Id = 1, Name = "DockAll-FrameworkDependent-win-x64.zip", BrowserDownloadUrl = "https://example.com/1" },
                new() { Id = 2, Name = "DockAll-Standalone-win-x64.zip", BrowserDownloadUrl = "https://example.com/2" },
                new() { Id = 3, Name = "checksums.txt", BrowserDownloadUrl = "https://example.com/3" }
            };

            var selected = AssetMatcher.SelectBestAsset(assets, pattern: "*Standalone*win-x64.zip");
            Assert.NotNull(selected);
            Assert.Equal(2, selected.Id);
            Assert.Equal("DockAll-Standalone-win-x64.zip", selected.Name);
        }

        [Fact]
        public void SelectBestAsset_WithCustomSelector_UsesCustomLogic()
        {
            var assets = new List<GitHubReleaseAsset>
            {
                new() { Id = 1, Name = "DockAll-FrameworkDependent-win-x64.zip" },
                new() { Id = 2, Name = "DockAll-Standalone-win-x64.zip" }
            };

            var selected = AssetMatcher.SelectBestAsset(assets, customSelector: list => list[0]);
            Assert.NotNull(selected);
            Assert.Equal(1, selected.Id);
        }

        [Fact]
        public void SelectBestAsset_FiltersOutAuxiliaryChecksumFiles()
        {
            var assets = new List<GitHubReleaseAsset>
            {
                new() { Id = 1, Name = "checksums.txt" },
                new() { Id = 2, Name = "DockAll.pdb" },
                new() { Id = 3, Name = "DockAll-win-x64.zip" }
            };

            var selected = AssetMatcher.SelectBestAsset(assets);
            Assert.NotNull(selected);
            Assert.Equal(3, selected.Id);
            Assert.Equal("DockAll-win-x64.zip", selected.Name);
        }

        [Fact]
        public void GlobToRegex_MatchesProperly()
        {
            var regex = AssetMatcher.GlobToRegex("DockAll-*-win-x64.zip");
            Assert.Matches(regex, "DockAll-Standalone-win-x64.zip");
            Assert.Matches(regex, "DockAll-FrameworkDependent-win-x64.zip");
            Assert.DoesNotMatch(regex, "DockAll-Standalone-win-arm64.zip");
            Assert.DoesNotMatch(regex, "OtherApp-Standalone-win-x64.zip");
        }
    }
}
