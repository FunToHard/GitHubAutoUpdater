using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GitHubAutoUpdater.Models
{
    public class GitHubRelease
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("target_commitish")]
        public string? TargetCommitish { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; set; } = new();

        [JsonIgnore]
        public SemanticVersion? Version => SemanticVersion.TryParse(TagName, out var v) ? v : (SemanticVersion.TryParse(Name, out var v2) ? v2 : null);

        public override string ToString() => $"{TagName} ({Name ?? TagName})";
    }
}
