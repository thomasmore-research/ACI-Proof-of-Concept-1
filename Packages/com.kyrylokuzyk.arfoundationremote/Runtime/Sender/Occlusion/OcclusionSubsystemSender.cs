#if ARFOUNDATION_4_0_2_OR_NEWER
using System;
using System.Collections;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Linq;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    public class OcclusionSubsystemSender : MonoBehaviour {
        AROcclusionManager manager;
        bool isSending;
        static bool isSupportChecked;

   
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void register() {
            Utils.TryCreateCompanionAppObjectOnSceneLoad<OcclusionSubsystemSender>();
        }

        void Awake() {
            var sender = Sender.Instance;
            sender.ExecuteOnDisabledCamera(() => {
                var _manager = sender.origin.camera.gameObject.AddComponent<AROcclusionManager>();
                _manager.enabled = false;
                _manager.requestedHumanStencilMode = HumanSegmentationStencilMode.Disabled;
                _manager.requestedHumanDepthMode = HumanSegmentationDepthMode.Disabled;

                #if ARFOUNDATION_4_1_OR_NEWER
                _manager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
                #endif

                manager = _manager;
            });
            
            manager.frameReceived += args => {
                if (!isSending && canSend() && args.textures.Any() && CameraSubsystemSender.Instance.canRunAsyncConversion) {
                    // AR Companion will crash on scene reload if CpuImageSerializer is running
                    // DontDestroyOnLoadSingleton.AddCoroutine ensures that all coroutines are finished before calling scene reload
                    CameraSubsystemSender.Instance.AddAsyncConversionCoroutine(sendTextures(args), nameof(sendTextures));

                    if (!isSupportChecked) {
                        isSupportChecked = true;
                        if (!isSupported()) {
                            Sender.AddRuntimeErrorOnce("- Occlusion is not supported on this device");
                        }
                    }
                }
            };
        }

        IEnumerator sendTextures(AROcclusionFrameEventArgs args) {
            isSending = true;

            var descriptor = manager.descriptor;
            var resolutionScale = Settings.occlusionSettings.resolutionScale;
            var humanStencilSerializer = new CpuImageSerializer(descriptor.supportsHumanSegmentationStencilImage(), () => manager.humanStencilTexture, manager.TryAcquireHumanStencilCpuImage, args, resolutionScale);
            var humanDepthSerializer = new CpuImageSerializer(descriptor.supportsHumanSegmentationDepthImage(), () => manager.humanDepthTexture, manager.TryAcquireHumanDepthCpuImage, args, resolutionScale);

            #if ARFOUNDATION_4_1_OR_NEWER
                var depthSerializer = new CpuImageSerializer(descriptor.supportsEnvironmentDepthImage(), () => manager.environmentDepthTexture, manager.TryAcquireEnvironmentDepthCpuImage, args, resolutionScale);
                var environmentDepthConfidenceSerializer = new CpuImageSerializer(descriptor.supportsEnvironmentDepthConfidenceImage(), () => manager.environmentDepthConfidenceTexture, manager.TryAcquireEnvironmentDepthConfidenceCpuImage, args, resolutionScale);
            #endif
            
            var serializers = new[] {
                humanStencilSerializer, 
                humanDepthSerializer, 
                #if ARFOUNDATION_4_1_OR_NEWER
                    depthSerializer, 
                    environmentDepthConfidenceSerializer
                #endif
            };
            
            while (serializers.Any(_ => !_.IsDone)) {
                yield return null;
            }

            if (Sender.isConnected) {
                Connection.Send(new OcclusionData {
                    humanStencil = humanStencilSerializer.result,
                    humanDepth = humanDepthSerializer.result
                });
                
                #if ARFOUNDATION_4_1_OR_NEWER
                Connection.Send(new OcclusionDataDepth {
                    environmentDepth = depthSerializer.result,
                    environmentDepthConfidence = environmentDepthConfidenceSerializer.result
                });
                #endif
            }

            isSending = false;
        }

        float lastSendTime;
        
        bool canSend() {
            if (!Connection.CanSendNonCriticalMessage) {
                return false;
            }
            
            var curTime = Time.time;
            if (curTime - lastSendTime > 1f / Settings.occlusionSettings.maxFPS) {
                lastSendTime = curTime;
                return true;
            } else {
                return false;
            }
        }

        void OnEnable() {
            Connection.Register<OcclusionDataEditor>(occlusionData => {
                if (occlusionData.requestedHumanDepthMode.HasValue) {
                    manager.requestedHumanDepthMode = occlusionData.requestedHumanDepthMode.Value;
                }
            
                if (occlusionData.requestedHumanStencilMode.HasValue) {
                    manager.requestedHumanStencilMode = occlusionData.requestedHumanStencilMode.Value;
                }

                if (occlusionData.enableOcclusion.HasValue) {
                    Sender.Instance.SetManagerEnabled(manager, occlusionData.enableOcclusion.Value);
                }
            });
            
            #if ARFOUNDATION_4_1_OR_NEWER
            Connection.Register<OcclusionDataDepthEditor>(occlusionData => {
                if (occlusionData.requestedEnvironmentDepthMode.HasValue) {
                    manager.requestedEnvironmentDepthMode = occlusionData.requestedEnvironmentDepthMode.Value;
                }

                if (occlusionData.requestedOcclusionPreferenceMode.HasValue) {
                    manager.requestedOcclusionPreferenceMode = occlusionData.requestedOcclusionPreferenceMode.Value;
                }
            });            
            #endif
        }

        void OnDisable() {
            Connection.UnRegister<OcclusionDataEditor>();
            #if ARFOUNDATION_4_1_OR_NEWER
            Connection.UnRegister<OcclusionDataDepthEditor>();
            #endif
        }

        bool isSupported() {
            var descriptor = manager.descriptor;
            if (descriptor == null) {
                return false;
            }
            
            #if ARFOUNDATION_4_1_OR_NEWER
            // correct values are reported only after first AROcclusionManager.frameReceived event
            // Debug.LogWarning($"supportsEnvironmentDepthImage: {descriptor.supportsEnvironmentDepthImage}, supportsEnvironmentDepthConfidenceImage: {descriptor.supportsEnvironmentDepthConfidenceImage}");
            if (descriptor.supportsEnvironmentDepthImage() || descriptor.supportsEnvironmentDepthConfidenceImage()) {
                return true;
            }
            #endif

            return descriptor.supportsHumanSegmentationDepthImage() || descriptor.supportsHumanSegmentationStencilImage();
        }
    }


    [Serializable]
    public class OcclusionData {
        public SerializedTextureAndPropId? humanStencil;
        public SerializedTextureAndPropId? humanDepth;
    }

    
    #if ARFOUNDATION_4_1_OR_NEWER
    [Serializable]
    public class OcclusionDataDepth {
        public SerializedTextureAndPropId? environmentDepth;
        public SerializedTextureAndPropId? environmentDepthConfidence;
    }
    #endif

    
    [Serializable]
    public class OcclusionDataEditor {
        public HumanSegmentationDepthMode? requestedHumanDepthMode;
        public HumanSegmentationStencilMode? requestedHumanStencilMode;
        public bool? enableOcclusion;
    }
  
    
    #if ARFOUNDATION_4_1_OR_NEWER
    [Serializable]
    public class OcclusionDataDepthEditor {
        public EnvironmentDepthMode? requestedEnvironmentDepthMode;
        public OcclusionPreferenceMode? requestedOcclusionPreferenceMode;
    }
    #endif


    static class XROcclusionSubsystemDescriptorExtensions {
        public static bool supportsHumanSegmentationDepthImage(this XROcclusionSubsystemDescriptor descriptor) {
            #if AR_SUBSYSTEMS_4_2_0_pre_2
            return descriptor.humanSegmentationDepthImageSupported == Supported.Supported;
            #endif

            #pragma warning disable
            return descriptor.supportsHumanSegmentationDepthImage;
            #pragma warning restore
        }
        
        public static bool supportsHumanSegmentationStencilImage(this XROcclusionSubsystemDescriptor descriptor) {
            #if AR_SUBSYSTEMS_4_2_0_pre_2
            return descriptor.humanSegmentationStencilImageSupported == Supported.Supported;
            #endif

            #pragma warning disable
            return descriptor.supportsHumanSegmentationStencilImage;
            #pragma warning restore
        }
        
        #if ARFOUNDATION_4_1_OR_NEWER
        public static bool supportsEnvironmentDepthImage(this XROcclusionSubsystemDescriptor descriptor) {
            #if AR_SUBSYSTEMS_4_2_0_pre_2
            return descriptor.environmentDepthImageSupported == Supported.Supported;
            #endif

            #pragma warning disable
            return descriptor.supportsEnvironmentDepthImage;
            #pragma warning restore
        }

        public static bool supportsEnvironmentDepthConfidenceImage(this XROcclusionSubsystemDescriptor descriptor) {
            #if AR_SUBSYSTEMS_4_2_0_pre_2
            return descriptor.environmentDepthConfidenceImageSupported == Supported.Supported;
            #endif

            #pragma warning disable
            return descriptor.supportsEnvironmentDepthConfidenceImage;
            #pragma warning restore
        }
        #endif
    }
}
#endif
