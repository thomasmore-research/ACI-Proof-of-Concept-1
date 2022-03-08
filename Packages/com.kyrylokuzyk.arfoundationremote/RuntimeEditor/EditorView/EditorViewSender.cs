#if UNITY_EDITOR
using System.Collections;
using System.Diagnostics;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.RuntimeEditor {
    public class EditorViewSender : MonoBehaviour {
        [SerializeField] bool debug = false;
        [SerializeField] [CanBeNull] Texture2D colorTexture = null;
        
        [CanBeNull] RenderTexture colorRt;
        Throttler throttler;
        bool canSend;

        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void beforeSceneLoad() {
            Utils.TryCreatePersistentEditorObject<EditorViewSender>();
        }

        IEnumerator Start() {
            throttler = new Throttler(Settings.Instance.editorGameViewSettings.maxEditorViewFps);
            Assert.IsNull(GetComponent<Camera>());
            Assert.AreEqual(1, FindObjectsOfType<EditorViewSender>().Length);

            var waitForEndOfFrame = new WaitForEndOfFrame();
            while (true) {
                while (!canSend) {
                    log("!canSend");
                    yield return null;
                }
                
                yield return waitForEndOfFrame;
                log("yield return waitForEndOfFrame");
                var w = Screen.width;
                var h = Screen.height;

                var tex = Texture2DSerializable.GetCachedTexture(w, h, TextureFormat.RGB24);
                tex.ReadPixels(new Rect(0,0,w,h),0,0);
                tex.Apply();
                Assert.IsNull(RenderTexture.active, "RenderTexture.active == null");
                Assert.IsNotNull(colorRt);
                Graphics.Blit(tex, colorRt);
                Assert.IsNotNull(colorRt);
                var serializedColorTex = Texture2DSerializable.SerializeToJPG(colorRt);
                if (debug) {
                    serializedColorTex.ResizeIfNeededAndDeserializeInto(ref colorTexture);
                }
        
                Connection.Send(new EditorViewData {
                    colorTexture = serializedColorTex
                });
                
                yield return null; // wait for updated canSend 
            }
        }

        void OnDestroy() {
            foreach (var _ in new Object[] {colorTexture}) {
                if (_ != null) {
                    Destroy(_);
                }
            }
        }

        void Update() {
            canSend = throttler.CanSendNonCriticalMessage;
            updateRenderTextures();
        }

        void updateRenderTextures() {
            var downscaled = getDownscaledResolution();
            var w = downscaled.x;
            var h = downscaled.y;
            updateRt(ref colorRt, RenderTextureFormat.ARGB32);

            void updateRt(ref RenderTexture rt, RenderTextureFormat format) {
                if (rt != null && rt.width == w && rt.height == h) {
                    return;
                }

                if (rt != null) {
                    Destroy(rt);
                }

                rt = new RenderTexture(w, h, 0, format);
            }
        }

        static Vector2Int getDownscaledResolution() {
            var scale = Settings.Instance.editorGameViewSettings.resolutionScale;
            var downscaledWidth = Mathf.RoundToInt(Screen.width * scale);
            var downscaledHeight = Mathf.RoundToInt(Screen.height * scale);
            return new Vector2Int(downscaledWidth, downscaledHeight);
        }

        
        [Conditional("_")]
        void log(string s) {
            Debug.Log(s);
        }
    }
}
#endif
