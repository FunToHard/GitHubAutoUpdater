using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using GitHubAutoUpdater.Models;

namespace GitHubAutoUpdater.Services
{
    public class UpdateInstaller : IUpdateInstaller
    {
        public Task ApplyUpdateAndRestartAsync(string packagePath, UpdateApplyOptions? options = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
            if (!File.Exists(packagePath))
                throw new FileNotFoundException("Update package not found.", packagePath);

            options ??= new UpdateApplyOptions();

            string ext = Path.GetExtension(packagePath).ToLowerInvariant();

            if (ext == ".msi")
            {
                ApplyMsiInstaller(packagePath, options);
                return Task.CompletedTask;
            }

            if (ext == ".exe")
            {
                if (IsSetupExecutable(packagePath))
                {
                    ApplyExeInstaller(packagePath, options);
                    return Task.CompletedTask;
                }
            }

            if (ext == ".zip")
            {
                ApplyZipPackage(packagePath, options);
                return Task.CompletedTask;
            }

            if (IsZipFile(packagePath))
            {
                ApplyZipPackage(packagePath, options);
            }
            else
            {
                ApplyExeInstaller(packagePath, options);
            }

            return Task.CompletedTask;
        }

        private static void ApplyZipPackage(string zipPath, UpdateApplyOptions options)
        {
            int currentPid = Environment.ProcessId;
            string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{AppDomain.CurrentDomain.FriendlyName}.exe");

            string targetDir = !string.IsNullOrWhiteSpace(options.TargetDirectory)
                ? Path.GetFullPath(options.TargetDirectory)
                : Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/'));

            string exeName = !string.IsNullOrWhiteSpace(options.ExecutableName)
                ? options.ExecutableName
                : Path.GetFileName(currentExePath);

            string stagingBase = Path.Combine(Path.GetTempPath(), "GitHubAutoUpdater", "Staging");
            Directory.CreateDirectory(stagingBase);
            string stagingDir = Path.Combine(stagingBase, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDir);

            ZipFile.ExtractToDirectory(zipPath, stagingDir, overwriteFiles: true);

            string sourceDir = stagingDir;
            var entries = Directory.GetFileSystemEntries(stagingDir);
            if (entries.Length == 1 && Directory.Exists(entries[0]))
            {
                sourceDir = entries[0];
            }

            bool requiresElevation = options.RunAsAdmin || !HasWritePermission(targetDir);

            string scriptPath = Path.Combine(stagingBase, $"updater_{Guid.NewGuid():N}.ps1");
            var sb = new StringBuilder();
            sb.AppendLine("[CmdletBinding()]");
            sb.AppendLine("param()");
            sb.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
            sb.AppendLine($"$targetPid = {currentPid}");
            sb.AppendLine($"$sourceDir = '{sourceDir.Replace("'", "''")}'");
            sb.AppendLine($"$targetDir = '{targetDir.Replace("'", "''")}'");
            sb.AppendLine($"$exeName = '{exeName.Replace("'", "''")}'");
            sb.AppendLine($"$relaunchArgs = '{options.RelaunchArguments?.Replace("'", "''") ?? ""}'");
            sb.AppendLine($"$stagingRoot = '{stagingDir.Replace("'", "''")}'");
            sb.AppendLine("$scriptFile = $MyInvocation.MyCommand.Path");
            sb.AppendLine();
            sb.AppendLine("# 1. Wait for parent process to exit");
            sb.AppendLine("$timeout = 45");
            sb.AppendLine("$timer = [System.Diagnostics.Stopwatch]::StartNew()");
            sb.AppendLine("while ($timer.Elapsed.TotalSeconds -lt $timeout) {");
            sb.AppendLine("    $proc = Get-Process -Id $targetPid -ErrorAction SilentlyContinue");
            sb.AppendLine("    if ($null -eq $proc) { break }");
            sb.AppendLine("    Start-Sleep -Milliseconds 250");
            sb.AppendLine("}");
            sb.AppendLine("Start-Sleep -Milliseconds 600");
            sb.AppendLine();
            sb.AppendLine("# 2. Copy and overwrite files recursively");
            sb.AppendLine("try {");
            sb.AppendLine("    Copy-Item -Path \"$sourceDir\\*\" -Destination $targetDir -Recurse -Force -ErrorAction Stop");
            sb.AppendLine("} catch {");
            sb.AppendLine("    & robocopy $sourceDir $targetDir /E /IS /IT /NP /R:3 /W:1 | Out-Null");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("# 3. Relaunch the application");
            sb.AppendLine("$targetExe = Join-Path $targetDir $exeName");
            sb.AppendLine("if (Test-Path $targetExe) {");
            sb.AppendLine("    if ([string]::IsNullOrWhiteSpace($relaunchArgs)) {");
            sb.AppendLine("        Start-Process -FilePath $targetExe");
            sb.AppendLine("    } else {");
            sb.AppendLine("        Start-Process -FilePath $targetExe -ArgumentList $relaunchArgs");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("# 4. Clean up staging directory");
            sb.AppendLine("Start-Sleep -Seconds 1");
            sb.AppendLine("try {");
            sb.AppendLine("    Remove-Item -Path $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue");
            sb.AppendLine("    Remove-Item -Path $scriptFile -Force -ErrorAction SilentlyContinue");
            sb.AppendLine("} catch {}");

            File.WriteAllText(scriptPath, sb.ToString(), Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = requiresElevation ? "runas" : ""
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex) when (requiresElevation)
            {
                throw new InvalidOperationException("Administrator permission was denied to apply update.", ex);
            }

            options.BeforeRestartAction?.Invoke();
            TerminateApplication();
        }

        private static void ApplyExeInstaller(string exePath, UpdateApplyOptions options)
        {
            bool requiresElevation = options.RunAsAdmin;
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = options.RelaunchArguments ?? string.Empty,
                UseShellExecute = true,
                Verb = requiresElevation ? "runas" : ""
            };

            Process.Start(psi);
            options.BeforeRestartAction?.Invoke();
            TerminateApplication();
        }

        private static void ApplyMsiInstaller(string msiPath, UpdateApplyOptions options)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/i \"{msiPath}\" {options.RelaunchArguments}".Trim(),
                UseShellExecute = true
            };

            Process.Start(psi);
            options.BeforeRestartAction?.Invoke();
            TerminateApplication();
        }

        private static bool HasWritePermission(string directoryPath)
        {
            try
            {
                string testFile = Path.Combine(directoryPath, $".write_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSetupExecutable(string filePath)
        {
            string fileName = Path.GetFileName(filePath).ToLowerInvariant();
            return fileName.Contains("setup") || fileName.Contains("install") || fileName.Contains("wizard");
        }

        private static bool IsZipFile(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                if (stream.Length < 4) return false;
                byte[] magic = new byte[4];
                stream.ReadExactly(magic, 0, 4);
                return magic[0] == 0x50 && magic[1] == 0x4B && (magic[2] == 0x03 || magic[2] == 0x05);
            }
            catch
            {
                return false;
            }
        }

        private static void TerminateApplication()
        {
            try
            {
                var wpfAppType = Type.GetType("System.Windows.Application, PresentationFramework");
                if (wpfAppType != null)
                {
                    var currentProp = wpfAppType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                    var currentApp = currentProp?.GetValue(null);
                    if (currentApp != null)
                    {
                        var shutdownMethod = wpfAppType.GetMethod("Shutdown", new[] { typeof(int) });
                        var dispatcherProp = wpfAppType.GetProperty("Dispatcher");
                        var dispatcher = dispatcherProp?.GetValue(currentApp);
                        if (dispatcher != null)
                        {
                            var invokeMethod = dispatcher.GetType().GetMethod("Invoke", new[] { typeof(Action) });
                            invokeMethod?.Invoke(dispatcher, new object[] { new Action(() => shutdownMethod?.Invoke(currentApp, new object[] { 0 })) });
                        }
                    }
                }
            }
            catch { }

            Environment.Exit(0);
        }
    }
}
