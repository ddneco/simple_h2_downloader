using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace SimpleH2downloader { namespace AssetBundles {

    public class BuildScript
    {
        public static string overloadedDevelopmentServerURL = "";
        const string assetBundleManagerResourcesDirectory = "Assets/SimpleH2downloader/AssetBundleManager/Resources";
        const string assetBundleServerURLAssetName = "AssetBundleServerURL.bytes";

        static public string CreateAssetBundleDirectory()
        {
            // Choose the output path according to the build target.
            string outputPath = Path.Combine(Utility.AssetBundlesOutputPath, Utility.GetPlatformName());
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            return outputPath;
        }

        public static void BuildAssetBundles()
        {
            BuildAssetBundles(null);
        }

        public static void BuildAssetBundles(AssetBundleBuild[] builds)
        {
            // Choose the output path according to the build target.
            string outputPath = CreateAssetBundleDirectory();

            var options = BuildAssetBundleOptions.DisableWriteTypeTree
//                        | BuildAssetBundleOptions.IgnoreTypeTreeChanges
                        ;
            if (builds == null || builds.Length == 0)
            {
                //@TODO: use append hash... (Make sure pipeline works correctly with it.)
                BuildPipeline.BuildAssetBundles(outputPath, options, EditorUserBuildSettings.activeBuildTarget);
            }
            else
            {
                BuildPipeline.BuildAssetBundles(outputPath, builds, options, EditorUserBuildSettings.activeBuildTarget);
            }
        }

        public static void WriteServerURL()
        {
            string downloadURL;
            if (string.IsNullOrEmpty(overloadedDevelopmentServerURL) == false)
            {
                downloadURL = overloadedDevelopmentServerURL;
            }
            else
            {
                IPHostEntry host;
                string localIP = "";
                host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localIP = ip.ToString();
                        break;
                    }
                }
                downloadURL = "http://" + localIP + ":7888/";
            }

            string assetBundleUrlPath = Path.Combine(assetBundleManagerResourcesDirectory, assetBundleServerURLAssetName);
            Directory.CreateDirectory(assetBundleManagerResourcesDirectory);
            File.WriteAllText(assetBundleUrlPath, downloadURL);
            AssetDatabase.Refresh();
        }

        public static void DeleteServerURL()
        {
            string assetBundleUrlPath = Path.Combine(assetBundleManagerResourcesDirectory, assetBundleServerURLAssetName);
            if (File.Exists(assetBundleUrlPath))
            {
                File.Delete(assetBundleUrlPath);
                AssetDatabase.Refresh();
            }
        }

        public static void BuildPlayer()
        {
            var outputPath = EditorUtility.SaveFolderPanel("Choose Location of the Built Game", "", "");
            if (outputPath.Length == 0)
                return;

            string[] levels = GetLevelsFromBuildSettings();
            if (levels.Length == 0)
            {
                Debug.Log("Nothing to build.");
                return;
            }

            string targetName = GetBuildTargetName(EditorUserBuildSettings.activeBuildTarget);
            if (targetName == null)
                return;

            // Build and copy AssetBundles.
            BuildScript.BuildAssetBundles();
            WriteServerURL();

            BuildOptions option = EditorUserBuildSettings.development ? BuildOptions.Development : BuildOptions.None;
            BuildPipeline.BuildPlayer(levels, outputPath + targetName, EditorUserBuildSettings.activeBuildTarget, option);
        }

        public static void BuildStandalonePlayer()
        {
            var outputPath = EditorUtility.SaveFolderPanel("Choose Location of the Built Game", "", "");
            if (outputPath.Length == 0)
                return;

            string[] levels = GetLevelsFromBuildSettings();
            if (levels.Length == 0)
            {
                Debug.Log("Nothing to build.");
                return;
            }

            string targetName = GetBuildTargetName(EditorUserBuildSettings.activeBuildTarget);
            if (targetName == null)
                return;

            // Build and copy AssetBundles.
            BuildScript.BuildAssetBundles();
            BuildScript.CopyAssetBundlesTo(Path.Combine(Application.streamingAssetsPath, Utility.AssetBundlesOutputPath));
            AssetDatabase.Refresh();

            BuildOptions option = EditorUserBuildSettings.development ? BuildOptions.Development : BuildOptions.None;
            BuildPipeline.BuildPlayer(levels, outputPath + targetName, EditorUserBuildSettings.activeBuildTarget, option);
        }

        public static string GetBuildTargetName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return "/test.apk";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "/test.exe";
#if UNITY_2017_OR_NEWER
                case BuildTarget.StandaloneOSX:
#else
                case BuildTarget.StandaloneOSXIntel64:
#endif
                    return "/test.app";
                case BuildTarget.WebGL:
                case BuildTarget.iOS:
                    return "";
                // Add more build targets for your own.
                default:
                    Debug.Log("Target not implemented.");
                    return null;
            }
        }

        static void CopyAssetBundlesTo(string outputPath)
        {
            // Clear streaming assets folder.
            FileUtil.DeleteFileOrDirectory(Application.streamingAssetsPath);
            Directory.CreateDirectory(outputPath);

            string outputFolder = Utility.GetPlatformName();

            // Setup the source folder for assetbundles.
            var source = Path.Combine(Path.Combine(System.Environment.CurrentDirectory, Utility.AssetBundlesOutputPath), outputFolder);
            if (!System.IO.Directory.Exists(source))
                Debug.Log("No assetBundle output folder, try to build the assetBundles first.");

            // Setup the destination folder for assetbundles.
            var destination = System.IO.Path.Combine(outputPath, outputFolder);
            if (System.IO.Directory.Exists(destination))
                FileUtil.DeleteFileOrDirectory(destination);

            FileUtil.CopyFileOrDirectory(source, destination);
        }

        static string[] GetLevelsFromBuildSettings()
        {
            List<string> levels = new List<string>();
            for (int i = 0; i < EditorBuildSettings.scenes.Length; ++i)
            {
                if (EditorBuildSettings.scenes[i].enabled)
                    levels.Add(EditorBuildSettings.scenes[i].path);
            }

            return levels.ToArray();
        }

        static string GetAssetBundleManifestFilePath()
        {
            var relativeAssetBundlesOutputPathForPlatform = Path.Combine(Utility.AssetBundlesOutputPath, Utility.GetPlatformName());
            return Path.Combine(relativeAssetBundlesOutputPathForPlatform,  Utility.GetPlatformName()) + ".manifest";
        }
    }
} }
