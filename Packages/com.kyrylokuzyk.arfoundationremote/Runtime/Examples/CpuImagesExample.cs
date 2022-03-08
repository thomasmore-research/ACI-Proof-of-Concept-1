#if ARFOUNDATION_4_0_2_OR_NEWER
    using XRCameraImageConversionParams = UnityEngine.XR.ARSubsystems.XRCpuImage.ConversionParams;
    using CameraImageTransformation = UnityEngine.XR.ARSubsystems.XRCpuImage.Transformation;
    using AsyncCameraImageConversionStatus = UnityEngine.XR.ARSubsystems.XRCpuImage.AsyncConversionStatus;
#else
    using XRCpuImage = UnityEngine.XR.ARSubsystems.XRCameraImage;
#endif
using System;
using System.Collections;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemoteExamples {
    public class CpuImagesExample : MonoBehaviour {
        [SerializeField] SetupARFoundationVersionSpecificComponents setup = null;
        [SerializeField] MeshRenderer meshRenderer = null;
        [SerializeField] CpuImageConversionType conversionType = CpuImageConversionType.Sync;
        [SerializeField] [Range(0.01f, 1)] float textureScale = 0.3f;
        [SerializeField] CameraImageTransformation transformation = CameraImageTransformation.None;
        [SerializeField] bool logWarnings = false;

        [CanBeNull] Texture2D texture;
        
        
        IEnumerator Start() {
            while (true) {
                if (setup.cameraManager.TryAcquireLatestCpuImageVersionAgnostic(out var cpuImage)) {
                    using (cpuImage) {
                        var format = TextureFormat.ARGB32;
                        var fullWidth = cpuImage.width;
                        var fullHeight = cpuImage.height;
                        var downsizedWidth = Mathf.RoundToInt(fullWidth * textureScale);
                        var downsizedHeight = Mathf.RoundToInt(fullHeight * textureScale);
                        var conversionParams = new XRCameraImageConversionParams {
                            transformation = transformation,
                            inputRect = new RectInt(0,0, fullWidth, fullHeight),
                            outputDimensions = new Vector2Int(downsizedWidth, downsizedHeight),
                            outputFormat = format
                        };

                        switch (conversionType) {
                            case CpuImageConversionType.Sync: {
                                var convertedDataSize = tryGetConvertedDataSize();
                                if (convertedDataSize.HasValue) {
                                    using (var buffer = new NativeArray<byte>(convertedDataSize.Value, Allocator.Temp)) {
                                        if (tryConvert()) {
                                            loadRawTextureData(buffer);
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

                                break;
                            }
                            case CpuImageConversionType.AsyncCoroutine: {
                                using (var conversion = cpuImage.ConvertAsync(conversionParams)) {
                                    while (!conversion.status.IsDone()) {
                                        yield return null;
                                    }

                                    if (conversion.status == AsyncCameraImageConversionStatus.Ready) {
                                        loadRawTextureData(conversion.GetData<byte>());
                                    } else if (logWarnings) {
                                        Debug.LogWarning($"ConvertAsync failed with status: {conversion.status}");
                                    }
                                }
                                break;
                            }
                            case CpuImageConversionType.AsyncCallback: {
                                bool isDone = false;
                                cpuImage.ConvertAsync(conversionParams, (status, _, data) => {
                                    isDone = true;
                                    if (status == AsyncCameraImageConversionStatus.Ready) {
                                        Assert.IsTrue(data.IsCreated);
                                        loadRawTextureData(data);
                                    } else if (logWarnings) {
                                        Debug.LogWarning($"ConvertAsync failed with status: {status}");
                                    }
                                });

                                while (!isDone) {
                                    yield return null;
                                }
                                
                                break;
                            }
                            default:
                                throw new Exception();
                        }

                        void loadRawTextureData(NativeArray<byte> data) {
                            if (texture != null) {
                                Destroy(texture);
                                texture = null;
                            }
                            
                            texture = new Texture2D(downsizedWidth, downsizedHeight, format, false);
                            texture.LoadRawTextureData(data);
                            texture.Apply();
                            meshRenderer.material.mainTexture = texture;
                        }
                    }
                }

                yield return null;
            }
        }

        void processException(Exception e) {
            if (e.Message.Contains(Constants.packageName)) {
                if (logWarnings) {
                    Debug.LogWarning(e.Message);
                }
            } else {
                Debug.LogError(e.ToString());
            }
        }

        enum CpuImageConversionType {
            Sync,
            AsyncCoroutine,
            AsyncCallback
        }
    }
}
