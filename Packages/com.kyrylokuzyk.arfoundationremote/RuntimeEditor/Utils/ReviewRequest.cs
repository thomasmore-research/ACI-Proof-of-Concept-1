#if UNITY_EDITOR
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Editor {
    [InitializeOnLoad]
    public static class ReviewRequest {
        static ReviewRequest() {
            log("ReviewRequest ctor");
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredEditMode) {
                    log("PlayModeStateChange.EnteredEditMode");
                    tryAskForReview();
                }
            };
        }

        static void tryAskForReview() {
            if (canAsk()) {
                ask();
                neverAskAgain();
            }
        }

        static void neverAskAgain() {
            EditorPrefs.SetBool(canAskKey, false);
            log("neverAskAgain");
        }

        static void ask() {
            var response = EditorUtility.DisplayDialogComplex("AR Foundation Editor Remote", 
                "Thank you for using the plugin! It already saved you from making " +
                         numOfUsagesBeforeAsk + " builds and it's approximately " + Mathf.RoundToInt(numOfUsagesBeforeAsk * 0.08f) + " hours of real time.\n\n" +
                         "Would you mind to leave an honest review on Asset store? It will help a lot!", "Sure, I'll leave a review!", "Never ask again", "");
            if (response == 0) {
                WaitForEditorUpdate.Wait(() => Application.OpenURL("https://assetstore.unity.com/packages/tools/utilities/ar-foundation-editor-remote-168773#reviews"));
            }
        }

        static bool canAsk() {
            if (!EditorPrefs.GetBool(canAskKey, true)) {
                log("can't ask anymore");
                return false;
            }

            if (usageCount < numOfUsagesBeforeAsk) {
                log("usageCount < numOfUsagesBeforeAsk");
                return false;
            }
            
            return true;
        }

        public static void RecordUsage() {
            if (!recordedUsageInThisSession) {
                recordedUsageInThisSession = true;
                usageCount++;
                log("usageCount " + usageCount);
            } else {
                log("recordedUsageInThisSession == true");
            }
        }

        const string usageCountKey = "ARFoundationRemote.numOfUsages";
        const string canAskKey = "ARFoundationRemote.canAskForReview";
        const int numOfUsagesBeforeAsk = 30;

        static bool recordedUsageInThisSession;
        
        static int usageCount {
            get => EditorPrefs.GetInt(usageCountKey, 0);
            set => EditorPrefs.SetInt(usageCountKey, value);
        }

        [Conditional("_")]
        static void log(string s) {
            Debug.Log(s);
        }

        /*public static void Reset() {
            EditorPrefs.DeleteKey(usageCountKey);
            EditorPrefs.DeleteKey(canAskKey);
        }*/
    }
}
#endif
