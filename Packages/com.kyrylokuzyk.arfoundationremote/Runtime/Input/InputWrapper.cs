using System;
using ARFoundationRemote.Runtime;
using UnityEngine;


namespace ARFoundationRemoteExamples {
    public static class InputWrapper {
        static readonly IInputWrapperImpl impl = createImpl();

        static IInputWrapperImpl createImpl() {
            if (Defines.isLegacyInputManagerEnabled) {
                return new LegacyInputWrapper();
            }
            
            #if AR_FOUNDATION_REMOTE_4_12_0_OR_NEWER
            if (Defines.isNewInputSystemEnabled) {
                #if INPUT_SYSTEM_INSTALLED 
                    return new InputSystemWrapper();
                #endif
            }

            throw new Exception("Input System (new) is enabled, but 'com.unity.inputsystem' package is not installed.");
            #endif

            throw new Exception("Input System (new) is not supported");
        }

        public static int touchCount => impl.touchCount;
        public static ITouchWrapper GetTouch(int i) => impl.GetTouch(i);
    }


    public interface IInputWrapperImpl {
        int touchCount { get; }
        ITouchWrapper GetTouch(int index);
    }
    
    
    public interface ITouchWrapper {
        TouchPhase phase { get; }
        Vector2 position { get; }
        int fingerId { get; }
    }
}
