#if UNITY_EDITOR
using System.Collections;
using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARFoundation;


namespace ARFoundationRemote.RuntimeEditor {
    public class OriginDataSender : MonoBehaviour {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void register() {
            Utils.TryCreatePersistentEditorObject<OriginDataSender>();
        }
        
        IEnumerator Start() {
            ARSessionOrigin origin = null;
            var oldData = new SessionOriginData();

            var waitForEndOfFrame = new WaitForEndOfFrame(); // WaitForEndOfFrame to guarantee image library and sessionOriginData order 
            while (true) {
                while (origin == null) {
                    origin = FindObjectOfType<ARSessionOrigin>();
                    yield return waitForEndOfFrame;
                }

                var newData = SessionOriginData.Create(origin);
                if (!newData.Equals(oldData)) {
                    oldData = newData;
                    // Debug.Log("send origin data\n" + newData);
                    Connection.Send(newData);
                }

                yield return waitForEndOfFrame;
            }
        }
    }
}
#endif
