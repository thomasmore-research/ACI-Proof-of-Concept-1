using System.Collections;
using System.Diagnostics;
using System.Linq;
using ARFoundationRemote.Runtime;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Editor {
    [InitializeOnLoad]
    public static class FixesForEditorSupport {
        static FixesForEditorSupport() {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode) {
                    DontDestroyOnLoadSingleton.Instance.StartCoroutine(tryApplyFixes());
                }
            };
        }

        static IEnumerator tryApplyFixes() {
            if (!Global.IsPluginEnabled()) {
                yield break;
            }
            
            var listRequest = Client.List(true, false);
            while (!listRequest.IsCompleted) {
                yield return null;
            }
            
            Assert.AreEqual(StatusCode.Success, listRequest.Status);
            var arf = listRequest.Result.Single(_ => _.name == "com.unity.xr.arfoundation");
            if (arf.source == PackageSource.Embedded) {
                if (Apply()) {
                    Debug.LogError($"{Constants.packageName}: applying AR Foundation fixes for Editor. Please restart current scene.");
                    EditorApplication.isPlaying = false;
                }
            } else {
                Debug.LogError($"{Constants.packageName}: please embed the AR Foundation package.");
                EditorApplication.isPlaying = false;
            }
        }

        public static bool Apply() {
            var isAnyFixApplied = false;
            isAnyFixApplied |= ARPointCloudManagerAppendMethodFixer.ApplyIfNeeded();
            isAnyFixApplied |= ARCameraBackgroundFixer.ApplyFixIfNeeded();
            isAnyFixApplied |= ARFaceEditorMemorySafetyErrorFixer.ApplyIfNeeded();
            if (Defines.isURPEnabled || Defines.isLWRPEnabled) {
                isAnyFixApplied |= ARBackgroundRendererFeatureFixer.ApplyFixIfNeeded();
            }

            isAnyFixApplied |= ARMeshManagerFixer.ApplyFixIfNeeded() | ARKitBlendShapeVisualizerFixer.ApplyIfNeeded();
            if (isAnyFixApplied) {
                AssetDatabase.Refresh();
            }

            log($"isAnyFixApplied: {isAnyFixApplied}");
            return isAnyFixApplied;
        }

        public static bool Undo() {
            var isAnyUndone = ARMeshManagerFixer.Undo() | ARKitBlendShapeVisualizerFixer.Undo() | ARCameraBackgroundFixer.Undo();
            if (isAnyUndone) {
                AssetDatabase.Refresh();
                return true;
            } else {
                return false;
            }            
        }
        
        [Conditional("_")]
        public static void log(string s) {
            Debug.Log(s);
        }
    }
}
