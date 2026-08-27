using System;
using System.Text.Json.Serialization;

namespace GitHubAutoUpdater.Models
{
    public class GitHubReleaseAsset
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("content_type")]
        public string? ContentType { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("download_count")]
        public int DownloadCount { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        public override string ToString() => $"{Name} ({Size / 1024.0 / 1024.0:F2} MB)";
    }
}
