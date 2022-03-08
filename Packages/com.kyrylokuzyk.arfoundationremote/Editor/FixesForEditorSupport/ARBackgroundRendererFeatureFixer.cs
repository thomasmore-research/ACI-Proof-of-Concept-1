using System.IO;
using UnityEditor;


namespace ARFoundationRemote.Editor {
    public static class ARBackgroundRendererFeatureFixer {
        public static bool ApplyFixIfNeeded() {
            var path = "Packages/com.unity.xr.arfoundation/Runtime/AR/ARBackgroundRendererFeature.cs";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            var text = script.text;
            var fix = @"#undef UNITY_EDITOR
";
            if (text.Contains(fix)) {
                // Debug.Log("fix already applied");
                return false;
            }

            var withFix = text.Insert(0, fix);
            File.WriteAllText(AssetDatabase.GetAssetPath(script), withFix);
            return true;
        }
    }
}
