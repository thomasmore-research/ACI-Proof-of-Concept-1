#if AR_SUBSYSTEMS_3_1_3_OR_NEWER && !ARFOUNDATION_4_0_0_PREVIEW_1
    using System.Collections.Generic;
#endif
using System;
using System.Diagnostics;
using System.Linq;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;


namespace ARFoundationRemote.Editor {
    public partial class CameraSubsystem : XRCameraSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.IsPluginEnabled()) {
                return;
            }

            var thisType = typeof(CameraSubsystem);
            bool isARKit = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
            Register(new XRCameraSubsystemCinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(CameraSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    implementationType = thisType,
                #endif
                supportsAverageBrightness = !isARKit, 
                supportsAverageColorTemperature = isARKit,
                supportsColorCorrection = !isARKit,
                supportsDisplayMatrix = true,
                supportsProjectionMatrix = true,
                supportsTimestamp = true,
                supportsCameraConfigurations = true,
                supportsCameraImage = true,
                supportsAverageIntensityInLumens = isARKit,
                supportsFocusModes = true
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new CameraSubsystemProvider();
        #endif

        partial class CameraSubsystemProvider : Provider {
            bool isRunning;
            bool enabled = true;
            bool fractalBugFixApplied;
            [CanBeNull] ARCameraFrameEventArgsSerializable? receivedCameraFrame { get; set; }
            [CanBeNull] TextureAndDescriptor[] textures { get; set; }

            
            public CameraSubsystemProvider() {
                cameraMaterial = CreateCameraMaterial(getShaderName());
            }

            [CanBeNull]
            static string getShaderName() {
                if (Defines.isIOS) {
                    #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED
                        return Application.platform == RuntimePlatform.WindowsEditor ? "Unlit/ARKitBackgroundWindows" : UnityEngine.XR.ARKit.ARKitCameraSubsystem.backgroundShaderName;
                    #endif
                } else if (Defines.isAndroid) {
                    #if (UNITY_ANDROID || UNITY_EDITOR) && ARCORE_INSTALLED
                        return "ARFoundationRemote/ARCoreBackgroundEditor";
                    #endif
                }

                #pragma warning disable 162
                Debug.LogWarning($"{Constants.packageName}: {EditorUserBuildSettings.activeBuildTarget} doesn't support camera video. Please ensure that you selected the correct build target and installed XR plugin.");
                return null;
                #pragma warning restore 162
            }

            #if ARFOUNDATION_4_0_OR_NEWER
                Feature currentRequestedCamera = Feature.None;

                public override Feature requestedCamera {
                    get => currentRequestedCamera;
                    set {
                        if (currentRequestedCamera != value) {
                            currentRequestedCamera = value;
                            Connection.Send(new CameraSubsystemSender.RequestedCameraData {
                                requestedCamera = value
                            });
                            
                            log("send currentRequestedCamera " + value);
                            CameraSubsystemSender.CheckSixDegreesOfFreedomBug();
                        }
                    }
                }

                public override Feature currentCamera => currentRequestedCamera;
                
                Feature _requestedLightEstimation;

                public override Feature requestedLightEstimation {
                    get => _requestedLightEstimation;
                    set {
                        if (_requestedLightEstimation != value) {
                            _requestedLightEstimation = value;                            
                            CameraSubsystemSender.log($"send requestedLightEstimation {value}");
                            Connection.Send(LightEstimationDataEditor.FromFeature(value));
                        }
                    }
                }

                public override Feature currentLightEstimation => _requestedLightEstimation;
                
                bool? autofocus;
                public override bool autoFocusRequested {
                    get => autofocus ?? false;
                    set {
                        if (autofocus != value) {
                            autofocus = value;
                            sendAutofocusEnabled(value);
                        }
                    }
                }
                public override bool autoFocusEnabled => autoFocusRequested;
            #else
                LightEstimationMode? _lightEstimationMode;
                
                public override bool TrySetLightEstimationMode(LightEstimationMode lightEstimationMode) {
                    if (_lightEstimationMode != lightEstimationMode) {
                        _lightEstimationMode = lightEstimationMode;
                        Connection.Send(LightEstimationDataEditor.FromLightEstimationMode(lightEstimationMode));
                    }

                    // correct return value is not supported 
                    return true;
                }

                CameraFocusMode? focusMode;
                public override CameraFocusMode cameraFocusMode {
                    get => focusMode ?? CameraFocusMode.Fixed;
                    set {
                        if (focusMode != value) {
                            focusMode = value;
                            sendAutofocusEnabled(value == CameraFocusMode.Auto);
                        }
                    }
                }
            #endif
            
            static void sendAutofocusEnabled(bool value) {
                CameraSubsystemSender.log($"sendAutofocusEnabled {value}");
                Connection.Send(new CameraDataEditor {
                    enableAutofocus = value
                });
            }
            
            [CanBeNull] public override Material cameraMaterial { get; }

            public override bool permissionGranted => true;

            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame) {
                if (receivedCameraFrame.HasValue) {
                    cameraFrame = receivedCameraFrame.Value.frame.Value;
                    return true;
                } else {
                    cameraFrame = default;
                    return false;    
                }
            }

            public override NativeArray<XRTextureDescriptor> GetTextureDescriptors(XRTextureDescriptor defaultDescriptor, Allocator allocator) {
                if (enabled) {
                    if (receivedCameraFrame.HasValue && textures != null && cameraMaterial != null) {
                        if (Defines.UNITY_2020_3_OR_NEWER && Defines.isAndroid && !fractalBugFixApplied) {
                            fractalBugFixApplied = true;
                            DontDestroyOnLoadSingleton.Instance.StartCoroutine(fixUnity2020_3_8_CameraFractalsBug());
                        }
                        
                        Assert.IsNotNull(cameraMaterial);
                        Assert.IsNotNull(textures);
                        Assert.IsTrue(receivedCameraFrame.HasValue);
                        var propertyNames = receivedCameraFrame.Value.textures.Select(_ => _.propName);
                        if (propertyNames.All(_ => SupportCheck.CheckCameraAndOcclusionSupport(cameraMaterial, _))) {
                            var result = textures
                                .Select(_ => _.descriptor)
                                .Where(_ => _.HasValue)
                                .Select(_ => _.Value)
                                .ToArray();
                        
                            return new NativeArray<XRTextureDescriptor>(result, allocator);    
                        } else {
                            enabled = false;
                        }
                    }
                }
                
                return new NativeArray<XRTextureDescriptor>(0, allocator);
            }

            public override bool invertCulling => receivedCameraFrame?.invertCulling ?? false;

            #if AR_SUBSYSTEMS_3_1_3_OR_NEWER && !ARFOUNDATION_4_0_0_PREVIEW_1
            public override void GetMaterialKeywords(out List<string> enabledKeywords, out List<string> disabledKeywords) {
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS) {
                    const string urp = "ARKIT_BACKGROUND_URP";
                    const string lwrp = "ARKIT_BACKGROUND_LWRP";
                    var urpKeywords = new List<string> {urp};
                    var lwrpKeywords = new List<string> {lwrp};
                    if (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset == null) {
                        enabledKeywords = null;
                        disabledKeywords = new List<string> {urp, lwrp};
                    } else if (isURP()) {
                        enabledKeywords = urpKeywords;
                        disabledKeywords = lwrpKeywords;
                    } else if (isLWRP()) {
                        enabledKeywords = lwrpKeywords;
                        disabledKeywords = urpKeywords;
                    }  else {
                        enabledKeywords = null;
                        disabledKeywords = null;
                    }
                } else {
                    enabledKeywords = null;
                    disabledKeywords = null;
                }
            }
            #endif

            static bool isURP() {
                #if MODULE_URP_ENABLED
                    return UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                #else
                    return false;
                #endif
            }
            
            /// LWRP was renamed into URP starting from Unity 2019.3. Even official ARKit XR Plugin doesn't compile with LWRP 
            static bool isLWRP() {
                #if MODULE_LWRP_ENABLED
                    return UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset is UnityEngine.Rendering.LWRP.LightweightRenderPipelineAsset;
                #else
                    return false;
                #endif
            }

            public override NativeArray<XRCameraConfiguration> GetConfigurations(XRCameraConfiguration defaultCameraConfiguration, Allocator allocator) {
                var configurationsContainer = blockUntilReceive(new CameraDataEditor {
                    request = CameraDataEditorRequest.GetAllConfigurations
                }).allConfigurations;
                Assert.IsTrue(configurationsContainer.HasValue);
                var configurations = configurationsContainer.Value;
                if (configurations.isSupported) {
                    return new NativeArray<XRCameraConfiguration>(configurations.configs.Select(_ => _.Value).ToArray(), allocator);
                } else {
                    throw new Exception($"{Constants.packageName}: your AR device doesn't support camera configurations");
                }
            }

            public override XRCameraConfiguration? currentConfiguration {
                get =>
                    blockUntilReceive(new CameraDataEditor {
                        request = CameraDataEditorRequest.GetCurrentConfiguration
                    }).currentConfiguration?.Value;
                set {
                    var error = blockUntilReceive(new CameraDataEditor {
                        request = CameraDataEditorRequest.SetCurrentConfiguration,
                        configToSet = CameraConfigurationSerializable.Create(value)
                    }).error;
                    
                    if (error != null) {
                        throw new Exception(error);
                    }
                }
            }

            static CameraData blockUntilReceive(CameraDataEditor cameraDataEditor) {
                return Connection.BlockUntilReceive<CameraData>(cameraDataEditor);
            }

            public override void Start() {
                Connection.Register<ARCameraFrameEventArgsSerializable>(receiveCameraFrame);
                Connection.Register<CameraData>(receiveCameraData);
                Connection.Register<XRCameraIntrinsicsSerializable>(serialized => intrinsics = serialized.Value);
                Connection.Register<CpuImageData>(receiveCpuImages);
                isRunning = true;
                setRemoteManagerEnabled(true);
            }

            public override void Stop() {
                isRunning = false;
                setRemoteManagerEnabled(false);
                Connection.UnRegister<ARCameraFrameEventArgsSerializable>();
                Connection.UnRegister<CameraData>();
                Connection.UnRegister<XRCameraIntrinsicsSerializable>();
                Connection.UnRegister<CpuImageData>();
                prevConversionParams = null;
                fractalBugFixApplied = false;
            }

            void setRemoteManagerEnabled(bool isEnabled) {
                log($"setRemoteManagerEnabled {isEnabled}");
                Connection.Send(new CameraDataEditor {
                    enableCameraManager = isEnabled
                });
            }

            public override void Destroy() {
                logCpuImages("Destroy()");
                enableCpuImages = false;
                if (textures != null) {
                    foreach (var _ in textures) {
                        _.DestroyTexture();
                    }

                    textures = null;
                }
            }

            /// Somewhere between Unity version range (2020.3.5f1, 2020.3.8f1] ARCoreBackgroundEditor.shader started to produce fractal image
            ///     Some versions of Unity 2021 have no image at all
            /// 
            ///     Generating color in shader doesn't produce the issue:
            ///         float4 color = float4(i.remapped_uv.x, i.remapped_uv.y, 0,0);
            ///     So maybe the problem lies in texture settings
            ///
            ///     Settings a custom bg mat with a pre-set _MainTex outputs only this texture disregarding newly received textures
            /// 
            /// Re-enabling camera is a temporary workaround
            System.Collections.IEnumerator fixUnity2020_3_8_CameraFractalsBug() {
                yield return new WaitForSeconds(0.5f);
                for (int i = 0; i < 2; i++) {
                    var cameraBackground = UnityEngine.Object.FindObjectOfType<UnityEngine.XR.ARFoundation.ARCameraBackground>();
                    if (cameraBackground == null) {
                        yield break;
                    }

                    log("fixUnity2020_3_8_CameraFractalsBug()");
                    if (cameraBackground.enabled) {
                        cameraBackground.enabled = false;
                        yield return new WaitForEndOfFrame();
                        cameraBackground.enabled = true;
                    }

                    yield return new WaitForSeconds(2f);
                }

            }

            void receiveCameraFrame(ARCameraFrameEventArgsSerializable remoteFrame) {
                receivedCameraFrame = remoteFrame;

                var receivedTextures = remoteFrame.textures;
                var count = receivedTextures.Length;
                if (textures == null) {
                    textures = new TextureAndDescriptor[count];
                    for (int i = 0; i < count; i++) {
                        textures[i] = new TextureAndDescriptor();
                    }
                }

                Assert.AreEqual(receivedTextures.Length, textures.Length);
                for (int i = 0; i < count; i++) {
                    var tex = receivedTextures[i];
                    var propNameId = Shader.PropertyToID(tex.propName);
                    if (tex.texture.HasValue) {
                        textures[i].Update(tex.texture.Value, propNameId);
                    } else if (!textures[i].descriptor.HasValue) {
                        var dummyTexture = new Texture2D(2, 2);
                        var color = tryGetCameraBgColor();
                        for (int x = 0; x < dummyTexture.width; x++) {
                            for (int y = 0; y < dummyTexture.height; y++) {
                                dummyTexture.SetPixel(x, y, color);
                            }
                        }

                        dummyTexture.Apply();
                        textures[i] = new TextureAndDescriptor(dummyTexture, propNameId);
                    }
                }
            }

            static Color tryGetCameraBgColor() {
                var origin = Object.FindObjectOfType<UnityEngine.XR.ARFoundation.ARSessionOrigin>();
                if (origin != null) {
                    var cam = origin.camera;
                    if (cam != null) {
                        return cam.backgroundColor;
                    }
                }

                return Color.clear;
            }

            void receiveCameraData(CameraData cameraData) {
                var screenOrientation = cameraData.screenOrientation;
                if (screenOrientation.HasValue) {
                    CameraSubsystemSender.logScreenOrientation($"receive orientation {screenOrientation.Value}");
                    ARFoundationRemoteUtils.ScreenOrientation = screenOrientation.Value;
                }

                var maybeResolution = cameraData.screenResolution;
                if (maybeResolution.HasValue) {
                    var playerResolution = maybeResolution.Value.Deserialize();
                    var editorResolution = new Vector2Int(Screen.width, Screen.height);
                    if (playerResolution != editorResolution) {
                        Debug.LogWarning(
                            $"{Constants.packageName}: please set Editor View resolution to match AR device's resolution: {playerResolution}. Otherwise, UI and other screen-size dependent features may be displayed incorrectly. Current Editor View resolution: {editorResolution}");
                    }
                }

                if (cameraData.colorSpace.HasValue) {
                    var companionColorSpace = cameraData.colorSpace.Value;
                    var editorColorSpace = QualitySettings.activeColorSpace;
                    if (companionColorSpace != editorColorSpace) {
                        Debug.LogError(
                            $"{Constants.packageName}: please use the the same Color Space in the AR Companion app (currently {companionColorSpace}) and in Unity Editor (currently {editorColorSpace}).");
                    }
                }
            }
            
            [Conditional("_")]
            static void log(string msg) {
                Debug.Log($"{nameof(CameraSubsystem)}: {msg}");
            }
        }
    }
}
