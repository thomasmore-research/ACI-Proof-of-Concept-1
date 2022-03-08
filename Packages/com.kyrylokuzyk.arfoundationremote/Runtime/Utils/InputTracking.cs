using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;


namespace ARFoundationRemote.Runtime {
    public static class InputTracking {
        static XRNodeState centerEyePoseState;
        
        
        public static void SetCenterEyeNodeState(XRNodeState state) {
            centerEyePoseState = state;
        }

        public static void GetNodeStates(List<XRNodeState> states) {
            #if UNITY_EDITOR
            if (Application.isEditor) {
                states.Clear();
                states.Add(centerEyePoseState);
                return;
            }
            #endif
            
            UnityEngine.XR.InputTracking.GetNodeStates(states);
        }
    }
}
