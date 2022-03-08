using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARFoundation;


public class SwitchBetweenPlaneAndFaceDetection : MonoBehaviour {
    #pragma warning disable 414
    [SerializeField] SetupARFoundationVersionSpecificComponents setupCameraManager = null;
    ARCameraManager ARCameraManager => setupCameraManager.cameraManager;
    [SerializeField] ARSession arSession = null;
    [SerializeField] ARPlaneManager planeManager = null;
    [SerializeField] ARFaceManager faceManager = null;
    [SerializeField] bool isPlaneTracking = true;
    #pragma warning restore 414

    
    void Awake() {
        updateTrackingMode();
    }

    void OnGUI() {
        if (GUI.Button(new Rect(0,0,200,200), $"Switch Plane/Face tracking\nCurrent: {(isPlaneTracking ? "Plane" : "Face")}")) {
            isPlaneTracking = !isPlaneTracking;
            updateTrackingMode();
        }
        
        if (GUI.Button(new Rect(200, 0, 200, 200), $"Pause/Resume AR Session\nCurrent: {(arSession.enabled ? "Running" : "Paused")}")) {
            if (arSession.enabled) {
                arSession.enabled = false;
                arSession.Reset();
            } else {
                arSession.enabled = true;
            }
        }
    }

    void updateTrackingMode() {
        if (isPlaneTracking) {
            planeManager.enabled = true;
            faceManager.enabled = false;
            #if ARFOUNDATION_4_0_OR_NEWER
                ARCameraManager.requestedFacingDirection = CameraFacingDirection.World;
                arSession.requestedTrackingMode = TrackingMode.PositionAndRotation;
            #endif
        } else {
            planeManager.enabled = false;
            faceManager.enabled = true;
            #if ARFOUNDATION_4_0_OR_NEWER
                arSession.requestedTrackingMode = TrackingMode.RotationOnly;
                ARCameraManager.requestedFacingDirection = CameraFacingDirection.User;
            #endif
        }
    }
}
