# GitHubAutoUpdater.NET

A lightweight, zero-dependency .NET library that brings seamless automatic updates to .NET / WPF / WinForms / Console applications distributed via GitHub Releases.

## Key Features

- **Zero Third-Party Dependencies**: Built exclusively on standard .NET BCL (System.Net.Http, System.Text.Json, System.IO.Compression, System.Security.Cryptography).
- **SemVer 2.0 Compliant**: Full semantic version parsing and comparison (supporting 1.2.3, 1.2.3-beta.1, build metadata, and version tags).
- **Multi-Framework Support**: Targets .NET 8, .NET 9, and .NET 10.
- **Smart Asset Matching**: Automatically resolves the correct release asset for current platform & architecture (win-x64, win-arm64, win-x86, standalone, or framework-dependent).
- **Integrity Verification**: Automatic SHA256 verification against checksums.txt, companion .sha256 files, or release notes.
- **Robust In-Place Updating**: Safe file locking handling with staged extraction and self-cleaning updater script.
- **Async & Cancelable**: Streamed downloads with real-time speed, bytes received, ETA, and progress callbacks.
- **Configurable**: Support for periodic background checks, pre-releases, and GitHub tokens for private repositories.

## Quick Start

```csharp
using GitHubAutoUpdater;
using GitHubAutoUpdater.Models;

var updater = new GitHubAutoUpdaterEngine(new UpdateCheckOptions
{
    RepositoryOwner = "FunToHard",
    RepositoryName = "DockAll",
    CurrentVersion = SemanticVersion.Parse("1.0.0")
});

// Check for updates
var updateInfo = await updater.CheckForUpdatesAsync();

if (updateInfo.IsUpdateAvailable)
{
    Console.WriteLine($"Update available: {updateInfo.LatestVersion}");
    
    var progress = new Progress<UpdateDownloadProgress>(p =>
    {
        Console.WriteLine($"Progress: {p.FormattedProgress} @ {p.FormattedSpeed}");
    });

    // Download and apply in-place restart
    await updater.DownloadAndApplyAsync(updateInfo, progress);
}
```

## Periodic Background Checks

```csharp
updater.UpdateAvailable += (sender, info) =>
{
    // Notify UI
};

updater.StartPeriodicChecks(interval: TimeSpan.FromHours(4), initialDelay: TimeSpan.FromSeconds(5));
```

## License

MIT License
