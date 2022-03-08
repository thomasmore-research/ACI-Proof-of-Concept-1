#if UNITY_EDITOR
using System.Collections;
using System.Diagnostics;
using ARFoundationRemote.Editor;
using ARFoundationRemote.Runtime;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.Management;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.RuntimeEditor {
    public class Receiver: MonoBehaviour, IConnectionDelegate {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void beforeSceneLoad() {
            if (!Global.IsPluginEnabled()) {
                return;
            }
        
            Assert.IsTrue(FindObjectsOfType<Receiver>().Length == 0);
            Assert.IsTrue(Application.isPlaying);
            Connection.Send(Settings.Instance.arCompanionSettings);
            Utils.TryCreatePersistentEditorObject<Receiver>();
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingPlayMode) {
                    logDestruction(state.ToString());
                    Global.IsExitingPlayMode = true;
                }
            };
        }

        void OnEnable() {
            Connection.Register<PackageVersionData[]>(packages => {
                PackageVersionData.CheckVersions(packages, Settings.Instance.packages);
            });
        }

        void OnDisable() {
            Connection.UnRegister<PackageVersionData[]>();
        }

        void Start() {
            logDestruction("Receiver.Start()");
            Connection.Instance.StartConnection(this);
            StartCoroutine(sendPackages());
        }

        void OnApplicationQuit() {
            logDestruction("OnApplicationQuit; isQuitting = true;");
            Global.isQuitting = true;
        }
        
        [Conditional("_")]
        public static void logDestruction(string s) {
            Debug.Log(s);
        }

        IEnumerator sendPackages() {
            var listRequest = Client.List(true, true);
            while (!listRequest.IsCompleted) {
                yield return null;
            }
                
            Assert.AreEqual(StatusCode.Success, listRequest.Status);
            Connection.Send(PackageVersionData.Create(listRequest.Result));
        }
        
        void IConnectionDelegate.OnConnected() {
            ReviewRequest.RecordUsage();
        }

        void IConnectionDelegate.OnDisconnected() {
            Debug.LogError($"{Constants.packageName}: Editor lost connection with AR Companion app. Please restart Editor scene.");
        }
    }
}
#endif
