#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HillbillyTaxi.EditorTools
{
    /// <summary>
    /// Fails Steam proof builds early when the development App ID is missing,
    /// then guarantees the file is beside the Windows executable.
    /// </summary>
    public sealed class HillbillyTaxiSteamBuildHelper :
        IPreprocessBuildWithReport,
        IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static string ProjectAppIdPath =>
            Path.Combine(
                Directory.GetParent(
                    Application.dataPath)!.FullName,
                "steam_appid.txt");

        public void OnPreprocessBuild(
            BuildReport report)
        {
            if (!File.Exists(ProjectAppIdPath))
            {
                throw new BuildFailedException(
                    "steam_appid.txt is missing from the project root.");
            }

            string appId =
                File.ReadAllText(
                    ProjectAppIdPath).Trim();

            if (appId != "480")
            {
                throw new BuildFailedException(
                    "The Steam proof currently expects " +
                    "steam_appid.txt to contain only 480.");
            }
        }

        public void OnPostprocessBuild(
            BuildReport report)
        {
            if (report.summary.platform !=
                    BuildTarget.StandaloneWindows &&
                report.summary.platform !=
                    BuildTarget.StandaloneWindows64)
            {
                return;
            }

            string outputDirectory =
                Path.GetDirectoryName(
                    report.summary.outputPath)!;

            string destination =
                Path.Combine(
                    outputDirectory,
                    "steam_appid.txt");

            File.Copy(
                ProjectAppIdPath,
                destination,
                overwrite: true);

            Debug.Log(
                $"Copied Steam App ID to {destination}.");
        }
    }
}
#endif
