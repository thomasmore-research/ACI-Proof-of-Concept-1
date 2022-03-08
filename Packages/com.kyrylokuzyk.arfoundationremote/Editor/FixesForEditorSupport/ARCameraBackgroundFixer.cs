using System;
using System.IO;
using UnityEditor;
using UnityEngine.XR.ARFoundation;


namespace ARFoundationRemote.Editor {
    public static class ARCameraBackgroundFixer {
        public static bool ApplyFixIfNeeded() {
            var path = "Packages/com.unity.xr.arfoundation/Runtime/AR/ARCameraBackground.cs";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            var text = script.text;
            if (text.Contains("AR_FOUNDATION_EDITOR_REMOTE")) {
                FixesForEditorSupport.log("ARCameraBackgroundFixer already applied");
                return false;
            }

            FixesForEditorSupport.log("ARCameraBackgroundFixer");
            var index = text.IndexOf("commandBuffer.IssuePluginEvent(", StringComparison.Ordinal);
            var withFix = text.Insert(index, @"// AR_FOUNDATION_EDITOR_REMOTE: calling commandBuffer.IssuePluginEvent is crashing Unity Editor 2019.2 and freezing newer versions of Unity
            #if UNITY_EDITOR
            return;
            #endif
            // AR_FOUNDATION_EDITOR_REMOTE***
            ");
            File.WriteAllText(AssetDatabase.GetAssetPath(script), withFix);
            return true;
        }

        public static bool Undo() {
            return ARMeshManagerFixer.undoScript(nameof(ARCameraBackground));
        }
    }
}
