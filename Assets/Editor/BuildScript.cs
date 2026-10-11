using UnityEditor;
using UnityEngine;

namespace SlotGame.EditorTools
{
    /// <summary>
    /// Command-line WebGL build entry point.
    ///
    /// Usage (from terminal):
    ///   "C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe" ^
    ///       -batchmode -nographics -quit ^
    ///       -projectPath "C:\project-unity\SlotGame" ^
    ///       -executeMethod SlotGame.EditorTools.BuildScript.BuildWebGL ^
    ///       -logFile build.log
    ///
    /// The script:
    ///   1. Ensures the game scene is in the build settings.
    ///   2. Builds a WebGL player into Build/WebGL.
    ///   3. Exits with code 0 on success, 1 on failure.
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("Slot Game/Build WebGL")]
        public static void BuildWebGL()
        {
            string[] scenes = { AssetPaths.ScenePath };

            Debug.Log("[SlotGame] Starting WebGL build...");
            Debug.Log("[SlotGame] Scene: " + AssetPaths.ScenePath);
            Debug.Log("[SlotGame] Output: " + AssetPaths.WebGLBuildPath);

            // Set WebGL-specific player settings.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.companyName = "SlotGame";
            PlayerSettings.productName = "Slot Machine";
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = AssetPaths.WebGLBuildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log("[SlotGame] ★ WebGL build succeeded! Output: " + AssetPaths.WebGLBuildPath);
            }
            else
            {
                Debug.LogError("[SlotGame] WebGL build FAILED: " + report.summary.totalErrors + " error(s)");
                // If running in batch mode, exit with error code.
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }
    }
}
