using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public class EnvironmentOcclusionExample : MonoBehaviour {
        [SerializeField] Transform plane = null;


        /// If AROcclusionManager is added at runtime, it will sometimes not work with URP
        /// It works with one order of scripts and not works with other.
        /// Probably, some race conditions involved
        /*void Awake() {
            #if ARFOUNDATION_4_1_OR_NEWER
                gameObject.SetActive(false);
                var manager = gameObject.AddComponent<UnityEngine.XR.ARFoundation.AROcclusionManager>();
                manager.requestedEnvironmentDepthMode = UnityEngine.XR.ARSubsystems.EnvironmentDepthMode.Fastest;
                manager.requestedOcclusionPreferenceMode = UnityEngine.XR.ARSubsystems.OcclusionPreferenceMode.PreferEnvironmentOcclusion;
                gameObject.SetActive(true);
            #endif
        }*/

        void OnGUI() {
            Sender.ShowTextAtCenter(getText());
        }

        string getText() {
            if (Defines.isARFoundation4_1_OrNewer) {
                return $"Objects further than {plane.localPosition.z}\nmeters away will be clipped." + "\n" + getIOSEnvWarning();
            } else {
                return "Environment occlusion is only available in AR Foundation >= 4.1";
            }
        }

        string getIOSEnvWarning() {
            return Defines.isIOS ? "Environment occlusion only available in iOS14." : "";
        }
    }
}
