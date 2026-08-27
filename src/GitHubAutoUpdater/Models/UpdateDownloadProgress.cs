using System;

namespace GitHubAutoUpdater.Models
{
    public class UpdateDownloadProgress
    {
        public long BytesReceived { get; set; }
        public long? TotalBytesToReceive { get; set; }
        public double ProgressPercentage { get; set; }
        public double BytesPerSecond { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }

        public string FormattedProgress => TotalBytesToReceive.HasValue
            ? $"{BytesReceived / 1024.0 / 1024.0:F1} MB / {TotalBytesToReceive.Value / 1024.0 / 1024.0:F1} MB ({ProgressPercentage:F0}%)"
            : $"{BytesReceived / 1024.0 / 1024.0:F1} MB";

        public string FormattedSpeed => BytesPerSecond switch
        {
            >= 1024 * 1024 => $"{BytesPerSecond / 1024.0 / 1024.0:F2} MB/s",
            >= 1024 => $"{BytesPerSecond / 1024.0:F0} KB/s",
            _ => $"{BytesPerSecond:F0} B/s"
        };
    }

    public class UpdateApplyOptions
    {
        /// <summary>
        /// Target installation directory to overwrite. Defaults to AppDomain.CurrentDomain.BaseDirectory.
        /// </summary>
        public string? TargetDirectory { get; set; }

        /// <summary>
        /// Name of the executable to restart after update. Defaults to current process executable name.
        /// </summary>
        public string? ExecutableName { get; set; }

        /// <summary>
        /// Command line arguments to pass when relaunching the updated process.
        /// </summary>
        public string? RelaunchArguments { get; set; }

        /// <summary>
        /// Whether to force elevated UAC prompt when running the update script.
        /// </summary>
        public bool RunAsAdmin { get; set; } = false;

        /// <summary>
        /// Action invoked right before the current application terminates.
        /// </summary>
        public Action? BeforeRestartAction { get; set; }
    }
}
