#if !ARFOUNDATION_4_0_2_OR_NEWER
    using XRCpuImage = UnityEngine.XR.ARSubsystems.XRCameraImage;
#endif
#if ARFOUNDATION_4_0_OR_NEWER
    using System;
#endif
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    public class SetupARFoundationVersionSpecificComponents : MonoBehaviour {
        [SerializeField] ARSessionOrigin origin = null;
        [SerializeField] bool isUserFacing = false;
        [SerializeField] LightEstimationDataEditor lightEstimationSettings = LightEstimationDataEditor.FromLightEstimationMode(LightEstimationMode.AmbientIntensity);
        [SerializeField] bool autofocus = false;
        [SerializeField] bool enableCameraManager = true;
        [SerializeField] bool addCameraBackground = true;
        [SerializeField] [CanBeNull] Material customCameraMaterial = null;
        #pragma warning disable 414
        [SerializeField] ARSession arSession = null;
        [SerializeField] TrackingModeWrapper trackingMode = TrackingModeWrapper.DontCare;
        #pragma warning restore

        [CanBeNull] ARCameraManager _cameraManager = null;
        [CanBeNull] ARCameraBackground _cameraBackground = null;
        bool initialized;

        
        [NotNull]
        public ARCameraManager cameraManager {
            get {
                if (_cameraManager == null) {
                    init();
                }

                Assert.IsNotNull(_cameraManager);
                return _cameraManager;
            }
        }

        public ARCameraBackground cameraBackground {
            get {
                if (!initialized) {
                    init();
                }

                return _cameraBackground;
            }
        }

        void Awake() {
            Assert.AreEqual(1, FindObjectsOfType<SetupARFoundationVersionSpecificComponents>().Length);
            
            if (!initialized) {
                init();
            }
        }

        void init() {
            Assert.IsFalse(initialized);
            initialized = true;
            var cameraGameObject = origin.camera.gameObject;
            cameraGameObject.SetActive(false);
            var camManager = cameraGameObject.AddComponent<ARCameraManager>();
            camManager.enabled = enableCameraManager;
            _cameraManager = camManager;
            if (addCameraBackground) {
                var bg = cameraGameObject.AddComponent<ARCameraBackground>();
                _cameraBackground = bg;
                if (customCameraMaterial != null) {
                    bg.useCustomMaterial = true;
                    bg.customMaterial = customCameraMaterial;
                }
            }

            if (isUserFacing) {
                #if ARFOUNDATION_4_0_OR_NEWER
                    camManager.requestedFacingDirection = CameraFacingDirection.User;
                #endif
            }
            
            camManager.SetCameraAutoFocus(autofocus);
            camManager.SetLightEstimation(lightEstimationSettings);

            #if ARFOUNDATION_4_0_OR_NEWER
                TrackingMode toTrackingMode(TrackingModeWrapper mode) {
                    switch (mode) {
                        case TrackingModeWrapper.DontCare:
                            return TrackingMode.DontCare;
                        case TrackingModeWrapper.RotationOnly:
                            return TrackingMode.RotationOnly;
                        case TrackingModeWrapper.PositionAndRotation:
                            return TrackingMode.PositionAndRotation;
                        default:
                            throw new Exception();
                    }
                }
                
                if (trackingMode != TrackingModeWrapper.DontSetup) {
                    arSession.requestedTrackingMode = toTrackingMode(trackingMode);
                }
            #endif
            
            cameraGameObject.SetActive(true);
        }
    }

    public static class ARCameraManagerExtensions {
        public static void SetCameraAutoFocus(this ARCameraManager cameraManager, bool auto) {
            #if ARFOUNDATION_4_0_OR_NEWER
                cameraManager.autoFocusRequested = auto;
            #endif

            if (!Defines.isARFoundation4_0_OrNewer) {
                #pragma warning disable 618
                cameraManager.focusMode = auto ? UnityEngine.XR.ARSubsystems.CameraFocusMode.Auto : UnityEngine.XR.ARSubsystems.CameraFocusMode.Fixed;
                #pragma warning restore 618
            }
        }

        public static void DisableLightEstimation(this ARCameraManager cameraManager) {
            #if ARFOUNDATION_4_0_OR_NEWER
                cameraManager.requestedLightEstimation = LightEstimation.None;
            #endif

            if (!Defines.isARFoundation4_0_OrNewer) {
                #pragma warning disable 618
                cameraManager.lightEstimationMode = UnityEngine.XR.ARSubsystems.LightEstimationMode.Disabled;
                #pragma warning restore 618
            }
        }
        
        public static bool TryAcquireLatestCpuImageVersionAgnostic(this ARCameraManager cameraManager, out XRCpuImage cameraImage) {
            return cameraManager.
                #if ARFOUNDATION_4_0_2_OR_NEWER
                    TryAcquireLatestCpuImage
                #else 
                    TryGetLatestImage
                #endif
                    (out cameraImage);
        }

        public static void SetLightEstimation(this ARCameraManager cameraManager, LightEstimationDataEditor data) {
            #if ARFOUNDATION_4_0_OR_NEWER
            {
                var value = data.ToFeature().ToLightEstimation();
                Sender.logSceneSpecific($"receive requestedLightEstimation {value}");
                cameraManager.requestedLightEstimation = value;
            }
            #endif

            if (!Defines.isARFoundation4_0_OrNewer) {
                var value = data.ToLightEstimationMode();
                Sender.logSceneSpecific($"receive requestedLightEstimation {value}");
                #pragma warning disable 618
                cameraManager.lightEstimationMode = value;
                #pragma warning restore 618
            }
        }
    }

    public enum TrackingModeWrapper {
        DontCare,
        RotationOnly,
        PositionAndRotation,
        DontSetup
    }
}
