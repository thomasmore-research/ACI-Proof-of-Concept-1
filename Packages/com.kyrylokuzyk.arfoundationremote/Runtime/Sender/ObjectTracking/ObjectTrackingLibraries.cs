using UnityEngine;
using UnityEngine.Assertions;


namespace ARFoundationRemote.Runtime {
    public class ObjectTrackingLibraries : ScriptableObject {
        #if ARFOUNDATION_4_0_OR_NEWER
            [Header("Make new AR Companion app build after adding new library")]
            [SerializeField]
            public UnityEngine.XR.ARSubsystems.XRReferenceObjectLibrary[] objectLibraries = new UnityEngine.XR.ARSubsystems.XRReferenceObjectLibrary[0];
        #endif
        
        static ObjectTrackingLibraries instance;
        public static ObjectTrackingLibraries Instance {
            get {
                if (instance == null) {
                    loadFromResources();
                    Assert.IsNotNull(instance);
                }
                
                Assert.IsNotNull(instance);
                return instance;
            }
        }
        
        static void loadFromResources() {
            instance = Resources.Load<ObjectTrackingLibraries>(nameof(ObjectTrackingLibraries));
        }
    }
}
