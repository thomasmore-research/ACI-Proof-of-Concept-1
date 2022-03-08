using System.IO;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using Object = UnityEngine.Object;


namespace ARFoundationRemote.Editor {
    public static class ARFaceEditorMemorySafetyErrorFixer {
        public static bool ApplyIfNeeded() {
            if (!IsFixApplied()) {
                applyFix();
                FixesForEditorSupport.log("ARFaceEditorMemorySafetyErrorFixer apply");
                return true;
            } else {
                FixesForEditorSupport.log("ARFaceEditorMemorySafetyErrorFixer already applied");
                return false;
            }
        }

        /// <summary>
        /// Calling <see cref="ARFace.GetUndisposable{T}"/> in Editor breaks read/write access to NativeArray because of memory safety checks.
        /// To fix this issue, we need to add these three lines in the beginning of <see cref="ARFace.GetUndisposable{T}"/>:
        /// #if UNITY_EDITOR
        /// return disposable;
        /// #endif
        /// </summary>
        static bool IsFixApplied() {
            var method = typeof(ARFace).GetMethod("GetUndisposable", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);
            var genericMethod = method.MakeGenericMethod(typeof(int));

            var arFace = new GameObject().AddComponent<ARFace>();
            var nativeArray = new NativeArray<int>(0, Allocator.Temp);
            var undisposableArray = (NativeArray<int>) genericMethod.Invoke(arFace, new object[] {nativeArray});
            try {
                var handle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(undisposableArray);
                AtomicSafetyHandle.CheckReadAndThrow(handle);
                return true;
            } catch {
                return false;
            } finally {
                Object.DestroyImmediate(arFace.gameObject);
                nativeArray.Dispose();
            }
        }

        static void applyFix() {
            var path = "Packages/com.unity.xr.arfoundation/Runtime/AR/ARFace.cs";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            var text = script.text;
            var i = text.IndexOf("GetUndisposable<T>");
            var j = text.IndexOf('{', i);
            var withFix = text.Insert(j + 1,
                @"
            #if UNITY_EDITOR
                return disposable;
            #endif

            #pragma warning disable 162");

            withFix = withFix.Insert(withFix.IndexOf('}', j), @"    #pragma warning restore 162
        ");

            File.WriteAllText(AssetDatabase.GetAssetPath(script), withFix);
        }
    }
}
