#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    /// todo face duplication if ARFaceManager is re-enabled quickly in AR Foundation Samples repo
    ///     still happening + 'unique face not found'
    /// todo came facing switch is not backward compatible
    public class FaceSubsystem: XRFaceSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.IsPluginEnabled()) {
                return;
            }

            var thisType = typeof(FaceSubsystem);
            XRFaceSubsystemDescriptor.Create(new FaceSubsystemParams {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(FaceSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType,
                #endif
                supportsFacePose = true,
                supportsEyeTracking = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS,
                supportsFaceMeshVerticesAndIndices = true,
                supportsFaceMeshUVs = true,
                supportsFaceMeshNormals = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new FaceSubsystemProvider();
        #endif
        
        #if (UNITY_IOS || UNITY_EDITOR) && ARFOUNDATION_REMOTE_ENABLE_IOS_BLENDSHAPES
        public NativeArray<UnityEngine.XR.ARKit.ARKitBlendShapeCoefficient> GetBlendShapeCoefficients(TrackableId trackableId, Allocator allocator) {
            var blendShapeCoefficients = FaceSubsystemProvider.startedInstance?.receiver.all[trackableId].blendShapeCoefficients;
            if (blendShapeCoefficients != null) {
                var result = blendShapeCoefficients.Select(_ => _.Value).ToArray();
                return new NativeArray<UnityEngine.XR.ARKit.ARKitBlendShapeCoefficient>(result, allocator);                
            } else {
                return new NativeArray<UnityEngine.XR.ARKit.ARKitBlendShapeCoefficient>();
            }
        }
        #endif
 
        class FaceSubsystemProvider: Provider {
            [CanBeNull] internal static FaceSubsystemProvider startedInstance { get; private set; }
            internal readonly TrackableChangesReceiver<ARFaceSerializable, XRFace> receiver = new TrackableChangesReceiver<ARFaceSerializable, XRFace>();
            readonly Dictionary<TrackableId, FaceUniqueData> uniqueFacesData = new Dictionary<TrackableId, FaceUniqueData>();


            void receive([NotNull] FaceSubsystemData data) {
                var uniqueData = data.uniqueData;
                if (uniqueData != null) {
                    foreach (var _ in uniqueData) {
                        uniqueFacesData[_.trackableId.Value] = _; // use [] instead of Add() to support scene change
                    }
                }

                receiver.Receive(data.added, data.updated, data.removed);
                if (data.needLogFaces) {
                    FaceSubsystemSender.log("receive faces\n" + data);
                }
            }

            public override void GetFaceMesh(TrackableId faceId, Allocator allocator, ref XRFaceMesh faceMesh) {
                if (!receiver.all.TryGetValue(faceId, out var face)) {
                    throw new Exception();
                }
            
                if (!uniqueFacesData.TryGetValue(faceId, out var uniqueData)) {
                    throw new Exception($"unique face not found {faceId}");
                }
            
                var indices = uniqueData.indices;
                var vertices = face.vertices.Select(_ => _.Value).ToArray();
                if (!vertices.Any()) {
                    faceMesh = default;
                    return;
                }
            
                var vertexCount = vertices.Length;
                var indicesCount = indices.Length;
                var normals = face.normals.Select(_ => _.Value).ToArray();
                var hasNormals = normals.Any();
                var attrs = XRFaceMesh.Attributes.None;
                var uvs = uniqueData.uvs.Select(_ => _.Value).ToArray();
                var hasUVs = uvs.Any();
                if (hasUVs) {
                    attrs |= XRFaceMesh.Attributes.UVs;
                }
            
                if (hasNormals) {
                    attrs |= XRFaceMesh.Attributes.Normals;
                }
            
                faceMesh.Resize(vertexCount, indicesCount / 3, attrs, allocator);
            
                var verticesNativeArray = faceMesh.vertices;
                Assert.AreEqual(verticesNativeArray.Length, vertexCount);
                verticesNativeArray.CopyFrom(vertices);

                var indicesNativeArray = faceMesh.indices;
                Assert.AreEqual(indicesNativeArray.Length, indicesCount);
                indicesNativeArray.CopyFrom(indices);

                if (hasUVs) {
                    var uvsNativeArray = faceMesh.uvs;
                    Assert.AreEqual(uvsNativeArray.Length, uvs.Length);
                    uvsNativeArray.CopyFrom(uvs);
                }

                if (hasNormals) {
                    var normalsNativeArray = faceMesh.normals;
                    Assert.AreEqual(normalsNativeArray.Length, normals.Length);
                    normalsNativeArray.CopyFrom(normals);                
                }
            }

            public override TrackableChanges<XRFace> GetChanges(XRFace defaultFace, Allocator allocator) {
                return receiver.GetChanges(allocator);
            }

            public override int supportedFaceCount => Int32.MaxValue;
            #if ARFOUNDATION_4_0_OR_NEWER
                public override int currentMaximumFaceCount => Int32.MaxValue;
                public override int requestedMaximumFaceCount { get => Int32.MaxValue; set {} }
            #endif

            public override void Start() {
                Assert.IsNull(startedInstance);
                startedInstance = this;
                Connection.Register<FaceSubsystemData>(receive);
                setRemoteFaceSubsystemEnabled(true);
            }

            public override void Stop() {
                startedInstance = null;
                setRemoteFaceSubsystemEnabled(false);
                Connection.UnRegister<FaceSubsystemData>();
            }
            
            public override void Destroy() {
            }

            void setRemoteFaceSubsystemEnabled(bool isEnabled) {
                Sender.logSceneSpecific("send " + GetType().Name + " " + isEnabled);
                Connection.Send(new FaceSubsystemDataEditor {
                    enableFaceSubsystem = isEnabled
                });
            }
        }
    }
}
#endif
