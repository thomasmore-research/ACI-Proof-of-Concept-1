#if ARFOUNDATION_4_0_OR_NEWER
    using UnityEngine.XR.ARFoundation;
    using UnityEngine.XR.ARSubsystems;
#endif
using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public class ARKitHumanSegmentationExample : MonoBehaviour {
        [SerializeField] Transform plane = null;

        
        void Awake() {
            if (!Defines.isARFoundation4_0_OrNewer) {
                Debug.LogError("Human Segmentation is only available in AR Foundation >= 4.0");
            }

            if (!Defines.isIOS) {
                Debug.LogError("Human Segmentation is only available on iOS.");
            }
            
            // To prevent a problem similar to EnvironmentOcclusionExample, don't create manager from code
            /*#if ARFOUNDATION_4_0_OR_NEWER
                var manager = gameObject.AddComponent<AROcclusionManager>();
                manager.requestedHumanStencilMode = HumanSegmentationStencilMode.Fastest;
                manager.requestedHumanDepthMode = HumanSegmentationDepthMode.Fastest;
                #if ARFOUNDATION_4_1_OR_NEWER
                    manager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferHumanOcclusion;
                #endif
            #endif*/
        }
        
        void OnGUI() {
            Sender.ShowTextAtCenter(getText());
        }

        string getText() {
            return Defines.isIOS ? $"Only human body that is closer than {plane.localPosition.z}\nmeters away will be visible." :
                "Human segmentation is only supported on iOS";
        }
    }
}
