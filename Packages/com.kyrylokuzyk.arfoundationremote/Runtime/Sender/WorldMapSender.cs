#if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED
using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement; // todo remove
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARKit;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class WorldMapSender : MonoBehaviour {
        ARSession session;
        
        ARKitSessionSubsystem subsystem => session.subsystem as ARKitSessionSubsystem;
        ARWorldMappingStatus curStatus;
        
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void register() {
            Utils.TryCreateCompanionAppObjectOnSceneLoad<WorldMapSender>();
        }

        void Awake() {
            session = FindObjectOfType<Sender>().arSession;
        }

        void OnEnable() {
            Connection.Register<WorldMapDataEditor>(dataReceived);
        }

        void OnDisable() {
            Connection.UnRegister<WorldMapDataEditor>();
        }

        void dataReceived(WorldMapDataEditor worldMapData) {
            var getWorldMapRequest = worldMapData.getWorldMapRequest;
            if (getWorldMapRequest.HasValue) {
                DontDestroyOnLoadSingleton.AddCoroutine(getWorldMapCoroutine(getWorldMapRequest.Value), nameof(getWorldMapCoroutine));
            }

            if (worldMapData.serializedWorldMap != null) {
                using (var nativeArray = new NativeArray<byte>(worldMapData.serializedWorldMap, Allocator.Temp)) {
                    var isSuccess = ARWorldMap.TryDeserialize(nativeArray, out var map);
                    if (isSuccess) {
                        Assert.IsTrue(map.valid);
                    }

                    Connection.SendResponse(new WorldMapData {
                        tryDeserializeMapHandleResponse = isSuccess ? map.GetNativeHandle() : (int?) null,
                    }, worldMapData);
                }
            }

            if (worldMapData.applyWorldMapNativeHandle.HasValue) {
                var map = ARWorldMapExtensions.Create(worldMapData.applyWorldMapNativeHandle.Value);
                subsystem.ApplyWorldMap(map);
            }
        }

        IEnumerator getWorldMapCoroutine(int requestId) {
            using (var request = subsystem.GetARWorldMapAsync()) {
                while (!request.status.IsDone())
                    yield return null;

                var status = request.status;
                int? nativeHandle = null;
                byte[] serializedBytes = null;
                bool isValid = false;

                if (!status.IsError()) {
                    using (var map = request.GetWorldMap()) {
                        nativeHandle = map.GetNativeHandle();

                        using (var nativeArray = map.Serialize(Allocator.Temp)) {
                            serializedBytes = nativeArray.ToArray();
                        }

                        isValid = map.valid;
                    }
                }

                Connection.Send(new WorldMapData {
                    GetARWorldMapAsyncResponse = new GetARWorldMapAsyncResponse {
                        status = status,
                        nativeHandle = nativeHandle,
                        serializedBytes = serializedBytes,
                        requestId = requestId,
                        isValid = isValid
                    }
                });
            }
        }

        /// this version produce crash:
        /// Thread 57 Queue : com.unity.xr.arkit (serial)
        /// #0	0x00000001e28e6630 in _platform_memmove ()
        /// #1	0x000000010676c8cc in UnityARKit_copyAndReleaseNsData at /Users/todd.stinson/Work/arfoundation/com.unity.xr.arkit/Source~/UnityARKit/WorldMapManager.mm:71
        /// #2	0x0000000106dc35b4 in ::Api_UnityARKit_copyAndReleaseNsData_mBE81424D8C7A771D6BB30694DD28A4AD03F852B7(intptr_t, intptr_t, int32_t, const RuntimeMethod *) at /Users/kyrylokuzyk/Documents/projects/ARFoundationRemote/builds/ios/Classes/Native/Unity.XR.ARKit.cpp:26507
        /// #3	0x0000000106dc3504 in ::ARWorldMap_Serialize_m0888FED892B775E57E8BEDCE88AEB7CC5296C847(ARWorldMap_t8BAE5D083A023D7DD23C29E4082B6BBD329010DE *, int32_t, const RuntimeMethod *) at /Users/kyrylokuzyk/Documents/projects/ARFoundationRemote/builds/ios/Classes/Native/Unity.XR.ARKit.cpp:25763
        /// #4	0x00000001067d71d8 in ::WorldMapSender_U3CgetWorldMapCallbackU3Eg__serializeU7C5_1_mD8156A1CD4414BD887AFAFC5FD38760222307A15(U3CU3Ec__DisplayClass5_1_t89A2BDCA25BC4050D0F0237A4D3E43C0CDFDA14B *, const RuntimeMethod *) at /Users/kyrylokuzyk/Documents/projects/ARFoundationRemote/builds/ios/Classes/Native/ARFoundationRemote.Runtime1.cpp:28586
        /// #5	0x00000001067d76a4 in ::U3CU3Ec__DisplayClass5_0_U3CgetWorldMapCallbackU3Eb__0_m7EBF4D1444D1E784148118A122575716D743CE37(U3CU3Ec__DisplayClass5_0_t61377B0790784139A0A1D36E280E60F84936F65F *, int32_t, ARWorldMap_t8BAE5D083A023D7DD23C29E4082B6BBD329010DE, const RuntimeMethod *) at /Users/kyrylokuzyk/Documents/projects/ARFoundationRemote/builds/ios/Classes/Native/ARFoundationRemote.Runtime1.cpp:28753
        /// #6	0x00000001068dda40 in ::Action_2_Invoke_m514AB5C30ECAB1DFA1C2D1430759E7EF343E4756_gshared(Action_2_t3F365260232979E3376DDF7E674235AA6466EC8E *, int32_t, ARWorldMap_t8BAE5D083A023D7DD23C29E4082B6BBD329010DE, const RuntimeMethod *) at /Users/kyrylokuzyk/Documents/projects/ARFoundationRemote/builds/ios/Classes/Native/Generics.cpp:27690
        /// #7	0x00000001055fb98c in Action_2_Invoke_m2B078E4FAAD6BB392B32D5CCDAC7AC41EF522435(Action_2_t84AF1D0E414DA3F3CC83837D20CF4E7B5050620A*, int, ARWorldMap_t8BAE5D083A023D7DD23C29E4082B6BBD329010DE, MethodInfo const*) at /Users/kyrylokuzyk/Documents/projects/ARFoundationRemote/builds/ios/Classes/Native/Unity.XR.ARKit.cpp:13886
        /// #8	0x0000000106dbdbac in ::ARKitSessionSubsystem_OnAsyncConversionComplete_mAF81D4ED67F8334B95C4376905DB2418863E3508(int32_t, int32_t, intptr_t, const RuntimeMethod *) at /Users/kyrylokuzyk/Documents/projects/ARFoundationRemote/builds/ios/Classes/Native/Unity.XR.ARKit.cpp:23145
        /// #9	0x00000001055fb880 in ::ReversePInvokeWrapper_ARKitSessionSubsystem_OnAsyncConversionComplete_mAF81D4ED67F8334B95C4376905DB2418863E3508(int32_t, int32_t, intptr_t) at /Users/kyrylokuzyk/Documents/projects/ARFoundationRemote/builds/ios/Classes/Native/Unity.XR.ARKit.cpp:22639
        /// #10	0x00000001067a4980 in WorldMapRequestManager::OnWorldMapSerialized(int, ARWorldMap*, NSError*) at /Users/todd.stinson/Work/arfoundation/com.unity.xr.arkit/Source~/UnityARKit/WorldMapRequestManager.mm:158
        /// #11	0x00000001067a4864 in invocation function for block in WorldMapRequestManager::CreateRequest(void (*)(ARWorldMapRequestStatus, int, void*), void*) at /Users/todd.stinson/Work/arfoundation/com.unity.xr.arkit/Source~/UnityARKit/WorldMapRequestManager.mm:118
        [SuppressMessage("ReSharper", "CommentTypo")]
        void getWorldMapCallback(int requestId) {
            subsystem.GetARWorldMapAsync((status, map) => {
                Assert.AreNotEqual(ARWorldMapRequestStatus.Pending, status);

                var isSuccess = !status.IsError();

                byte[] serialize() {
                    if (isSuccess) {
                        Assert.IsTrue(map.valid);
                        // crashes here
                        using (var nativeArray = map.Serialize(Allocator.Temp)) {
                            return nativeArray.ToArray();
                        }
                    }

                    return null;
                }

                Connection.Send(new WorldMapData {
                    GetARWorldMapAsyncResponse = new GetARWorldMapAsyncResponse {
                        status = status,
                        nativeHandle = isSuccess ? map.GetNativeHandle() : (int?) null,
                        serializedBytes = serialize(),
                        requestId = requestId,
                        isValid = map.valid
                    }
                });

                if (isSuccess) {
                    map.Dispose();
                }
            });
        }

        /// can't be public because the script if enclosed in #if define
        [Conditional("_")]
        static void log(string msg) {
            Debug.Log(msg);
        }

        void Update() {
            sendWorldMappingStatus();
        }

        void sendWorldMappingStatus() {
            if (session.enabled && Sender.isConnected) {
                var newStatus = subsystem.worldMappingStatus;
                if (curStatus != newStatus) {
                    curStatus = newStatus;
                    Connection.Send(new WorldMapData {
                        worldMappingStatus = newStatus
                    });
                    
                    log($"send worldMappingStatus {newStatus}");
                }
            }
        }
    }


    static class ARWorldMapExtensions {
        [NotNull] static readonly PropertyInfo property = typeof(ARWorldMap)
            .GetProperty("nativeHandle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty) ?? throw new Exception();

        
        public static int GetNativeHandle(this ARWorldMap map) {
            return (int) property.GetValue(map);
        }

        public static ARWorldMap Create(int nativeHandle) {
            var boxed = (object) new ARWorldMap();
            property.SetValue(boxed, nativeHandle);
            return (ARWorldMap) boxed;
        }
    }


    [Serializable]
    public struct GetARWorldMapAsyncResponse {
        public ARWorldMapRequestStatus status;
        public int? nativeHandle;
        [CanBeNull] public byte[] serializedBytes;
        public int requestId;
        public bool isValid;
    }
    
    
    [Serializable]
    public class WorldMapData : BlockingMessage {
        public GetARWorldMapAsyncResponse? GetARWorldMapAsyncResponse;
        public int? tryDeserializeMapHandleResponse;
        public ARWorldMappingStatus? worldMappingStatus;
    }

    
    [Serializable]
    public class WorldMapDataEditor : BlockingMessage {
        public int? getWorldMapRequest;
        [CanBeNull] public byte[] serializedWorldMap;
        public int? applyWorldMapNativeHandle;
    }
}
#endif
