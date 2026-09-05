#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// Opens a local ADB mirror of the Quest compositor output. Unlike the
    /// reconstructed F8 camera, this path includes Quest-only hand meshes,
    /// detected hand poses, palm menus, and other client-side UI.
    /// </summary>
    internal static class QuestCompleteViewMirror
    {
        private const string WindowTitle = "VBSOED Quest Complete First-Person (Hands + Menu)";
        private static Process s_Process;
        private static string s_LastError;
        private static string s_Backend;

        public static bool IsRunning
        {
            get
            {
                if (s_Process == null)
                {
                    return false;
                }

                try
                {
                    if (!s_Process.HasExited)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Process may already have been disposed by the OS.
                }

                s_Process.Dispose();
                s_Process = null;
                return false;
            }
        }

        public static string Status => string.IsNullOrEmpty(s_LastError)
            ? $"Complete Quest View: {(IsRunning ? s_Backend + " live" : "ready (MQDH/ADB)")}"
            : $"Complete Quest View: {s_LastError}";

        public static void Toggle()
        {
            if (IsRunning)
            {
                Stop();
            }
            else
            {
                Start();
            }
        }

        private static void Start()
        {
            string castingPath = FindMetaCastingExecutable();
            string executablePath = castingPath ?? FindScrcpyExecutable();
            if (string.IsNullOrEmpty(executablePath))
            {
                s_LastError = "MQDH Casting/scrcpy not installed";
                UnityEngine.Debug.LogWarning(
                    "[QuestCompleteView] Neither Meta Quest Developer Hub Casting nor scrcpy was found.");
                return;
            }

            s_LastError = null;
            bool useMetaCasting = !string.IsNullOrEmpty(castingPath);
            s_Backend = useMetaCasting ? "Meta Casting" : "scrcpy";
            string arguments;
            if (useMetaCasting)
            {
                string adbPath = FindMetaAdbExecutable(castingPath);
                string deviceId = FindConnectedAdbDevice(adbPath);
                if (string.IsNullOrEmpty(deviceId))
                {
                    s_LastError = "no authorized Quest found over ADB";
                    UnityEngine.Debug.LogWarning(
                        "[QuestCompleteView] Meta Casting is installed, but no authorized Quest was found over ADB.");
                    return;
                }

                string cacheDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Meta Quest Developer Hub",
                    "casting");
                Directory.CreateDirectory(cacheDirectory);

                // These are the same local-only arguments used by Meta Quest
                // Developer Hub. No Meta account, cloud casting, or VPN is used.
                string targetDeviceJson = $"{{\\\"id\\\":\\\"{deviceId}\\\"}}";
                arguments =
                    $"--adb \"{adbPath}\" " +
                    $"--application-caches-dir \"{cacheDirectory}\" " +
                    "--exit-on-close --launch-surface MQDH " +
                    $"--target-device \"{targetDeviceJson}\" " +
                    $"--launch-surface-session-uuid {Guid.NewGuid()}";
            }
            else
            {
                arguments =
                    "--max-size=1920 --video-bit-rate=16M --max-fps=60 " +
                    "--no-audio --no-control " +
                    $"--window-title=\"{WindowTitle}\"";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = !useMetaCasting
            };

            try
            {
                s_Process = Process.Start(startInfo);
                if (s_Process == null)
                {
                    s_LastError = "failed to start";
                    return;
                }

                UnityEngine.Debug.Log(
                    $"[QuestCompleteView] Complete Quest compositor view started with {s_Backend}. " +
                    "Press F9 to close it.");
            }
            catch (Exception exception)
            {
                s_Process = null;
                s_LastError = exception.Message;
                UnityEngine.Debug.LogWarning($"[QuestCompleteView] Start failed: {exception.Message}");
            }
        }

        private static void Stop()
        {
            if (s_Process == null)
            {
                return;
            }

            try
            {
                if (!s_Process.HasExited)
                {
                    s_Process.CloseMainWindow();
                    if (!s_Process.WaitForExit(800))
                    {
                        s_Process.Kill();
                    }
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[QuestCompleteView] Stop warning: {exception.Message}");
            }
            finally
            {
                s_Process.Dispose();
                s_Process = null;
                s_Backend = null;
            }
        }

        private static string FindMetaCastingExecutable()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string[] candidates =
            {
                Path.Combine(programFiles, "Meta Quest Developer Hub", "resources", "bin", "Casting", "Casting.exe"),
                Path.Combine(programFilesX86, "Meta Quest Developer Hub", "resources", "bin", "Casting", "Casting.exe"),
                @"D:\Program Files\Meta Quest Developer Hub\resources\bin\Casting\Casting.exe"
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static string FindMetaAdbExecutable(string castingPath)
        {
            DirectoryInfo castingDirectory = Directory.GetParent(castingPath);
            DirectoryInfo binDirectory = castingDirectory?.Parent;
            string bundledAdb = binDirectory == null
                ? null
                : Path.Combine(binDirectory.FullName, "adb.exe");
            if (!string.IsNullOrEmpty(bundledAdb) && File.Exists(bundledAdb))
            {
                return bundledAdb;
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string androidSdkAdb = Path.Combine(
                localAppData,
                "Android",
                "Sdk",
                "platform-tools",
                "adb.exe");
            return File.Exists(androidSdkAdb) ? androidSdkAdb : null;
        }

        private static string FindConnectedAdbDevice(string adbPath)
        {
            if (string.IsNullOrEmpty(adbPath) || !File.Exists(adbPath))
            {
                return null;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = "devices",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process adbProcess = Process.Start(startInfo))
                {
                    if (adbProcess == null)
                    {
                        return null;
                    }

                    string output = adbProcess.StandardOutput.ReadToEnd();
                    adbProcess.StandardError.ReadToEnd();
                    if (!adbProcess.WaitForExit(5000))
                    {
                        adbProcess.Kill();
                        return null;
                    }

                    string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string[] columns = line.Split(
                            new[] { ' ', '\t' },
                            StringSplitOptions.RemoveEmptyEntries);
                        if (columns.Length >= 2 &&
                            string.Equals(columns[1], "device", StringComparison.OrdinalIgnoreCase))
                        {
                            return columns[0];
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"[QuestCompleteView] ADB device detection failed: {exception.Message}");
            }

            return null;
        }

        private static string FindScrcpyExecutable()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string projectToolsRoot = Path.Combine(projectRoot, "Tools", "scrcpy");
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates =
            {
                Path.Combine(projectToolsRoot, "scrcpy.exe"),
                Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "scrcpy.exe")
            };

            string directMatch = candidates.FirstOrDefault(File.Exists);
            if (!string.IsNullOrEmpty(directMatch))
            {
                return directMatch;
            }

            if (Directory.Exists(projectToolsRoot))
            {
                string bundledMatch = Directory
                    .EnumerateFiles(projectToolsRoot, "scrcpy.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(bundledMatch))
                {
                    return bundledMatch;
                }
            }

            string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string pathEntry in pathValue.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(pathEntry))
                {
                    continue;
                }

                string pathCandidate = Path.Combine(pathEntry.Trim(), "scrcpy.exe");
                if (File.Exists(pathCandidate))
                {
                    return pathCandidate;
                }
            }

            string packagesRoot = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
            if (!Directory.Exists(packagesRoot))
            {
                return null;
            }

            try
            {
                return Directory
                    .EnumerateFiles(packagesRoot, "scrcpy.exe", SearchOption.AllDirectories)
                    .FirstOrDefault(path => path.IndexOf("Genymobile.scrcpy", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch
            {
                return null;
            }
        }
    }
}
#else
namespace GameMain
{
    internal static class QuestCompleteViewMirror
    {
        public static bool IsRunning => false;
        public static string Status => "Complete Quest View: Windows only";
        public static void Toggle() { }
    }
}
#endif
