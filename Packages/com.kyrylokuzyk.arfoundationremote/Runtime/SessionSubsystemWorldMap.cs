#if UNITY_EDITOR && ARKIT_INSTALLED
using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine.Assertions;
using UnityEngine.XR.ARKit;


namespace ARFoundationRemote.Runtime {
    public partial class SessionSubsystem {
        partial class ARemoteSessionSubsystemProvider {
            internal ARWorldMappingStatus _worldMappingStatus { get; private set; }

            
            partial void onStart_internal() {
                Connection.Register<WorldMapData>(receiveWorldMap);
            }

            partial void onStop_internal() {
                Connection.UnRegister<WorldMapData>();
            }
            
            void receiveWorldMap(WorldMapData worldMapData) {
                var maybeWorldMapResponse = worldMapData.GetARWorldMapAsyncResponse;
                if (maybeWorldMapResponse.HasValue) {
                    var worldMapResponse = maybeWorldMapResponse.Value;
                    ARWorldMapRequest.requests.Add(worldMapResponse.requestId, worldMapResponse);
                }

                var mappingStatus = worldMapData.worldMappingStatus;
                if (mappingStatus.HasValue) {
                    log($"receive _worldMappingStatus {mappingStatus.Value}");
                    _worldMappingStatus = mappingStatus.Value;
                }
            }
        }
        
        
        [PublicAPI]
        public ARWorldMapRequest GetARWorldMapAsync() {
            return ARWorldMapRequest.GetARWorldMapAsync();
        }

        /// WARNING: calling <see cref="ARKitSessionSubsystem.GetARWorldMapAsync(Action{ARWorldMapRequestStatus, ARWorldMap})"/> will crash on real devices.
        /// See WorldMapSender.getWorldMapCallback(int) for details about the crash.
        /// The remote plugin uses <see cref="ARKitSessionSubsystem.GetARWorldMapAsync()"/> version to prevent the crash.
        [PublicAPI]
        [Obsolete]
        public void GetARWorldMapAsync(Action<ARWorldMapRequestStatus, ARWorldMapRemote> onComplete) {
            DontDestroyOnLoadSingleton.Instance.StartCoroutine(GetARWorldMapAsyncCor(onComplete));
        }

        IEnumerator GetARWorldMapAsyncCor(Action<ARWorldMapRequestStatus, ARWorldMapRemote> onComplete) {
            var request = GetARWorldMapAsync();
            while (!request.status.IsDone()) {
                yield return null;
            }

            var status = request.status;
            onComplete(request.status, status == ARWorldMapRequestStatus.Success ? request.GetWorldMap() : new ARWorldMapRemote());
        }
        
        [PublicAPI]
        public void ApplyWorldMap(ARWorldMapRemote worldMap) {
            Connection.Send(new WorldMapDataEditor {
                applyWorldMapNativeHandle = worldMap.nativeHandle
            });
        }

        [PublicAPI]
        public static bool worldMapSupported => true;
        [PublicAPI]
        public ARWorldMappingStatus worldMappingStatus => ARemoteSessionSubsystemProvider.startedInstance?._worldMappingStatus ?? ARWorldMappingStatus.NotAvailable;
        [PublicAPI]
        public static bool supportsCollaboration => false;
        [PublicAPI]
        public static bool coachingOverlaySupported => false;
    }

    
    public readonly struct ARWorldMapRequest : IDisposable {
        static int currentId;
        public static readonly Dictionary<int, GetARWorldMapAsyncResponse> requests = new Dictionary<int, GetARWorldMapAsyncResponse>();
        
        readonly int id;


        ARWorldMapRequest(int id) {
            this.id = id;
        }
        
        public ARWorldMapRequestStatus status => requests.TryGetValue(id, out var request) ? request.status : ARWorldMapRequestStatus.Pending;

        public ARWorldMapRemote GetWorldMap() {
            Assert.AreEqual(ARWorldMapRequestStatus.Success, status, "Check if status is Success before calling GetWorldMap()");
            var request = requests[id];
            Assert.IsTrue(request.nativeHandle.HasValue);
            var serializedBytes = request.serializedBytes;
            Assert.IsNotNull(serializedBytes);
            return new ARWorldMapRemote(request.nativeHandle.Value, serializedBytes, request.isValid);
        }

        public void Dispose() {
        }

        public static ARWorldMapRequest GetARWorldMapAsync() {
            var requestId = currentId;
            var request = new ARWorldMapRequest(requestId);
            currentId++;

            Connection.Send(new WorldMapDataEditor {
                getWorldMapRequest = requestId 
            });

            return request;
        }
    }
    
    
    public readonly struct ARWorldMapRemote : IDisposable {
        internal readonly int nativeHandle;
        [NotNull] readonly byte[] serializedBytes;


        internal ARWorldMapRemote(int nativeHandle, [NotNull] byte[] serializedBytes, bool isValid) {
            this.nativeHandle = nativeHandle;
            this.serializedBytes = serializedBytes;
            valid = isValid;
        }
        
        public bool valid { get; }

        public NativeArray<byte> Serialize(Allocator allocator) {
            return new NativeArray<byte>(serializedBytes, allocator);
        }

        public static bool TryDeserialize(NativeArray<byte> serializedWorldMapNativeArray, out ARWorldMapRemote worldMap) {
            var serializedWorldMap = serializedWorldMapNativeArray.ToArray();
            var response = Connection.BlockUntilReceive<WorldMapData>(new WorldMapDataEditor {
                serializedWorldMap = serializedWorldMap
            }).tryDeserializeMapHandleResponse;
            
            if (response.HasValue) {
                worldMap = new ARWorldMapRemote(response.Value, serializedWorldMap, true);
                return true;
            } else {
                worldMap = default;
                return false;
            }
        }

        public void Dispose() {
        }
    }
}
#endif
