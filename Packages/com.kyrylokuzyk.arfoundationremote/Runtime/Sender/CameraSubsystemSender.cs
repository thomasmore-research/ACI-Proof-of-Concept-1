using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class CameraSubsystemSender : MonoBehaviour {
        public static CameraSubsystemSender Instance { get; private set; }
        ARCameraManager cameraManager { get; set; }
        ARSession session;
        Throttler throttler;
        Dictionary<int, string> propIdsAndNamesDict;
        bool didSendColorSpace;
        ScreenOrientation? currentOrientation;        

        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void register() {
            Utils.TryCreateCompanionAppObjectOnSceneLoad<CameraSubsystemSender>();
        }

        void Awake() {
            Instance = this;
            throttler = new Throttler(Settings.cameraVideoSettings.maxVideoFps); // todoo constructs before new video settings received
            var sender = Sender.Instance;
            cameraManager = sender.setuper.cameraManager;
            session = sender.arSession;
            cameraManager.frameReceived += args => {
                {
                    // this causes crash on Android. We should not store the XRCameraConfiguration
                    // cameraManager.currentConfiguration = iniConfig;
                    // instead, I set the first config
                    // can't reliably set first config here because Editor will still report old config on session start
                    // tryResetConfig();
                }
            
                var textures = args.textures;
                if (textures != null && textures.All(_ => _ != null)) {
                    if (throttler.CanSendNonCriticalMessage) {
                        var payload = new ARCameraFrameEventArgsSerializable {
                            textures = serializeTextures(args, getMaterialForTextureBlit()),
                            frame = XRCameraFrameSerializable.Create(args),
                            invertCulling = cameraManager.subsystem.invertCulling
                        };
                        
                        Connection.Send(payload);
                    }
                }

                if (!didSendColorSpace) {
                    didSendColorSpace = true;
                    Connection.Send(new CameraData {
                        colorSpace = QualitySettings.activeColorSpace
                    });
                }
            };
        }

        [NotNull]
        PropIdAndMaybeTexture[] serializeTextures(ARCameraFrameEventArgs args, Material material) {
            var textures = args.textures;
            Assert.IsNotNull(textures);
            Assert.IsTrue(textures.All(_ => _ != null));

            return textures
                .Select((texture, index) => {
                    Texture2DSerializable? serializedTexture;
                    if (Settings.EnableBackgroundVideo) {
                        serializedTexture = Texture2DSerializable.SerializeToJPG(texture, Settings.cameraVideoSettings.resolutionScale, material, Settings.cameraVideoSettings.quality);
                    } else {
                        serializedTexture = null;
                    }
                    
                    return new PropIdAndMaybeTexture {
                        texture = serializedTexture,
                        propName = PropIdToName(args.propertyNameIds[index])
                    };
                }).ToArray();
        }
        
        /// My best understanding:
        /// ARCoreBackground.shader uses external texture (samplerExternalOES) so Graphics.Blit() produces gray textures without camera material.
        /// ARKitBackground.shader doesn't have the _MainTex property, so Graphics.Blit() with camera material produces green texture.
        /// <seealso cref="CameraSubsystemSenderCpuImage.TrySerializeCameraTextureFromCpuImage"/>
        [CanBeNull]
        Material getMaterialForTextureBlit() {
            if (Defines.isAndroid) {
                return Sender.Instance.arcoreOriginalUVsMaterial;
            }
            
            var camMat = cameraManager.cameraMaterial;
            if (camMat.HasProperty("_MainTex")) {
                return camMat;
            } else {
                return null;
            }
        }

        void OnEnable() {
            #if ARFOUNDATION_4_0_OR_NEWER
            Connection.Register<RequestedCameraData>(requestedCameraData => {
                var requestedFacingDirection = (CameraFacingDirection) requestedCameraData.requestedCamera;
                Sender.logSceneSpecific("received requestedFacingDirection " + requestedFacingDirection);
                cameraManager.requestedFacingDirection = requestedFacingDirection;
                CheckSixDegreesOfFreedomBug(cameraManager, session);
            });
            
            Connection.Register<TrackingModeData>(trackingModeData => {
                var requestedTrackingMode = trackingModeData.trackingMode.ToTrackingMode();
                Sender.logSceneSpecific($"receive requestedTrackingMode {requestedTrackingMode}");
                session.requestedTrackingMode = requestedTrackingMode;
                CheckSixDegreesOfFreedomBug(cameraManager, session);
            });
            #endif
            
            Connection.Register<LightEstimationDataEditor>(cameraManager.SetLightEstimation);
            Connection.Register<CameraDataEditor>(receiveCameraData);
        }

        void OnDisable() {
            #if ARFOUNDATION_4_0_OR_NEWER
            Connection.UnRegister<RequestedCameraData>();
            Connection.UnRegister<TrackingModeData>();
            #endif
            Connection.UnRegister<LightEstimationDataEditor>();
            Connection.UnRegister<CameraDataEditor>();
        }
        
          
        #if ARFOUNDATION_4_0_OR_NEWER
        [Serializable]
        public struct RequestedCameraData {
            public Feature requestedCamera;
        }


        [Serializable]
        public struct TrackingModeData {
            public Feature trackingMode;
        }
        #endif


        string PropIdToName(int id) {
            var found = PropIdToName(id, out var result);
            Assert.IsTrue(found, "PropIdToName not found id {id");
            return result;
        }

        public bool PropIdToName(int id, out string propName) {
            cachePropNamesIfNeeded();
            return propIdsAndNamesDict.TryGetValue(id, out propName);
        }

        void cachePropNamesIfNeeded() {
            if (propIdsAndNamesDict == null) {
                propIdsAndNamesDict = new Dictionary<int, string>();
                foreach (var propName in cameraManager.subsystem.cameraMaterial.GetTexturePropertyNames()) {
                    var id = Shader.PropertyToID(propName);
                    propIdsAndNamesDict[id] = propName;
                }
            }
        }

        void receiveCameraData(CameraDataEditor cameraData) {
            var request = cameraData.request;
            if (request.HasValue) {
                void send(CameraData _) {
                    Connection.SendResponse(_, cameraData);
                }

                switch (request.Value) {
                    case CameraDataEditorRequest.GetAllConfigurations:
                        send(new CameraData {
                            allConfigurations = serializeConfigurations()
                        });
                        break;
                    case CameraDataEditorRequest.GetCurrentConfiguration:
                        send(new CameraData {
                            currentConfiguration = serializeCurrentConfiguration()
                        });
                        break;
                    case CameraDataEditorRequest.SetCurrentConfiguration:
                        waitForAsyncConversionsToFinish(() => {
                            string error = null;
                            var configToSet = cameraData.configToSet?.Value;
                            try {
                                cameraManager.currentConfiguration = configToSet;
                            } catch (Exception e) {
                                error = e.ToString();
                            }

                            Texture2DSerializable.ClearCache();
                            send(new CameraData {
                                error = error
                            });
                        });
                        break;
                    default:
                        throw new Exception();
                }
            }

            var autofocus = cameraData.enableAutofocus;
            if (autofocus.HasValue) {
                log($"cameraManager.SetCameraAutoFocus {autofocus.Value}");
                cameraManager.SetCameraAutoFocus(autofocus.Value);
            }

            var enableCameraManager = cameraData.enableCameraManager;
            if (enableCameraManager.HasValue) {
                Sender.Instance.SetManagerEnabled(cameraManager, enableCameraManager.Value);
            }
        }

        public static void CheckSixDegreesOfFreedomBug() {
            var cameraManager = UnityEngine.Object.FindObjectOfType<ARCameraManager>();
            if (cameraManager != null) {
                var session = UnityEngine.Object.FindObjectOfType<ARSession>();
                if (session != null) {
                    CheckSixDegreesOfFreedomBug(cameraManager, session);
                }
            }
        }

        static void CheckSixDegreesOfFreedomBug(ARCameraManager cameraManager, ARSession session) {
            #if ARFOUNDATION_4_0_OR_NEWER
            if (Defines.isIOS) {
                if (checkBug(cameraManager.requestedFacingDirection, session.requestedTrackingMode, "requested") || checkBug(cameraManager.currentFacingDirection, session.currentTrackingMode, "current")) {
                    // the correct fix is to check the session configuration before applying it
                    Debug.LogError(
                        @"Bug in ARKit: camera position and rotation will not receive updates with world camera and session state will always be <= SessionInitializing if all these conditions are met:
	- AR Foundation >= 4.0;
	- Your AR device supports face tracking with 6 degrees of freedom;
    - ARFaceManager.enabled == true;
        EITHER
    - Light estimation is enabled AND scene is switching from world to user facing camera;
        OR
	- ARCameraManager.currentFacingDirection == CameraFacingDirection.User;
	- ARSession.currentTrackingMode == TrackingMode.PositionAndRotation;
");
                }
            }

            bool checkBug(CameraFacingDirection facingDirection, TrackingMode trackingMode, string debugStr) {
                var result = facingDirection == CameraFacingDirection.User && trackingMode == TrackingMode.PositionAndRotation;
                if (result) {
                    Debug.LogError($"{debugStr} facingDirection == CameraFacingDirection.User && {debugStr} trackingMode == TrackingMode.PositionAndRotation");
                }
                        
                return result;
            }
            #endif
        }
        
        [Conditional("_")]
        public static void log(string log) {
            Debug.Log(log);
        }
   
        [Conditional("_")]
        public static void logScreenOrientation(string log) {
            Debug.Log(log);
        }

        void Update() {
            if (Sender.isConnected) {
                var newOrientation = Screen.orientation;
                if (newOrientation != currentOrientation) {
                    currentOrientation = newOrientation;
                    logScreenOrientation($"send orientation {newOrientation}");
                    Connection.Send(new CameraData {
                        screenOrientation = newOrientation,
                        screenResolution = Vector2IntSerializable.Create(new Vector2Int(Screen.width, Screen.height))
                    });
                } 
            }
        }

        void waitForAsyncConversionsToFinish(Action action) {
            DontDestroyOnLoadSingleton.AddCoroutine(waitForAsyncConversionsToFinishCor(action), nameof(waitForAsyncConversionsToFinishCor));
        }

        IEnumerator waitForAsyncConversionsToFinishCor([NotNull] Action action) {
            canRunAsyncConversion = false;
            while (asyncConversionCoroutineNames.Any()) {
                yield return null;
            }

            action();
            canRunAsyncConversion = true;
        }
        
        /// ARCore can't set currentConfiguration while image conversion is running:
        /// Exception: System.InvalidOperationException: Cannot set camera configuration because you have not disposed of all XRCpuImage and allowed all asynchronous conversion jobs to complete
        public bool canRunAsyncConversion { get; private set; } = true;

        readonly List<string> asyncConversionCoroutineNames = new List<string>();

        public void AddAsyncConversionCoroutine(IEnumerator cor, string name) {
            DontDestroyOnLoadSingleton.AddCoroutine(asyncConversionCoroutine(cor, name), name);
        }

        IEnumerator asyncConversionCoroutine(IEnumerator cor, string name) {
            asyncConversionCoroutineNames.Add(name);
            while (cor.MoveNext()) {
                yield return cor.Current;
            }

            var removed = asyncConversionCoroutineNames.Remove(name);
            Assert.IsTrue(removed);
        }

        [Conditional("_")]
        void logConfigs(string s) {
            Debug.Log($"{nameof(CameraSubsystemSender)} log configs: {s}");
        }
        
        CameraConfigurationSerializable? serializeCurrentConfiguration() {
            var config = cameraManager.currentConfiguration;
            if (config.HasValue) {
                return CameraConfigurationSerializable.Create(config.Value);
            } else {
                return null;
            }
        }

        CameraConfigurations serializeConfigurations() {
            if (cameraManager.descriptor.supportsCameraConfigurations) {
                using (var configs = cameraManager.GetConfigurations(Allocator.Temp)) {
                    return new CameraConfigurations {
                        isSupported = true,
                        configs = configs.Select(CameraConfigurationSerializable.Create).ToArray()
                    };
                }
            } else {
                return new CameraConfigurations {
                    isSupported = false
                };
            }
        }
    }
    

    public static class DebugUtils {
        static readonly HashSet<string> logs = new HashSet<string>();
            
        public static bool LogOnce(string msg) {
            if (IsFirstOccurrence(msg)) {
                Debug.Log(msg);
                return true;
            } else {
                return false;
            }
        }

        public static bool IsFirstOccurrence(string msg) {
            return logs.Add(msg);
        }
    }

    
    [Serializable]
    public struct CameraConfigurationSerializable {
        static readonly Reflector<XRCameraConfiguration> reflector = new Reflector<XRCameraConfiguration>();
        
        public Vector2IntSerializable m_Resolution;
        public int m_Framerate;
        public IntPtr m_NativeConfigurationHandle;

        
        public static CameraConfigurationSerializable? Create(XRCameraConfiguration? _) {
            if (_.HasValue) {
                return Create(_.Value);
            } else {
                return null;
            }
        }

        public static CameraConfigurationSerializable Create(XRCameraConfiguration _) {
            return new CameraConfigurationSerializable {
                m_Resolution = Vector2IntSerializable.Create(_.resolution),
                m_Framerate = _.framerate ?? 0,
                #if ARFOUNDATION_4_0_OR_NEWER
                    m_NativeConfigurationHandle = _.nativeConfigurationHandle
                #endif
            };
        }

        public XRCameraConfiguration Value {
            get {
                var result = reflector.GetResultBuilder();
                result.SetField(nameof(m_Resolution), m_Resolution.Deserialize());
                result.SetField(nameof(m_Framerate), m_Framerate);
                #if ARFOUNDATION_4_0_OR_NEWER
                    result.SetField(nameof(m_NativeConfigurationHandle), m_NativeConfigurationHandle);
                #endif
                return result.Result;
            }
        }
    }

    [Serializable]
    public struct CameraConfigurations {
        [CanBeNull] public CameraConfigurationSerializable[] configs;
        public bool isSupported;
    }

    
    [Serializable]
    public struct ARCameraFrameEventArgsSerializable {
        [NotNull] public PropIdAndMaybeTexture[] textures;
        public XRCameraFrameSerializable frame;
        public bool invertCulling;
    }

    [Serializable]
    public struct XRCameraFrameSerializable {
        long timestampNs;
        float averageBrightness;
        float averageColorTemperature;
        ColorSerializable colorCorrection;
        Matrix4x4Serializable projectionMatrix;
        Matrix4x4Serializable displayMatrix;
        TrackingState trackingState;
        XRCameraFrameProperties properties;
        float averageIntensityInLumens;
        double exposureDuration;
        float exposureOffset;
        #if ARFOUNDATION_4_0_OR_NEWER
            float m_MainLightIntensityLumens;
            ColorSerializable m_MainLightColor;
            Vector3Serializable m_MainLightDirection;
            SphericalHarmonicsL2Serializable m_AmbientSphericalHarmonics;
            // float noiseIntensity; // available only in AR Foundation >= 4.0.2
        #endif


        public static XRCameraFrameSerializable Create(ARCameraFrameEventArgs a) {
            var lightEstimation = a.lightEstimation;
            return new XRCameraFrameSerializable {
                timestampNs = a.timestampNs ?? 0,
                averageBrightness = lightEstimation.averageBrightness ?? 0,
                averageColorTemperature = lightEstimation.averageColorTemperature ?? 0,
                colorCorrection = ColorSerializable.Create(lightEstimation.colorCorrection ?? new Color()),
                projectionMatrix = Matrix4x4Serializable.Create(a.projectionMatrix ?? Matrix4x4.zero),
                displayMatrix = Matrix4x4Serializable.Create(a.displayMatrix ?? Matrix4x4.zero),
                trackingState = TrackingState.Tracking,
                properties = getProps(a),
                averageIntensityInLumens = lightEstimation.averageIntensityInLumens ?? 0,
                exposureDuration = a.exposureDuration ?? 0,
                exposureOffset = a.exposureOffset ?? 0,
                #if ARFOUNDATION_4_0_OR_NEWER
                    m_MainLightIntensityLumens = lightEstimation.mainLightIntensityLumens ?? 0,
                    m_MainLightColor = ColorSerializable.Create(lightEstimation.mainLightColor ?? new Color()),
                    m_MainLightDirection = Vector3Serializable.Create(lightEstimation.mainLightDirection ?? Vector3.zero),
                    m_AmbientSphericalHarmonics = SphericalHarmonicsL2Serializable.Create(lightEstimation.ambientSphericalHarmonics ?? new UnityEngine.Rendering.SphericalHarmonicsL2()),
                    // noiseIntensity = a.noiseIntensity
                #endif
            };
        }

        static XRCameraFrameProperties getProps(ARCameraFrameEventArgs a) {
            var lightEstimation = a.lightEstimation;
            var props = new XRCameraFrameProperties();
            if (a.timestampNs.HasValue) {
                props |= XRCameraFrameProperties.Timestamp;
            }

            if (lightEstimation.averageBrightness.HasValue) {
                props |= XRCameraFrameProperties.AverageBrightness;
            }

            if (lightEstimation.averageColorTemperature.HasValue) {
                props |= XRCameraFrameProperties.AverageColorTemperature;
            }

            if (lightEstimation.colorCorrection.HasValue) {
                props |= XRCameraFrameProperties.ColorCorrection;
            }
            
            if (a.projectionMatrix.HasValue) {
                props |= XRCameraFrameProperties.ProjectionMatrix;
            }
            
            if (a.displayMatrix.HasValue) {
                props |= XRCameraFrameProperties.DisplayMatrix;
            }
            
            if (lightEstimation.averageIntensityInLumens.HasValue) {
                props |= XRCameraFrameProperties.AverageIntensityInLumens;
            }
            
            if (a.exposureDuration.HasValue) {
                props |= XRCameraFrameProperties.ExposureDuration;
            }
            
            if (a.exposureOffset.HasValue) {
                props |= XRCameraFrameProperties.ExposureOffset;
            }
            
            #if ARFOUNDATION_4_0_OR_NEWER
                if (lightEstimation.mainLightDirection.HasValue) {
                    props |= XRCameraFrameProperties.MainLightDirection;
                }

                if (lightEstimation.mainLightColor.HasValue) {
                    props |= XRCameraFrameProperties.MainLightColor;
                }

                if (lightEstimation.mainLightIntensityLumens.HasValue) {
                    props |= XRCameraFrameProperties.MainLightIntensityLumens;
                }

                if (lightEstimation.ambientSphericalHarmonics.HasValue) {
                    props |= XRCameraFrameProperties.AmbientSphericalHarmonics;
                }

                // props |= XRCameraFrameProperties.NoiseIntensity;
            #endif

            return props;
        }

        public XRCameraFrame Value {
            get {
                object boxed = new XRCameraFrame();
                setValue(boxed, "m_TimestampNs", timestampNs);
                setValue(boxed, "m_AverageBrightness", averageBrightness);
                setValue(boxed, "m_AverageColorTemperature", averageColorTemperature);
                setValue(boxed, "m_ColorCorrection", colorCorrection.Value);
                setValue(boxed, "m_ProjectionMatrix", projectionMatrix.Value);
                if (Settings.Instance.debugSettings.modifyDisplayMatrix) {
                    setValue(boxed, "m_DisplayMatrix", Settings.Instance.debugSettings.displayMatrix);
                } else {
                    Settings.Instance.debugSettings.displayMatrix = displayMatrix.Value;
                    setValue(boxed, "m_DisplayMatrix", displayMatrix.Value);
                }
                
                setValue(boxed, "m_TrackingState", trackingState);
                setValue(boxed, "m_Properties", properties);
                setValue(boxed, "m_AverageIntensityInLumens", averageIntensityInLumens);
                setValue(boxed, "m_ExposureDuration", exposureDuration);
                setValue(boxed, "m_ExposureOffset", exposureOffset);
                #if ARFOUNDATION_4_0_OR_NEWER
                    setValue(boxed, "m_MainLightIntensityLumens", m_MainLightIntensityLumens);
                    setValue(boxed, "m_MainLightColor", m_MainLightColor.Value);
                    setValue(boxed, "m_MainLightDirection", m_MainLightDirection.Value);
                    setValue(boxed, "m_AmbientSphericalHarmonics", m_AmbientSphericalHarmonics.Value);
                    // setValue(boxed, "m_NoiseIntensity", noiseIntensity);
                #endif
                return (XRCameraFrame) boxed;
            }
        }
        
        static readonly Dictionary<string, FieldInfo> fields = typeof(XRCameraFrame).GetFields(BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic).ToDictionary(_ => _.Name);

        static void setValue(object obj, string fieldName, object val) {
            fields[fieldName].SetValue(obj, val);
        }
    }

    [Serializable]
    struct Matrix4x4Serializable {
        Vector4Serializable v1, v2, v3, v4;
        
        
        public static Matrix4x4Serializable Create(Matrix4x4 m) {
            return new Matrix4x4Serializable {
                v1 = Vector4Serializable.Create(m.GetColumn(0)),
                v2 = Vector4Serializable.Create(m.GetColumn(1)),
                v3 = Vector4Serializable.Create(m.GetColumn(2)),
                v4 = Vector4Serializable.Create(m.GetColumn(3))
            };
        }
        
        public Matrix4x4 Value => new Matrix4x4(v1.Value,v2.Value,v3.Value,v4.Value);
    }
    
    [Serializable]
    public struct Vector4Serializable {
        float x, y, z, w;

        public static Vector4Serializable Create(Vector4 v) {
            return new Vector4Serializable {
                x = v.x,
                y = v.y,
                z = v.z,
                w = v.w
            };
        }

        public Vector4 Value => new Vector4(x, y, z, w);
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ColorSerializable {
        float r, g, b, a;

        public static ColorSerializable Create(Color c) {
            return new ColorSerializable {
                r = c.r,
                g = c.g,
                b = c.b,
                a = c.a
            };
        }

        public Color Value => new Color(r, g, b, a);
    }

    
    [Serializable]
    public class CameraData : BlockingMessage {
        public CameraConfigurations? allConfigurations;
        public CameraConfigurationSerializable? currentConfiguration;
        [CanBeNull] public string error;
        public ScreenOrientation? screenOrientation;
        public Vector2IntSerializable? screenResolution;
        public ColorSpace? colorSpace;
    }


    [Serializable]
    public struct CpuImageData {
        public XRCpuImageSerializable? cpuImage;
        public ConvertedCpuImage? convertedImage;
    }
    
    
    [Serializable]
    public class CameraDataEditor : BlockingMessage {
        public CameraDataEditorRequest? request;
        public CameraConfigurationSerializable? configToSet;
        public bool? enableAutofocus;
        public bool? enableCameraManager;
    }

    
    [Serializable]
    public struct CameraDataCpuImagesEditor {
        public bool? enableCpuImages;
        public ConversionParamsSerializable? conversionParams;
    }

    
    public enum CameraDataEditorRequest {
        GetAllConfigurations,
        GetCurrentConfiguration,
        SetCurrentConfiguration
    }
    
        
    [Serializable]
    public readonly struct SerializedTextureAndPropId {
        public readonly Texture2DSerializable texture;
        [CanBeNull] public readonly string propName;
        readonly int propertyNameId;

        
        public SerializedTextureAndPropId(Texture2DSerializable texture, [CanBeNull] string propName, int propertyNameId) {
            this.texture = texture;
            this.propName = propName;
            this.propertyNameId = propertyNameId;
        }
        
        public int GetPropertyNameId() {
            return propName != null ? Shader.PropertyToID(propName) : propertyNameId;
        }
    }
        
    
    [Serializable]
    public struct PropIdAndMaybeTexture {
        public Texture2DSerializable? texture;
        [NotNull] public string propName;
    }


    [Serializable]
    public class LightEstimationDataEditor {
        [SerializeField] bool ambientIntensity;
        [SerializeField] bool ambientColor;
        [SerializeField] bool ambientSphericalHarmonics;
        [SerializeField] bool mainLightDirection;
        [SerializeField] bool mainLightIntensity;
        #pragma warning disable 649
        [SerializeField] bool environmentalHDR;
        #pragma warning restore 649


        LightEstimationDataEditor() {
        }
        
        #if ARFOUNDATION_4_0_OR_NEWER
        public static LightEstimationDataEditor FromFeature(Feature value) {
            return new LightEstimationDataEditor {
                ambientIntensity = value.HasFlag(Feature.LightEstimationAmbientIntensity),
                ambientColor = value.HasFlag(Feature.LightEstimationAmbientColor),
                ambientSphericalHarmonics = value.HasFlag(Feature.LightEstimationAmbientSphericalHarmonics),
                mainLightDirection = value.HasFlag(Feature.LightEstimationMainLightDirection),
                mainLightIntensity = value.HasFlag(Feature.LightEstimationMainLightIntensity)
            };
        }

        public Feature ToFeature() {
            var result = Feature.None;
            if (ambientIntensity) {
                result |= Feature.LightEstimationAmbientIntensity;
            }

            if (ambientColor) {
                result |= Feature.LightEstimationAmbientColor;
            }

            if (ambientSphericalHarmonics) {
                result |= Feature.LightEstimationAmbientSphericalHarmonics;
            }

            if (mainLightDirection) {
                result |= Feature.LightEstimationMainLightDirection;
            }

            if (mainLightIntensity) {
                result |= Feature.LightEstimationMainLightIntensity;
            }

            return result;
        }
        #endif

        public static LightEstimationDataEditor FromLightEstimationMode(LightEstimationMode mode) {
            return new LightEstimationDataEditor {
                ambientIntensity = mode.HasFlag(LightEstimationMode.AmbientIntensity),
                #if ARFOUNDATION_4_0_0_PREVIEW_1
                environmentalHDR = mode.HasFlag(LightEstimationMode.EnvironmentalHDR)
                #endif
            };
        }

        public LightEstimationMode ToLightEstimationMode() {
            if (ambientIntensity) {
                return LightEstimationMode.AmbientIntensity;
            }

            if (environmentalHDR) {
                #if ARFOUNDATION_4_0_0_PREVIEW_1
                return LightEstimationMode.EnvironmentalHDR;
                #endif
            }
            
            return LightEstimationMode.Disabled;
        }
    }
}
