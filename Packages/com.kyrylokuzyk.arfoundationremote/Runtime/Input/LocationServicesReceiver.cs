#if UNITY_EDITOR
using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public class LocationServicesReceiver : MonoBehaviour {
        public static LocationDataPlayer? locationData;

        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void register() {
             Utils.TryCreatePersistentEditorObject<LocationServicesReceiver>();
        }

        void OnEnable() {
            Connection.Register<LocationDataPlayer>(data => {
                LocationServicesSender.log("receive location data");
                locationData = data;
            });
        }

        void OnDisable() {
            Connection.UnRegister<LocationDataPlayer>();
        }
    }
}
#endif
