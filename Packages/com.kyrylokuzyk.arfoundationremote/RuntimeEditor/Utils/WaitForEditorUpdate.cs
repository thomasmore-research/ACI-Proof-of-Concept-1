#if UNITY_EDITOR
using System;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine.Assertions;


namespace ARFoundationRemote.Editor {
    public static class WaitForEditorUpdate {
        [CanBeNull] static Action callback;
        
        
        public static void Wait(Action action) {
            Assert.IsNull(callback);
            callback = action;
            EditorApplication.update += update;
        }
        
        static void update() {
            EditorApplication.update -= update;            
            Assert.IsNotNull(callback);
            callback();
            callback = null;
        }
    }
}
#endif
