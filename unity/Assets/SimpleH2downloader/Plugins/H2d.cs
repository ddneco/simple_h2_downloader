using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SimpleH2downloader {
    public class H2d : MonoBehaviour {
        public delegate int ProxyresolverFunc(string url, IntPtr proxyBuf, int proxyBufSize);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("h2downloader")]
        public static extern int dlboot(string sourceAssetBundleURL, string tmpDir, ProxyresolverFunc proxyresolver, int maxconn);
        [DllImport("h2downloader")]
        public static extern int dlshutdown();
        [DllImport("h2downloader")]
        public static extern int dlreq(string url);
        [DllImport("h2downloader")]
        public static extern int dlres(IntPtr presult, IntPtr pfilebuf, int buflen, IntPtr premain);
        [DllImport("h2downloader")]
        public static extern int dlprogress(int id);
        [DllImport("h2downloader")]
        public static extern int dlsetmaxconn(int maxconn);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        [DllImport("h2downloader_osx")]
        public static extern int dlboot(string sourceAssetBundleURL, string tmpDir, ProxyresolverFunc proxyresolver, int maxconn);
        [DllImport("h2downloader_osx")]
        public static extern int dlshutdown();
        [DllImport("h2downloader_osx")]
        public static extern int dlreq(string url);
        [DllImport("h2downloader_osx")]
        public static extern int dlres(IntPtr presult, IntPtr pfilebuf, int buflen, IntPtr premain);
        [DllImport("h2downloader_osx")]
        public static extern int dlprogress(int id);
        [DllImport("h2downloader_osx")]
        public static extern int dlsetmaxconn(int maxconn);
#elif UNITY_IOS
        [DllImport("__Internal")]
        public static extern int dlboot(string sourceAssetBundleURL, string tmpDir, ProxyresolverFunc proxyresolver, int maxconn);
        [DllImport("__Internal")]
        public static extern int dlshutdown();
        [DllImport("__Internal")]
        public static extern int dlreq(string url);
        [DllImport("__Internal")]
        public static extern int dlres(IntPtr presult, IntPtr pfilebuf, int buflen, IntPtr premain);
        [DllImport("__Internal")]
        public static extern int dlprogress(int id);
        [DllImport("__Internal")]
        public static extern int dlsetmaxconn(int maxconn);
#elif UNITY_ANDROID
        [DllImport("h2downloader")]
        public static extern int dlboot(string sourceAssetBundleURL, string tmpDir, ProxyresolverFunc proxyresolver, int maxconn);
        [DllImport("h2downloader")]
        public static extern int dlshutdown();
        [DllImport("h2downloader")]
        public static extern int dlreq(string url);
        [DllImport("h2downloader")]
        public static extern int dlres(IntPtr presult, IntPtr pfilebuf, int buflen, IntPtr premain);
        [DllImport("h2downloader")]
        public static extern int dlprogress(int id);
        [DllImport("h2downloader")]
        public static extern int dlsetmaxconn(int maxconn);
#endif
    }
}
