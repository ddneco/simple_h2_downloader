#define USABLE_UWR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Net;
using UnityEngine;
using UnityEngine.UI;
#if USABLE_UWR
using UnityEngine.Networking;
#endif

namespace SimpleH2downloader {
    public class WWWAssetBundle : IEnumerator {
        static public readonly string DL_WEB_CACHE_PATH = Application.persistentDataPath+"/h2dwebcache/";
        static public readonly string DL_TMP_PATH = Application.temporaryCachePath+"/h2dtmp/";

        static public string BaseDownloadingURL { get; set; }
        static float lastExecTime;

        public AssetBundle assetBundle { get; protected set; }
        public string error { get; protected set; }
        public string url { get; private set; }

        protected int id;
        protected AssetBundleCreateRequest req;
        protected string tmpPath;

#if USABLE_UWR
        public static bool UseUWR;
        protected static int prevId;
#if UNITY2018_OR_NEWER
        protected UnityWebRequest uwr;
#else
        protected WWW uwr;
#endif
#endif

        public WWWAssetBundle(string url) {
            this.url = url;
#if USABLE_UWR
            if (UseUWR) {
#if UNITY2018_OR_NEWER
                uwr = UnityWebRequestAssetBundle.GetAssetBundle(BaseDownloadingURL + url);
                uwr.SendWebRequest();
#else
                uwr = new WWW(BaseDownloadingURL + url);
#endif
            }
#endif
        }

        public WWWAssetBundle(string url, Hash128 hash) {
            this.url = url;
#if USABLE_UWR
            if (UseUWR) {
#if UNITY2018_OR_NEWER
                uwr = UnityWebRequestAssetBundle.GetAssetBundle(BaseDownloadingURL + url, hash, 0);
                uwr.SendWebRequest();
#else
                uwr = WWW.LoadFromCacheOrDownload(BaseDownloadingURL + url, hash);
#endif
            }
#endif
        }

        public static void DeleteAllWebCache() {
            var files = Directory.GetFiles(DL_WEB_CACHE_PATH);
            foreach (var f in files) {
                File.Delete(f);
            }
#if USABLE_UWR
#if UNITY_2017_OR_NEWER
            Caching.ClearCache();
#else
            Caching.CleanCache();
#endif
#endif
        }

        public static WWWAssetBundle LoadFromCacheOrDownload(string url, Hash128 hash) {
#if USABLE_UWR
            if (UseUWR) {
                return new WWWAssetBundle(url, hash);
            }
#endif
            return new WWWAssetBundleViaCache(url, hash);
        }

        public bool isDone {
            get {
                return !MoveNext();
            }
        }
        public object Current { get { return null; } }
        public void Reset() {}
        public bool MoveNext() {
            if (error != null || assetBundle != null) {
                return false;
            }
#if USABLE_UWR
            if (uwr != null) {
                if (!uwr.isDone) {
                    return true;
                }
#if UNITY2018_OR_NEWER
                if (uwr.isNetworkError) {
                    error = "network-error";
                }
                if (uwr.isHttpError) {
                    error = "http-error";
                }
#else
                if (!string.IsNullOrEmpty(uwr.error)) {
                    error = uwr.error;
                }
#endif
#if UNITY2018_OR_NEWER
                var handler = uwr.downloadHandler as DownloadHandlerAssetBundle;
                if (handler != null) {
                    assetBundle = handler.assetBundle;
                }
#else
                assetBundle = uwr.assetBundle;
#endif
                if (assetBundle == null) {
                    error = "read-error";
                }
                uwr.Dispose();
                uwr = null;
                return false;
            }
#endif
            if (req != null) {
                if (!req.isDone) {
                    return true;
                }
                assetBundle = req.assetBundle;
                OnLoad(assetBundle != null);
                req = null;
                if (assetBundle == null) {
                    error = "read-error";
                }
                return false;
            }
            if (lastExecTime != Time.time) {
                lastExecTime = Time.time;
                Exec();
            }

            if (id == 0) {
                id = H2d.dlreq(this.url);
                exec[id] = this;
            }
            return true;
        }

        public void Dispose() {}

        protected virtual void OnReceived(string path, string error) {
            if (error != null) {
                this.error = error;
                return;
            }
            if (path != null) {
                tmpPath = path;
                req = AssetBundle.LoadFromFileAsync(DL_TMP_PATH + path);
                return;
            }
        }
        protected virtual void OnLoad(bool isValid) {
            File.Delete(DL_TMP_PATH + tmpPath);
        }

        [AOT.MonoPInvokeCallback(typeof(H2d.ProxyresolverFunc))]
        static int Proxyresolver(string url, IntPtr proxyBuf, int proxyBufSize) {
            Uri uri = new Uri(url);
            var proxy = WebRequest.GetSystemWebProxy();
            if (proxy != null && !proxy.IsBypassed(uri)) {
                var proxyUri = proxy.GetProxy(uri);
                var proxyStr = proxyUri.Scheme + "://"+proxyUri.Host + ":" + proxyUri.Port;
                byte[] data = System.Text.Encoding.ASCII.GetBytes(proxyStr);
                int len = Math.Min(data.Length, proxyBufSize);
                Marshal.Copy(data, 0, proxyBuf, len);
                return len;
            }
            else {
                return 0;
            }
        }

        static Dictionary<int, WWWAssetBundle> exec;
        static IntPtr hresult;
        static IntPtr hfilebuf;
        static IntPtr hremain;

        static public void Initialize() {
            Debug.Log("[WWWAssetBundle] Initialize BaseDownloadingURL=" + BaseDownloadingURL + " DL_TMP_PATH=" + DL_TMP_PATH + " DL_WEB_CACHE_PATH=" + DL_WEB_CACHE_PATH);
            Directory.CreateDirectory(DL_WEB_CACHE_PATH);
            hresult = Marshal.AllocHGlobal(4);
            hfilebuf = Marshal.AllocHGlobal(1024);
            hremain = Marshal.AllocHGlobal(4);
            H2d.dlboot(BaseDownloadingURL, DL_TMP_PATH, Proxyresolver, -1);
#if UNITY_EDITOR
#if UNITY_2018_3_OR_NEWER
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif
#endif
            exec = new Dictionary<int, WWWAssetBundle>();
        }

#if UNITY_EDITOR
#if UNITY_2018_3_OR_NEWER
        static void OnBeforeAssemblyReload() {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            H2d.dlshutdown(); // avoid freeze when script reloading.
        }
#endif
#endif

        static public void Terminate() {
            if (exec == null) {
                return;
            }
            exec.Clear();
            exec = null;
            H2d.dlshutdown();
            Marshal.FreeHGlobal(hremain);
            Marshal.FreeHGlobal(hfilebuf);
            Marshal.FreeHGlobal(hresult);
            Debug.Log("[WWWAssetBundle] Terminate");
        }

        static void Exec() {
            var exec_ = exec;
            if (exec_ == null) {
                throw new UnityException("illegal state!");
            }
            int remain = 1;
            while (remain != 0) {
                remain = 0;
                var id = H2d.dlres(hresult, hfilebuf, 1024, hremain);
                if (id != 0) {
                    var result = Marshal.ReadInt32(hresult);
                    remain = Marshal.ReadInt32(hremain);
                    if (result == 0) {
                        var file = Marshal.PtrToStringAnsi(hfilebuf);
                        var www = exec_[id];
                        exec_.Remove(id);
                        www.OnReceived(file, null);
                    } else {
                        var www = exec_[id];
                        exec_.Remove(id);
                        var http_status_code = result % 10000;
                        var internal_code = result / 10000;
                        www.OnReceived(null, "error icode=" + internal_code + " http_status_code=" + http_status_code);
                    }
                }
            }
        }
    }

    class WWWAssetBundleViaCache : WWWAssetBundle
    {
        Hash128 hash;

        public WWWAssetBundleViaCache(string url, Hash128 hash) : base(url) {
            this.hash = hash;
            string destPath = WWWAssetBundle.DL_WEB_CACHE_PATH + hash.ToString();
            if (File.Exists(destPath)) {
                req = AssetBundle.LoadFromFileAsync(destPath);
            }
        }

        protected override void OnLoad(bool isValid) {
            if (!isValid) {
                base.OnLoad(isValid);
                return;
            }
            string destPath = WWWAssetBundle.DL_WEB_CACHE_PATH + hash.ToString();
            if (tmpPath != null && File.Exists(WWWAssetBundle.DL_TMP_PATH + tmpPath)) {
                if (File.Exists(destPath)) {
                    File.Delete(destPath);
                }
                File.Move(WWWAssetBundle.DL_TMP_PATH + tmpPath, destPath);
            }
        }
    }
}
