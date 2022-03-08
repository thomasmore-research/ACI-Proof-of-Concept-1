using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;
#if ENABLE_AR_FOUNDATION_REMOTE_LOCATION_SERVICES
    using LocationService = UnityEngine.LocationService;
    using LocationServiceStatus = UnityEngine.LocationServiceStatus;
#else
    using LocationService = ARFoundationRemote.LocationServiceRemote;
    using LocationServiceStatus = ARFoundationRemote.LocationServiceStatusDummy;
#endif


namespace ARFoundationRemote.Runtime {
    public class LocationServicesSender : MonoBehaviour {
        LocationDataPlayer prevData;

                
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void registerLocationServicesSender() {
            Utils.TryCreateCompanionAppObjectOnSceneLoad<LocationServicesSender>();
        }

        void OnEnable() {
            Connection.Register<LocationDataEditor>(receiveLocationData);
            if (Defines.isAndroid && !Defines.isLegacyInputManagerEnabled) {
                Debug.LogError("Location Services are not working on Android if Input Manager (Old) is disabled.");
            }
        }

        void OnDisable() {
            Connection.UnRegister<LocationDataEditor>();
            if (Defines.enableLocationServices) {
                location.Stop();
            }
        }

        void receiveLocationData(LocationDataEditor locationData) {
            if (locationData.isStart) {
                log("location.Start()");
                if (Defines.isAndroid) {
                    Permission.RequestUserPermission(Permission.FineLocation);
                }
                
                Assert.IsTrue(locationData.desiredAccuracyInMeters.HasValue);
                Assert.IsTrue(locationData.updateDistanceInMeters.HasValue);
                location.Start(locationData.desiredAccuracyInMeters.Value, locationData.updateDistanceInMeters.Value);
            } else {
                log("location.Stop();");
                location.Stop();
            }
        }

        void Update() {
            if (!Defines.enableLocationServices || !Sender.isConnected) {
                return;
            }
            
            var newData = new LocationDataPlayer {
                status = location.status,
                lastData = location.status == LocationServiceStatus.Running ? LocationInfoSerializable.Create(location.lastData) : (LocationInfoSerializable?) null,
                isEnabledByUser = location.isEnabledByUser,
            };

            if (!prevData.Equals(newData)) {
                log("send location data");
                prevData = newData;
                Connection.Send(newData);
            }
        }

        static LocationService location {
            get {
                #if ENABLE_AR_FOUNDATION_REMOTE_LOCATION_SERVICES
                    return UnityEngine.Input.location;
                #else
                    throw new Exception(LocationServiceRemote.missingDefineError);;
                #endif
            }
        }
        
        [Conditional("_")]
        public static void log(string msg) {
            Debug.Log(msg);
        }
    }
    

    [Serializable]
    public struct LocationDataPlayer {
        public LocationServiceStatus status;
        public LocationInfoSerializable? lastData;
        public bool isEnabledByUser;
    }


    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct LocationInfoSerializable {
        [SerializeField] double m_Timestamp;
        [SerializeField] float m_Latitude;
        [SerializeField] float m_Longitude;
        [SerializeField] float m_Altitude;
        [SerializeField] float m_HorizontalAccuracy;
        [SerializeField] float m_VerticalAccuracy;

        
        public static LocationInfoSerializable Create(LocationInfo _) {
            return new Union {nonSerializable = _}.serializable;
        }

        public LocationInfo Deserialize() {
            return new Union {serializable = this}.nonSerializable;
        }
        
        [StructLayout(LayoutKind.Explicit)]
        struct Union {
            [FieldOffset(0)] public LocationInfoSerializable serializable;
            [FieldOffset(0)] public LocationInfo nonSerializable;
        }
    }
}
