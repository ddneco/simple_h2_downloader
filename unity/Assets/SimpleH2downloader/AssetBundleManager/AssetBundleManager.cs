using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;

/*  The AssetBundle Manager provides a High-Level API for working with AssetBundles. 
    The AssetBundle Manager will take care of loading AssetBundles and their associated 
    Asset Dependencies.
        Initialize()
            Initializes the AssetBundle manifest object.
        LoadAssetAsync()
            Loads a given asset from a given AssetBundle and handles all the dependencies.
        LoadLevelAsync()
            Loads a given scene from a given AssetBundle and handles all the dependencies.
        LoadDependencies()
            Loads all the dependent AssetBundles for a given AssetBundle.
        BaseDownloadingURL
            Sets the base downloading url which is used for automatic downloading dependencies.
        SimulateAssetBundleInEditor
            Sets Simulation Mode in the Editor.
        Variants
            Sets the active variant.
*/

namespace SimpleH2downloader { namespace AssetBundles {
    /// <summary>
    /// Loaded assetBundle contains the references count which can be used to
    /// unload dependent assetBundles automatically.
    /// </summary>
    public class LoadedAssetBundle
    {
        public AssetBundle m_AssetBundle;
        public int m_ReferencedCount;

        internal event Action unload;

        internal void OnUnload()
        {
            m_AssetBundle.Unload(false);
            if (unload != null)
                unload();
        }

        public LoadedAssetBundle(AssetBundle assetBundle)
        {
            m_AssetBundle = assetBundle;
            m_ReferencedCount = 1;
        }
    }

    /// <summary>
    /// Class takes care of loading assetBundle and its dependencies
    /// automatically, loading variants automatically.
    /// </summary>
    public class AssetBundleManager : MonoBehaviour
    {
        public enum LogMode { All, JustErrors };
        public enum LogType { Info, Warning, Error };

        static LogMode m_LogMode = LogMode.All;
        static string[] m_ActiveVariants =  {};
        static AssetBundleManifest m_AssetBundleManifest = null;

#if UNITY_EDITOR
        static int m_SimulateAssetBundleInEditor = -1;
        const string kSimulateAssetBundles = "SimpleH2downloaderSimulateAssetBundles";
#endif

        static Dictionary<string, LoadedAssetBundle> m_LoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();
        static Dictionary<string, string> m_DownloadingErrors = new Dictionary<string, string>();
        static List<string> m_DownloadingBundles = new List<string>();
        static List<AssetBundleLoadOperation> m_InProgressOperations = new List<AssetBundleLoadOperation>();
        static Dictionary<string, string[]> m_Dependencies = new Dictionary<string, string[]>();

        public static LogMode logMode
        {
            get { return m_LogMode; }
            set { m_LogMode = value; }
        }

        /// <summary>
        /// The base downloading url which is used to generate the full
        /// downloading url with the assetBundle names.
        /// </summary>
        public static string BaseDownloadingURL {
            get { return WWWAssetBundle.BaseDownloadingURL; }
            private set { WWWAssetBundle.BaseDownloadingURL = value; }
        }

        /// <summary>
        /// Variants which is used to define the active variants.
        /// </summary>
        public static string[] ActiveVariants
        {
            get { return m_ActiveVariants; }
            set { m_ActiveVariants = value; }
        }

        /// <summary>
        /// AssetBundleManifest object which can be used to load the dependecies
        /// and check suitable assetBundle variants.
        /// </summary>
        public static AssetBundleManifest AssetBundleManifestObject
        {
            set {m_AssetBundleManifest = value; }
        }

        private static void Log(LogType logType, string text)
        {
            if (logType == LogType.Error)
                Debug.LogError("[SH2dAssetBundleManager] " + text);
            else if (m_LogMode == LogMode.All && logType == LogType.Warning)
                Debug.LogWarning("[SH2dAssetBundleManager] " + text);
            else if (m_LogMode == LogMode.All)
                Debug.Log("[SH2dAssetBundleManager] " + text);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Flag to indicate if we want to simulate assetBundles in Editor without building them actually.
        /// </summary>
        public static bool SimulateAssetBundleInEditor
        {
            get
            {
                if (m_SimulateAssetBundleInEditor == -1)
                    m_SimulateAssetBundleInEditor = EditorPrefs.GetBool(kSimulateAssetBundles, false) ? 1 : 0;

                return m_SimulateAssetBundleInEditor != 0;
            }
            set
            {
                int newValue = value ? 1 : 0;
                if (newValue != m_SimulateAssetBundleInEditor)
                {
                    m_SimulateAssetBundleInEditor = newValue;
                    EditorPrefs.SetBool(kSimulateAssetBundles, value);
                }
            }
        }
#endif

        /// <summary>
        /// Sets base downloading URL to a web URL. The directory pointed to by this URL
        /// on the web-server should have the same structure as the AssetBundles directory
        /// in the demo project root.
        /// </summary>
        /// <example>For example, AssetBundles/iOS/xyz-scene must map to
        /// absolutePath/iOS/xyz-scene.
        /// <example>
        public static void SetSourceAssetBundleURL(string absolutePath)
        {
            if (!absolutePath.EndsWith("/"))
            {
                absolutePath += "/";
            }

            BaseDownloadingURL = absolutePath + Utility.GetPlatformName() + "/";
        }

        /// <summary>
        /// Sets base downloading URL to a local development server URL.
        /// </summary>
        public static void SetDevelopmentAssetBundleServer()
        {
#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to setup a download URL
            if (SimulateAssetBundleInEditor)
                return;
#endif

            TextAsset urlFile = Resources.Load("AssetBundleServerURL") as TextAsset;
            string url = (urlFile != null) ? urlFile.text.Trim() : null;
            if (url == null || url.Length == 0)
            {
                // nop
            }
            else
            {
                AssetBundleManager.SetSourceAssetBundleURL(url);
            }
        }

        /// <summary>
        /// Retrieves an asset bundle that has previously been requested via LoadAssetBundle.
        /// Returns null if the asset bundle or one of its dependencies have not been downloaded yet.
        /// </summary>
        static public LoadedAssetBundle GetLoadedAssetBundle(string assetBundleName, out string error)
        {
            if (m_DownloadingErrors.TryGetValue(assetBundleName, out error))
                return null;

            LoadedAssetBundle bundle = null;
            m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
            if (bundle == null)
                return null;

            // No dependencies are recorded, only the bundle itself is required.
            string[] dependencies = null;
            if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
                return bundle;

            // Make sure all dependencies are loaded
            foreach (var dependency in dependencies)
            {
                if (m_DownloadingErrors.TryGetValue(dependency, out error))
                    return null;

                // Wait all the dependent assetBundles being loaded.
                LoadedAssetBundle dependentBundle;
                m_LoadedAssetBundles.TryGetValue(dependency, out dependentBundle);
                if (dependentBundle == null)
                    return null;
            }

            return bundle;
        }

        /// <summary>
        /// Returns true if certain asset bundle has been downloaded without checking
        /// whether the dependencies have been loaded.
        /// </summary>
        static public bool IsAssetBundleDownloaded(string assetBundleName)
        {
            return m_LoadedAssetBundles.ContainsKey(assetBundleName);
        }

        /// <summary>
        /// Initializes asset bundle namager and starts download of manifest asset bundle.
        /// Returns the manifest asset bundle downolad operation object.
        /// </summary>
        static public AssetBundleLoadManifestOperation Initialize()
        {
            return Initialize(Utility.GetPlatformName());
        }

        /// <summary>
        /// Initializes asset bundle namager and starts download of manifest asset bundle.
        /// Returns the manifest asset bundle downolad operation object.
        /// </summary>
        static public AssetBundleLoadManifestOperation Initialize(string manifestAssetBundleName)
        {
#if UNITY_EDITOR
            Log(LogType.Info, "Simulation Mode: " + (SimulateAssetBundleInEditor ? "Enabled" : "Disabled"));
#endif

            var go = new GameObject("AssetBundleManager", typeof(AssetBundleManager));
            DontDestroyOnLoad(go);

#if UNITY_EDITOR
            SetDevelopmentAssetBundleServer();
#endif

            WWWAssetBundle.Initialize();

#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't need the manifest assetBundle.
            if (SimulateAssetBundleInEditor)
                return null;
#endif

            LoadAssetBundle(manifestAssetBundleName, true);
            var operation = new AssetBundleLoadManifestOperation(manifestAssetBundleName, "AssetBundleManifest", typeof(AssetBundleManifest));
            m_InProgressOperations.Add(operation);
            return operation;
        }

        void OnApplicationQuit() {
            WWWAssetBundle.Terminate();
        }

        // Temporarily work around a il2cpp bug
        static protected void LoadAssetBundle(string assetBundleName)
        {
            LoadAssetBundle(assetBundleName, false);
        }
            
        // Starts the download of the asset bundle identified by the given name, and asset bundles
        // that this asset bundle depends on.
        static protected void LoadAssetBundle(string assetBundleName, bool isLoadingAssetBundleManifest)
        {
            Log(LogType.Info, "Loading Asset Bundle " + (isLoadingAssetBundleManifest ? "Manifest: " : ": ") + assetBundleName);

#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to really load the assetBundle and its dependencies.
            if (SimulateAssetBundleInEditor)
                return;
#endif

            if (!isLoadingAssetBundleManifest)
            {
                if (m_AssetBundleManifest == null)
                {
                    Log(LogType.Error, "Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                    return;
                }
            }

            // Check if the assetBundle has already been processed.
            bool isAlreadyProcessed = LoadAssetBundleInternal(assetBundleName, isLoadingAssetBundleManifest);

            // Load dependencies.
            if (!isAlreadyProcessed && !isLoadingAssetBundleManifest)
                LoadDependencies(assetBundleName);
        }

        // Checks who is responsible for determination of the correct asset bundle variant
        // that should be loaded on this platform. 
        //
        // On most platforms, this is done by the AssetBundleManager itself. However, on
        // certain platforms (iOS at the moment) it's possible that an external asset bundle
        // variant resolution mechanism is used. In these cases, we use base asset bundle 
        // name (without the variant tag) as the bundle identifier. The platform-specific 
        // code is responsible for correctly loading the bundle.
        static protected bool UsesExternalBundleVariantResolutionMechanism(string baseAssetBundleName)
        {
            return false;
        }

        // Sets up download operation for the given asset bundle if it's not downloaded already.
        static protected bool LoadAssetBundleInternal(string assetBundleName, bool isLoadingAssetBundleManifest)
        {
            // Already loaded.
            LoadedAssetBundle bundle = null;
            m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
            if (bundle != null)
            {
                bundle.m_ReferencedCount++;
                return true;
            }

            // @TODO: Do we need to consider the referenced count of WWWs?
            // In the demo, we never have duplicate WWWs as we wait LoadAssetAsync()/LoadLevelAsync() to be finished before calling another LoadAssetAsync()/LoadLevelAsync().
            // But in the real case, users can call LoadAssetAsync()/LoadLevelAsync() several times then wait them to be finished which might have duplicate WWWs.
            if (m_DownloadingBundles.Contains(assetBundleName))
                return true;

            WWWAssetBundle download = null;
            string url = assetBundleName;

            // For manifest assetbundle, always download it as we don't have hash for it.
            if (isLoadingAssetBundleManifest)
                download = new WWWAssetBundle(url);
            else
                download = WWWAssetBundle.LoadFromCacheOrDownload(url, m_AssetBundleManifest.GetAssetBundleHash(assetBundleName));

            m_InProgressOperations.Add(new AssetBundleDownloadFromWebOperation(assetBundleName, download));
            m_DownloadingBundles.Add(assetBundleName);

            return false;
        }

        // Where we get all the dependencies and load them all.
        static protected void LoadDependencies(string assetBundleName)
        {
            if (m_AssetBundleManifest == null)
            {
                Log(LogType.Error, "Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                return;
            }

            // Get dependecies from the AssetBundleManifest object..
            string[] dependencies = m_AssetBundleManifest.GetAllDependencies(assetBundleName);
            if (dependencies.Length == 0)
                return;

            // Record and load all dependencies.
            m_Dependencies.Add(assetBundleName, dependencies);
            for (int i = 0; i < dependencies.Length; i++)
                LoadAssetBundleInternal(dependencies[i], false);
        }

        /// <summary>
        /// Unloads assetbundle and its dependencies.
        /// </summary>
        static public void UnloadAssetBundle(string assetBundleName)
        {
#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to load the manifest assetBundle.
            if (SimulateAssetBundleInEditor)
                return;
#endif
            UnloadAssetBundleInternal(assetBundleName);
            UnloadDependencies(assetBundleName);
        }

        static protected void UnloadDependencies(string assetBundleName)
        {
            string[] dependencies = null;
            if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
                return;

            // Loop dependencies.
            foreach (var dependency in dependencies)
            {
                UnloadAssetBundleInternal(dependency);
            }

            m_Dependencies.Remove(assetBundleName);
        }

        static protected void UnloadAssetBundleInternal(string assetBundleName)
        {
            string error;
            LoadedAssetBundle bundle = GetLoadedAssetBundle(assetBundleName, out error);
            if (bundle == null)
                return;

            if (--bundle.m_ReferencedCount == 0)
            {
                bundle.OnUnload();
                m_LoadedAssetBundles.Remove(assetBundleName);

                Log(LogType.Info, assetBundleName + " has been unloaded successfully");
            }
        }

        void Update()
        {
            // Update all in progress operations
            for (int i = 0; i < m_InProgressOperations.Count;)
            {
                var operation = m_InProgressOperations[i];
                if (operation.Update())
                {
                    i++;
                }
                else
                {
                    m_InProgressOperations.RemoveAt(i);
                    ProcessFinishedOperation(operation);
                }
            }
        }

        void ProcessFinishedOperation(AssetBundleLoadOperation operation)
        {
            AssetBundleDownloadOperation download = operation as AssetBundleDownloadOperation;
            if (download == null)
                return;

            if (string.IsNullOrEmpty(download.error))
                m_LoadedAssetBundles.Add(download.assetBundleName, download.assetBundle);
            else
            {
                string msg = string.Format("Failed downloading bundle {0} from {1}: {2}",
                        download.assetBundleName, download.GetSourceURL(), download.error);
                m_DownloadingErrors.Add(download.assetBundleName, msg);
            }

            m_DownloadingBundles.Remove(download.assetBundleName);
        }

        /// <summary>
        /// Starts a load operation for an asset from the given asset bundle.
        /// </summary>
        static public AssetBundleLoadAssetOperation LoadAssetAsync(string assetBundleName, string assetName, System.Type type)
        {
            Log(LogType.Info, "Loading " + assetName + " from " + assetBundleName + " bundle");

            AssetBundleLoadAssetOperation operation = null;
#if UNITY_EDITOR
            if (SimulateAssetBundleInEditor)
            {
                string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, assetName);
                if (assetPaths.Length == 0)
                {
                    Log(LogType.Error, "There is no asset with name \"" + assetName + "\" in " + assetBundleName);
                    return null;
                }

                // @TODO: Now we only get the main object from the first asset. Should consider type also.
                UnityEngine.Object target = AssetDatabase.LoadMainAssetAtPath(assetPaths[0]);
                operation = new AssetBundleLoadAssetOperationSimulation(target);
            }
            else
#endif
            {
                LoadAssetBundle(assetBundleName);
                operation = new AssetBundleLoadAssetOperationFull(assetBundleName, assetName, type);

                m_InProgressOperations.Add(operation);
            }

            return operation;
        }

        /// <summary>
        /// Starts a load operation for a level from the given asset bundle.
        /// </summary>
        static public AssetBundleLoadOperation LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive)
        {
            Log(LogType.Info, "Loading " + levelName + " from " + assetBundleName + " bundle");

            AssetBundleLoadOperation operation = null;
#if UNITY_EDITOR
            if (SimulateAssetBundleInEditor)
            {
                operation = new AssetBundleLoadLevelSimulationOperation(assetBundleName, levelName, isAdditive);
            }
            else
#endif
            {
                LoadAssetBundle(assetBundleName);
                operation = new AssetBundleLoadLevelOperation(assetBundleName, levelName, isAdditive);

                m_InProgressOperations.Add(operation);
            }

            return operation;
        }
    } // End of AssetBundleManager.
} }
