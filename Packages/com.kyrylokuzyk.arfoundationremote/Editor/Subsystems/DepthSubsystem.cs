using System.Linq;
using ARFoundationRemote.Runtime;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Editor {
    public class DepthSubsystem: XRDepthSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.IsPluginEnabled()) {
                return;
            }

            var thisType = typeof(DepthSubsystem);
            XRDepthSubsystemDescriptor.RegisterDescriptor(new XRDepthSubsystemDescriptor.Cinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(DepthSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    implementationType = thisType,
                #endif
                supportsFeaturePoints = true,
                supportsConfidence = false,
                supportsUniqueIds = true
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new DepthSubsystemProvider();
        #endif
    
        class DepthSubsystemProvider: Provider {
            readonly TrackableChangesReceiver<ARPointCloudSerializable, XRPointCloud> receiver = new TrackableChangesReceiver<ARPointCloudSerializable, XRPointCloud>();

            
            public override TrackableChanges<XRPointCloud> GetChanges(XRPointCloud defaultPointCloud, Allocator allocator) {
                return receiver.GetChanges(allocator);
            }

            public override XRPointCloudData GetPointCloudData(TrackableId trackableId, Allocator allocator) {
                if (receiver.all.TryGetValue(trackableId, out var cloud)) {
                    return new XRPointCloudData {
                        positions = new NativeArray<Vector3>(cloud.positions.Select(_ => _.Value).ToArray(), allocator),
                        identifiers = new NativeArray<ulong>(cloud.identifiers.ToArray(), allocator)
                    };
                } else {
                    return new XRPointCloudData();
                }
            }
            
            public override void Start() {
                Connection.Register<PointCloudData>(pointCloudData => receiver.Receive(pointCloudData.added, pointCloudData.updated, pointCloudData.removed));
                setRemoteSubsystemEnabled(true);
            }

            public override void Stop() {
                setRemoteSubsystemEnabled(false);
                Connection.UnRegister<PointCloudData>();
            }

            public override void Destroy() {
            }

            void setRemoteSubsystemEnabled(bool isEnabled) {
                Sender.logSceneSpecific("send " + GetType().Name + " " + isEnabled);
                Connection.Send(new PointCloudDataEditor {
                    enableDepthSubsystem = isEnabled
                });
            }
        }
    }    
}
