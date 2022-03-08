#if UNITY_EDITOR && ARKIT_INSTALLED && ARFOUNDATION_4_0_2_OR_NEWER
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine.Assertions;
using UnityEngine.XR.ARKit;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    public static class ARKitMeshingExtensions {
        public static readonly Dictionary<TrackableId, ARMeshClassification[]> faceClassifications = new Dictionary<TrackableId, ARMeshClassification[]>();
        
        public static NativeArray<ARMeshClassification> GetFaceClassifications(this IXRMeshSubsystem subsystem, TrackableId meshId, Allocator allocator) {
            if (faceClassifications.TryGetValue(meshId, out var result)) {
                return new NativeArray<ARMeshClassification>(result, allocator);
            } else {
                return new NativeArray<ARMeshClassification>();
            }
        }

        public static void SetClassificationEnabled(this IXRMeshSubsystem subsystem, bool enabled) {
            Connection.Send(new MeshingDataEditor {
                setClassificationEnabled = enabled
            });
        }

        [PublicAPI]
        public static bool GetClassificationEnabled(this IXRMeshSubsystem subsystem) {
            var classificationEnabled = Connection.BlockUntilReceive<MeshingDataPlayer>(new MeshingDataEditor {
                getClassificationEnabled = true
            }).classificationEnabled;
            Assert.IsTrue(classificationEnabled.HasValue);
            return classificationEnabled.Value;
        }
    }
}
#endif
