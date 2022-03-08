#if ARFOUNDATION_4_0_OR_NEWER
    using JetBrains.Annotations;
    using UnityEngine.Assertions;
    using UnityEngine.XR.ARFoundation;
    using UnityEngine.XR.ARSubsystems;
#endif
using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public static class ARFoundationRemoteUtils {
        public static ScreenOrientation ScreenOrientation = 0;
        
        
        #if ARFOUNDATION_4_0_OR_NEWER
        public static bool AreAllFeaturesSupportedSimultaneously([NotNull] this ARSession session, Feature features) {
            var subsystem = session.subsystem;
            Assert.IsNotNull(subsystem, $"Please ensure that {nameof(ARSession)} was enabled at least once before calling the {nameof(AreAllFeaturesSupportedSimultaneously)}() method.");
            var config = subsystem.DetermineConfiguration(features);
            return config.HasValue && features.SetDifference(config.Value.features) == 0;
        }
        #endif
    }
}
