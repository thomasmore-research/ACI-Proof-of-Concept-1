using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    public class Settings : ScriptableObjectSingleton<Settings> {
        public const string maxFPSTooltip = "This sets only the upper bound. Actual FPS depend on the performance of your AR device.";

        [Header("Connection Settings")]
        [SerializeField] public string ARCompanionAppIP = "192.168.0.";

        [Header("AR Companion Settings")] 
        [Tooltip("Please restart AR scene in Editor to apply settings. Building new AR Companion app is not required.")]
        [SerializeField] public ARCompanionSettings arCompanionSettings;

        [Tooltip("The plugin sends Editor Game View back to the companion app. Setting higher resolution scale will result in higher latency and lower frames-per-second.")]
        [SerializeField] 
        public EditorViewSettings editorGameViewSettings;

        [SerializeField] [HideInInspector] public PackageVersionData[] packages = new PackageVersionData[0];

        [Header("New AR Companion app build is required after modification.")] 
        [SerializeField] 
        public int port = 44819;
        
        [Space]
        [SerializeField] 
        public XRReferenceImageLibrary[] embeddedImageLibraries = new XRReferenceImageLibrary[0];

        [Header("Debug")] 
        [SerializeField] 
        public DebugSettings debugSettings;

        public InputSimulationType inputSimulationType => InputSimulationType.SimulateSingleTouchWithMouse;

        public bool logStartupErrors => debugSettings.logStartupErrors;
        public bool showTelepathyLogs => debugSettings.showTelepathyLogs;
        public bool showTelepathyWarningsAndErrors => debugSettings.showTelepathyWarningsAndErrors;

        public static bool EnableBackgroundVideo => cameraVideoSettings.enableVideo;
        public static CameraVideoSettings cameraVideoSettings => Instance.arCompanionSettings.cameraVideoSettings;
        public static OcclusionSettings occlusionSettings => Instance.arCompanionSettings.occlusionSettings;
        public static FaceTrackingSettings faceTrackingSettings => Instance.arCompanionSettings.faceTrackingSettings;
    }


    [Serializable]
    public class ARCompanionSettings {
        public CameraVideoSettings cameraVideoSettings;
        public OcclusionSettings occlusionSettings;
        [Tooltip("Disable unnecessary face tracking features to increase FPS.")]
        [SerializeField] 
        public FaceTrackingSettings faceTrackingSettings;
    }


    [Serializable]
    public class FaceTrackingSettings {
        [Tooltip(Settings.maxFPSTooltip), Range(0.5f, 60f)] 
        [SerializeField] public float maxFPS = 30;
        [SerializeField] public bool sendVertices = true;
        [SerializeField] public bool sendNormals = true;
        [SerializeField] public bool sendARKitBlendshapes = true;
    }
    

    [Serializable]
    public class CameraVideoSettings {
        [SerializeField] public bool enableVideo = true;
        [SerializeField] [Range(.01f, 1f)] public float resolutionScale = 1f/3;
        [SerializeField, Range(0, 100)] public int quality = 95;
        [Tooltip(Settings.maxFPSTooltip), Range(0.5f, 30f)]
        [SerializeField] public float maxVideoFps = 15;
        [Tooltip(Settings.maxFPSTooltip), Range(0.5f, 30f)]
        [SerializeField] public float maxCpuImagesFps = 15;
        [SerializeField] public bool enableCpuImageRawPlanes = false;
    }


    [Serializable]
    public class OcclusionSettings {
        [Tooltip(Settings.maxFPSTooltip), Range(0.5f, 20f)] 
        [SerializeField] public float maxFPS = 10f;
        /// setting scale to 1 will clip the texture, don't know why
        /// also, this may cause companion app crashes
        [SerializeField] [Range(.01f, 0.95f)] public float resolutionScale = 1f/3;
    }
    
    
    public enum InputSimulationType {
        SimulateSingleTouchWithMouseLegacy,
        SimulateSingleTouchWithMouse,
        SimulateMouseWithTouches
    }


    [Serializable]
    public class DebugSettings {
        // todo add setting to not check package versions (if user know what he's doing) to support backward compatibility
        [SerializeField] public bool logStartupErrors = true;
        [SerializeField] public bool showTelepathyLogs = false;
        [SerializeField] public bool showTelepathyWarningsAndErrors = true;
        [SerializeField] public bool printCompanionAppIPsToConsole = true;
        [SerializeField] public bool warnIfReceiverConnectionWasDestroyed = false;
        [SerializeField] public bool modifyDisplayMatrix = false;
        [SerializeField] public Matrix4x4 displayMatrix;
    }


    [Serializable]
    public class EditorViewSettings {
        [SerializeField] [Range(.01f, 1f)] public float resolutionScale = 1f/3;
        [Tooltip(Settings.maxFPSTooltip), Range(0.5f, 30f)]
        public float maxEditorViewFps = 10;
    }
}
