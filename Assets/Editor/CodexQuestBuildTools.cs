#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Xml;
using HybridCLR.Editor.Installer;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Codex.Editor
{
    /// <summary>
    /// Deterministic command-line helpers for preparing and building the Quest APK.
    /// This file lives under Assets/Editor and is never included in the player.
    /// </summary>
    public static class CodexQuestBuildTools
    {
        internal static bool BoundarylessBuildRequested { get; private set; }

        public static void InstallHybridClr()
        {
            var installer = new InstallerController();
            if (installer.HasInstalledHybridCLR()
                && installer.PackageVersion == installer.InstalledLibil2cppVersion)
            {
                Debug.Log($"[CodexQuestBuild] HybridCLR is already initialized: {installer.PackageVersion}");
                return;
            }

            Debug.Log($"[CodexQuestBuild] Initializing HybridCLR package {installer.PackageVersion}...");
            installer.InstallDefaultHybridCLR();

            var verification = new InstallerController();
            if (!verification.HasInstalledHybridCLR()
                || verification.PackageVersion != verification.InstalledLibil2cppVersion)
            {
                throw new BuildFailedException(
                    $"HybridCLR initialization verification failed. Package={verification.PackageVersion}, "
                    + $"Installed={verification.InstalledLibil2cppVersion ?? "<missing>"}");
            }

            Debug.Log($"[CodexQuestBuild] HybridCLR initialized successfully: {verification.PackageVersion}");
        }

        public static void GenerateHybridClrAllWithoutBundles()
        {
            string packagePath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "StreamingAssets/package"));
            string packageMetaPath = packagePath + ".meta";
            string temporaryRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../.codex-build-temp"));
            string temporaryPackagePath = Path.Combine(temporaryRoot, "StreamingAssets-package");
            string temporaryPackageMetaPath = temporaryPackagePath + ".meta";

            if (!Directory.Exists(packagePath))
            {
                throw new BuildFailedException(
                    $"StreamingAssets package directory was not found: {packagePath}");
            }

            if (Directory.Exists(temporaryPackagePath) || File.Exists(temporaryPackageMetaPath))
            {
                throw new BuildFailedException(
                    $"Temporary bundle backup already exists; refusing to overwrite it: {temporaryRoot}");
            }

            Directory.CreateDirectory(temporaryRoot);
            Debug.Log(
                "[CodexQuestBuild] Temporarily excluding StreamingAssets/package from the "
                + "HybridCLR stripped-AOT build to avoid Windows MAX_PATH failures.");

            Directory.Move(packagePath, temporaryPackagePath);
            if (File.Exists(packageMetaPath))
            {
                File.Move(packageMetaPath, temporaryPackageMetaPath);
            }

            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                HybridCLR.Editor.Commands.PrebuildCommand.GenerateAll();
                Debug.Log("[CodexQuestBuild] HybridCLR Generate/All completed successfully.");
            }
            finally
            {
                if (Directory.Exists(temporaryPackagePath) && !Directory.Exists(packagePath))
                {
                    Directory.Move(temporaryPackagePath, packagePath);
                }

                if (File.Exists(temporaryPackageMetaPath) && !File.Exists(packageMetaPath))
                {
                    File.Move(temporaryPackageMetaPath, packageMetaPath);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[CodexQuestBuild] Restored StreamingAssets/package after HybridCLR generation.");
            }
        }

        public static void RegenerateHybridClrArtifactsFromExistingAot()
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new BuildFailedException("Could not switch the active build target to Android.");
            }

            string strippedAotDirectory = HybridCLR.Editor.SettingsUtil
                .GetAssembliesPostIl2CppStripDir(BuildTarget.Android);
            if (!Directory.Exists(strippedAotDirectory)
                || Directory.GetFiles(strippedAotDirectory, "*.dll").Length == 0)
            {
                throw new BuildFailedException(
                    $"No stripped Android AOT assemblies were found: {strippedAotDirectory}");
            }

            Debug.Log("[CodexQuestBuild] Regenerating HybridCLR artifacts from existing Android AOT DLLs...");
            HybridCLR.Editor.Commands.CompileDllCommand.CompileDll(BuildTarget.Android);
            HybridCLR.Editor.Commands.Il2CppDefGeneratorCommand.GenerateIl2CppDef();
            HybridCLR.Editor.Commands.LinkGeneratorCommand.GenerateLinkXml(BuildTarget.Android);
            HybridCLR.Editor.Commands.MethodBridgeGeneratorCommand
                .GenerateMethodBridgeAndReversePInvokeWrapper(BuildTarget.Android);
            HybridCLR.Editor.Commands.AOTReferenceGeneratorCommand
                .GenerateAOTGenericReference(BuildTarget.Android);
            BuildDLLCommand.CopyAOTHotUpdateDlls(BuildTarget.Android);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[CodexQuestBuild] HybridCLR artifacts and DLL text assets regenerated successfully.");
        }

        public static void BuildAndroidPlayer()
        {
            BuildAndroidPlayerInternal(
                "VBSOED-Quest-Pro.apk",
                null,
                null,
                BuildOptions.None);
        }

        /// <summary>
        /// Rebuilds the HybridCLR DLL assets and YooAsset built-in package before
        /// producing the standard Quest APK. Use this entry for deployable builds
        /// so hot-update code cannot lag behind the native Unity player.
        /// </summary>
        public static void BuildAndroidPlayerWithFreshBundles()
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new BuildFailedException("Could not switch the active build target to Android.");
            }

            Debug.Log("[CodexQuestBuild] Refreshing HybridCLR DLL assets before rebuilding YooAsset bundles...");
            BuildDLLCommand.BuildAndCopyDlls(BuildTarget.Android);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            UnityGameFramework.Editor.ReleaseTools.BuildCurrentPlatformAB();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            BuildAndroidPlayerInternal(
                "VBSOED-Quest-Pro.apk",
                null,
                null,
                BuildOptions.None,
                false);
        }

        public static void BuildAndroidSideBySideTestPlayer()
        {
            BuildAndroidPlayerInternal(
                "VBSOED-Quest-Pro-CodexTest.apk",
                "com.AovisVision.VisualBehaviorSimulationOfEyeDisease.User.Debug.CodexTest",
                "VBSOED Quest Test",
                BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        /// <summary>
        /// Produces a non-development APK for representative Quest frame-rate
        /// testing while retaining the side-by-side test package identifier.
        /// </summary>
        public static void BuildAndroidPerformanceOptimizedSideBySidePlayer()
        {
            BuildAndroidPlayerInternal(
                "VBSOED-Quest-Pro-CodexTest-Optimized.apk",
                "com.AovisVision.VisualBehaviorSimulationOfEyeDisease.User.Debug.CodexTest",
                "VBSOED Quest Optimized",
                BuildOptions.None);
        }

        /// <summary>
        /// Rebuilds the HybridCLR DLL assets and the YooAsset built-in package
        /// before producing the side-by-side Quest test APK. This keeps the
        /// interpreted hot-update assemblies and AOT supplemental metadata in
        /// the APK synchronized with the current IL2CPP player.
        /// </summary>
        public static void BuildAndroidSideBySideTestPlayerWithFreshBundles()
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new BuildFailedException("Could not switch the active build target to Android.");
            }

            Debug.Log("[CodexQuestBuild] Refreshing HybridCLR DLL assets before rebuilding YooAsset bundles...");
            BuildDLLCommand.BuildAndCopyDlls(BuildTarget.Android);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            UnityGameFramework.Editor.ReleaseTools.BuildCurrentPlatformAB();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            BuildAndroidPlayerInternal(
                "VBSOED-Quest-Pro-CodexTest.apk",
                "com.AovisVision.VisualBehaviorSimulationOfEyeDisease.User.Debug.CodexTest",
                "VBSOED Quest Test",
                BuildOptions.Development | BuildOptions.AllowDebugging,
                false);
        }

        /// <summary>
        /// Rebuilds HotUpdate DLLs and YooAsset built-in bundles, then creates the
        /// non-development Quest APK used for representative frame-rate testing.
        /// </summary>
        public static void BuildAndroidPerformanceOptimizedSideBySidePlayerWithFreshBundles()
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new BuildFailedException("Could not switch the active build target to Android.");
            }

            Debug.Log("[CodexQuestBuild] Refreshing HybridCLR DLL assets before rebuilding YooAsset bundles...");
            BuildDLLCommand.BuildAndCopyDlls(BuildTarget.Android);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            UnityGameFramework.Editor.ReleaseTools.BuildCurrentPlatformAB();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            BuildAndroidPlayerInternal(
                "VBSOED-Quest-Pro-CodexTest-Optimized.apk",
                "com.AovisVision.VisualBehaviorSimulationOfEyeDisease.User.Debug.CodexTest",
                "VBSOED Quest Optimized",
                BuildOptions.None,
                false);
        }

        public static void BuildAndroidBoundarylessSideBySideTestPlayer()
        {
            BuildAndroidPlayerInternal(
                "VBSOED-Quest-Pro-CodexTest-Boundaryless.apk",
                "com.AovisVision.VisualBehaviorSimulationOfEyeDisease.User.Debug.CodexTest",
                "VBSOED Boundaryless Test",
                BuildOptions.None,
                refreshHybridClrArtifacts: true,
                enableBoundaryless: true);
        }

        public static void BuildAndroidBoundarylessOfflineSideBySideTestPlayer()
        {
            BuildAndroidPlayerInternal(
                "VBSOED-Quest-Pro-CodexTest-Boundaryless-Offline.apk",
                "com.AovisVision.VisualBehaviorSimulationOfEyeDisease.User.Debug.CodexTest",
                "VBSOED Offline Test",
                BuildOptions.None,
                refreshHybridClrArtifacts: true,
                enableBoundaryless: true);
        }

        private static void BuildAndroidPlayerInternal(
            string outputFileName,
            string temporaryApplicationIdentifier,
            string temporaryProductName,
            BuildOptions buildOptions,
            bool refreshHybridClrArtifacts = true,
            bool enableBoundaryless = false)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new BuildFailedException("Could not switch the active build target to Android.");
            }

            if (refreshHybridClrArtifacts)
            {
                Debug.Log("[CodexQuestBuild] Compiling and copying HybridCLR hot-update/AOT DLL assets...");
                BuildDLLCommand.BuildAndCopyDlls(BuildTarget.Android);
            }

            string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/Android"));
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, outputFileName);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes were found in EditorBuildSettings.");
            }

            Debug.Log($"[CodexQuestBuild] Building {scenes.Length} scenes to: {outputPath}");
            bool previousExportAsGradleProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            string previousApplicationIdentifier =
                PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            string previousProductName = PlayerSettings.productName;
            bool previousBoundarylessBuildRequested = BoundarylessBuildRequested;
            BuildReport report;
            try
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                BoundarylessBuildRequested = enableBoundaryless;
                if (!string.IsNullOrEmpty(temporaryApplicationIdentifier))
                {
                    PlayerSettings.SetApplicationIdentifier(
                        BuildTargetGroup.Android,
                        temporaryApplicationIdentifier);
                }

                if (!string.IsNullOrEmpty(temporaryProductName))
                {
                    PlayerSettings.productName = temporaryProductName;
                }

                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    targetGroup = BuildTargetGroup.Android,
                    target = BuildTarget.Android,
                    options = buildOptions
                });
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(
                    BuildTargetGroup.Android,
                    previousApplicationIdentifier);
                PlayerSettings.productName = previousProductName;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExportAsGradleProject;
                BoundarylessBuildRequested = previousBoundarylessBuildRequested;
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded || !File.Exists(outputPath))
            {
                throw new BuildFailedException(
                    $"Quest APK build failed. Result={summary.result}, Errors={summary.totalErrors}, "
                    + $"Warnings={summary.totalWarnings}, Output={outputPath}");
            }

            Debug.Log(
                $"[CodexQuestBuild] Quest APK build succeeded: {outputPath} "
                + $"({summary.totalSize / 1024d / 1024d:F1} MB)");
        }
    }

    /// <summary>
    /// Adds Meta's boundaryless declaration while retaining the focus-aware
    /// metadata required for Quest to hand rendering focus to Unity. The
    /// processor is enabled only by the explicit boundaryless build entries.
    /// </summary>
    internal sealed class CodexBoundarylessManifestProcessor : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        private const string BoundarylessFeature = "com.oculus.feature.BOUNDARYLESS_APP";
        private const string FocusAwareMetadata = "com.oculus.vr.focusaware";
        private const string UnityPlayerActivity = "com.unity3d.player.UnityPlayerActivity";
        private const string QuestFocusResumeMarker = "CODEX_QUEST_INITIAL_FOCUS_WORKAROUND";

        // Meta XR's OVRGradleGeneration callback runs at 99999 and rewrites the
        // activity metadata, so the isolated test-package override must run last.
        public int callbackOrder => 100000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string applicationIdentifier =
                PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                throw new BuildFailedException(
                    $"Could not configure the Quest launch mode; generated manifest not found: {manifestPath}");
            }

            var document = new XmlDocument { PreserveWhitespace = true };
            document.Load(manifestPath);

            var namespaceManager = new XmlNamespaceManager(document.NameTable);
            namespaceManager.AddNamespace("android", AndroidNamespace);
            XmlNode manifest = document.SelectSingleNode("/manifest");
            XmlNode existing = document.SelectSingleNode(
                $"/manifest/uses-feature[@android:name='{BoundarylessFeature}']",
                namespaceManager);

            if (!CodexQuestBuildTools.BoundarylessBuildRequested)
            {
                bool removedBoundarylessFeature = false;
                if (existing != null)
                {
                    existing.ParentNode?.RemoveChild(existing);
                    document.Save(manifestPath);
                    removedBoundarylessFeature = true;
                }

                bool removedFocusWorkaround = RemoveInitialQuestFocusWorkaround(path);
                if (removedBoundarylessFeature || removedFocusWorkaround)
                {
                    Debug.Log(
                        $"[CodexQuestBuild] Removed stale boundaryless launch configuration "
                        + $"from standard Quest build: {applicationIdentifier}");
                }

                return;
            }

            bool changed = false;
            if (existing == null)
            {
                XmlElement feature = document.CreateElement("uses-feature");
                feature.SetAttribute("name", AndroidNamespace, BoundarylessFeature);
                feature.SetAttribute("required", AndroidNamespace, "true");
                manifest.PrependChild(feature);
                changed = true;
            }

            XmlNode activity = document.SelectSingleNode(
                $"/manifest/application/activity[@android:name='{UnityPlayerActivity}']",
                namespaceManager);
            if (activity == null)
            {
                throw new BuildFailedException(
                    $"Could not update Quest focus handling; Unity activity not found: {manifestPath}");
            }

            // The launcher module owns the application icon. Keeping the same
            // reference in unityLibrary makes AGP verify the library in isolation,
            // where launcher-only mipmap resources are intentionally unavailable.
            XmlElement application = document.SelectSingleNode(
                "/manifest/application", namespaceManager) as XmlElement;
            if (application != null && application.HasAttribute("icon", AndroidNamespace))
            {
                application.RemoveAttribute("icon", AndroidNamespace);
                changed = true;
            }

            XmlElement focusAware = document.SelectSingleNode(
                $"/manifest/application/activity[@android:name='{UnityPlayerActivity}']"
                + $"/meta-data[@android:name='{FocusAwareMetadata}']",
                namespaceManager) as XmlElement;
            if (focusAware == null)
            {
                focusAware = document.CreateElement("meta-data");
                focusAware.SetAttribute("name", AndroidNamespace, FocusAwareMetadata);
                focusAware.SetAttribute("value", AndroidNamespace, "true");
                activity.AppendChild(focusAware);
                changed = true;
            }
            else if (focusAware.GetAttribute("value", AndroidNamespace) != "true")
            {
                focusAware.SetAttribute("value", AndroidNamespace, "true");
                changed = true;
            }

            if (changed)
            {
                document.Save(manifestPath);
            }

            EnsureInitialQuestFocusOnResume(path);
            DisableBrokenReleaseLint(path);

            Debug.Log(
                $"[CodexQuestBuild] Enabled Meta boundaryless mode, retained focus-aware "
                + $"startup, and added the initial Quest focus workaround for test package: "
                + applicationIdentifier);
        }

        private static void EnsureInitialQuestFocusOnResume(string unityLibraryPath)
        {
            string activitySourcePath = Path.Combine(
                unityLibraryPath,
                "src",
                "main",
                "java",
                "com",
                "unity3d",
                "player",
                "UnityPlayerActivity.java");
            if (!File.Exists(activitySourcePath))
            {
                throw new BuildFailedException(
                    $"Could not apply Quest focus workaround; Unity activity source not found: "
                    + activitySourcePath);
            }

            string source = File.ReadAllText(activitySourcePath);
            if (source.Contains(QuestFocusResumeMarker))
            {
                return;
            }

            const string resumeCall = "        mUnityPlayer.onResume();";
            int resumeCallIndex = source.IndexOf(resumeCall, StringComparison.Ordinal);
            if (resumeCallIndex < 0)
            {
                throw new BuildFailedException(
                    $"Could not apply Quest focus workaround; onResume call was not found: "
                    + activitySourcePath);
            }

            string replacement = resumeCall
                + "\n\n        // " + QuestFocusResumeMarker
                + "\n        // Some Quest firmware does not deliver the initial Android window-focus event"
                + "\n        // to a side-loaded focus-aware activity. Unity then remains paused forever."
                + "\n        if (getPackageName().endsWith(\".CodexTest\")"
                + " || getPackageName().contains(\".CodexTest.\"))"
                + "\n        {"
                + "\n            mUnityPlayer.windowFocusChanged(true);"
                + "\n        }";
            source = source.Substring(0, resumeCallIndex)
                + replacement
                + source.Substring(resumeCallIndex + resumeCall.Length);
            File.WriteAllText(activitySourcePath, source);

            Debug.Log(
                $"[CodexQuestBuild] Added the initial Quest focus workaround to: "
                + activitySourcePath);
        }

        private static bool RemoveInitialQuestFocusWorkaround(string unityLibraryPath)
        {
            string activitySourcePath = Path.Combine(
                unityLibraryPath,
                "src",
                "main",
                "java",
                "com",
                "unity3d",
                "player",
                "UnityPlayerActivity.java");
            if (!File.Exists(activitySourcePath))
            {
                return false;
            }

            string source = File.ReadAllText(activitySourcePath);
            if (!source.Contains(QuestFocusResumeMarker))
            {
                return false;
            }

            string workaround =
                "\n\n        // " + QuestFocusResumeMarker
                + "\n        // Some Quest firmware does not deliver the initial Android window-focus event"
                + "\n        // to a side-loaded focus-aware activity. Unity then remains paused forever."
                + "\n        if (getPackageName().endsWith(\".CodexTest\")"
                + " || getPackageName().contains(\".CodexTest.\"))"
                + "\n        {"
                + "\n            mUnityPlayer.windowFocusChanged(true);"
                + "\n        }";
            if (!source.Contains(workaround))
            {
                throw new BuildFailedException(
                    $"Could not remove the stale Quest focus workaround because its generated "
                    + $"source no longer matches the expected template: {activitySourcePath}");
            }

            File.WriteAllText(activitySourcePath, source.Replace(workaround, string.Empty));
            return true;
        }

        /// <summary>
        /// Unity's generated local AAR contains legacy non-UTF-8 resource names.
        /// AGP 7.4 lintVital fails while extracting that AAR even though Android
        /// compilation, resource merging, and packaging all succeed. Disable only
        /// the release lint task for this test package; normal Gradle errors remain fatal.
        /// </summary>
        private static void DisableBrokenReleaseLint(string unityLibraryPath)
        {
            string launcherGradlePath = Path.GetFullPath(
                Path.Combine(unityLibraryPath, "..", "launcher", "build.gradle"));
            if (!File.Exists(launcherGradlePath))
            {
                throw new BuildFailedException(
                    $"Could not disable the broken release lint task; launcher Gradle file not found: "
                    + launcherGradlePath);
            }

            string gradle = File.ReadAllText(launcherGradlePath);
            const string lintBlock = "    lintOptions {\n        abortOnError false\n    }";
            const string patchedLintBlock =
                "    lintOptions {\n        abortOnError false\n        checkReleaseBuilds false\n    }";

            if (gradle.Contains("checkReleaseBuilds false"))
            {
                return;
            }

            if (!gradle.Contains(lintBlock))
            {
                throw new BuildFailedException(
                    $"Could not disable the broken release lint task; expected lintOptions block not found: "
                    + launcherGradlePath);
            }

            File.WriteAllText(launcherGradlePath, gradle.Replace(lintBlock, patchedLintBlock));
            Debug.Log("[CodexQuestBuild] Disabled AGP release lint for the generated test launcher.");
        }

    }
}
#endif
