using ARFoundationRemote.Runtime;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Editor {
    public sealed class PlaneSubsystem: XRPlaneSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.IsPluginEnabled()) {
                return;
            }

            //Debug.Log("RegisterDescriptor ARemotePlaneSubsystem");
            var thisType = typeof(PlaneSubsystem);
            XRPlaneSubsystemDescriptor.Create(new XRPlaneSubsystemDescriptor.Cinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(ARemotePlaneSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType,
                #endif
                supportsHorizontalPlaneDetection = true,
                supportsVerticalPlaneDetection = true,
                supportsBoundaryVertices = true,
                supportsClassification = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS,
                supportsArbitraryPlaneDetection = false
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new ARemotePlaneSubsystemProvider();
        #endif
        
        class ARemotePlaneSubsystemProvider: Provider {
            readonly TrackableChangesReceiver<BoundedPlaneSerializable, BoundedPlane> receiver = new TrackableChangesReceiver<BoundedPlaneSerializable, BoundedPlane>();

            
            public override void GetBoundary(TrackableId trackableId, Allocator allocator, ref NativeArray<Vector2> boundary) {
                var points = receiver.all[trackableId].boundary;
                var length = points.Length;
                CreateOrResizeNativeArrayIfNecessary(length, allocator, ref boundary);
                for (int i = 0; i < length; i++) {
                    boundary[i] = points[i].Value;
                }
            }

            public override TrackableChanges<BoundedPlane> GetChanges(BoundedPlane defaultPlane, Allocator allocator) {
                return receiver.GetChanges(allocator);
            }
            
            public override void Start() {
                Connection.Register<PlanesUpdateData>(planesData => receiver.Receive(planesData.added, planesData.updated, planesData.removed));
                setRemoteSubsystemEnabled(true);
            }

            public override void Stop() {
                setRemoteSubsystemEnabled(false);
                Connection.UnRegister<PlanesUpdateData>();
            }
            
            public override void Destroy() {
            }

            void setRemoteSubsystemEnabled(bool isEnabled) {
                Sender.logSceneSpecific("send " + GetType().Name + " " + isEnabled);
                Connection.Send(new PlaneDetectionDataEditor {
                    enablePlaneManager = isEnabled
                });
            }

            #if !ARFOUNDATION_4_0_OR_NEWER
                public override PlaneDetectionMode planeDetectionMode {
                    set => sendPlaneDetectionMode(value);
                }
            #else
                PlaneDetectionMode detectionMode = PlaneDetectionMode.None;
    
                public override PlaneDetectionMode requestedPlaneDetectionMode {
                    get => detectionMode;
                    set {
                        if (detectionMode != value) {
                            detectionMode = value;
                            sendPlaneDetectionMode(value);
                        }
                    }
                }

                public override PlaneDetectionMode currentPlaneDetectionMode => detectionMode;
            #endif
            
            static void sendPlaneDetectionMode(PlaneDetectionMode mode) {
                Connection.Send(new PlaneDetectionDataEditor {
                    horizontal = mode.HasFlag(PlaneDetectionMode.Horizontal),
                    vertical = mode.HasFlag(PlaneDetectionMode.Vertical)
                });
            }
        }
    }
}
