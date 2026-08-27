using System;
using System.Collections.Generic;
using GitHubAutoUpdater.Models;
using Xunit;

namespace GitHubAutoUpdater.Tests
{
    public class SemanticVersionTests
    {
        [Theory]
        [InlineData("1.0.0", 1, 0, 0, null, null, null)]
        [InlineData("v1.2.3", 1, 2, 3, null, null, null)]
        [InlineData("V2.5.9", 2, 5, 9, null, null, null)]
        [InlineData("release-3.4.1", 3, 4, 1, null, null, null)]
        [InlineData("release/3.4.1", 3, 4, 1, null, null, null)]
        [InlineData("1.0", 1, 0, 0, null, null, null)]
        [InlineData("2.1.0.4", 2, 1, 0, 4, null, null)]
        [InlineData("1.2.3-preview.1", 1, 2, 3, null, "preview.1", null)]
        [InlineData("v2.0.0-rc.2+build123", 2, 0, 0, null, "rc.2", "build123")]
        [InlineData("1.0.0+20240101", 1, 0, 0, null, null, "20240101")]
        public void TryParse_ValidVersions_ReturnsCorrectValues(
            string input, int major, int minor, int patch, int? build, string? preRelease, string? metadata)
        {
            bool success = SemanticVersion.TryParse(input, out var ver);
            Assert.True(success);
            Assert.NotNull(ver);
            Assert.Equal(major, ver.Major);
            Assert.Equal(minor, ver.Minor);
            Assert.Equal(patch, ver.Patch);
            Assert.Equal(build, ver.Build);
            Assert.Equal(preRelease, ver.PreRelease);
            Assert.Equal(metadata, ver.BuildMetadata);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("abc")]
        [InlineData("v")]
        [InlineData("1.2.3.4.5")]
        [InlineData("-1.0.0")]
        public void TryParse_InvalidVersions_ReturnsFalse(string? input)
        {
            bool success = SemanticVersion.TryParse(input, out var ver);
            Assert.False(success);
            Assert.Null(ver);
        }

        [Fact]
        public void Parse_InvalidVersion_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("invalid-version"));
        }

        [Theory]
        [InlineData("1.0.0", "1.0.1", -1)]
        [InlineData("1.1.0", "1.0.0", 1)]
        [InlineData("2.0.0", "1.9.9", 1)]
        [InlineData("1.0.0", "1.0.0", 0)]
        [InlineData("v1.0.0", "1.0.0", 0)]
        [InlineData("1.0.0-preview.1", "1.0.0", -1)]
        [InlineData("1.0.0", "1.0.0-rc.1", 1)]
        [InlineData("1.0.0-alpha", "1.0.0-beta", -1)]
        [InlineData("1.0.0-beta.1", "1.0.0-beta.2", -1)]
        [InlineData("1.0.0-beta.10", "1.0.0-beta.2", 1)]
        [InlineData("1.0.0.1", "1.0.0.0", 1)]
        public void CompareTo_OrdersCorrectly(string v1, string v2, int expectedSign)
        {
            var ver1 = SemanticVersion.Parse(v1);
            var ver2 = SemanticVersion.Parse(v2);

            int result = ver1.CompareTo(ver2);
            int sign = Math.Sign(result);

            Assert.Equal(expectedSign, sign);
        }

        [Fact]
        public void ComparisonOperators_WorkCorrectly()
        {
            var v1 = SemanticVersion.Parse("1.0.0");
            var v2 = SemanticVersion.Parse("1.1.0");
            var v1Copy = SemanticVersion.Parse("v1.0.0");

            Assert.True(v1 < v2);
            Assert.True(v1 <= v2);
            Assert.True(v2 > v1);
            Assert.True(v2 >= v1);
            Assert.True(v1 == v1Copy);
            Assert.False(v1 != v1Copy);
            Assert.False(v1 == v2);
        }

        [Fact]
        public void Sorting_SemanticVersions_SortsInAscendingOrder()
        {
            var versions = new List<SemanticVersion>
            {
                SemanticVersion.Parse("2.0.0"),
                SemanticVersion.Parse("1.0.0-alpha"),
                SemanticVersion.Parse("1.0.0"),
                SemanticVersion.Parse("1.0.0-beta"),
                SemanticVersion.Parse("1.1.0"),
                SemanticVersion.Parse("0.9.0")
            };

            versions.Sort();

            var expected = new List<string>
            {
                "0.9.0",
                "1.0.0-alpha",
                "1.0.0-beta",
                "1.0.0",
                "1.1.0",
                "2.0.0"
            };

            Assert.Equal(expected, versions.ConvertAll(v => v.ToString()));
        }
    }
}
