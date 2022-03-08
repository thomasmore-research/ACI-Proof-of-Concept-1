using System;
using ARFoundationRemote.Runtime;
using UnityEngine;
#if ENABLE_AR_FOUNDATION_REMOTE_LOCATION_SERVICES
    using LocationService = UnityEngine.LocationService;
    using LocationServiceStatus = UnityEngine.LocationServiceStatus;
#else
    using LocationService = ARFoundationRemote.LocationServiceRemote;
    using LocationServiceStatus = ARFoundationRemote.LocationServiceStatusDummy;
#endif


namespace ARFoundationRemote {
    public  static partial class Input {
        public static readonly LocationServiceRemote location = new LocationServiceRemote();
    }
    
    
    public class LocationServiceRemote {
        public void Start(float desiredAccuracyInMeters = 10f, float updateDistanceInMeters = 10f) {
            #if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isRemoteConnected) {
                Connection.Send(new LocationDataEditor {
                    isStart = true,
                    desiredAccuracyInMeters = desiredAccuracyInMeters,
                    updateDistanceInMeters = updateDistanceInMeters
                });

                return;
            }
            #endif
            
            locationService.Start(desiredAccuracyInMeters, updateDistanceInMeters);
        }

        public void Stop() {
            #if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isRemoteConnected) {
                Connection.Send(new LocationDataEditor {
                    isStart = false
                });

                return;
            }
            #endif
            
            locationService.Stop();
        }
        
        public LocationServiceStatus status {
            get {
                #if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isRemoteConnected) {
                    return LocationServicesReceiver.locationData?.status ?? LocationServiceStatus.Stopped;
                }
                #endif

                return locationService.status;
            }
        }

        public LocationInfo lastData {
            get {
                #if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isRemoteConnected) {
                    return LocationServicesReceiver.locationData?.lastData?.Deserialize() ?? throw new Exception("Check LocationService.status before querying last location.");
                }
                #endif

                return locationService.lastData;
            }
        }

        public bool isEnabledByUser {
            get {
                #if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isRemoteConnected) {
                    return LocationServicesReceiver.locationData?.isEnabledByUser ?? false;
                }
                #endif

                return locationService.isEnabledByUser;
            }
        }

        static LocationService locationService {
            get {
                #if ENABLE_AR_FOUNDATION_REMOTE_LOCATION_SERVICES
                    return UnityEngine.Input.location;
                #else
                    throw new Exception(missingDefineError);
                #endif
            }
        }
        
        public static string missingDefineError => $"{Constants.packageName}: please add the 'ENABLE_AR_FOUNDATION_REMOTE_LOCATION_SERVICES' define to 'Project Settings/Player/Scripting Define Symbols' and make new build of AR Companion app to enable Location Services";
    }

    
    [Serializable]
    public struct LocationDataEditor {
        public bool isStart;
        public float? desiredAccuracyInMeters;
        public float? updateDistanceInMeters;
    }


    public enum LocationServiceStatusDummy {
        Running,
        Stopped
    }
}
