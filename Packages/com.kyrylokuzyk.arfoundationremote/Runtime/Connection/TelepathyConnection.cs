using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
using Telepathy;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;
using EventType = Telepathy.EventType;
using ThreadPriority = System.Threading.ThreadPriority;


namespace ARFoundationRemote.Runtime {
    public abstract class TelepathyConnection : MonoBehaviour, IConnection {
        bool IConnection.CanSendNonCriticalMessage => outgoingMessages.Count < 2;

        protected const int maxMessageSize = 100 * 1024 * 1024;
        protected static int port => Settings.Instance.port;
        readonly Dictionary<Type, Action<object>> callbacks = new Dictionary<Type, Action<object>>();

        protected int connectionId = -1;
        Thread backgroundThread;
        protected ConcurrentQueue<IncomingMessage> incomingMessages = new ConcurrentQueue<IncomingMessage>();
        protected ConcurrentQueue<object> outgoingMessages = new ConcurrentQueue<object>();
        protected IConnectionDelegate connectionDelegate;
        bool didStart;
        #pragma warning disable 649
        [CanBeNull] internal Action<object> onIncomingMessageReceived;
        #pragma warning restore 649


        protected abstract IEnumerator startConnectionCor();

        void IConnection.StartConnection(IConnectionDelegate del) {
            Assert.IsNull(connectionDelegate);
            Assert.IsNotNull(del);
            connectionDelegate = del;
            if (!didStart) {
                didStart = true;
                StartCoroutine(startConnectionCor());
            }
        }

        void Awake() {
            log($"{GetType().Name} {nameof(Awake)}()");
            if (Settings.Instance.showTelepathyLogs) {
                Telepathy.Logger.Log = Debug.Log;
            }

            if (Settings.Instance.showTelepathyWarningsAndErrors) {
                Telepathy.Logger.LogWarning = msg => {
                    if (!Application.isEditor) {
                        Sender.waitingErrorMessage += msg + "\n";
                    }

                    Debug.LogWarning(msg);
                };
                
                Telepathy.Logger.LogError = msg => {
                    Sender.waitingErrorMessage += msg + "\n";
                    Debug.LogError(msg);
                };
            }
            
            backgroundThread = new Thread(runBackgroundThread) {IsBackground = true, Priority = ThreadPriority.Highest};
            backgroundThread.Start();
        }

        [Conditional("_")]
        void log(string s) {
            Debug.Log(s);
        }

        void OnDestroy() {
            backgroundThread.Abort(); // Interrupt() doesn't stop the thread instantly
            onDestroyInternal();
        }

        protected virtual void onDestroyInternal() { }

        void runBackgroundThread() {
            try {
                while (true) {
                    Thread.Sleep(1000/60);
                    while (getCommon().GetNextMessage(out var msg)) {
                        var id = msg.connectionId;
                        var eventType = msg.eventType;
                        switch (eventType) {
                            case EventType.Connected:
                                connectionId = id;
                                break;
                            case EventType.Data:
                                break;
                            case EventType.Disconnected:
                                if (connectionId == id) {
                                    connectionId = -1;
                                }

                                break;
                            default:
                                throw new Exception();
                        }

                        // pass all messages to main thread
                        incomingMessages.Enqueue(new IncomingMessage {
                            message = eventType == EventType.Data ? msg.data.Deserialize<object>() : null,
                            eventType = eventType
                        });
                    }

                    if (connectionDelegate != null && isConnected_internal) {
                        while (outgoingMessages.TryDequeue(out var msg)) {
                            send(msg.SerializeToByteArray());
                        }
                    }
                }
            } catch (ThreadAbortException) {
            }
        }

        void Update() {
            if (connectionDelegate != null) {
                while (incomingMessages.TryDequeue(out var msg)) {
                    switch (msg.eventType) {
                        case EventType.Connected:
                            connectionDelegate.OnConnected();
                            break;
                        case EventType.Data:
                            var message = msg.message;
                            if (onIncomingMessageReceived != null) {
                                // onIncomingMessageReceived should be called before registered callback
                                onIncomingMessageReceived(message);
                            }
                            
                            var type = message.GetType();
                            if (callbacks.TryGetValue(type, out var callback)) {
                                callback(message);
                            } else {
                                // callback can be null if we Stop some AR subsystem without closing the AR scene entirely 
                                // Debug.LogError($"callback is not registered for type {type}");
                            }
                            break;
                        case EventType.Disconnected:
                            connectionDelegate.OnDisconnected();
                            break;
                        default: 
                            throw new Exception();
                    }
                }
            }
        }

        protected abstract Common getCommon();
        bool IConnection.isConnected => isConnected_internal;
        protected abstract bool isConnected_internal { get; }

        void IConnection.Register<T>(Action<T> callback) {
            var type = typeof(T);
            Assert.IsFalse(callbacks.ContainsKey(type));
            callbacks.Add(type, data => {
                Assert.IsTrue(data is T);
                callback((T) data);
            });
        }

        void IConnection.UnRegister<T>() {
            var type = typeof(T);
            Assert.IsTrue(callbacks.ContainsKey(type));
            callbacks.Remove(type);
        }

        public void Send(object msg) {
            outgoingMessages.Enqueue(msg);
        }

        protected abstract void send(byte[] payload);

        protected struct IncomingMessage {
            [CanBeNull] public object message;
            public EventType eventType;
        }
    }
}
