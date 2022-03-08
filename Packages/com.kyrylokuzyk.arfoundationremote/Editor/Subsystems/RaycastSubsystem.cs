using ARFoundationRemote.Runtime;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Editor {
    public class RaycastSubsystem : XRRaycastSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            if (!Global.IsPluginEnabled()) {
                return;
            }

            var thisType = typeof(RaycastSubsystem);
            XRRaycastSubsystemDescriptor.RegisterDescriptor(new XRRaycastSubsystemDescriptor.Cinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(RaycastSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType,
                #endif
                supportedTrackableTypes =
                    TrackableType.Planes |
                    TrackableType.FeaturePoint,
                supportsWorldBasedRaycast = true // world-based raycast is a fallback option if screen-based raycast is not supported
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new RaycastSubsystemProvider();
        #endif

        class RaycastSubsystemProvider: Provider {
            public override NativeArray<XRRaycastHit> Raycast(XRRaycastHit defaultRaycastHit, Ray sessionSpaceRay, TrackableType trackableTypeMask, Allocator allocator) {
                var origin = Object.FindObjectOfType<ARSessionOrigin>();
                var raycastManager = Object.FindObjectOfType<ARRaycastManager>();
                if (origin == null || raycastManager == null) {
                    return new NativeArray<XRRaycastHit>();
                }

                var result = new List<XRRaycastHit>();
                if ((trackableTypeMask & TrackableType.Planes) != 0) {
                    var planeManager = Object.FindObjectOfType<ARPlaneManager>();
                    if (planeManager != null) {
                        var hits = planeManager.Raycast(sessionSpaceRay, trackableTypeMask, Allocator.Temp);
                        if (hits.IsCreated) {
                            result.AddRange(hits);
                            hits.Dispose();
                        }
                    } else {
                        Debug.LogError($"{Constants.packageName}: please add ARPlaneManager to raycast against detected planes.");
                    }
                }

                if ((trackableTypeMask & TrackableType.FeaturePoint) != 0) {
                    var pointCloudManager = Object.FindObjectOfType<ARPointCloudManager>();
                    if (pointCloudManager != null) {
                        var hits = pointCloudManager.Raycast(sessionSpaceRay, trackableTypeMask, Allocator.Temp);
                        if (hits.IsCreated) {
                            result.AddRange(hits);
                            hits.Dispose();
                        }
                    } else {
                        Debug.LogError($"{Constants.packageName}: please add ARPointCloudManager to raycast against detected cloud points.");
                    }
                }

                return new NativeArray<XRRaycastHit>(result.ToArray(), allocator);
            }
        }
    }
}
