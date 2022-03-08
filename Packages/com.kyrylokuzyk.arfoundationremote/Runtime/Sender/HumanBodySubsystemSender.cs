#if ARFOUNDATION_4_0_2_OR_NEWER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class HumanBodySubsystemSender : MonoBehaviour {
        ARHumanBodyManager manager;
        
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void register() {
            if (Defines.isARCompanionDefine && !Application.isEditor) {
                SceneManager.sceneLoaded += delegate {
                    Assert.IsNull(FindObjectOfType<HumanBodySubsystemSender>());
                    var sender = Sender.Instance;
                    sender.ExecuteOnDisabledOrigin(() => {
                        var descriptors = new List<XRHumanBodySubsystemDescriptor>();
                        SubsystemManager.GetSubsystemDescriptors(descriptors);
                        if (descriptors.Any()) {
                            var _manager = sender.origin.gameObject.AddComponent<ARHumanBodyManager>();
                            _manager.pose2DRequested = false;
                            _manager.pose3DRequested = false;
                            _manager.pose3DScaleEstimationRequested = false;
                            _manager.enabled = false;

                            var go = new GameObject(nameof(HumanBodySubsystemSender));
                            go.SetActive(false);
                            var instance = go.AddComponent<HumanBodySubsystemSender>();
                            instance.manager = _manager;
                            go.SetActive(true);
                        }
                    });
                };
            }
        }

        void Awake() {
            manager.humanBodiesChanged += args => {
                var bodies = new TrackableChangesData<ARHumanBodySerializable> {
                    added = serialize(args.added),
                    updated = serialize(args.updated),
                    removed = serialize(args.removed)
                };
                
                log($"send {bodies.ToString()}");

                Connection.Send(new HumanBodyData {
                    bodies = bodies,
                });
            };   
        }

        void OnEnable() {
            Connection.Register<HumanBodyDataEditor>(receive);
        }

        void OnDisable() {
            Connection.UnRegister<HumanBodyDataEditor>();
        }

        void Update() {
            trySend2DJoints();
        }

        void trySend2DJoints() {
            if (Sender.isConnected && manager.enabled && manager.pose2DRequested) {
                using (var joints = manager.GetHumanBodyPose2DJoints(Allocator.Temp)) {
                    if (joints.IsCreated) {
                        log($"send 2ds {joints.Length}");
                        Connection.Send(new HumanBodyData {
                            joints2d = joints.Select(XRHumanBodyPose2DJointSerializable.Create).ToArray()
                        });
                    }
                }    
            }
        }

        ARHumanBodySerializable[] serialize(List<ARHumanBody> list) {
            return list.Select(ARHumanBodySerializable.Create).ToArray();
        }

        void receive(HumanBodyDataEditor humanBodyData) {
            log($"humanBodyData {humanBodyData.ToString()}");
            if (humanBodyData.enableManager.HasValue) {
                Sender.Instance.SetManagerEnabled(manager, humanBodyData.enableManager.Value);
            }

            if (humanBodyData.pose2DRequested.HasValue) {
                manager.pose2DRequested = humanBodyData.pose2DRequested.Value;
            }

            if (humanBodyData.pose3DRequested.HasValue) {
                manager.pose3DRequested = humanBodyData.pose3DRequested.Value;
            }

            if (humanBodyData.pose3DScaleEstimationRequested.HasValue) {
                manager.pose3DScaleEstimationRequested = humanBodyData.pose3DScaleEstimationRequested.Value;
            }
        }

        [Conditional("_")]
        static void log(string s) {
            Debug.Log(s);
        }
    }

    
    [Serializable]
    public struct HumanBodyData {
        public TrackableChangesData<ARHumanBodySerializable>? bodies;
        [CanBeNull] public XRHumanBodyPose2DJointSerializable[] joints2d;
    }


    [Serializable]
    public struct ARHumanBodySerializable : ISerializableTrackable<XRHumanBody> {
        static readonly Reflector<ARHumanBody> reflector = new Reflector<ARHumanBody>();
        
        XRHumanBodySerializable sessionRelativeData;
        public XRHumanBodyJointSerializable[] joints { get; private set; }
        
        public static ARHumanBodySerializable Create(ARHumanBody humanBody) {
            return new ARHumanBodySerializable {
                sessionRelativeData = XRHumanBodySerializable.Create(reflector.GetProperty<XRHumanBody>(humanBody, nameof(sessionRelativeData))),
                joints = humanBody.joints.Select(XRHumanBodyJointSerializable.Create).ToArray(),
            };
        }

        public TrackableId trackableId => sessionRelativeData.m_TrackableId.Value;
        public XRHumanBody Value => sessionRelativeData.Value;
    }


    [Serializable]
    public struct XRHumanBodyPose2DJointSerializable {
        public int index;
        public int parentIndex;
        public Vector3Serializable position;
        public bool tracked;

        public static XRHumanBodyPose2DJointSerializable Create(XRHumanBodyPose2DJoint _) {
            return new XRHumanBodyPose2DJointSerializable {
                index = _.index,
                parentIndex = _.parentIndex,
                position = Vector3Serializable.Create(_.position),
                tracked = _.tracked
            };
        }

        public XRHumanBodyPose2DJoint Value => new XRHumanBodyPose2DJoint(index, parentIndex, position.Value, tracked);
    }
    

    [Serializable]
    public struct XRHumanBodySerializable {
        public TrackableIdSerializable m_TrackableId { get; private set; }
        PoseSerializable m_Pose;
        float m_EstimatedHeightScaleFactor;
        TrackingState m_TrackingState;
        

        static readonly Reflector<XRHumanBody> reflector = new Reflector<XRHumanBody>();

        public static XRHumanBodySerializable Create(XRHumanBody _) {
            return new XRHumanBodySerializable {
                m_TrackableId = TrackableIdSerializable.Create(_.trackableId),
                m_Pose = PoseSerializable.Create(_.pose),
                m_EstimatedHeightScaleFactor = _.estimatedHeightScaleFactor,
                m_TrackingState = _.trackingState,
            };
        }

        public XRHumanBody Value {
            get {
                var result = reflector.GetResultBuilder();
                result.SetField(nameof(m_TrackableId), m_TrackableId.Value);
                result.SetField(nameof(m_Pose), m_Pose.Value);
                result.SetField(nameof(m_EstimatedHeightScaleFactor), m_EstimatedHeightScaleFactor);
                result.SetField(nameof(m_TrackingState), m_TrackingState);
                return result.Result;
            }
        }
    }

    
    [Serializable]
    public struct XRHumanBodyJointSerializable {
        public int index;
        public int parentIndex;
        public Vector3Serializable localScale;
        public PoseSerializable localPose;
        public Vector3Serializable anchorScale;
        public PoseSerializable anchorPose;
        public bool tracked;


        public static XRHumanBodyJointSerializable Create(XRHumanBodyJoint _) {
            return new XRHumanBodyJointSerializable {
                index = _.index,
                parentIndex = _.parentIndex,
                localScale = Vector3Serializable.Create(_.localScale),
                localPose = PoseSerializable.Create(_.localPose),
                anchorScale = Vector3Serializable.Create(_.anchorScale),
                anchorPose = PoseSerializable.Create(_.anchorPose),
                tracked = _.tracked
            };
        }
        
        public XRHumanBodyJoint Value => new XRHumanBodyJoint(index, parentIndex, localScale.Value, localPose.Value, anchorScale.Value, anchorPose.Value, tracked);
    }
    
    
    [Serializable]
    public struct HumanBodyDataEditor {
        public bool? enableManager;
        public bool? pose2DRequested;
        public bool? pose3DRequested;
        public bool? pose3DScaleEstimationRequested;

        public override string ToString() {
            return $"enableManager: {enableManager}, pose2DRequested: {pose2DRequested}, pose3DRequested: {pose3DRequested}, pose3DScaleEstimationRequested: {pose3DScaleEstimationRequested}";
        }
    }
}
#endif
