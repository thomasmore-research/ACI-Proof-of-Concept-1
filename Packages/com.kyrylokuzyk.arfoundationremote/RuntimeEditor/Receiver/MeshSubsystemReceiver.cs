#if UNITY_EDITOR
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
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR;


namespace ARFoundationRemote.RuntimeEditor {
    public class MeshSubsystemReceiver : MonoBehaviour, IXRMeshSubsystem {
        readonly Dictionary<MeshId, GenerateMeshAsyncReceiverData> meshGenerationRequests = new Dictionary<MeshId, GenerateMeshAsyncReceiverData>();
        readonly List<MeshInfo> meshInfos = new List<MeshInfo>();
        
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void initOnLoad() {
            var instance = Utils.TryCreatePersistentEditorObject<MeshSubsystemReceiver>();
            if (instance != null) {
                XRMeshSubsystemRemote.SetDelegate(instance);
            }
        }

        void ISubsystem.Start() {
            Connection.Register<MeshingDataPlayer>(receive);
            #if UNITY_EDITOR && ARKIT_INSTALLED && ARFOUNDATION_4_0_2_OR_NEWER
            Connection.Register<MeshClassificationData>(data => {
                ARKitMeshingExtensions.faceClassifications[data.meshId.Value.trackableId()] = data.faceClassifications;
            });
            #endif
            enableRemoteManager(true);
            running = true;
        }

        void ISubsystem.Stop() {
            Connection.UnRegister<MeshingDataPlayer>();
            #if UNITY_EDITOR && ARKIT_INSTALLED && ARFOUNDATION_4_0_2_OR_NEWER
            Connection.UnRegister<MeshClassificationData>();
            ARKitMeshingExtensions.faceClassifications.Clear();
            #endif
            meshGenerationRequests.Clear();
            enableRemoteManager(false);
            running = false;
        }

        static void enableRemoteManager(bool enableArMeshManager) {
            MeshSubsystemSender.log("send ARMeshManager enabled " + enableArMeshManager);
            Connection.Send(new MeshingDataEditor {
                enableMeshing = enableArMeshManager
            });
        }

        void ISubsystem.Destroy() {
        }

        public bool running { get; private set; }

        bool IXRMeshSubsystem.TryGetMeshInfos(List<MeshInfo> meshInfosOut) {
            meshInfosOut.Clear();
            if (meshInfos.Any()) {
                meshInfosOut.AddRange(meshInfos);
                meshInfos.Clear();
                return true;
            } else {
                return false;
            }
        }

        void IXRMeshSubsystem.GenerateMeshAsync(MeshId meshId, Mesh mesh, MeshCollider meshCollider, MeshVertexAttributes attributes,
            Action<MeshGenerationResult> onMeshGenerationComplete) {
            meshGenerationRequests.Add(meshId, new GenerateMeshAsyncReceiverData {
                mesh = mesh,
                meshCollider = meshCollider,
                onMeshGenerationComplete = onMeshGenerationComplete,
                attributes = attributes
            });

            MeshSubsystemSender.log("Receiver generateMeshAsyncRequest " + meshId);
            Connection.Send(new MeshingDataEditor {
                generateMeshAsyncRequest = new GenerateMeshAsyncRequest {
                    meshId = MeshIdSerializable.Create(meshId),
                    attributes = attributes
                }
            });
        }
        
        bool IXRMeshSubsystem.SetBoundingVolume(Vector3 origin, Vector3 extents) {
            MeshSubsystemSender.log($"send SetBoundingVolume {origin}, {extents}");
            Connection.Send(new MeshingDataEditor {
                boundingVolumeData = new SetBoundingVolumeCallData {
                    origin = Vector3Serializable.Create(origin),
                    extents = Vector3Serializable.Create(extents)
                }
            });

            return true;
        }

        float _meshDensity;

        float IXRMeshSubsystem.meshDensity {
            get => _meshDensity;
            set {
                if (_meshDensity != value) {
                    MeshSubsystemSender.log($"send meshDensity {value}");
                    _meshDensity = value;
                    Connection.Send(new MeshingDataEditor {
                        meshDensity = value
                    });
                }
            }
        }

        void receive(MeshingDataPlayer meshingData) {
            var remoteMeshInfos = meshingData.meshInfos;
            if (remoteMeshInfos != null) {
                MeshSubsystemSender.log("receive meshInfos " + remoteMeshInfos.Count);
                meshInfos.AddRange(remoteMeshInfos.Select(_ => _.Value));
            }

            var maybeGenerateMeshAsyncResponse = meshingData.generateMeshAsyncResponse;
            if (!maybeGenerateMeshAsyncResponse.HasValue) {
                return;
            }
            
            var response = maybeGenerateMeshAsyncResponse.Value;
            var meshId = response.meshId.Value;
            var request = meshGenerationRequests[meshId];
            var removed = meshGenerationRequests.Remove(meshId);
            Assert.IsTrue(removed);

            var mesh = request.mesh;
            mesh.MarkDynamic();
            var meshCollider = request.meshCollider;
            var status = response.Status;
            if (status == MeshGenerationStatus.Success) {
                Assert.IsFalse(response.indices.All(_ => _ == 0));
                Assert.AreNotEqual(0, response.vertices.Length);
                        
                mesh.Clear();
                mesh.SetVertices(response.vertices.ToNonSerializableList());
                mesh.SetIndices(response.indices, MeshTopology.Triangles, 0);

                var normals = response.normals;
                if (normals != null) {
                    mesh.SetNormals(normals.ToNonSerializableList());
                }

                var tangents = response.tangents;
                if (tangents != null) {
                    mesh.SetTangents(tangents.ToNonSerializableList());
                }

                var uvs = response.uvs;
                if (uvs != null) {
                    mesh.SetUVs(0, uvs.ToNonSerializableList());
                }

                var colors = response.colors;
                if (colors != null) {
                    mesh.SetColors(colors.ToNonSerializableList());
                }
                    
                mesh.RecalculateBounds();

                if (meshCollider != null) {
                    meshCollider.sharedMesh = mesh;
                }
            }
                      
            if (mesh.bounds.extents == Vector3.zero) {
                MeshSubsystemSender.errorOccursSometimes("mesh.bounds.extents == Vector3.zero, mesh.RecalculateBounds() doesn't help");
                status = MeshGenerationStatus.UnknownError;
            }

            MeshSubsystemSender.log("Receiver: onMeshGenerationComplete " + meshId);
            request.onMeshGenerationComplete(new MeshGenerationResultWrapper {
                MeshId = meshId,
                Mesh = mesh,
                MeshCollider = meshCollider,
                Status = status,
                Attributes = request.attributes
            }.Value);
        }
    }
}
#endif
