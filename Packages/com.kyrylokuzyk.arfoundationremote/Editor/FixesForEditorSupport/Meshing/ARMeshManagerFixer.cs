using System;
using System.IO;
using System.Linq;
using ARFoundationRemote.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;


namespace ARFoundationRemote.Editor {
    public static class ARMeshManagerFixer {
        static string meshingDependency = @"
        ""ARFoundationRemote.Meshing"",";
        
        public static bool ApplyFixIfNeeded() {
            return addMeshingDependency() | applyUsingFixes($"Packages/com.unity.xr.arfoundation/Runtime/AR/{nameof(ARMeshManager)}.cs") | fixScript("MeshClassificationFracking") | fixScript("ToggleMeshClassification") | applyCheckAvailableFeaturesFix();
        }

        static bool addMeshingDependency() {
            var path = "Packages/com.unity.xr.arfoundation/Runtime/Unity.XR.ARFoundation.asmdef";
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(path).text;

            if (text.Contains(meshingDependency)) {
                return false;
            }

            text = text.Insert(text.IndexOf("[", text.IndexOf("references", StringComparison.Ordinal), StringComparison.Ordinal) + 1, meshingDependency);

            File.WriteAllText(path, text);
            return true;
        }

        static bool removeMeshingDependency() {
            var path = "Packages/com.unity.xr.arfoundation/Runtime/Unity.XR.ARFoundation.asmdef";
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(path).text;
            if (text.Contains(meshingDependency)) {
                text = text.Replace(meshingDependency, "");
                File.WriteAllText(path, text);
                return true;
            } else {
                return false;
            }
        }

        static bool applyCheckAvailableFeaturesFix() {
            var path = AssetDatabase.FindAssets("CheckAvailableFeatures")
                .Select(AssetDatabase.GUIDToAssetPath)
                .SingleOrDefault();
            if (path != null) {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                var text = script.text;
                if (text.Contains("AR_FOUNDATION_EDITOR_REMOTE")) {
                    return false;
                }
                
                var i = text.IndexOf("if(activeLoader && activeLoader.GetLoadedSubsystem", StringComparison.Ordinal);
                if (i != -1) {
                    text = text.Insert(i, @"// AR_FOUNDATION_EDITOR_REMOTE: fix for Editor applied
            // ");
                    File.WriteAllText(path, text);
                    return true;
                } else {
                    return false;
                }
            } else {
                return false;
            }
        }

        internal static bool Undo() {
            return undoScript("MeshClassificationFracking") | undoScript("ToggleMeshClassification") | undoScript(nameof(ARMeshManager)) | removeMeshingDependency();
        }

        public static bool undoScript(string scriptName) {
            var result = false;
            foreach (var guid in AssetDatabase.FindAssets(scriptName)) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("com.kyrylokuzyk.arfoundationremote")) {
                    continue;
                }
                
                FixesForEditorSupport.log(path);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) {
                    continue;
                }

                var text = script.text;
                if (undo(ref text)) {
                    result = true;
                    File.WriteAllText(path, text);
                }
            }

            return result;
        }

        static bool undo(ref string result) {
            var applied = false;
            while (true) {
                var startString = "// AR_FOUNDATION_EDITOR_REMOTE";
                var start = result.IndexOf(startString, StringComparison.Ordinal);
                if (start == -1) {
                    break;
                }

                var endString = "// AR_FOUNDATION_EDITOR_REMOTE***";
                var end = result.IndexOf(endString, start + startString.Length, StringComparison.Ordinal);
                Assert.AreNotEqual(-1, end, result);
                var endIndex = end + endString.Length + 2;
                FixesForEditorSupport.log($"start: {start}, end: {end}, endIndex: {endIndex}");

                var substringWithFix = result.Substring(start, endIndex - start);
                var commentStart = substringWithFix.IndexOf("/*", StringComparison.Ordinal);
                if (commentStart != -1) {
                    var commentEnd = substringWithFix.IndexOf("*/", commentStart, StringComparison.Ordinal);
                    Assert.AreNotEqual(-1, commentEnd);
                    result = result.Remove(start + commentEnd, endIndex - (start + commentEnd));
                    result = result.Remove(start, commentStart + 2);
                } else {
                    result = result.Remove(start, endIndex - start);
                }
                
                applied = true;
            }

            return applied;
        }
        
        static bool fixScript(string scriptName) {
            bool anyFixed = false;
            foreach (var guid in AssetDatabase.FindAssets(scriptName)) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) {
                    continue;
                }

                var text = script.text;
                if (!text.Contains("AR_FOUNDATION_EDITOR_REMOTE")) {
                    File.WriteAllText(path, addUndefAndUsing(text));
                    anyFixed = true;
                }
            }

            return anyFixed;
        }

        public static string addUndefAndUsing(string text, string usingDirective = "using XRMeshSubsystem = IXRMeshSubsystem;") {
            var withUndef = text.Insert(0, @"// AR_FOUNDATION_EDITOR_REMOTE: fix for Editor applied
#if UNITY_EDITOR
    #define IS_EDITOR
#endif
#undef UNITY_EDITOR
using ARFoundationRemote.Runtime;
// AR_FOUNDATION_EDITOR_REMOTE***
");
            
            var withUsingDirective = withUndef.Insert(withUndef.IndexOf("{", StringComparison.Ordinal) + 1, $@"
    // AR_FOUNDATION_EDITOR_REMOTE: fix for Editor applied
    #if IS_EDITOR
    {usingDirective}
    #endif
    // AR_FOUNDATION_EDITOR_REMOTE***
");
            return withUsingDirective;
        }

        static bool applyUsingFixes(string path) {
            if (IsARMeshManagerFixApplied()) {
                FixesForEditorSupport.log($"{nameof(ARMeshManagerFixer)} {nameof(applyUsingFixes)} already applied");
                return false;
            }

            FixesForEditorSupport.log($"{nameof(ARMeshManagerFixer)} {nameof(applyUsingFixes)}");
            var text = AssetDatabase.LoadAssetAtPath<MonoScript>(path).text;
            if (text.Contains("AR_FOUNDATION_EDITOR_REMOTE")) {
                throw new Exception($"{Constants.packageName}: Unity Package Manager cache bug. The ARMeshManager fix is applied in source file, but the compiled version doesn't contain the modification. Please restart Unity.");
            }

            var i = text.IndexOf("{", StringComparison.Ordinal);
            var withFix = text.Insert(i + 1, @"
    // AR_FOUNDATION_EDITOR_REMOTE: fix for Editor applied
    #if UNITY_EDITOR
    using XRMeshSubsystem = ARFoundationRemote.Runtime.IXRMeshSubsystem;
    using SubsystemManager = ARFoundationRemote.Runtime.SubsystemManagerRemote;
    using XRMeshSubsystemDescriptor = ARFoundationRemote.Runtime.XRMeshSubsystemDescriptorRemote;
    #endif
    // AR_FOUNDATION_EDITOR_REMOTE***
    ");
            File.WriteAllText(path, withFix);
            return true;
        }
        
        public static bool IsARMeshManagerFixApplied() {
            var subsystemProperty = typeof(ARMeshManager).GetProperty("subsystem");
            Assert.IsNotNull(subsystemProperty);
            return subsystemProperty.PropertyType == typeof(IXRMeshSubsystem);
        }
    }
}
