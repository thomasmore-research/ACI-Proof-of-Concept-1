#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.Management;


namespace ARFoundationRemote.Runtime {
    public static class Global {
        public static bool? IsInitialized;
        public static bool isQuitting;
        public static bool IsExitingPlayMode;


        public static bool IsPluginEnabled() {
            if (isQuitting) {
                throw new Exception($"{Constants.packageName}: please disable the 'Project Settings/Editor/Enter Play Mode Options' or enable the 'Reload Domain' setting.\n");
            }

            return Application.isEditor && isPluginEnabledInXRManagementWindow();
        }

        static bool isPluginEnabledInXRManagementWindow() {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget buildTargetSettings);
            Assert.IsNotNull(buildTargetSettings, "buildTargetSettings != null");
            var xrGeneralSettings = buildTargetSettings.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            Assert.IsNotNull(xrGeneralSettings, "xrGeneralSettings != null");
            var xrManagerSettings = xrGeneralSettings.Manager;
            Assert.IsNotNull(xrManagerSettings, "xrManagerSettings != null");
            var loaders = getLoaders(xrManagerSettings);
            Assert.IsNotNull(loaders, "loaders != null");
            // Debug.Log($"loaders: {string.Join(", ", loaders)}");
            return loaders.Any(_ => _ != null && _.GetType().Name == "ARFoundationRemoteLoader");
        }

        [CanBeNull]
        static IReadOnlyList<XRLoader> getLoaders(XRManagerSettings manager) {
            return manager.
                #if XR_MANAGEMENT_4_0_1_OR_NEWER
                    activeLoaders;
                #else
                    loaders;
                #endif
        }
    }
}
#endif
