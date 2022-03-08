#if ARFOUNDATION_4_0_2_OR_NEWER
    using XRCameraImagePlane = UnityEngine.XR.ARSubsystems.XRCpuImage.Plane;
    using CameraImageFormat = UnityEngine.XR.ARSubsystems.XRCpuImage.Format;
    using XRCameraImageConversionParams = UnityEngine.XR.ARSubsystems.XRCpuImage.ConversionParams;
    using CameraImageTransformation = UnityEngine.XR.ARSubsystems.XRCpuImage.Transformation;
#else
    using XRCpuImage = UnityEngine.XR.ARSubsystems.XRCameraImage;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    public class CpuImageSender : MonoBehaviour, IEditorEventSubscriber {
        readonly Throttler throttler = new Throttler(Settings.cameraVideoSettings.maxCpuImagesFps);
        bool sendCpuImages;
        ARCameraManager manager;
        ConversionParamsSerializable? requestedConversionParams;

        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void register() {
            Utils.TryCreateCompanionAppObjectOnSceneLoad<CpuImageSender>();
        }

        void OnEnable() {
            Connection.Register<CameraDataCpuImagesEditor>(receiveCameraData);
            manager = Sender.Instance.setuper.cameraManager;
            Sender.RegisterEditorEventSubscriber(this);
            manager.frameReceived += delegate {
                if (throttler.CanSendNonCriticalMessage) {
                    var intrinsics = tryGetIntrinsics();
                    if (intrinsics.HasValue) {
                        Connection.Send(intrinsics.Value);
                    }
                    
                    var (cpuImageSerialized, convertedImage) = trySerializeCpuImage();
                    if (cpuImageSerialized.HasValue || convertedImage.HasValue) {
                        Connection.Send(new CpuImageData {
                            cpuImage = cpuImageSerialized,
                            convertedImage = convertedImage
                        });    
                    }

                    (XRCpuImageSerializable?, ConvertedCpuImage?) trySerializeCpuImage() {
                        if (!sendCpuImages) {
                            return (null, null);
                        }
                        
                        if (!manager.descriptor.supportsCameraImage) {
                            Sender.AddRuntimeErrorOnce("Cpu images are not supported by this device.");
                            return (null, null);
                        }

                        var acquired = tryAcquireLatestCpuImage(out var cpuImage);
                        if (!acquired) {
                            return (null, null);
                        }

                        if (!cpuImage.valid) {
                            Sender.AddRuntimeError("!cpuImage.valid");
                            return (null, null);
                        }
                        
                        using (cpuImage) {
                            return (XRCpuImageSerializable.Create(cpuImage), tryConvertImage());
                            
                            ConvertedCpuImage? tryConvertImage() {
                                if (requestedConversionParams.HasValue) {
                                    var serializedConversionParams = requestedConversionParams.Value;
                                    var conversionParams = serializedConversionParams.Deserialize();
                                    if (conversionParams.outputDimensions.x > cpuImage.width || conversionParams.outputDimensions.y > cpuImage.height) {
                                        return null;
                                    }
                                        
                                    var size = tryGetSize();
                                    if (size.HasValue) {
                                        using (var nativeArray = new NativeArray<byte>(size.Value, Allocator.Temp)) {
                                            if (tryConvertSync()) {
                                                return new ConvertedCpuImage {
                                                    conversionParams = serializedConversionParams,
                                                    bytes = nativeArray.ToArray()
                                                };
                                            }

                                            bool tryConvertSync() {
                                                try {
                                                    cpuImage.ConvertSync(conversionParams, nativeArray);
                                                    return true;
                                                } catch (Exception e) {
                                                    Debug.LogError($"tryConvertSync() failed: {e}");
                                                    return false;
                                                }
                                            }
                                        }
                                    }

                                    int? tryGetSize() {
                                        try {
                                            return cpuImage.GetConvertedDataSize(conversionParams);
                                        } catch (Exception e) {
                                            Debug.LogError($"tryGetSize() failed: {e}");
                                            return null;
                                        }
                                    }
                                }

                                return null;
                            }
                        }
                    }
                }
            };
        }

        void OnDisable() {
            Connection.UnRegister<CameraDataCpuImagesEditor>();
            Sender.UnRegisterEditorEventSubscriber(this);
        }

        bool tryAcquireLatestCpuImage(out XRCpuImage cameraImage) {
            return manager.
                #if ARFOUNDATION_4_0_2_OR_NEWER
                    TryAcquireLatestCpuImage
                #else
                    TryGetLatestImage
                #endif
                    (out cameraImage);
        }
        
        XRCameraIntrinsicsSerializable? tryGetIntrinsics() {
            if (manager.TryGetIntrinsics(out var intrinsics)) {
                return XRCameraIntrinsicsSerializable.Create(intrinsics);
            } else {
                return null;
            }
        }

        void IEditorEventSubscriber.EditorEventReceived(EditorToPlayerMessageType message) {
            if (message.IsStop()) {
                sendCpuImages = false;
                requestedConversionParams = null;
            }
        }

        void receiveCameraData(CameraDataCpuImagesEditor cameraData) {
            if (cameraData.enableCpuImages == true) {
                sendCpuImages = true;
            }

            if (cameraData.conversionParams.HasValue) {
                requestedConversionParams = cameraData.conversionParams.Value;
            }
        }
    }


    [Serializable]
    public struct ConvertedCpuImage {
        public ConversionParamsSerializable conversionParams;
        [NotNull] public byte[] bytes;
    }
    
    
    [Serializable]
    public struct XRCameraIntrinsicsSerializable {
        public Vector2Serializable focalLength;
        public Vector2Serializable principalPoint;
        int width, height;


        public static XRCameraIntrinsicsSerializable Create(XRCameraIntrinsics _) {
            var resolution = _.resolution;
            return new XRCameraIntrinsicsSerializable {
                focalLength = Vector2Serializable.Create(_.focalLength),
                principalPoint = Vector2Serializable.Create(_.principalPoint),
                width = resolution.x,
                height = resolution.y
            };
        }

        public XRCameraIntrinsics Value => new XRCameraIntrinsics(focalLength.Value, principalPoint.Value, new UnityEngine.Vector2Int(width, height));
    }


    [Serializable]
    public struct XRCpuImageSerializable {
        public int width { get; private set; }
        public int height { get; private set; }
        public int planeCount { get; private set; }
        public CameraImageFormat format { get; private set; }
        public double timestamp { get; private set; }
        [NotNull] public XRCpuImagePlaneSerializable?[] serializedPlanes { get; private set; }


        public static XRCpuImageSerializable Create(XRCpuImage i) {
            return new XRCpuImageSerializable {
                width = i.width,
                height = i.height,
                planeCount = i.planeCount,
                format = i.format,
                timestamp = i.timestamp,
                serializedPlanes = trySerializePlanes()
            };

            XRCpuImagePlaneSerializable?[] trySerializePlanes() {
                if (!Settings.cameraVideoSettings.enableCpuImageRawPlanes) {
                    return new XRCpuImagePlaneSerializable?[0];
                }
                
                var serializedPlanes = i
                    .GetPlanes()
                    .Select(_ => _.HasValue ? XRCpuImagePlaneSerializable.Create(_.Value) : null)
                    .ToArray();

                Assert.AreEqual(i.planeCount, serializedPlanes.Length);
                return serializedPlanes;
            }
        }
    }


    [Serializable]
    public struct XRCpuImagePlaneSerializable {
        public int rowStride { get; private set; }
        public int pixelStride { get; private set; }
        [CanBeNull] public byte[] data { get; private set; }


        public static XRCpuImagePlaneSerializable? Create(XRCameraImagePlane _) {
            var nativeData = _.data; // data is a "view" into memory, no need to Dispose()
            if (nativeData.IsCreated) {
                return new XRCpuImagePlaneSerializable {
                    rowStride = _.rowStride,
                    pixelStride = _.pixelStride,
                    data = nativeData.ToArray()
                };
            } else {
                return null;
            }
        }
    }


    public static class XRCpuImageExtensions {
        static bool TryGetPlane(this XRCpuImage image, int index, out XRCameraImagePlane plane) {
            try {
                plane = image.GetPlane(index);
                return true;
            } catch (InvalidOperationException) {
                plane = default;
                return false;
            }
        }

        public static IEnumerable<XRCameraImagePlane?> GetPlanes(this XRCpuImage image) {
            for (int i = 0; i < image.planeCount; i++) {
                if (image.TryGetPlane(i, out var plane)) {
                    yield return plane;
                } else {
                    yield return null;
                }
            }
        }
    }


    [Serializable]
    public struct ConversionParamsSerializable {
        RectIntSerializable inputRect;
        Vector2IntSerializable outputDimensions;
        TextureFormat format;
        public CameraImageTransformation transformation;


        public static ConversionParamsSerializable Create(XRCameraImageConversionParams _) {
            return new ConversionParamsSerializable {
                inputRect = RectIntSerializable.Create(_.inputRect),
                outputDimensions = Vector2IntSerializable.Create(_.outputDimensions),
                format = _.outputFormat,
                transformation = _.transformation
            };
        }

        public XRCameraImageConversionParams Deserialize() {
            return new XRCameraImageConversionParams {
                transformation = transformation,
                inputRect = inputRect.Deserialize(),
                outputDimensions = outputDimensions.Deserialize(),
                outputFormat = format
            };
        }
    }


    [Serializable]
    public struct RectIntSerializable {
        int x, y, w, h;
        
        public static RectIntSerializable Create(RectInt _) {
            return new RectIntSerializable {
                x = _.x,
                y = _.y,
                w = _.width,
                h = _.height
            };
        }

        public RectInt Deserialize() {
            return new RectInt(x, y, w, h);
        }
    }


    [Serializable]
    public struct Vector2IntSerializable {
        int x, y;
        
        public static Vector2IntSerializable Create(Vector2Int _) {
            return new Vector2IntSerializable {
                x = _.x,
                y = _.y
            };
        }

        public Vector2Int Deserialize() {
            return new Vector2Int(x, y);
        }
    }


    public static class CpuImageExtensions {
        /// unsafe is needed for AR Foundation 3.1 support
        public static unsafe void ConvertSync(this XRCpuImage cpuImage, XRCameraImageConversionParams conversionParams, NativeArray<byte> buffer) {
            cpuImage.Convert(conversionParams, 
                #if ARFOUNDATION_4_0_2_OR_NEWER
                    buffer);
                #else
                    new IntPtr(Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafePtr(buffer)), buffer.Length);
                #endif
        }
    }
}
