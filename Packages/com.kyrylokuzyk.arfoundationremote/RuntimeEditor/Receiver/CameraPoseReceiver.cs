#if UNITY_EDITOR
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using InputTracking = ARFoundationRemote.Runtime.InputTracking;


namespace ARFoundationRemote.RuntimeEditor {
    static class CameraPoseReceiver {
        [CanBeNull] static ARSessionOrigin _origin;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void afterSceneLoad() {
            if (Global.IsPluginEnabled()) {
                Connection.Register<CameraPoseData>(receive);
            }
        }
        
        [CanBeNull]
        static ARSessionOrigin getOrigin() {
            if (_origin == null) {
                _origin = Object.FindObjectOfType<ARSessionOrigin>();
                var drivers = Object.FindObjectsOfType<ARPoseDriver>();
                if (drivers.Length > 1) {
                    Debug.LogError($"{Constants.packageName}: only one ARPoseDriver is supported. More info: https://forum.unity.com/threads/ar-foundation-editor-remote-test-and-debug-your-ar-project-in-the-editor.898433/page-9#post-6740278");
                }
            }

            return _origin;
        }

        static void receive(CameraPoseData data) {
            var pose = new Pose(data.position.Value, data.rotation.Quaternion);
            setCenterEyeNodeState(pose);

            var origin = getOrigin();
            if (origin == null) {
                return;
            }
            
            var cam = origin.camera;
            if (cam != null) {
                if (hasEnabledPoseDriver(cam) && ARSession.state >= ARSessionState.SessionInitializing && !EditorApplication.isPaused) {
                    var arCameraTransform = cam.transform;
                    arCameraTransform.localPosition = pose.position;
                    arCameraTransform.localRotation = pose.rotation;
                }
            }
        }

        static bool hasEnabledPoseDriver(Camera cam) {
            #if LEGACY_INPUT_HELPERS_INSTALLED
                var legacyPoseDriver = cam.GetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
                if (legacyPoseDriver != null) {
                    return legacyPoseDriver.enabled;
                }
            #endif
            
            var poseDriver = cam.GetComponent<ARPoseDriver>();
            return poseDriver != null && poseDriver.enabled;
        }

        static void setCenterEyeNodeState(Pose pose) {
            var nodeState = new XRNodeState {
                position = pose.position,
                rotation = pose.rotation,
                nodeType = XRNode.CenterEye,
                tracked = true,
            };

            InputTracking.SetCenterEyeNodeState(nodeState);
        }
    }
}
#endif
