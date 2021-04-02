using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Net;
using System.Threading;
using UnityEditor.Utils;
using System.Linq;

namespace SimpleH2downloader { namespace AssetBundles {

    internal class LaunchAssetBundleServer : ScriptableSingleton<LaunchAssetBundleServer>
    {
        const string kLocalAssetbundleServerMenu = "Tools/SimpleH2downloader/AssetBundles/Local AssetBundle Server";

        [SerializeField]
        int     m_ServerPID = 0;

        [MenuItem(kLocalAssetbundleServerMenu)]
        public static void ToggleLocalAssetBundleServer()
        {
            bool isRunning = IsRunning();
            if (!isRunning)
            {
                if (!File.Exists("Assets/SimpleH2downloader/AssetBundleManager/Editor/AssetBundleServer.exe"))
                {
                    EditorUtility.DisplayDialog("need server program.", "to use 'Local AssetBundle Server', get 'AssetBundleServer.exe', \n"
                        + "from 'https://bitbucket.org/Unity-Technologies/assetbundledemo/src/'."
                        + "and put 'Assets/SimpleH2downloader/AssetBundleManager/Editor/'", "OK");
                    return;
                }
                Run();
            }
            else
            {
                KillRunningAssetBundleServer();
            }
        }

        [MenuItem(kLocalAssetbundleServerMenu, true)]
        public static bool ToggleLocalAssetBundleServerValidate()
        {
            bool isRunnning = IsRunning();
            Menu.SetChecked(kLocalAssetbundleServerMenu, isRunnning);
            return true;
        }

        static bool IsRunning()
        {
            if (instance.m_ServerPID == 0)
                return false;

            try
            {
                var process = System.Diagnostics.Process.GetProcessById(instance.m_ServerPID);
                if (process == null)
                    return false;

                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        static void KillRunningAssetBundleServer()
        {
            // Kill the last time we ran
            try
            {
                if (instance.m_ServerPID == 0)
                    return;

                var lastProcess = System.Diagnostics.Process.GetProcessById(instance.m_ServerPID);
                lastProcess.Kill();
                instance.m_ServerPID = 0;
            }
            catch
            {
            }
            BuildScript.DeleteServerURL();
        }

        static void Run()
        {
            string pathToAssetServer = Path.GetFullPath("Assets/SimpleH2downloader/AssetBundleManager/Editor/AssetBundleServer.exe");
            string assetBundlesDirectory = Path.Combine(Environment.CurrentDirectory, "AssetBundles");

            KillRunningAssetBundleServer();

            BuildScript.CreateAssetBundleDirectory();
            BuildScript.WriteServerURL();

            string args = assetBundlesDirectory;
            args = string.Format("\"{0}\" {1}", args, System.Diagnostics.Process.GetCurrentProcess().Id);
            var startInfo = ExecuteInternalMono.GetProfileStartInfoForMono(MonoInstallationFinder.GetMonoInstallation("MonoBleedingEdge"), GetMonoProfileVersion(), pathToAssetServer, args, true);
            startInfo.WorkingDirectory = assetBundlesDirectory;
            startInfo.UseShellExecute = false;
            var launchProcess = System.Diagnostics.Process.Start(startInfo);
            if (launchProcess == null || launchProcess.HasExited == true || launchProcess.Id == 0)
            {
                //Unable to start process
                UnityEngine.Debug.LogError("Unable Start AssetBundleServer process");
            }
            else
            {
                //We seem to have launched, let's save the PID
                instance.m_ServerPID = launchProcess.Id;
            }
        }

        static string GetMonoProfileVersion()
        {
            string path = Path.Combine(Path.Combine(MonoInstallationFinder.GetMonoInstallation("MonoBleedingEdge"), "lib"), "mono");

            string[] folders = Directory.GetDirectories(path);
            string[] foldersWithApi = folders.Where(f => f.Contains("-api")).ToArray();
            string profileVersion = "1.0";

            for (int i = 0; i < foldersWithApi.Length; i++)
            {
                foldersWithApi[i] = foldersWithApi[i].Split(Path.DirectorySeparatorChar).Last();
                foldersWithApi[i] = foldersWithApi[i].Split('-').First();

                if (profileVersion.CompareTo(foldersWithApi[i]) < 0)
                {
                    profileVersion = foldersWithApi[i];
                }
            }

            return profileVersion;
        }
    }
} }
