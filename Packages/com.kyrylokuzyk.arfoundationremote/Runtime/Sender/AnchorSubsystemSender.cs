using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    /// todoo remove component from ARSessionOrigin object in companion app after transition to OnEnable()/OnDisable() callback register 
    public class AnchorSubsystemSender : MonoBehaviour {
        [SerializeField] ARAnchorManager manager = null;
        [SerializeField] ARSessionOrigin origin = null;


        void OnEnable() {
            manager.anchorsChanged += anchorsChanged;
            Connection.Register<AnchorDataEditor>(receive);
        }

        void OnDisable() {
            manager.anchorsChanged -= anchorsChanged;
            Connection.UnRegister<AnchorDataEditor>();
        }

        void anchorsChanged(ARAnchorsChangedEventArgs args) {
            LogChangedAnchors(args);
            Connection.Send(new TrackableChangesData<ARAnchorSerializable> {
                added = toSerializable(args.added),
                updated = toSerializable(args.updated),
                removed = toSerializable(args.removed)
            });
        }

        [Conditional("_")]
        public static void LogChangedAnchors(ARAnchorsChangedEventArgs args) {
            var added = args.added;
            if (added.Any()) {
                foreach (var anchor in added) {
                    print($"added {anchor.trackableId}");
                }
            }

            /*var updated = args.updated;
            if (updated.Any()) {
                foreach (var anchor in updated) {
                    print($"updated {anchor.trackableId}");
                }
            }*/

            var removed = args.removed;
            if (removed.Any()) {
                foreach (var anchor in removed) {
                    print($"removed {anchor.trackableId}");
                }
            }
        }

        ARAnchorSerializable[] toSerializable(List<ARAnchor> anchors) {
            return anchors.Select(_ => ARAnchorSerializable.Create(_, origin)).ToArray();
        }

        void receive(AnchorDataEditor anchorsData) {
            if (anchorsData.enableManager.HasValue) {
                Sender.Instance.SetManagerEnabled(manager, anchorsData.enableManager.Value);
                return;
            }

            void sendMethodResponse(XRAnchor? anchor, bool? anchorDeletedSuccessfully = null) {
                Connection.SendResponse(new AnchorSubsystemMethodsResponse {
                    anchor = anchor.HasValue ? ARAnchorSerializable.Create(anchor.Value) : (ARAnchorSerializable?) null,
                    anchorDeletedSuccessfully = anchorDeletedSuccessfully
                }, anchorsData);
            }

            var tryAddAnchorData = anchorsData.tryAddAnchorData;
            if (tryAddAnchorData.HasValue) {
                var success = manager.subsystem.TryAddAnchor(tryAddAnchorData.Value.sessionRelativePose.Value, out var anchor);
                sendMethodResponse(success ? anchor : (XRAnchor?) null);
                return;
            }

            var tryAttachAnchorData = anchorsData.tryAttachAnchorData;
            if (tryAttachAnchorData.HasValue) {
                var attachAnchorData = tryAttachAnchorData.Value;
                var success = manager.subsystem.TryAttachAnchor(attachAnchorData.trackableToAffix.Value, attachAnchorData.sessionRelativePose.Value, out var anchor);
                sendMethodResponse(success ? anchor : (XRAnchor?) null);
                return;
            }

            var tryRemoveAnchorData = anchorsData.tryRemoveAnchorData;
            if (tryRemoveAnchorData.HasValue) {
                sendMethodResponse(null, manager.subsystem.TryRemoveAnchor(tryRemoveAnchorData.Value.anchorId.Value));
                return;
            }

            throw new Exception();
        }

        [Conditional("_")]
        public static void log(string s) {
            Debug.Log(s);
        }

        [Conditional("_")]
        static void LogAllTrackables(ARAnchorManager m) {
            log("\nALL TRACKABLES ");
            foreach (var trackable in m.trackables) {
                print(trackable.trackableId);
            }
        }
    }
    
    [Serializable]
    public struct ARAnchorSerializable : ISerializableTrackable<XRAnchor> {
        TrackableIdSerializable trackableIdSer;
        PoseSerializable sessionRelativePose;
        TrackingState trackingState;
        Guid sessionId;
        IntPtr nativePtr;


        public static ARAnchorSerializable Create([NotNull] ARAnchor a, [NotNull] ARSessionOrigin origin) {
            var transform = a.transform;
            return new ARAnchorSerializable {
                trackableIdSer = TrackableIdSerializable.Create(a.trackableId),
                sessionRelativePose = PoseSerializable.Create(origin.trackablesParent.InverseTransformPose(new Pose(transform.position, transform.rotation))),
                trackingState = a.trackingState,
                sessionId = a.sessionId,
                nativePtr = a.nativePtr
            };
        }

        public static ARAnchorSerializable Create(XRAnchor a) {
            return new ARAnchorSerializable {
                trackableIdSer = TrackableIdSerializable.Create(a.trackableId),
                sessionRelativePose = PoseSerializable.Create(a.pose),
                trackingState = a.trackingState,
                sessionId = a.sessionId,
                nativePtr = a.nativePtr
            };
        }

        public TrackableId trackableId => trackableIdSer.Value;
        public XRAnchor Value => new XRAnchor(trackableId, sessionRelativePose.Value, trackingState, nativePtr, sessionId);
    }

    
    [Serializable]
    public class AnchorSubsystemMethodsResponse : BlockingMessage {
        public ARAnchorSerializable? anchor;
        public bool? anchorDeletedSuccessfully;
    }

    
    [Serializable]
    public class AnchorDataEditor : BlockingMessage {
        public TryAddAnchorData? tryAddAnchorData;
        public TryAttachAnchorData? tryAttachAnchorData;
        public TryRemoveAnchorData? tryRemoveAnchorData;
        public bool? enableManager;

        public override string ToString() {
            return $"tryAddAnchorData: {tryAddAnchorData?.ToString()}, tryAttachAnchorData: {tryAttachAnchorData.HasValue}, tryRemoveAnchorData: {tryRemoveAnchorData.HasValue}, enableManager: {enableManager}";
        }
    }

    [Serializable]
    public struct TryAddAnchorData {
        public PoseSerializable sessionRelativePose;

        public override string ToString() {
            return $"pose: {sessionRelativePose.Value.ToString("F6")}";
        }
    }

    [Serializable]
    public struct TryAttachAnchorData {
        public TrackableIdSerializable trackableToAffix;
        public PoseSerializable sessionRelativePose;
    }

    [Serializable]
    public struct TryRemoveAnchorData {
        public TrackableIdSerializable anchorId;
    }
}
