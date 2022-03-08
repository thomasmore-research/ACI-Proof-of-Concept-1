using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    public class PlaneManagerSender : MonoBehaviour {
        [SerializeField] ARPlaneManager manager = null;


        void Awake() {
            if (Application.isEditor) {
                Debug.LogError(GetType().Name + " is written for running on device, not in Editor");
            }

            manager.planesChanged += onPlanesChanged;
        }

        void OnEnable() {
            Connection.Register<PlaneDetectionDataEditor>(receive);
        }

        void OnDisable() {
            Connection.UnRegister<PlaneDetectionDataEditor>();
        }

        void OnDestroy() {
            manager.planesChanged -= onPlanesChanged;
        }

        void onPlanesChanged(ARPlanesChangedEventArgs args) {
            if (!Sender.isConnected) {
                return;
            }
            
            var payload = PlanesUpdateData.Create(args);
            //print("send planes\n" + payload);
            Connection.Send(payload);
        }

        void receive(PlaneDetectionDataEditor data) {
            if (data.horizontal.HasValue || data.vertical.HasValue) {
                var planeDetectionMode = data.ToPlaneDetectionMode();
                Sender.logSceneSpecific("receive planeDetectionMode " + planeDetectionMode);
                manager.SetRequestedDetectionMode(planeDetectionMode);
            }

            var enablePlaneManager = data.enablePlaneManager;
            if (enablePlaneManager.HasValue) {
                Sender.Instance.SetManagerEnabled(manager, enablePlaneManager.Value);
            }
        }
    }


    /// split <see cref="PlaneDetectionMode"/> to horizontal and vertical for compatibility with different versions of AR Foundation 
    [Serializable]
    public struct PlaneDetectionDataEditor {
        public bool? horizontal;
        public bool? vertical;
        public bool? enablePlaneManager;


        public PlaneDetectionMode ToPlaneDetectionMode() {
            var result = PlaneDetectionMode.None;
            if (horizontal == true) {
                result |= PlaneDetectionMode.Horizontal;
            }

            if (vertical == true) {
                result |= PlaneDetectionMode.Vertical;
            }

            return result;
        }
    }


    [Serializable]
    public class PlanesUpdateData {
        public BoundedPlaneSerializable[] added, updated, removed;


        public static PlanesUpdateData Create(ARPlanesChangedEventArgs args) {
            return new PlanesUpdateData {
                added = args.added.Select(BoundedPlaneSerializable.Create).ToArray(),
                updated = args.updated.Select(BoundedPlaneSerializable.Create).ToArray(),
                removed = args.removed.Select(BoundedPlaneSerializable.Create).ToArray()
            };
        }

        static Vector2Serializable[] getBoundaries(ARPlane p) {
            var boundary = p.boundary;
            return boundary.IsCreated ? boundary.Select(Vector2Serializable.Create).ToArray() : new Vector2Serializable[0];
        }

        public override string ToString() {
            string result = "";
            if (added.Any()) {
                result += "added:\n";
                foreach (var p in added) {
                    result += p.trackableId + "\n";
                }
            }

            if (updated.Any()) {
                result += "updated: " + updated.Length + "\n";                
            }
            
            if (removed.Any()) {
                result += "removed:\n";
                foreach (var p in removed) {
                    result += p.trackableId + "\n";
                }
            }
            
            return result;
        }
    }


    [Serializable]
    public class BoundedPlaneSerializable: ISerializableTrackable<BoundedPlane> {
        TrackableIdSerializable trackableIdSer;
        TrackableIdSerializable subsumedById;
        PoseSerializable poseSer;
        Vector2Serializable centerInPlaneSpace;
        Vector2Serializable size;
        PlaneAlignment alignment;
        TrackingState trackingState;
        PlaneClassification classification;
        public Vector2Serializable[] boundary { get; private set; }
        
        public TrackableId trackableId => trackableIdSer.Value;

        
        public static BoundedPlaneSerializable Create(ARPlane plane) {
            var subsumedBy = plane.subsumedBy;
            return new BoundedPlaneSerializable {
                trackableIdSer = TrackableIdSerializable.Create(plane.trackableId),
                subsumedById = TrackableIdSerializable.Create(subsumedBy != null ? subsumedBy.trackableId : TrackableId.invalidId),
                poseSer = PoseSerializable.Create(plane.transform.LocalPose()),
                centerInPlaneSpace = Vector2Serializable.Create(plane.centerInPlaneSpace),
                size = Vector2Serializable.Create(plane.size),
                alignment = plane.alignment,
                trackingState = plane.trackingState,
                classification = plane.classification,
                boundary = plane.boundary.Select(Vector2Serializable.Create).ToArray()
            };
        }

        public BoundedPlane Value => new BoundedPlane(trackableIdSer.Value, subsumedById.Value, poseSer.Value, centerInPlaneSpace.Value, size.Value, alignment, trackingState, IntPtr.Zero, classification);
    }


    [Serializable]
    public struct TrackableIdSerializable {
        ulong subId1;
        ulong subId2;


        public static TrackableIdSerializable Create(TrackableId id) {
            return new TrackableIdSerializable {
                subId1 = id.subId1,
                subId2 = id.subId2
            };
        }

        public TrackableId Value => new TrackableId(subId1, subId2);
    }


    public static class ARPlaneManagerExtensions {
        public static void SetRequestedDetectionMode(this ARPlaneManager manager, PlaneDetectionMode planeDetectionMode) {
            #if ARFOUNDATION_4_0_OR_NEWER
                manager.requestedDetectionMode = planeDetectionMode;
                return;
            #endif
            
            #pragma warning disable 0162
            #pragma warning disable 0618
                manager.detectionMode = planeDetectionMode;
            #pragma warning restore
        }
    }
}
