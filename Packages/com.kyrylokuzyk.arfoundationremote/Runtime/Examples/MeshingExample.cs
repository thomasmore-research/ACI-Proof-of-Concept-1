using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARFoundation;


namespace ARFoundationRemoteExamples {
    public class MeshingExample : MonoBehaviour {
        [SerializeField] ARMeshManager manager = null;
        [SerializeField] bool showMeshDestructionButton = false;
        
        
        void Awake() {
            if (Defines.isIOS && !(Defines.arKitInstalled && Defines.isARFoundation4_0_OrNewer)) {
                Debug.LogError($"{Constants.packageName}: please install ARKit XR Plugin >= 4.0");
            }

            if (Defines.isAndroid) {
                Debug.LogError($"{Constants.packageName}: meshing is not supported by ARCore");
            }
        }
        
        void OnGUI() {
            if (showMeshDestructionButton && GUI.Button(new Rect(0,0,400,200), "DestroyAllMeshes")) {
                manager.DestroyAllMeshes();
            }
        }
    }
}
