using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public static class TransformExtensions {
        public static Pose LocalPoseOrDefaultIfNull(this Transform t) {
            return t != null ? t.LocalPose() : default(Pose);
        }

        public static Pose LocalPose(this Transform t) {
            return new Pose(t.localPosition, t.localRotation);
        }

        public static Vector3 LocalPositionOrDefaultIfNull(this Transform t) {
            return t != null ? t.localPosition : default(Vector3);
        }
    }
}
