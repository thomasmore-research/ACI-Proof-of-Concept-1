using System.Diagnostics;
using UnityEngine.XR.Management;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public static class Extensions {
        public static void InitializeLoaderSyncIfNotInitialized(this XRManagerSettings settings) {
            if (!settings.isInitializationComplete) {
                log("settings.InitializeLoaderSync();");
                settings.InitializeLoaderSync();
            }
        }

        [Conditional("_")]
        static void log(string msg) {
            Debug.Log(msg);
        }
    }
}
