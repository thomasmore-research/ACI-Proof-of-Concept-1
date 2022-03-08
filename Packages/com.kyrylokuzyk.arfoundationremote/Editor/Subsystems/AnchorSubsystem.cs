using ARFoundationRemote.Runtime;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Editor {
    public class AnchorSubsystem : XRAnchorSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.IsPluginEnabled()) {
                return;
            }
            
            var thisType = typeof(AnchorSubsystem);
            XRAnchorSubsystemDescriptor.Create(new XRAnchorSubsystemDescriptor.Cinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(AnchorSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType,
                #endif
                supportsTrackableAttachments = true
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new AnchorSubsystemProvider();
        #endif
     
        public override TrackableChanges<XRAnchor> GetChanges(Allocator allocator) {
            if (!running) {
                // silent exception when closing the scene
                return new TrackableChanges<XRAnchor>();
            }

            return base.GetChanges(allocator);
        }

        class AnchorSubsystemProvider : Provider {
            readonly TrackableChangesReceiver<ARAnchorSerializable, XRAnchor> receiver = new TrackableChangesReceiver<ARAnchorSerializable, XRAnchor>();


            public override TrackableChanges<XRAnchor> GetChanges(XRAnchor defaultAnchor, Allocator allocator) {
                return receiver.GetChanges(allocator);
            }

            public override bool TryAddAnchor(Pose pose, out XRAnchor anchor) {
                var response = sendBlocking(new AnchorDataEditor {
                    tryAddAnchorData = new TryAddAnchorData {
                        sessionRelativePose = PoseSerializable.Create(pose)
                    }
                });

                var responseAnchor = response.anchor;
                anchor = responseAnchor?.Value ?? XRAnchor.defaultValue;
                return responseAnchor.HasValue;
            }

            public override bool TryAttachAnchor(TrackableId trackableToAffix, Pose pose, out XRAnchor anchor) {
                var response = sendBlocking(new AnchorDataEditor {
                    tryAttachAnchorData = new TryAttachAnchorData {
                        trackableToAffix = TrackableIdSerializable.Create(trackableToAffix),
                        sessionRelativePose = PoseSerializable.Create(pose)
                    }
                });

                var responseAnchor = response.anchor;
                anchor = responseAnchor?.Value ?? XRAnchor.defaultValue;
                return responseAnchor.HasValue;
            }

            public override bool TryRemoveAnchor(TrackableId anchorId) {
                if (Defines.arSubsystems_4_1_0_preview_11_or_newer && Global.IsExitingPlayMode) {
                    return false;
                }
                
                AnchorSubsystemSender.log($"remote subsystem TryRemoveAnchor {anchorId}");
                var anchorDeletedSuccessfully = sendBlocking(new AnchorDataEditor {
                    tryRemoveAnchorData = new TryRemoveAnchorData {
                        anchorId = TrackableIdSerializable.Create(anchorId)
                    }
                }).anchorDeletedSuccessfully;
                Assert.IsTrue(anchorDeletedSuccessfully.HasValue);
                return anchorDeletedSuccessfully.Value;
            }

            static AnchorSubsystemMethodsResponse sendBlocking(AnchorDataEditor anchorSubsystemMethodsData) {
                return Connection.BlockUntilReceive<AnchorSubsystemMethodsResponse>(anchorSubsystemMethodsData);
            }
            
            public override void Start() {
                Connection.Register<TrackableChangesData<ARAnchorSerializable>>(data => receiver.Receive(data));
                enableRemoteManager(true);
            }

            public override void Stop() {
                enableRemoteManager(false);
                Connection.UnRegister<TrackableChangesData<ARAnchorSerializable>>();
            }
            
            void enableRemoteManager(bool enable) {
                Connection.Send(new AnchorDataEditor {
                    enableManager = enable
                });
            }

            public override void Destroy() {
            }
        }
    }
}
