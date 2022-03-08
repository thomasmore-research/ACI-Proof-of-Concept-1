#if UNITY_EDITOR
using System.Diagnostics;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public partial class SessionSubsystem : XRSessionSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.IsPluginEnabled()) {
                return;
            }

            //Debug.Log("RegisterDescriptor ARemoteSessionSubsystem");
            var thisType = typeof(SessionSubsystem);
            XRSessionSubsystemDescriptor.RegisterDescriptor(new XRSessionSubsystemDescriptor.Cinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(ARemoteSessionSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType
                #endif
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new ARemoteSessionSubsystemProvider();
        #endif
        
        
        partial class ARemoteSessionSubsystemProvider : Provider {
            [CanBeNull] internal static ARemoteSessionSubsystemProvider startedInstance;
            ARSessionState remoteSessionState = ARSessionState.None;


            public ARemoteSessionSubsystemProvider() {
                log($"ARemoteSessionSubsystemProvider() {GetHashCode()}");
            }
            
            public override Promise<SessionAvailability> GetAvailabilityAsync() {
                return Promise<SessionAvailability>.CreateResolvedPromise(SessionAvailability.Supported | SessionAvailability.Installed);
            }

            public override TrackingState trackingState {
                get {
                    switch (remoteSessionState) {
                        case ARSessionState.SessionInitializing:
                            return TrackingState.Limited;
                        case ARSessionState.SessionTracking:
                            return TrackingState.Tracking;
                        default:
                            return TrackingState.None;
                    }
                }
            }

            public override void Reset() {
                SendMessageToRemote(EditorToPlayerMessageType.ResetSession);
            }
            
            public override void 
                #if UNITY_2020_2_OR_NEWER
                    Start
                #else
                    Resume
                #endif
                () {
                Assert.IsNull(startedInstance);
                startedInstance = this;
                log($"ARemoteSessionSubsystemProvider.Resume() {GetHashCode()}");
                Connection.Register<ARSessionState>(receivedSessionState => {
                    remoteSessionState = receivedSessionState;
                    // Debug.Log("receivedSessionState " + receivedSessionState);
                });
                
                SendMessageToRemote(EditorToPlayerMessageType.ResumeSession);
                onStart_internal();
            }

            partial void onStart_internal();
            
            public override void 
                #if UNITY_2020_2_OR_NEWER
                    Stop
                #else
                    Pause
                #endif
                () {
                startedInstance = null;
                log($"ARemoteSessionSubsystemProvider.Pause() {GetHashCode()}");
                SendMessageToRemote(EditorToPlayerMessageType.PauseSession);
                Connection.UnRegister<ARSessionState>();
                onStop_internal();
            }
            
            partial void onStop_internal();

            public override void Destroy() {
                SendMessageToRemote(EditorToPlayerMessageType.DestroySession);
            }

            void SendMessageToRemote(EditorToPlayerMessageType messageType) {
                Connection.Send(messageType);
            }

            #if ARFOUNDATION_4_0_OR_NEWER
                Feature? trackingMode = Feature.None;

                public override Feature currentTrackingMode => trackingMode ?? Feature.None;

                public override Feature requestedTrackingMode {
                    get => currentTrackingMode;
                    set {
                        if (trackingMode != value) {
                            trackingMode = value;
                            Sender.logSceneSpecific($"send trackingMode {trackingMode}");
                            Connection.Send(new CameraSubsystemSender.TrackingModeData {
                                trackingMode = value
                            });
                            
                            CameraSubsystemSender.CheckSixDegreesOfFreedomBug();
                        }
                    }
                }
            #endif
            
            
            [Conditional("_")]
            static void log(string msg) {
                Debug.Log(msg);
            }
        }
    }
}
#endif
