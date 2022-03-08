#if ARFOUNDATION_4_0_2_OR_NEWER
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;


// This errors also appears in real debug builds, so this is not the problem with plugin itself:
// Texture descriptor dimension should not change from None to Tex2D.
// UnityEngine.XR.ARFoundation.Samples.DisplayDepthImage:Update() (at Assets/Scripts/DisplayDepthImage.cs:250)
namespace ARFoundationRemote.Editor {
    public class OcclusionSubsystem : XROcclusionSubsystem {
        [CanBeNull] static TextureAndDescriptor 
            humanStencil,
            humanDepth,
            #pragma warning disable 649
            environmentDepth,
            environmentDepthConfidence;
            #pragma warning restore
        static bool enabled = true;
        [CanBeNull] static Material _cameraMaterial;
        
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.IsPluginEnabled()) {
                return;
            }

            log("RegisterDescriptor");
            var isIOS = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
            var type = typeof(OcclusionSubsystem);
            var cinfo = new XROcclusionSubsystemCinfo {
                id = nameof(OcclusionSubsystem),
                #if UNITY_2020_2_OR_NEWER
                providerType = typeof(OcclusionSubsystemProvider),
                subsystemTypeOverride = type,
                #else
                    implementationType = type,
                #endif
                #if AR_SUBSYSTEMS_4_2_0_pre_2
                    humanSegmentationDepthImageSupportedDelegate = () => isIOS ? Supported.Supported : Supported.Unsupported,
                    humanSegmentationStencilImageSupportedDelegate = () => isIOS ? Supported.Supported : Supported.Unsupported,
                    environmentDepthImageSupportedDelegate = () => Supported.Supported, // iOS 14 is required
                    environmentDepthConfidenceImageSupportedDelegate = () => Supported.Supported
                #endif
            };
            
            if (!Defines.AR_SUBSYSTEMS_4_2_0_pre_2) {
                #pragma warning disable 618
                cinfo.supportsHumanSegmentationDepthImage = isIOS;
                cinfo.supportsHumanSegmentationStencilImage = isIOS;
                #if ARFOUNDATION_4_1_OR_NEWER
                    cinfo.queryForSupportsEnvironmentDepthImage = () => true; // iOS 14 is required
                    cinfo.queryForSupportsEnvironmentDepthConfidenceImage = () => true;
                #endif
                #pragma warning restore 618
            }
            
            Register(cinfo);
        }

        public OcclusionSubsystem() {
            log("OcclusionSubsystem");
        }
        
        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new OcclusionSubsystemProvider();
        #endif
        
        
        class OcclusionSubsystemProvider : Provider {
            public override void Start() {
                enableRemoteManager(true);
                Connection.Register<OcclusionData>(occlusionData => {
                    tryDeserialize(occlusionData.humanStencil, ref humanStencil);
                    tryDeserialize(occlusionData.humanDepth, ref humanDepth);
                });
                
                #if ARFOUNDATION_4_1_OR_NEWER
                Connection.Register<OcclusionDataDepth>(occlusionData => {
                    tryDeserialize(occlusionData.environmentDepth, ref environmentDepth);
                    tryDeserialize(occlusionData.environmentDepthConfidence, ref environmentDepthConfidence);    
                });
                #endif
            }

            public override void Stop() {
                enableRemoteManager(false);
                Connection.UnRegister<OcclusionData>();
                #if ARFOUNDATION_4_1_OR_NEWER
                Connection.UnRegister<OcclusionDataDepth>();
                #endif
            }

            static IEnumerable<TextureAndDescriptor> getNonNullTextures() {
                return new[] {humanStencil, humanDepth, environmentDepth, environmentDepthConfidence}.Where(_ => _ != null);
            }

            public override void Destroy() {
                foreach (var _ in getNonNullTextures()) {
                    _.DestroyTexture();
                }
            }

            void enableRemoteManager(bool enable) {
                Connection.Send(new OcclusionDataEditor {
                    enableOcclusion = enable
                });
            }

            HumanSegmentationStencilMode _humanStencilMode;
            public override HumanSegmentationStencilMode requestedHumanStencilMode {
                get => _humanStencilMode;
                set {
                    if (_humanStencilMode != value) {
                        _humanStencilMode = value;
                        Connection.Send(new OcclusionDataEditor {
                            requestedHumanStencilMode = value
                        });
                    }
                }
            }

            public override HumanSegmentationStencilMode currentHumanStencilMode => _humanStencilMode;

            HumanSegmentationDepthMode _humanDepthMode;
            public override HumanSegmentationDepthMode requestedHumanDepthMode {
                get => _humanDepthMode;
                set {
                    if (_humanDepthMode != value) {
                        _humanDepthMode = value;
                        Connection.Send(new OcclusionDataEditor {
                            requestedHumanDepthMode = value
                        });
                    }
                }
            }

            public override HumanSegmentationDepthMode currentHumanDepthMode => _humanDepthMode;

            public override bool TryGetHumanStencil(out XRTextureDescriptor descriptor) {
                return tryGetDescriptor(out descriptor, humanStencil);
            }

            public override bool TryGetHumanDepth(out XRTextureDescriptor descriptor) {
                return tryGetDescriptor(out descriptor, humanDepth);
            }

            static bool tryGetDescriptor(out XRTextureDescriptor descriptor, [CanBeNull] TextureAndDescriptor pair) {
                if (pair != null) {
                    var _ = pair.descriptor;
                    if (_.HasValue) {
                        descriptor = _.Value;
                        return true;    
                    }
                }
                
                descriptor = default;
                return false;
            }

            public override NativeArray<XRTextureDescriptor> GetTextureDescriptors(XRTextureDescriptor defaultDescriptor, Allocator allocator) {
                    var result = getNonNullTextures()
                        .Select(_ => _.descriptor)
                        .Where(_ => _.HasValue)
                        .Select(_ => _.Value)
                        .ToArray();
                    
                    return new NativeArray<XRTextureDescriptor>(result, allocator);
            }

            public override void GetMaterialKeywords(out List<string> enabledKeywords, out List<string> disabledKeywords) {
                switch (EditorUserBuildSettings.activeBuildTarget) {
                    case BuildTarget.iOS:
                        getIOSKeywords(out enabledKeywords, out disabledKeywords);
                        break;
                    case BuildTarget.Android:
                        getAndroidKeywords(out enabledKeywords, out disabledKeywords);
                        break;
                    default:
                        enabledKeywords = null;
                        disabledKeywords = null;
                        break;
                }
            }

            void getAndroidKeywords(out List<string> enabledKeywords, out List<string> disabledKeywords) {
                if (IsEnvDepthEnabled())
                {
                    enabledKeywords = AndroidKeywords.m_EnvironmentDepthEnabledMaterialKeywords;
                    disabledKeywords = null;
                }
                else
                {
                    enabledKeywords = null;
                    disabledKeywords = AndroidKeywords.m_EnvironmentDepthEnabledMaterialKeywords;
                }
            }

            static class AndroidKeywords {
                public static readonly List<string> m_EnvironmentDepthEnabledMaterialKeywords = new List<string>() {k_EnvironmentDepthEnabledMaterialKeyword};
                const string k_EnvironmentDepthEnabledMaterialKeyword = "ARCORE_ENVIRONMENT_DEPTH_ENABLED";
            }
            
            void getIOSKeywords(out List<string> enabledKeywords, out List<string> disabledKeywords) {
                bool isHumanDepthEnabled = currentHumanDepthMode != HumanSegmentationDepthMode.Disabled || currentHumanStencilMode != HumanSegmentationStencilMode.Disabled;

                if (IsEnvDepthEnabled()
                    && (!isHumanDepthEnabled
                        || isEnvironmentOcclusionPreferred()))
                {
                    enabledKeywords = IOSKeywords.m_EnvironmentDepthEnabledMaterialKeywords;
                    disabledKeywords = IOSKeywords.m_HumanEnabledMaterialKeywords;
                }
                else if (isHumanDepthEnabled)
                {
                    enabledKeywords = IOSKeywords.m_HumanEnabledMaterialKeywords;
                    disabledKeywords = IOSKeywords.m_EnvironmentDepthEnabledMaterialKeywords;
                }
                else
                {
                    enabledKeywords = null;
                    disabledKeywords = IOSKeywords.m_AllDisabledMaterialKeywords;
                }
            }

            bool isEnvironmentOcclusionPreferred() {
                #if ARFOUNDATION_4_1_OR_NEWER
                    return currentOcclusionPreferenceMode == OcclusionPreferenceMode.PreferEnvironmentOcclusion;
                #else
                    return false;
                #endif
            }

            bool IsEnvDepthEnabled() {
                #if ARFOUNDATION_4_1_OR_NEWER
                    return currentEnvironmentDepthMode != EnvironmentDepthMode.Disabled;
                #else
                    return false;
                #endif
            }

            static class IOSKeywords {
                public static readonly List<string> m_EnvironmentDepthEnabledMaterialKeywords = new List<string>() {k_EnvironmentDepthEnabledMaterialKeyword};
                const string k_EnvironmentDepthEnabledMaterialKeyword = "ARKIT_ENVIRONMENT_DEPTH_ENABLED";
                public static readonly List<string> m_HumanEnabledMaterialKeywords = new List<string>() {k_HumanEnabledMaterialKeyword};
                const string k_HumanEnabledMaterialKeyword = "ARKIT_HUMAN_SEGMENTATION_ENABLED";
                public static readonly List<string> m_AllDisabledMaterialKeywords = new List<string>() {k_HumanEnabledMaterialKeyword, k_EnvironmentDepthEnabledMaterialKeyword};
            }

            #if ARFOUNDATION_4_1_OR_NEWER
            EnvironmentDepthMode _environmentDepthMode;
            public override EnvironmentDepthMode currentEnvironmentDepthMode => _environmentDepthMode;
            public override EnvironmentDepthMode requestedEnvironmentDepthMode {
                get => _environmentDepthMode;
                set {
                    if (_environmentDepthMode != value) {
                        _environmentDepthMode = value;
                        Connection.Send(new OcclusionDataDepthEditor {
                            requestedEnvironmentDepthMode = value
                        });
                    }
                }
            }

            OcclusionPreferenceMode _occlusionPreferenceMode;
            public override OcclusionPreferenceMode currentOcclusionPreferenceMode => _occlusionPreferenceMode;
            public override OcclusionPreferenceMode requestedOcclusionPreferenceMode {
                get => _occlusionPreferenceMode;
                set {
                    if (_occlusionPreferenceMode != value) {
                        _occlusionPreferenceMode = value;
                        Connection.Send(new OcclusionDataDepthEditor {
                            requestedOcclusionPreferenceMode = value
                        });
                    }
                }
            }

            public override bool TryGetEnvironmentDepth(out XRTextureDescriptor descriptor) {
                return tryGetDescriptor(out descriptor, environmentDepth);
            }

            public override bool TryGetEnvironmentDepthConfidence(out XRTextureDescriptor descriptor) {
                return tryGetDescriptor(out descriptor, environmentDepthConfidence);
            }
            #endif
        }

        [CanBeNull]
        static Material cameraMaterial {
            get {
                if (_cameraMaterial == null) {
                    var bg = Object.FindObjectOfType<ARCameraBackground>();
                    if (bg != null) {
                        _cameraMaterial = bg.material;
                    }
                }

                return _cameraMaterial;
            }
        }
        
        static void tryDeserialize(SerializedTextureAndPropId? ser, [CanBeNull] ref TextureAndDescriptor cached) {
            if (!enabled) {
                return;
            }
            
            if (ser.HasValue) {
                var value = ser.Value;
                if (cameraMaterial != null) {
                    var propName = value.propName;
                    if (propName != null && !SupportCheck.CheckCameraAndOcclusionSupport(cameraMaterial, propName)) {
                        enabled = false;
                        return;
                    }
                }
                
                if (cached == null) {
                    cached = new TextureAndDescriptor();
                }
                
                cached.Update(value);
            }
        }

        [Conditional("_")]
        static void log(string msg) {
            Debug.Log(msg);
        }
    }
}
#endif
