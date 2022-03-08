using ARFoundationRemote.RuntimeEditor;
using System.Collections.Generic;
using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;


namespace ARFoundationRemote.Editor {
    public class ARFoundationRemoteLoader: XRLoaderHelper {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void initOnLoad() {
            if (!Global.IsPluginEnabled() && Settings.Instance.logStartupErrors) {
                Debug.LogError("Please enable \"" + Constants.packageName + $"\" provider in Project Settings -> XR Plug-in Management -> PC, Mac & Linux Standalone. {Constants.silentStartupErrorsMessage}");
                if (Defines.isUnity2019_2) {
                    Debug.LogError("And enable the 'Initialize on Startup' setting");
                }
            }
        }
        
        public override bool Initialize() {
            Receiver.logDestruction("ARFoundationRemoteLoader.Initialize()");
            Connection.Send(EditorToPlayerMessageType.InitializeLoader);
            #if ARFOUNDATION_4_0_OR_NEWER
                CreateSubsystem<XRObjectTrackingSubsystemDescriptor, XRObjectTrackingSubsystem>(new List<XRObjectTrackingSubsystemDescriptor>(), nameof(ObjectTrackingSubsystem));
            #endif
            #if ARFOUNDATION_4_0_2_OR_NEWER
                CreateSubsystem<XRHumanBodySubsystemDescriptor, XRHumanBodySubsystem>(new List<XRHumanBodySubsystemDescriptor>(), nameof(HumanBodySubsystem));
                CreateSubsystem<XROcclusionSubsystemDescriptor, XROcclusionSubsystem>(new List<XROcclusionSubsystemDescriptor>(), nameof(OcclusionSubsystem));
            #endif
            CreateSubsystem<XRSessionSubsystemDescriptor, XRSessionSubsystem>(new List<XRSessionSubsystemDescriptor>(), nameof(SessionSubsystem));
            CreateSubsystem<XRPlaneSubsystemDescriptor, XRPlaneSubsystem>(new List<XRPlaneSubsystemDescriptor>(), nameof(PlaneSubsystem));
            CreateSubsystem<XRDepthSubsystemDescriptor, XRDepthSubsystem>(new List<XRDepthSubsystemDescriptor>(), nameof(DepthSubsystem));
            CreateSubsystem<XRFaceSubsystemDescriptor, XRFaceSubsystem>(new List<XRFaceSubsystemDescriptor>(), nameof(FaceSubsystem));
            CreateSubsystem<XRCameraSubsystemDescriptor, XRCameraSubsystem>(new List<XRCameraSubsystemDescriptor>(), nameof(CameraSubsystem));
            CreateSubsystem<XRImageTrackingSubsystemDescriptor, XRImageTrackingSubsystem>(new List<XRImageTrackingSubsystemDescriptor>(), nameof(ImageTrackingSubsystem));
            CreateSubsystem<XRRaycastSubsystemDescriptor, XRRaycastSubsystem>(new List<XRRaycastSubsystemDescriptor>(), nameof(RaycastSubsystem));
            CreateSubsystem<XRAnchorSubsystemDescriptor, XRAnchorSubsystem>(new List<XRAnchorSubsystemDescriptor>(), nameof(AnchorSubsystem));
            Global.IsInitialized = true;
            return true;
        }

        public override bool Deinitialize() {
            Receiver.logDestruction("ARFoundationRemoteLoader.Deinitialize()");
            Global.IsInitialized = false;
            DestroySubsystem<XRSessionSubsystem>();
            DestroySubsystem<XRPlaneSubsystem>();
            DestroySubsystem<XRDepthSubsystem>();
            DestroySubsystem<XRFaceSubsystem>();
            DestroySubsystem<XRCameraSubsystem>();
            DestroySubsystem<XRImageTrackingSubsystem>();
            DestroySubsystem<XRRaycastSubsystem>();
            DestroySubsystem<XRAnchorSubsystem>();
            #if ARFOUNDATION_4_0_OR_NEWER
                DestroySubsystem<XROcclusionSubsystem>();
                DestroySubsystem<XRHumanBodySubsystem>();
            #endif
            #if ARFOUNDATION_4_0_OR_NEWER
                DestroySubsystem<XRObjectTrackingSubsystem>();
            #endif

            Connection.receiverConnection.BlockUntilSent(EditorToPlayerMessageType.DeinitializeLoader);
            return true;
        }

        public override T GetLoadedSubsystem<T>() {
            if (typeof(T) == typeof(IXRMeshSubsystem)) {
                return XRGeneralSettingsRemote.GetMeshSubsystem() as T;
            } else {
                return base.GetLoadedSubsystem<T>();
            }
        }
    }
}
