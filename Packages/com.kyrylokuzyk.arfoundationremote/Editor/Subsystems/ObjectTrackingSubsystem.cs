#if ARFOUNDATION_4_0_OR_NEWER
using System;
using System.Diagnostics;
using System.Linq;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;

 
namespace ARFoundationRemote.RuntimeEditor {
    [UsedImplicitly]
    public class ObjectTrackingSubsystem : XRObjectTrackingSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.IsPluginEnabled()) {
                return;
            }

            Register<
                #if UNITY_2020_2_OR_NEWER
                    ObjectTrackingSubsystemProvider, 
                #endif
                ObjectTrackingSubsystem>(nameof(ObjectTrackingSubsystem), new XRObjectTrackingSubsystemDescriptor.Capabilities());
        }
        
        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => providerSingleton ?? (providerSingleton = new ObjectTrackingSubsystemProvider());
        [CanBeNull] static ObjectTrackingSubsystemProvider providerSingleton;
        #endif

        
        [UsedImplicitly]
        class ObjectTrackingSubsystemProvider : Provider {
            readonly TrackableChangesReceiver<XRTrackedObjectSerializable, XRTrackedObject> receiver = new TrackableChangesReceiver<XRTrackedObjectSerializable, XRTrackedObject>();

            
            public ObjectTrackingSubsystemProvider() {
                if (!Defines.UNITY_2020_2_OR_NEWER) {
                    Connection.Register<ObjectTrackingData>(receive);
                }
            }
            
            public override TrackableChanges<XRTrackedObject> GetChanges(XRTrackedObject template, Allocator allocator) {
                return receiver.GetChanges(allocator);
            }

            [CanBeNull]
            public override XRReferenceObjectLibrary library {
                set {
                    if (value != null) {
                        if (ObjectTrackingLibraries.Instance.objectLibraries.SingleOrDefault(_ => _.guid == value.guid) == null) {
                            ObjectTrackingSubsystemSender.logMissingObjRefLibError();
                        }
                    }
                    
                    var guid = value != null ? value.guid : (Guid?) null;
                    log($"send XRReferenceObjectLibrary: {(guid != null ? guid.ToString() : "NULL")}");
                    Connection.Send(new ObjectTrackingDataEditor {
                        objectLibrary = new ObjectLibraryContainer {
                            guid = guid
                        }
                    });
                    base.library = value;
                }
            }

            #if UNITY_2020_2_OR_NEWER
            public override void Start() {
                Connection.Register<ObjectTrackingData>(receive);
            }

            public override void Stop() {
                Connection.UnRegister<ObjectTrackingData>();
            }
            #endif

            public override void Destroy() {
                if (!Defines.UNITY_2020_2_OR_NEWER) {
                    receiver.Reset();
                }
            }
        
            void receive(ObjectTrackingData objectTrackingData) {
                var changes = objectTrackingData.changes;
                log(changes.ToString());
                receiver.Receive(changes);    
            }
        }
        
        
        [Conditional("_")]
        static void log(string message) {
            Debug.Log(message);
        }
    }
}
#endif
