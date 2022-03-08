#if UNITY_EDITOR
using ARFoundationRemote.Runtime;
using UnityEditor;
using UnityEngine;


namespace ARFoundationRemote.RuntimeEditor {
    public static class InputRemotingUtils {
        static bool loggedOnce;
        
        
        public static void CheckGameViewIsFocused() {
            var debugSettings = Settings.Instance.debugSettings;
            if (!loggedOnce && getFocusedWindowTypeName() != "GameView") {
                Debug.LogError($"{Constants.packageName}: UI can respond to touch simulation and remoting only if Game View window is focused.");
                loggedOnce = true;
            }
        }
        
        static string getFocusedWindowTypeName() {
            var focusedWindow = EditorWindow.focusedWindow;
            return focusedWindow != null ? focusedWindow.GetType().Name : "";
        }
    }
}
#endif
