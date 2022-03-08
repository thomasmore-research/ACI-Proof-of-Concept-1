using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;


namespace ARFoundationRemote.Runtime {
    // todoo add user callbacks for messages. Should add separate callbacks for users
    public static class Connection {
        [CanBeNull] static TelepathySenderConnection _senderConnection;

        static TelepathySenderConnection senderConnection {
            get {
                if (Application.isEditor) {
                    throw new Exception("senderConnection getter in Editor!");
                }

                if (_senderConnection == null) {
                    _senderConnection = Utils.CreatePersistentObject<TelepathySenderConnection>();
                }

                return _senderConnection;
            }
        }

        public static bool IsSenderConnectionActive => senderConnection.isActive;

        #if UNITY_EDITOR
        /// receiverConnection will not be null after TelepathyReceiverConnection.OnDestroy() because it will bypass UnityEngine.Object null check
        static IReceiverConnection _receiverConnection;

        [NotNull]
        public static IReceiverConnection receiverConnection {
            get {
                if (_receiverConnection == null) {
                    _receiverConnection = createReceiverConnection();
                }

                return _receiverConnection;
            }
        }

        static IReceiverConnection createReceiverConnection() {
            #if AR_FOUNDATION_REMOTE_4_12_0_OR_NEWER
            if (SessionRecordings.Instance.isRecording) {
                return Utils.TryCreatePersistentEditorObject<RecordingReceiverConnection>();
            } else if (SessionRecordings.Instance.playbackRecordedSession) {
                return Utils.TryCreatePersistentEditorObject<PlaybackReceiverConnection>();
            }
            #endif
            
            return Utils.TryCreatePersistentEditorObject<RuntimeEditor.TelepathyReceiverConnection>();
        }
      
        public static ResponseType BlockUntilReceive<ResponseType>(BlockingMessage payload) where ResponseType : BlockingMessage {
            return receiverConnection.BlockUntilReceive<ResponseType>(payload);
        }
      
        #if AR_FOUNDATION_REMOTE_4_12_0_OR_NEWER
        public static void TryRecordMessage(object msg) {
            if (receiverConnection is RecordingReceiverConnection recordingConnection) {
                recordingConnection.RecordMessage(msg);
            }
        }
        #endif
        #endif // UNITY_EDITOR

        public static bool CanSendNonCriticalMessage => Instance.CanSendNonCriticalMessage;

        public static IConnection Instance {
            get {
                #if UNITY_EDITOR
                if (Application.isEditor) {
                    return receiverConnection;
                }
                #endif

                return senderConnection;
            }
        }

        public static void Register<T>(Action<T> callback) {
            Instance.Register(callback);
        }

        public static void UnRegister<T>() {
            Instance.UnRegister<T>();
        }

        public static void Send([NotNull] object msg) {
            if (!Application.isEditor && msg is BlockingMessage iguid) {
                Assert.IsFalse(iguid.blockingMessageGuid.HasValue, "Assert.IsFalse(iguid.guid.HasValue), use Connection.SendResponse() instead.");
            }

            #if UNITY_EDITOR
            if (Settings.Instance.debugSettings.warnIfReceiverConnectionWasDestroyed) {
                var monoBehaviour = receiverConnection as MonoBehaviour;
                if (monoBehaviour == null) {
                    Debug.LogWarning("Send() is called after TelepathyReceiverConnection.OnDestroy()");
                }    
            }
            #endif
            
            Instance.Send(msg);
        }

        public static void SendResponse([NotNull] BlockingMessage response, BlockingMessage request) {
            var guid = request.blockingMessageGuid;
            Assert.IsTrue(guid.HasValue);
            Assert.IsFalse(response.blockingMessageGuid.HasValue);
            response.blockingMessageGuid = guid;
            Instance.Send(response);
        }
    }
    
        
    [Serializable]
    public abstract class BlockingMessage {
        public Guid? blockingMessageGuid { get; set; } 
    }
    
    
    public interface IConnection {
        bool isConnected { get; }
        void Register<T>([NotNull] Action<T> callback);
        void UnRegister<T>();
        void Send([NotNull] object msg);
        bool CanSendNonCriticalMessage { get; }
        void StartConnection([NotNull] IConnectionDelegate del);
    }


    public interface IReceiverConnection : IConnection {
        ResponseType BlockUntilReceive<ResponseType>(BlockingMessage payload) where ResponseType : BlockingMessage;
        void BlockUntilSent(object payload);
    }


    public interface IConnectionDelegate {
        void OnConnected();
        void OnDisconnected();
    }
}
