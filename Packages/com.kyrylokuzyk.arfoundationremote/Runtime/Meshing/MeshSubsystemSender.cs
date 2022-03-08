#if UNITY_2019_3_OR_NEWER
    using MeshId = UnityEngine.XR.MeshId;
#else
    using MeshGenerationResult = UnityEngine.Experimental.XR.MeshGenerationResult;
    using MeshId = UnityEngine.Experimental.XR.TrackableId;
    using XRMeshSubsystem = UnityEngine.Experimental.XR.XRMeshSubsystem;
    using MeshInfo = UnityEngine.Experimental.XR.MeshInfo;
    using MeshVertexAttributes = UnityEngine.Experimental.XR.MeshVertexAttributes;
    using MeshChangeState = UnityEngine.Experimental.XR.MeshChangeState;
    using MeshGenerationStatus = UnityEngine.Experimental.XR.MeshGenerationStatus;
    using System.Collections;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class MeshSubsystemSender : MonoBehaviour {
        [CanBeNull] XRMeshSubsystem realSubsystem;
        readonly TrackableChangesReceiverBase<MeshInfoSerializable, MeshInfo> receiver = new TrackableChangesReceiverBase<MeshInfoSerializable, MeshInfo>();
        readonly HashSet<Mesh> meshes = new HashSet<Mesh>();


        void OnEnable() {
            Connection.Register<MeshingDataEditor>(receive);
        }

        void OnDisable() {
            Connection.UnRegister<MeshingDataEditor>();
        }

        void receive(MeshingDataEditor meshingData) {
            if (realSubsystem == null) {
                realSubsystem = XRGeneralSettingsRemote.GetRealSubsystem();
                logInitialization($"realSubsystem != null: {realSubsystem != null}");
            }
            
            if (realSubsystem == null) {
                log("realSubsystem == null, skipping Editor message");
                if (meshingData.enableMeshing == true) {
                    logInitialization($"realSubsystem == null: {realSubsystem == null}");
                    Sender.AddRuntimeErrorOnce("- Meshing is not supported on this device");
                }
                
                return;
            }

            var density = meshingData.meshDensity;
            if (density.HasValue) {
                log($"realSubsystem.meshDensity = {density.Value};");
                realSubsystem.meshDensity = density.Value;
                return;
            }

            var boundingVolumeData = meshingData.boundingVolumeData;
            if (boundingVolumeData.HasValue) {
                var volumeData = boundingVolumeData.Value;
                var originPos = volumeData.origin.Value;
                var extents = volumeData.extents.Value;
                log($"SetBoundingVolume {originPos}, {extents}");
                realSubsystem.SetBoundingVolume(originPos, extents);
                return;
            }

            var enableMeshing = meshingData.enableMeshing;
            if (enableMeshing.HasValue) {
                if (enableMeshing.Value) {
                    log("realSubsystem.Start()");
                    realSubsystem.Start();
                } else {
                    log("realSubsystem.Stop()");
                    reset();
                    if (realSubsystem != null && realSubsystem.running) {
                        realSubsystem.Stop();
                    }
                    
                    realSubsystem = null;    
                }
                
                return;
            }

            var maybeGenerateMeshAsyncRequest = meshingData.generateMeshAsyncRequest;
            Assert.IsNotNull(realSubsystem);
            if (maybeGenerateMeshAsyncRequest.HasValue) {
                if (!realSubsystem.running) {
                    return;
                }
                
                var editorRequest = maybeGenerateMeshAsyncRequest.Value;
                log("GenerateMeshAsyncRequest " + editorRequest.meshId);
                var meshId = editorRequest.meshId.Value;

                var mesh = new Mesh();
                meshes.Add(mesh);
                var attributes = editorRequest.attributes;
                realSubsystem.GenerateMeshAsync(meshId, mesh, null, attributes, result => {
                    log("onMeshGenerationComplete " + result.MeshId);
                    if (mesh != null) {
                        Assert.IsNotNull(mesh, "IsNotNull(mesh)");

                        bool success = true;
                        if (result.Mesh == null) {
                            log("result.Mesh == null");
                            success = false;
                        } else if (meshId != result.MeshId) {
                            Debug.LogError("realSubsystem.GenerateMeshAsync meshId != result.MeshId");
                            success = false;
                        } else if (mesh != result.Mesh) {
                            Debug.LogError($"realSubsystem.GenerateMeshAsync mesh != result.Mesh, mesh != null: {mesh != null}, result.Mesh != null: {result.Mesh != null}");
                            success = false;
                        }

                        var subMeshIndex = 0;
                        var indices = mesh.GetIndices(subMeshIndex);
                        if (indices.All(_ => _ == 0)) {
                            // don't know why, but sometimes indices will have all zero values
                            // this causes mesh flickering
                            success = false;
                        }

                        if (success) {
                            #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED && ARFOUNDATION_4_0_2_OR_NEWER
                            // send mesh classifications before generateMeshAsyncResponse
                            var meshClassifications = tryGetFaceClassifications(meshId.trackableId());
                            if (meshClassifications != null) {
                                Connection.Send(new MeshClassificationData(MeshIdSerializable.Create(meshId), meshClassifications));    
                            }
                            #endif
                            
                            Connection.Send(new MeshingDataPlayer {
                                generateMeshAsyncResponse = new GenerateMeshAsyncResponse {
                                    meshId = MeshIdSerializable.Create(meshId),
                                    vertices = mesh.vertices.Select(Vector3Serializable.Create).ToArray(),
                                    indices = indices,
                                    normals = attributes.HasFlag(MeshVertexAttributes.Normals) ? mesh.normals.Select(Vector3Serializable.Create).ToArray() : null,
                                    tangents = attributes.HasFlag(MeshVertexAttributes.Tangents) ? mesh.tangents.Select(Vector4Serializable.Create).ToArray() : null,
                                    uvs = attributes.HasFlag(MeshVertexAttributes.UVs) ? mesh.uv.Select(Vector2Serializable.Create).ToArray() : null,
                                    colors = attributes.HasFlag(MeshVertexAttributes.Colors) ? mesh.colors.Select(ColorSerializable.Create).ToArray() : null,
                                    Status = result.Status
                                }
                            });
                        } else {
                            Connection.Send(new MeshingDataPlayer {
                                generateMeshAsyncResponse = new GenerateMeshAsyncResponse {
                                    meshId = MeshIdSerializable.Create(meshId),
                                    Status = MeshGenerationStatus.UnknownError
                                }
                            });
                        }
                    } else {
                        log($"mesh was destroyed {meshId}");
                    }

                    var removed = meshes.Remove(mesh);
                    // Assert.IsTrue(removed); // can be false if reset() was called before callback 
                    Destroy(mesh);
                });
                
                return;
            }

            #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED && ARFOUNDATION_4_0_2_OR_NEWER
            var setClassificationEnabled = meshingData.setClassificationEnabled;
            if (setClassificationEnabled.HasValue) {
                log("ARKitMeshSubsystemExtensions.SetClassificationEnabled " + setClassificationEnabled.Value);
                UnityEngine.XR.ARKit.ARKitMeshSubsystemExtensions.SetClassificationEnabled(realSubsystem, setClassificationEnabled.Value);
                Assert.AreEqual(true, isClassificationEnabled);
                return;
            }

            var getClassificationEnabled = meshingData.getClassificationEnabled;
            if (getClassificationEnabled.HasValue) {
                Connection.SendResponse(new MeshingDataPlayer {
                    classificationEnabled = isClassificationEnabled
                }, meshingData);
                
                return;
            }
            #endif

            throw new Exception(meshingData.AllFieldsAndPropsToString());
        }
        
        void reset() {
            foreach (var _ in meshes) {
                Assert.IsNotNull(_);
                Destroy(_);
            }
            
            meshes.Clear();
            receiver.Reset();
        }

        #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED && ARFOUNDATION_4_0_2_OR_NEWER
            [CanBeNull]
            UnityEngine.XR.ARKit.ARMeshClassification[] tryGetFaceClassifications(TrackableId meshId) {
                if (isClassificationEnabled) {
                    using (var result = UnityEngine.XR.ARKit.ARKitMeshSubsystemExtensions.GetFaceClassifications(realSubsystem, meshId, Unity.Collections.Allocator.Temp)) {
                        return result.ToArray();
                    }
                } else {
                    return null;
                }
            }
            
            bool isClassificationEnabled => UnityEngine.XR.ARKit.ARKitMeshSubsystemExtensions.GetClassificationEnabled(realSubsystem);
        #endif

   
        [Conditional("_")]
        static void logInitialization(string s) {
            Debug.Log("MeshSubsystem: " + s);
        }
        
        [Conditional("_")]
        public static void log(string s) {
            Debug.Log("MeshSubsystem: " + s);
        }
        
        [Conditional("_")]
        public static void errorOccursSometimes(string s) {
            Debug.LogError("Mesh sender error: " + s);
        }
        
        void Update() {
            var infos = new List<MeshInfo>();
            if (tryGetMeshInfos(infos)) {
                log("send meshInfos " + infos.Count);
                Connection.Send(new MeshingDataPlayer {
                    meshInfos = infos.Select(MeshInfoSerializable.Create).ToList()
                });
            }
        }

        /// <summary>
        /// todo no need to do this anymore?
        /// after realSubsystem.Stop(), previously added meshes will be reported as updated
        /// I use TrackableChangesReceiverBase to turn updated meshes back into added
        /// </summary>
        bool tryGetMeshInfos(List<MeshInfo> meshInfosOut) {
            meshInfosOut.Clear();
            var infos = new List<MeshInfo>();
            if (realSubsystem != null && realSubsystem.running && realSubsystem.TryGetMeshInfos(infos)) {
                var result = new List<MeshInfoSerializable>();
                foreach (var meshInfo in infos) {
                    var empty = new MeshInfoSerializable[0];
                    var meshInfoSerializable = MeshInfoSerializable.Create(meshInfo);
                    switch (meshInfo.ChangeState) {
                        case MeshChangeState.Added:
                            receiver.Receive(new[] {meshInfoSerializable}, empty, empty);
                            break;
                        case MeshChangeState.Updated:
                            receiver.Receive(empty, new[] {meshInfoSerializable}, empty);
                            break;
                        case MeshChangeState.Removed:
                            receiver.Receive(empty, empty, new[] {meshInfoSerializable});
                            break;
                        case MeshChangeState.Unchanged:
                            result.Add(meshInfoSerializable);
                            break;
                    }
                }
                
                foreach (var _ in receiver.updated.Values) {
                    Assert.AreEqual(MeshChangeState.Updated, _.ChangeState, "AreEqual(MeshChangeState.Updated, _.ChangeState)");
                }
                
                foreach (var _ in receiver.removed.Values) {
                    Assert.AreEqual(MeshChangeState.Removed, _.ChangeState, "AreEqual(MeshChangeState.Removed, _.ChangeState)");
                }
                
                // updated items will be placed into added dict after receiver.Reset() so we have to change their state to added
                result.AddRange(receiver.added.Values.Select(_ => _.WithState(MeshChangeState.Added)));
                result.AddRange(receiver.updated.Values);
                result.AddRange(receiver.removed.Values);
                receiver.OnAfterGetChanges();
                meshInfosOut.AddRange(result.Select(_ => _.Value));
                return true;
            } else {
                return false;
            }
        }
    }

    
    [Serializable]
    public class MeshingDataPlayer : BlockingMessage {
        [CanBeNull] public List<MeshInfoSerializable> meshInfos;
        public GenerateMeshAsyncResponse? generateMeshAsyncResponse;
        public bool? classificationEnabled;
    }


    [Serializable]
    public class MeshingDataEditor : BlockingMessage {
        public bool? enableMeshing;
        public GenerateMeshAsyncRequest? generateMeshAsyncRequest;
        public float? meshDensity;
        public SetBoundingVolumeCallData? boundingVolumeData;
        public bool? setClassificationEnabled;
        public bool? getClassificationEnabled; // bool value in not used
    }

    
    [Serializable]
    public struct SetBoundingVolumeCallData {
        public Vector3Serializable origin;
        public Vector3Serializable extents;
    }
    
    
    [Serializable]
    public struct GenerateMeshAsyncRequest {
        public MeshIdSerializable meshId;
        public MeshVertexAttributes attributes;
        
        public override string ToString() {
            return $"{meshId.Value}, {attributes}";
        }
    }


    [Serializable]
    public struct GenerateMeshAsyncResponse {
        public MeshIdSerializable meshId;
        public Vector3Serializable[] vertices;
        public int[] indices;
        [CanBeNull] public Vector3Serializable[] normals;
        [CanBeNull] public Vector4Serializable[] tangents;
        [CanBeNull] public Vector2Serializable[] uvs;
        [CanBeNull] public ColorSerializable[] colors;
        public MeshGenerationStatus Status;
    }


    #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED && ARFOUNDATION_4_0_2_OR_NEWER
    [Serializable]
    public class MeshClassificationData {
        public readonly MeshIdSerializable meshId;
        [NotNull] public readonly UnityEngine.XR.ARKit.ARMeshClassification[] faceClassifications;

        public MeshClassificationData(MeshIdSerializable meshId, [NotNull] UnityEngine.XR.ARKit.ARMeshClassification[] faceClassifications) {
            this.meshId = meshId;
            this.faceClassifications = faceClassifications;
        }
    }
    #endif


    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct MeshInfoSerializable : ISerializableTrackable<MeshInfo> {
        MeshIdSerializable MeshId;
        public MeshChangeState ChangeState;
        int PriorityHint;

        public static MeshInfoSerializable Create(MeshInfo info) => new Union {nonSerializable = info}.serializable;

        public TrackableId trackableId => MeshId.Value.trackableId();
        
        public MeshInfo Value => new Union {serializable = this}.nonSerializable;

        public MeshInfoSerializable WithState(MeshChangeState state) {
            return new MeshInfoSerializable {
                MeshId = MeshId,
                ChangeState = state,
                PriorityHint = PriorityHint
            };
        }

        [StructLayout(LayoutKind.Explicit)]
        struct Union {
            [FieldOffset(0)] public MeshInfoSerializable serializable;
            [FieldOffset(0)] public MeshInfo nonSerializable;
        }
    }
    
    

    [StructLayout(LayoutKind.Sequential)]
    public struct MeshGenerationResultWrapper {
        public MeshId MeshId;
        public Mesh Mesh;
        public MeshCollider MeshCollider;
        public MeshGenerationStatus Status;
        public MeshVertexAttributes Attributes;
        
        public MeshGenerationResult Value => new Union {serializable = this}.nonSerializable;
        
        [StructLayout(LayoutKind.Explicit)]
        struct Union {
            [FieldOffset(0)] public MeshGenerationResultWrapper serializable;
            [FieldOffset(0)] public readonly MeshGenerationResult nonSerializable;
        }
    }
    
    
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct MeshIdSerializable {
        ulong m_SubId1, m_SubId2;


        public static MeshIdSerializable Create(MeshId id) {
            return new union {nonSerializable = id}.serializable;
        }

        public MeshId Value => new union {serializable = this}.nonSerializable;
            
        [StructLayout(LayoutKind.Explicit)]
        struct union {
            // MeshId LayoutKind is not Sequential. This may cause problems
            [FieldOffset(0)] public MeshIdSerializable serializable;
            [FieldOffset(0)] public MeshId nonSerializable;
        }
    }


    public class GenerateMeshAsyncReceiverData {
        public Mesh mesh;
        [CanBeNull] public MeshCollider meshCollider;
        public Action<MeshGenerationResult> onMeshGenerationComplete;
        public MeshVertexAttributes attributes;
    }
}
