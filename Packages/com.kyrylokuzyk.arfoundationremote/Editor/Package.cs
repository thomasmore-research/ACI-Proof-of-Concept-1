#if XR_MANAGEMENT_3_2_10_OR_NEWER
using System.Collections.Generic;
using ARFoundationRemote.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEditor.XR.Management.Metadata;


namespace ARFoundationRemote.Editor {
    class Package: IXRPackage {
        class LoaderMetadata: IXRLoaderMetadata {
            public string loaderName { get; set; }
            public string loaderType { get; set; }
            public List<BuildTargetGroup> supportedBuildTargets { get; set; }
        }

        class PackageMetadata: IXRPackageMetadata {
            public string packageName { get; set; }
            public string packageId { get; set; }
            public string settingsType { get; set; }
            public List<IXRLoaderMetadata> loaderMetadata { get; set; }
        }

        static readonly IXRPackageMetadata meta = new PackageMetadata {
            packageName = Constants.packageName,
            packageId = "com.kyrylokuzyk.arfoundationremote",
            settingsType = typeof(ARFoundationRemoteLoaderSettings).FullName,
            loaderMetadata = new List<IXRLoaderMetadata> {
                new LoaderMetadata {
                    loaderName = Constants.packageName,
                    loaderType = typeof(ARFoundationRemoteLoader).FullName,
                    supportedBuildTargets = new List<BuildTargetGroup> {
                        BuildTargetGroup.Standalone 
                    }
                }
            }
        };

        public IXRPackageMetadata metadata => meta;

        public bool PopulateNewSettingsInstance(ScriptableObject obj) {
            return true;
        }
    }
}
#endif
