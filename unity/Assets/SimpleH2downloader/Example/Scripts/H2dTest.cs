using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SimpleH2downloader.AssetBundles;

namespace SimpleH2downloader {
    [RequireComponent(typeof(Button))]
    public class H2dTest : MonoBehaviour {
        private static readonly int X_NUM = 16;
        private static readonly int Y_NUM = 6;
        private static readonly int TILE_HEIGHT = 32;
        private static readonly int TILE_WIDTH = 32;

        [SerializeField]
        Button btn = null;
        [SerializeField]
        Text txt = null;
        [SerializeField]
        Toggle tglH2 = null;

        [SerializeField]
        Transform panel = null;

        Image[] imgs;
        float startTime;

        IEnumerator Start()
        {
            btn.interactable = false;
            WWWAssetBundle.UseUWR = !tglH2.isOn;

            AssetBundleManager.SetSourceAssetBundleURL(
#if UNITY_2017_OR_NEWER
                "https://simpleh2downloader.web.app/AssetBundles/"
#else
                "https://simpleh2downloader.web.app/AssetBundles/u56/"
#endif
            );
            var abmOp = AssetBundleManager.Initialize();
            if (abmOp != null) {
                yield return abmOp;
            }

            imgs = new Image[X_NUM * Y_NUM];
            btn.interactable = true;
            yield break;
        }

        void Update() {
            if (startTime != 0) {
                UpdateTime(Time.realtimeSinceStartup - startTime);
            }
        }

        void UpdateTime(float duration) {
            txt.text = string.Format("{0:F3}s", duration);
        }

        public void OnClick() {
            int cnt = 0;
            for (int i = 0; i < imgs.Length; i++) {
                cnt++;
                int x = i % X_NUM;
                int y = i / X_NUM;

                var go = new GameObject();
                go.name = y+"_"+x;
                go.transform.SetParent(panel);
                go.transform.localPosition = new Vector3(x * TILE_WIDTH - X_NUM/2 * TILE_WIDTH, - y * TILE_HEIGHT + Y_NUM/2 * TILE_HEIGHT, 0);
                var img = go.AddComponent<Image>();
                go.SetActive(false);
                imgs[i] = img;

                StartCoroutine(LoadCoroutine<Texture2D>(y+"_"+x, y+"_"+x, (asset)=>{
                    cnt--;
                    if (cnt == 0) {
                        UpdateTime(Time.realtimeSinceStartup - startTime);
                        startTime = 0;
                    }
                    if (asset == null) {
                        return;
                    }
                    var sp = Sprite.Create(
                        texture : asset,
                        rect    : new Rect(0, 0, asset.width, asset.height),
                        pivot   : new Vector2(0.5f, 0.5f)
                    );
                    img.gameObject.SetActive(true);
                    img.sprite = sp;
                    img.SetNativeSize();
                }));
            }
            btn.interactable = false;
            startTime = Time.realtimeSinceStartup;
        }

        public void OnDelClick() {
            WWWAssetBundle.DeleteAllWebCache();
        }

        public void OnChangeH2() {
            var useUWR = !tglH2.isOn;
            Debug.Log("OnChangeH2 "+useUWR);
            WWWAssetBundle.UseUWR = useUWR;
        }

        IEnumerator LoadCoroutine<T>(string assetBundleName, string assetName, Action<T> assetCb) where T : UnityEngine.Object {
            var op = AssetBundleManager.LoadAssetAsync(assetBundleName, assetName, typeof(T));
            while (!op.IsDone()) {
                yield return null;
            }
            assetCb(op.GetAsset<T>());
        }

        void Callback(string message) {
            Debug.Log(message);
        }
    }
}
