#if ARFOUNDATION_4_0_2_OR_NEWER
using System.Diagnostics;
using System.Linq;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.RuntimeEditor {
    public class HumanBodySubsystem : XRHumanBodySubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.IsPluginEnabled()) {
                return;
            }

            var type = typeof(HumanBodySubsystem);
            Register(new XRHumanBodySubsystemCinfo {
                id = nameof(HumanBodySubsystem),
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(HumanBodySubsystemProvider),
                    subsystemTypeOverride = type,
                #else
                    implementationType = type,
                #endif
                supportsHumanBody2D = true,
                supportsHumanBody3D = true,
                supportsHumanBody3DScaleEstimation = true
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new HumanBodySubsystemProvider();
        #endif

        class HumanBodySubsystemProvider : Provider {
            readonly TrackableChangesReceiver<ARHumanBodySerializable, XRHumanBody> receiver = new TrackableChangesReceiver<ARHumanBodySerializable, XRHumanBody>();
            [NotNull] XRHumanBodyPose2DJoint[] joints2d = new XRHumanBodyPose2DJoint[0];
            
            
            public override void Start() {
                Connection.Register<HumanBodyData>(receive);
                enableRemoteManager(true);
            }

            public override void Stop() {
                enableRemoteManager(false);
                Connection.UnRegister<HumanBodyData>();
            }

            public override void Destroy() {
            }

            void enableRemoteManager(bool enable) {
                log($"enableRemoteManager: {enable}");
                send(new HumanBodyDataEditor {
                    enableManager = enable
                });
            }

            bool _pose2DRequested;

            public override bool pose2DRequested {
                get => _pose2DRequested;
                set {
                    _pose2DRequested = value;
                    send(new HumanBodyDataEditor {
                        pose2DRequested = value           
                    });
                }
            }

            public override bool pose2DEnabled => _pose2DRequested;

            bool _pose3DRequested;

            public override bool pose3DRequested {
                get => _pose3DRequested;
                set {
                    log($"set pose3DRequested: {value}");
                    _pose3DRequested = value;
                    send(new HumanBodyDataEditor {
                        pose3DRequested = value
                    });
                }
            }

            public override bool pose3DEnabled => _pose3DRequested;

            bool _pose3DScaleEstimationRequested;

            public override bool pose3DScaleEstimationRequested {
                get => _pose3DScaleEstimationRequested;
                set {
                    _pose3DScaleEstimationRequested = value;
                    send(new HumanBodyDataEditor {
                        pose3DScaleEstimationRequested = value
                    });
                }
            }

            public override bool pose3DScaleEstimationEnabled => _pose3DScaleEstimationRequested;

            
            public override TrackableChanges<XRHumanBody> GetChanges(XRHumanBody defaultHumanBody, Allocator allocator) {
                return receiver.GetChanges(allocator);
            }

            public override void GetSkeleton(TrackableId trackableId, Allocator allocator, ref NativeArray<XRHumanBodyJoint> skeleton) {
                var joints = receiver.all[trackableId].joints;
                var numJoints = joints.Length;
                if (!skeleton.IsCreated || (skeleton.Length != numJoints)) {
                    if (skeleton.IsCreated) {
                        skeleton.Dispose();
                    }

                    skeleton = new NativeArray<XRHumanBodyJoint>(numJoints, allocator);
                }

                skeleton.CopyFrom(joints.Select(_ => _.Value).ToArray());
            }

            public override NativeArray<XRHumanBodyPose2DJoint> GetHumanBodyPose2DJoints(XRHumanBodyPose2DJoint defaultHumanBodyPose2DJoint, int screenWidth, int screenHeight, ScreenOrientation screenOrientation,
                Allocator allocator) {
                return new NativeArray<XRHumanBodyPose2DJoint>(joints2d, allocator);
            }

            static void send(HumanBodyDataEditor humanBodyData) {
                Connection.Send(humanBodyData);
            }
      
            void receive(HumanBodyData bodyData) {
                receiver.Receive(bodyData.bodies);

                var receivedJoints2D = bodyData.joints2d;
                if (receivedJoints2D != null) {
                    log($"receive 2ds {receivedJoints2D.Length}");
                    joints2d = receivedJoints2D.Select(_ => _.Value).ToArray();
                }
            }
        }

        
        [Conditional("_")]
        static void log(string s) {
            Debug.Log(s);
        }
    }
}
#endif
