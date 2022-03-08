using System.Runtime.InteropServices;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
#if UNITY_2019_3_OR_NEWER
    using MeshId = UnityEngine.XR.MeshId;
#else
    using MeshId = UnityEngine.Experimental.XR.TrackableId;
#endif


namespace ARFoundationRemote.Runtime {
    public static class MeshingExtensions {
        public static TrackableId trackableId(this MeshId meshId) {
            return new MeshIdTrackableUnion {meshId = meshId}._trackableId;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct MeshIdTrackableUnion {
            // MeshId LayoutKind is not Sequential. This may cause problems
            [FieldOffset(0)] public MeshId meshId;
            [FieldOffset(0)] public readonly TrackableId _trackableId;
        }
    } 
}
