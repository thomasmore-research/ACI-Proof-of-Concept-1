#if UNITY_EDITOR
    using UnityEditor.PackageManager;
    using System.Linq;
#endif
using System;
using UnityEngine;


namespace ARFoundationRemote.Runtime {
    [Serializable]
    public struct PackageVersionData {
        #pragma warning disable 649
        [SerializeField] string displayName;
        [SerializeField] string version;
        [SerializeField] string id;
        #pragma warning restore 649


        #if UNITY_EDITOR
        public static PackageVersionData[] Create(PackageCollection packages) {
            return new[] {
                    "com.kyrylokuzyk.arfoundationremote",
                    "com.unity.xr.arfoundation",
                    "com.unity.xr.arcore",
                    "com.unity.xr.arkit",
                    "com.unity.xr.arkit-face-tracking",
                    "com.kyrylokuzyk.arfoundationremote.arcoreextensions",
                    "com.unity.inputsystem",
                    "com.unity.xr.arsubsystems"
                }.Select(dep => packages.SingleOrDefault(_ => _.name == dep))
                .Where(_ => _ != null)
                .Select(_ => new PackageVersionData {displayName = _.displayName, version = _.version, id = _.name})
                .ToArray();
        }
        #endif

        public static bool CheckVersions(PackageVersionData[] companionAppPackages, PackageVersionData[] editorPackages) {
            var result = true;
            foreach (var currentPackage in companionAppPackages) {
                var i = Array.FindIndex(editorPackages, _ => _.id == currentPackage.id);
                if (i != -1) {
                    var editorPackage = editorPackages[i];
                    if (currentPackage.version != editorPackage.version) {
                        Debug.LogError("Package version mismatch\n" +
                                       $"{currentPackage.displayName}\n" +
                                       $"AR Companion's version: {currentPackage.version}\n" +
                                       $"Editor version: {editorPackage.version}\n" +
                                       "Please make a new build of AR Companion app to fix the error.\n");
                        
                        result = false;
                    }
                }
            }

            return result;
        }

        public override string ToString() {
            return $"{displayName}: {version}";
        }
    }
}
