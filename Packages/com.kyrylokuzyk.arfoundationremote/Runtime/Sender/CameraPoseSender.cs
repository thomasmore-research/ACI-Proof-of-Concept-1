using System;
using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public class CameraPoseSender : MonoBehaviour {
        readonly Throttler throttler = new Throttler(20);
        Transform cameraTransform;
            
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void register() {
            Utils.TryCreateCompanionAppObjectOnSceneLoad<CameraPoseSender>();
        }

        void Awake() {
            cameraTransform = FindObjectOfType<Sender>().origin.camera.transform;
        }

        void LateUpdate() {
            if (Sender.isConnected && throttler.CanSendNonCriticalMessage) {
                Connection.Send(new CameraPoseData {
                    position = Vector3Serializable.Create(cameraTransform.localPosition),
                    rotation = QuaternionSerializable.Create(cameraTransform.localRotation)
                });
            }
        }
    }


    [Serializable]
    public struct CameraPoseData {
        public Vector3Serializable position;
        public QuaternionSerializable rotation;
    }
}
