using System.IO;
using UnityEditor;


namespace ARFoundationRemote.Editor {
    public static class ARKitBlendShapeVisualizerFixer {
        public static bool ApplyIfNeeded() {
            bool anyFixed = false;
            foreach (var guid in AssetDatabase.FindAssets("ARKitBlendShapeVisualizer")) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) {
                    continue;
                }

                var result = script.text;
                if (!result.Contains("AR_FOUNDATION_EDITOR_REMOTE")) {
                    result = ARMeshManagerFixer.addUndefAndUsing(result, "using ARKitFaceSubsystem = ARFoundationRemote.Runtime.FaceSubsystem;");
                    File.WriteAllText(path, result);
                    anyFixed = true;
                }
            }

            return anyFixed;
        }

        internal static bool Undo() {
            return ARMeshManagerFixer.undoScript("ARKitBlendShapeVisualizer");
        }
    }
}
