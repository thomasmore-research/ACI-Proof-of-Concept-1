using System.Linq;
using JetBrains.Annotations;
using UnityEngine;


namespace ARFoundationRemote {
    public static partial class Input {
        #if UNITY_EDITOR
        public static Touch? simulatedMouseTouch;
        [NotNull] public static Touch[] remoteTouches = new Touch[0];
        static Touch[] simulatedAndRemoteTouches {
            get {
                if (simulatedMouseTouch.HasValue) {
                    return remoteTouches.Append(simulatedMouseTouch.Value).ToArray();
                } else {
                    return remoteTouches;
                }
            }
        }
        #endif
        

        [PublicAPI]
        public static int touchCount {
            get {
                #if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isRemoteConnected) {
                    return simulatedAndRemoteTouches.Length;
                }
                #endif
                
                return UnityEngine.Input.touchCount;
            }
        }

        [PublicAPI]
        public static Touch GetTouch(int index) {
            #if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isRemoteConnected) {
                return simulatedAndRemoteTouches[index];
            }
            #endif

            return UnityEngine.Input.GetTouch(index);
        }

        [PublicAPI]
        public static Touch[] touches {
            get {
                #if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isRemoteConnected) {
                    return simulatedAndRemoteTouches;
                }
                #endif
                
                return UnityEngine.Input.touches;
            }
        }
    }
}
