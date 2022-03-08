#if ARFOUNDATION_4_0_2_OR_NEWER
    using XRCameraImagePlane = UnityEngine.XR.ARSubsystems.XRCpuImage.Plane;
    using CameraImageFormat = UnityEngine.XR.ARSubsystems.XRCpuImage.Format;
    using CameraImageCinfo = UnityEngine.XR.ARSubsystems.XRCpuImage.Cinfo; 
    using CameraImagePlaneCinfo = UnityEngine.XR.ARSubsystems.XRCpuImage.Plane.Cinfo;
    using XRCameraImageConversionParams = UnityEngine.XR.ARSubsystems.XRCpuImage.ConversionParams;
    using AsyncCameraImageConversionStatus = UnityEngine.XR.ARSubsystems.XRCpuImage.AsyncConversionStatus;
#else
    using XRCpuImage = UnityEngine.XR.ARSubsystems.XRCameraImage; 
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ARFoundationRemote.Runtime;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Editor {
    /// todo cpu image transformation is not backward compatible?
    /// todo transformation.everything is not working in build-in example scene
    public partial class CameraSubsystem {
        static XRCpuImageSerializable? cpuImage;
        static XRCameraIntrinsics? intrinsics;
        static ConvertedCpuImage? maybeConvertedImage;
        static ConversionParamsSerializable? prevConversionParams;


        static void receiveCpuImages(CpuImageData cameraData) {
            var receivedCpuImage = cameraData.cpuImage;
            if (receivedCpuImage.HasValue) {
                var image = receivedCpuImage.Value;
                cpuImage = image;
            }
            
            if (cameraData.convertedImage.HasValue) {
                maybeConvertedImage = cameraData.convertedImage.Value;
            }
        }
        
        
        partial class CameraSubsystemProvider {
            bool enableCpuImages;


            public override bool 
                #if ARFOUNDATION_4_0_2_OR_NEWER
                    TryAcquireLatestCpuImage
                #else
                    TryAcquireLatestImage
                #endif
                (out CameraImageCinfo cameraImageCinfo) {
                if (!isRunning) {
                    cameraImageCinfo = default;
                    return false;
                }
                
                if (!enableCpuImages) {
                    enableCpuImages = true;
                    logCpuImages("send enableCpuImages = true");
                    Connection.Send(new CameraDataCpuImagesEditor {
                        enableCpuImages = true
                    });
                }
                
                if (cpuImage.HasValue) {
                    cameraImageCinfo = 
                        #if ARFOUNDATION_4_0_2_OR_NEWER 
                        CpuImageApi.
                        #endif
                        CreateCpuImageInfo(cpuImage.Value);
                    return true;
                } else {
                    cameraImageCinfo = default;
                    return false;
                }
            }

            public override bool TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics) {
                if (intrinsics.HasValue) {
                    cameraIntrinsics = intrinsics.Value;
                    intrinsics = null;
                    return true;
                } else {
                    cameraIntrinsics = default;
                    return false;
                }
            }

            #if ARFOUNDATION_4_0_2_OR_NEWER 
            public override XRCpuImage.Api cpuImageApi { get; } = new CpuImageApi();
            class CpuImageApi : XRCpuImage.Api {
            #endif
                const int dummyNativeHandle = 42;
                const int conversionRequestId = 1337;
                static Dictionary<int, NativeArray<byte>> planeDataArrays;
                NativeArray<byte>? convertedData;
                XRCameraImageConversionParams? requestedConversionParams;


                internal static CameraImageCinfo CreateCpuImageInfo(XRCpuImageSerializable _) {
                    logCpuImages("CreateCpuImageInfo()");
                    Assert.IsNull(planeDataArrays, $"{Constants.packageName}: please Dispose() the previous XRCpuImage before calling TryAcquireLatestCpuImage() again.");
                    planeDataArrays = new Dictionary<int, NativeArray<byte>>();
                    return new CameraImageCinfo(dummyNativeHandle, new Vector2Int(_.width, _.height), _.planeCount, _.timestamp, _.format);
                }
                
                public override bool NativeHandleValid(int nativeHandle) {
                    Assert.AreEqual(dummyNativeHandle, nativeHandle);
                    return true;
                }

                public override unsafe bool TryGetPlane(int nativeHandle, int planeIndex, out CameraImagePlaneCinfo planeCinfo) {
                    Assert.IsTrue(cpuImage.HasValue);
                    var planes = cpuImage.Value.serializedPlanes;
                    if (planeIndex < planes.Length) {
                        var maybePlane = planes[planeIndex];
                        if (maybePlane.HasValue) {
                            var _ = maybePlane.Value;
                            var data = _.data;
                            Assert.IsNotNull(data);
                            var nativeArray = getOrCreateNativeDataArray(planeIndex, data);
                            logCpuImages($"native array: {planeIndex}, count: {nativeArray.Length}");
                            var dataPtr = new IntPtr(nativeArray.GetUnsafeReadOnlyPtr());
                            planeCinfo = new CameraImagePlaneCinfo(dataPtr, data.Length, _.rowStride, _.pixelStride);
                            return true;
                        }
                    }
                    
                    Debug.LogError($"{Constants.packageName}: please enable the 'Enable Cpu Image Raw Planes' in the plugin's settings.");
                    planeCinfo = default;
                    return false;    
                }

                NativeArray<byte> getOrCreateNativeDataArray(int planeIndex, byte[] data) {
                    if (planeDataArrays.TryGetValue(planeIndex, out var nativeArray)) {
                        return nativeArray;
                    } else {
                        var newArray = new NativeArray<byte>(data, Allocator.Persistent);
                        planeDataArrays[planeIndex] = newArray;
                        return newArray;
                    }
                }

                public override void DisposeImage(int nativeHandle) {
                    Assert.IsTrue(NativeHandleValid(nativeHandle));
                    logCpuImages("DisposeImage");
                    foreach (var _ in planeDataArrays.Values) {
                        _.Dispose();
                    }

                    planeDataArrays.Clear();
                    planeDataArrays = null;
                    cpuImage = null;
                    maybeConvertedImage = null;
                    if (convertedData.HasValue) {
                        Assert.IsTrue(convertedData.Value.IsCreated);
                        convertedData.Value.Dispose();
                        convertedData = null;
                    }
                }
          
                public override bool TryGetConvertedDataSize(int nativeHandle, Vector2Int dimensions, TextureFormat format, out int size) {
                    Assert.AreEqual(dummyNativeHandle, nativeHandle);
                    if (maybeConvertedImage.HasValue) {
                        var converted = maybeConvertedImage.Value;
                        var conversionParams = converted.conversionParams.Deserialize();
                        if (conversionParams.outputDimensions == dimensions && conversionParams.outputFormat == format) {
                            size = converted.bytes.Length;
                            return true;
                        }
                    }
                    
                    // this exception prevents TryConvert() from being called. Return true instead.
                    // throw new InvalidOperationException($"{Constants.packageName}: XRCpuImage.GetConvertedDataSize() because the image is not yet received from AR Companion app. Please try-catch the call to XRCpuImage.GetConvertedDataSize(). Real XR providers can also throw exceptions here, so catching them is a good practice anyway.\n");
                    size = 0;
                    return true;
                }

                #region AsyncConversion.cs
                public override int ConvertAsync(int nativeHandle, XRCameraImageConversionParams conversionParams) {
                    Assert.AreEqual(dummyNativeHandle, nativeHandle);
                    Assert.IsFalse(requestedConversionParams.HasValue, $"{Constants.packageName}: please Dispose() the previous AsyncConversion before creating a new one.");
                    requestedConversionParams = conversionParams;
                    sendConversionParams(conversionParams);
                    var result = conversionRequestId;
                    validateAsyncConversion(result);
                    return result;
                }

                public override AsyncCameraImageConversionStatus GetAsyncRequestStatus(int requestId) {
                    validateAsyncConversion(requestId);
                    Assert.IsTrue(requestedConversionParams.HasValue);
                    var converted = tryGetConvertedImage(requestedConversionParams.Value);
                    return converted.HasValue ? AsyncCameraImageConversionStatus.Ready : AsyncCameraImageConversionStatus.Pending;
                }

                public override unsafe bool TryGetAsyncRequestData(int requestId, out IntPtr dataPtr, out int dataLength) {
                    validateAsyncConversion(requestId);
                    if (maybeConvertedImage.HasValue) {
                        var bytes = maybeConvertedImage.Value.bytes;
                        Assert.IsFalse(convertedData.HasValue);
                        var nativeArray = new NativeArray<byte>(bytes, Allocator.Persistent);
                        convertedData = nativeArray;
                        dataPtr = new IntPtr(nativeArray.GetUnsafeReadOnlyPtr());
                        dataLength = bytes.Length;
                        return true;
                    }
                    
                    dataPtr = IntPtr.Zero;
                    dataLength = 0;
                    return true;
                }

                public override void DisposeAsyncRequest(int requestId) {
                    validateAsyncConversion(requestId);
                    requestedConversionParams = null;
                }

                void validateAsyncConversion(int requestId) {
                    Assert.AreEqual(conversionRequestId, requestId);
                    Assert.IsTrue(requestedConversionParams.HasValue);
                }
                #endregion

                public override bool TryConvert(int nativeHandle, XRCameraImageConversionParams conversionParams, IntPtr destinationBuffer, int bufferLength) {
                    Assert.AreEqual(dummyNativeHandle, nativeHandle);
                    sendConversionParams(conversionParams);
                    var converted = tryGetConvertedImage(conversionParams);
                    if (converted.HasValue) {
                        Marshal.Copy(converted.Value.bytes, 0, destinationBuffer, bufferLength);
                        return true;
                    }

                    throw new InvalidOperationException($"{Constants.packageName}: XRCpuImage.Convert() failed because the image is not yet received from AR Companion app. " +
                        "Please try-catch the call to XRCpuImage.Convert(). Real XR providers can also throw exceptions here, so catching them is a good practice anyway.\n\n" +
                        "XRCpuImage.GetConvertedDataSize() can also throw exceptions on real devices, so try-catching the XRCpuImage.GetConvertedDataSize() is also a good idea.\n");
                }

                public override unsafe void ConvertAsync(int nativeHandle, XRCameraImageConversionParams conversionParams, OnImageRequestCompleteDelegate callback, IntPtr context) {
                    Assert.AreEqual(dummyNativeHandle, nativeHandle);
                    Assert.IsFalse(requestedConversionParams.HasValue, $"{Constants.packageName}: AsyncConversion was running. The plugin supports only one conversion at a time.");
                    sendConversionParams(conversionParams);
                    var converted = tryGetConvertedImage(conversionParams);
                    if (converted.HasValue) {
                        Assert.IsTrue(maybeConvertedImage.HasValue);
                        var bytes = maybeConvertedImage.Value.bytes;
                        using (var nativeArray = new NativeArray<byte>(bytes, Allocator.Temp)) {
                            callback(AsyncCameraImageConversionStatus.Ready, 
                                conversionParams, 
                                new IntPtr(nativeArray.GetUnsafeReadOnlyPtr()), 
                                bytes.Length, 
                                context 
                            );
                        }
                    } else {
                        callback(AsyncCameraImageConversionStatus.Failed, conversionParams, IntPtr.Zero, 0, context);
                    }
                }
      
                void sendConversionParams(XRCameraImageConversionParams conversionParams) {
                    var newParams = ConversionParamsSerializable.Create(conversionParams);
                    if (!newParams.Equals(prevConversionParams)) {
                        prevConversionParams = newParams;
                        Connection.Send(new CameraDataCpuImagesEditor {
                            conversionParams = newParams
                        });
                    }
                }

                ConvertedCpuImage? tryGetConvertedImage(XRCameraImageConversionParams conversionParams) {
                    if (maybeConvertedImage.HasValue) {
                        var convertedImage = maybeConvertedImage.Value;
                        if (convertedImage.conversionParams.Deserialize() == conversionParams) {
                            return convertedImage;
                        }
                    }

                    return null;
                }
                
            #if ARFOUNDATION_4_0_2_OR_NEWER 
                public override bool FormatSupported(XRCpuImage image, TextureFormat format) {
                    // return true for simplicity
                    // AR Companion app will display an error if !FormatSupported  
                    return true;
                }
            }
            #endif
            
            
            [Conditional("_")]
            static void logCpuImages(string msg) {
                Debug.Log($"CameraSubsystemCpuImages: {msg}");
            }
        }
    }
}
