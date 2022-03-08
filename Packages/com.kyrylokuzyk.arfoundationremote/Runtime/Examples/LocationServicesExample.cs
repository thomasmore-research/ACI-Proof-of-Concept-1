using ARFoundationRemote;
using UnityEngine;
using ARFoundationRemote.Runtime;
using UnityEngine.Android;
using Input = ARFoundationRemote.Input; // THIS LINE IS REQUIRED FOR LOCATION SERVICES TO WORK WITH AR FOUNDATION EDITOR REMOTE
#if !ENABLE_AR_FOUNDATION_REMOTE_LOCATION_SERVICES
    using LocationServiceStatus = ARFoundationRemote.LocationServiceStatusDummy;
#endif


namespace ARFoundationRemoteExamples {
    public class LocationServicesExample : MonoBehaviour {
        void Awake() {
            if (Defines.isAndroid) {
                Permission.RequestUserPermission(Permission.FineLocation);
            }

            if (!Defines.enableLocationServices) {
                Debug.LogError(LocationServiceRemote.missingDefineError);
            }
        }


        [SerializeField] float desiredAccuracyInMeters = 10f;
        [SerializeField] float updateDistanceInMeters = 10f;

        [Header("Location data (read-only)")]
        [SerializeField] bool isEnabledByUser;
        [SerializeField] LocationServiceStatus status;
        [SerializeField] LocationInfoSerializable lastData;


        void OnGUI() {
            if (GUI.Button(new Rect(0, 0, 400, 200), "Start")) {
                Input.location.Start(desiredAccuracyInMeters, updateDistanceInMeters);
            } else if (GUI.Button(new Rect(0, 200, 400, 200), "Stop")) {
                Input.location.Stop();
            }
        }


        void Update() {
            isEnabledByUser = Input.location.isEnabledByUser;
            status = Input.location.status;
            lastData = Input.location.status == LocationServiceStatus.Running ? LocationInfoSerializable.Create(Input.location.lastData) : default;
        }
    }
}
