#if ARFOUNDATION_4_0_2_OR_NEWER
    using XRCameraImageConversionParams = UnityEngine.XR.ARSubsystems.XRCpuImage.ConversionParams;
#else
    using XRCameraImageConversionParams = UnityEngine.XR.ARSubsystems.XRCameraImageConversionParams;
#endif
using System;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using Object = UnityEngine.Object;


namespace ARFoundationRemote.Runtime {
    public static class CameraSubsystemSenderCpuImage {
        [CanBeNull] static Texture2D texture;
        
        /// ARCoreBackground.shader transforms texture's UVs in this line:
        ///     textureCoord = (_UnityDisplayTransform * vec4(gl_MultiTexCoord0.x, 1.0f - gl_MultiTexCoord0.y, 1.0f, 0.0f)).xy;
        ///     Texture's UVs will not correct even if the <see cref="ARCameraBackground.k_DisplayTransformName"/> property is set to <see cref="Matrix4x4.identity"/> before serialization
        ///         because shader also flips the Y axis
        ///
        /// While CPU image API has greater performance, it supplies original texture from camera sensor,
        ///     and this texture is not the same size as the texture from <see cref="ARCameraFrameEventArgs.textures"/>
        /// 
        /// iOS uses two video textures (TextureY, TextureCbCr), so CPU image API can't also be used on iOS
        ///
        /// So the solution I use is a custom ARCoreBackgroundOriginalUVs.shader that produces texture with unmodified UVs before sending the camera texture back to the Editor
        ///     Then, the ARCoreBackgroundEditor.shader mimics the ARCoreBackground.shader by applying the displayMatrix and Y axis inversion as the original shader does 
        /// <seealso cref="ARCameraFrameEventArgsSerializable.getMaterialForTextureBlit"/>
        [CanBeNull]
        public static PropIdAndMaybeTexture[] TrySerializeCameraTextureFromCpuImage(ARCameraManager cameraManager) {
            const string mainTexPropName = "_MainTex";
            if (!cameraManager.cameraMaterial.HasProperty(mainTexPropName)) {
                return null;
            }

            if (cameraManager.TryAcquireLatestCpuImageVersionAgnostic(out var cpuImage)) {
                using (cpuImage) {
                    var format = TextureFormat.RGB24;
                    var fullWidth = cpuImage.width;
                    var fullHeight = cpuImage.height;
                    var textureScale = Settings.cameraVideoSettings.resolutionScale;
                    var downsizedWidth = Mathf.RoundToInt(fullWidth * textureScale);
                    var downsizedHeight = Mathf.RoundToInt(fullHeight * textureScale);
                    var conversionParams = new XRCameraImageConversionParams {
                        inputRect = new RectInt(0, 0, fullWidth, fullHeight),
                        outputDimensions = new Vector2Int(downsizedWidth, downsizedHeight),
                        outputFormat = format
                    };

                    var convertedDataSize = tryGetConvertedDataSize();
                    if (convertedDataSize.HasValue) {
                        using (var buffer = new NativeArray<byte>(convertedDataSize.Value, Allocator.Temp)) {
                            if (tryConvert()) {
                                if (texture != null) {
                                    if (texture.width != downsizedWidth || texture.height != downsizedHeight || texture.format != format) {
                                        Object.Destroy(texture);
                                        texture = null;
                                    }
                                }

                                if (texture == null) {
                                    texture = new Texture2D(downsizedWidth, downsizedHeight, format, false);
                                }
                                
                                Assert.IsNotNull(texture);
                                texture.LoadRawTextureData(buffer);
                                return new[] {
                                    new PropIdAndMaybeTexture {
                                        propName = mainTexPropName,
                                        texture = new Texture2DSerializable(
                                            texture.EncodeToJPG(Settings.cameraVideoSettings.quality),
                                            downsizedWidth,
                                            downsizedHeight,
                                            format,
                                            true
                                        )
                                    }
                                };
                            }

                            bool tryConvert() {
                                try {
                                    cpuImage.ConvertSync(conversionParams, buffer);
                                    return true;
                                } catch (Exception e) {
                                    processException(e);
                                    return false;
                                }
                            }
                        }
                    }

                    int? tryGetConvertedDataSize() {
                        try {
                            return cpuImage.GetConvertedDataSize(conversionParams);
                        } catch (Exception e) {
                            processException(e);
                            return null;
                        }
                    }

                    void processException(Exception e) {
                        Debug.LogError(e);
                    }
                }
            }

            return null;
        }
    }
}
