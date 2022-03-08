using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class Sender : MonoBehaviour, IConnectionDelegate {
        [SerializeField] public ARSession arSession = null;
        #pragma warning disable 414
        [SerializeField] public ARSessionOrigin origin = null;
        #pragma warning disable 169
        [SerializeField] public ARAnchorManager anchorManager = null;
        #pragma warning restore 169
        #pragma warning restore 414
        [SerializeField] public SetupARFoundationVersionSpecificComponents setuper = null;
        [SerializeField] public Material arcoreOriginalUVsMaterial;


        const string noARCapabilitiesMessage = "Please run this scene on device with AR capabilities\n" +
                                               "and install AR Provider (ARKit XR Plugin, ARCore XR Plugin, etc)\n" +
                                               "and enable AR Provider in Project Settings -> XR Plug-in Management";
        static readonly string[] logMessagesToIgnore = {
            "ARPoseDriver is already consuming data from", // warning because ARPoseDriver.s_InputTrackingDevice field is static
        };
        
        static readonly string[] logMessagesToLogOnce = {
            "You can only call cameraDepthTarget inside the scope",
        };
        
        static Sender _instance;
        public static Sender Instance {
            get {
                if (_instance == null) {
                    _instance = FindObjectOfType<Sender>();
                    Assert.IsNotNull(_instance);
                }

                return _instance;
            }
        }

        bool isAlive = true;
        public static bool isConnected => Connection.Instance.isConnected;
        ARSessionState? sessionState;
        static readonly HashSet<IEditorEventSubscriber> editorEventSubscribers = new HashSet<IEditorEventSubscriber>();


        void Awake() {
            if (Application.isEditor) {
                Debug.LogError("Please run this scene on AR capable device");
                enabled = false;
                return;
            }
            
            Assert.IsTrue(origin.camera.transform.parent.lossyScale == Vector3.one);

            logSceneReload("Sender.Awake()");
            Application.logMessageReceivedThreaded += logMessageReceivedThreaded;
            XRGeneralSettings.Instance.Manager.InitializeLoaderSyncIfNotInitialized();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        void OnEnable() {
            Connection.Register<ARCompanionSettings>(settings => {
                Settings.Instance.arCompanionSettings = settings;
                Texture2DSerializable.ClearCache();   
            });
            
            Connection.Register<EditorToPlayerMessageType>(receiveEditorMessageType);
            Connection.Register<PackageVersionData[]>(receiveEditorPackages);
        }

        void OnDisable() {
            Connection.UnRegister<ARCompanionSettings>();
            Connection.UnRegister<EditorToPlayerMessageType>();
            Connection.UnRegister<PackageVersionData[]>();
        }

        public void ExecuteOnDisabledOrigin(Action action) {
            var go = origin.gameObject;
            go.SetActive(false);
            action();
            go.SetActive(true);
        }

        public void ExecuteOnDisabledCamera(Action action) {
            var cameraGameObject = origin.camera.gameObject;
            cameraGameObject.SetActive(false);
            action();
            cameraGameObject.SetActive(true);
        }

        void Start() {
            Connection.Instance.StartConnection(this);
            StartCoroutine(checkAvailability());
        }

        void Update() {
            if (isConnected) {
                var currentState = ARSession.state;
                if (sessionState != currentState) {
                    sessionState = currentState;
                    Connection.Send(currentState);
                }
            }
        }

        void OnDestroy() {
            logIfNeeded("Sender.OnDestroy()");
            Application.logMessageReceivedThreaded -= logMessageReceivedThreaded;
        }
        
        void logMessageReceivedThreaded(string message, string stacktrace, LogType type) {
            if (shouldShowLog()) {
                runningErrorMessage += $"{type}: {message}\n{stacktrace}\n";
            }

            bool shouldShowLog() {
                if (type == LogType.Log) {
                    return false;
                }
                
                if (logMessagesToIgnore.Any(message.Contains)) {
                    return false;
                }

                if (logMessagesToLogOnce.Any(message.Contains) && !DebugUtils.IsFirstOccurrence(message)) {
                    return false;
                }

                return true;
            }
        }

        [Conditional("_")]
        static void logSceneReload(string message) {
            Debug.Log(message);
        }

        void IConnectionDelegate.OnConnected() {
        }

        void IConnectionDelegate.OnDisconnected() {
            logIfNeeded("onDisconnectedFromEditor");
            ((TelepathySenderConnection) Connection.Instance).ResetConnection(this);
            tryReloadScene();
        }

        void tryReloadScene([CanBeNull] Action onCompleted = null) {
            if (isAlive) {
                isAlive = false;
                stopSession();
                StartCoroutine(reloadSceneCor(onCompleted));
            }
        }

        IEnumerator reloadSceneCor([CanBeNull] Action callback) {
            logSceneReload("reloadSceneCor()");
            var timeStart = Time.time;
            while (DontDestroyOnLoadSingleton.runningCoroutineNames.Count > 0) {
                if (Time.time - timeStart > 5) {
                    Debug.LogError($"reloadSceneCor() while coroutines were running: {string.Join(", ", DontDestroyOnLoadSingleton.runningCoroutineNames)}");
                    break;
                }
                
                yield return null;
            }
            
            var manager = XRGeneralSettings.Instance.Manager;
            if (manager.isInitializationComplete) {
                logSceneReload("manager.DeinitializeLoader();");
                manager.DeinitializeLoader();
            }

            SceneManager.LoadScene("ARCompanion");
            callback?.Invoke();
        }
        
        void receiveEditorMessageType(EditorToPlayerMessageType messageType) {
            if (messageType != EditorToPlayerMessageType.None) {
                logIfNeeded("editorMessageReceived type: " + messageType);
                foreach (var _ in editorEventSubscribers) {
                    _.EditorEventReceived(messageType);
                }
            }
            
            switch (messageType) {
                case EditorToPlayerMessageType.ResumeSession:
                    setSessionEnabled(true);
                    setARComponentsEnabled(true);
                    break;
                case EditorToPlayerMessageType.PauseSession:
                    pauseSession();
                    break;
                case EditorToPlayerMessageType.ResetSession:
                    resetSession();
                    break;
                case EditorToPlayerMessageType.DestroySession:
                    stopSession();
                    break;
                case EditorToPlayerMessageType.InitializeLoader:
                    XRGeneralSettings.Instance.Manager.InitializeLoaderSyncIfNotInitialized();
                    break;
                case EditorToPlayerMessageType.DeinitializeLoader:
                    var xrManagerSettings = XRGeneralSettings.Instance.Manager;
                    if (xrManagerSettings.isInitializationComplete) {
                        xrManagerSettings.DeinitializeLoader();
                    }
                    
                    break;
            }
        }

        void receiveEditorPackages(PackageVersionData[] editorPackages) {
            if (!PackageVersionData.CheckVersions(Settings.Instance.packages, editorPackages)) {
                Connection.Send(Settings.Instance.packages);
            }
        }
        
        void setSessionEnabled(bool isEnabled) {
            LogObjectTrackingCrash($"setSessionEnabled {isEnabled}");
            arSession.enabled = isEnabled;
        }

        void stopSession() {
            logSceneReload("stopSession()");
            pauseAndResetSession();
            setARComponentsEnabled(false);
            setManagersEnabled(false);
        }

        readonly Dictionary<Behaviour, bool> managers = new Dictionary<Behaviour, bool>();
        
        void setARComponentsEnabled(bool enable) {
            // logSceneReload($"setARComponentsEnabled {enable}");
            var types = new[] {
                typeof(ARInputManager), // disable and enable to prevent native errors
                // typeof(ARCameraBackground) // todoo sync ARCameraBackground.enabled with Editor? 
            };
            
            foreach (var _ in types.Select(FindObjectOfType).Cast<MonoBehaviour>()) {
                _.enabled = enable;
            }
        }

        void setManagersEnabled(bool enable) {
            logSceneSpecific($"setManagersEnabled {enable}");
            foreach (var pair in managers) {
                pair.Key.enabled = enable && pair.Value;
            }
        }

        void resetSession() {
            LogObjectTrackingCrash("resetSession()");
            arSession.Reset();    
        }

        public void SetManagerEnabled<T>([NotNull] T manager, bool managerEnabled) where T : Behaviour {
            logSceneSpecific($"{typeof(T)} enabled {managerEnabled}");
            manager.enabled = managerEnabled;
            managers[manager] = managerEnabled;
        }

        void pauseSession() {
            setSessionEnabled(false);
        }

        IEnumerator checkAvailability() {
            yield return ARSession.CheckAvailability();
            while (ARSession.state == ARSessionState.Installing) {
                yield return null;
            }
            
            Assert.IsTrue(isSupported, noARCapabilitiesMessage);

            if (Settings.Instance.debugSettings.printCompanionAppIPsToConsole) {
                while (true) {
                    var ips = getIPAddresses().ToList();
                    if (ips.Any() && Connection.IsSenderConnectionActive) {
                        Debug.Log(getIPsMessage(ips));
                        break;
                    } else {
                        yield return null;
                    }
                }    
            }
        }

        void pauseAndResetSession() {
            logSceneReload("pauseAndResetSession()");
            pauseSession();
            resetSession();
        }

        static bool isSupported => ARSession.state >= ARSessionState.Ready;

        [Conditional("_")]
        void logIfNeeded(string message) {
            Debug.Log(message);
        }

        void OnGUI() {
            ShowTextAtCenter(getUserMessageAndAppendErrorIfNeeded());
            showPackages();

            void showPackages() {
                if (isConnected) {
                    return;
                }
                
                const int height = 300;
                const int margin = 30;
                var position = new Rect(margin, Screen.height - height - margin, 200, height);
                var text = string.Join("\n", Settings.Instance.packages.Select(_ => _.ToString()));
                var style = new GUIStyle {
                    fontSize = 30,
                    normal = new GUIStyleState {textColor = Color.white},
                    alignment = TextAnchor.LowerLeft
                };

                GUI.Label(position, text, style);
            }

            string getUserMessageAndAppendErrorIfNeeded() {
                if (isConnected) {
                    return runningErrorMessage;
                } else {
                    var result = getWaitingMessage() + "\n\n" + waitingErrorMessage + "\n\n" + runningErrorMessage;
                    if (string.IsNullOrEmpty(runningErrorMessage)) {
                        result += "\n\nPlease leave an honest review on the Asset Store :)";
                    }

                    return result;
                }
            }
        }

        public static string waitingErrorMessage = "";
        static string runningErrorMessage = "";

        string getWaitingMessage() {
            if (!isSupported) {
                return noARCapabilitiesMessage;
            } else {
                var ips = getIPAddresses().ToList();
                if (ips.Any()) {
                    Assert.IsNotNull(Connection.Instance);
                    if (Connection.IsSenderConnectionActive) {
                        return getIPsMessage(ips);
                    } else {
                        return "AR Companion app can't start server.\n" +
                               "Please ensure only one instance of the app is running or restart the app.";
                    }
                } else {
                    return "Can't start sender. Please connect AR device to private network.";
                }
            }
        }

        static string getIPsMessage([NotNull] List<IPAddress> ips) {
            return "Please enter AR Companion app IP in\n" +
                   "Assets/Plugins/ARFoundationRemoteInstaller/Resources/Settings\n" +
                   "and start AR scene in Editor.\n\n" +
                   "Available IP addresses:\n" + String.Join("\n", ips);
        }

        [NotNull]
        static IEnumerable<IPAddress> getIPAddresses() {
            return NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(_ => _.GetIPProperties().UnicastAddresses)
                .Select(_ => _.Address)
                .Where(_ => _.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(_))
                .Distinct();
        }

        [Conditional("_")]
        public static void logSceneSpecific(string msg) {
            Debug.Log(msg);
        }

        public static void ShowTextAtCenter(string text) {
            GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(text, new GUIStyle {fontSize = 30, normal = new GUIStyleState {textColor = Color.white}});
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        [Conditional("_")]
        public static void LogObjectTrackingCrash(string msg) {
            Debug.Log(msg);
        }

        public static void AddRuntimeErrorOnce(string error) {
            if (DebugUtils.LogOnce(error)) {
                AddRuntimeError(error);
            }
        }

        public static void AddRuntimeError(string error) {
            runningErrorMessage += error + "\n";
        }

        public static void RegisterEditorEventSubscriber([NotNull] IEditorEventSubscriber subscriber) {
            var added = editorEventSubscribers.Add(subscriber);
            Assert.IsTrue(added);
        }

        public static void UnRegisterEditorEventSubscriber([NotNull] IEditorEventSubscriber del) {
            var removed = editorEventSubscribers.Remove(del);
            Assert.IsTrue(removed);
        }
    }

    
    public interface IEditorEventSubscriber {
        void EditorEventReceived(EditorToPlayerMessageType message);
    }


    public enum EditorToPlayerMessageType {
        None,
        ResumeSession,
        PauseSession,
        ResetSession,
        DestroySession,
        InitializeLoader,
        DeinitializeLoader
    }


    public static class EditorToPlayerMessageTypeExtensions {
        public static bool IsStop(this EditorToPlayerMessageType _) {
            switch (_) {
                case EditorToPlayerMessageType.DestroySession:
                    return true;
            }

            return false;
        }
    }
    
    
    public interface ISerializableTrackable<out V> {
        TrackableId trackableId { get; }
        V Value { get; }
    }

    
    [Serializable]
    public struct TrackableChangesData<T> {
        // removed can be replaced with TrackableID[]
        public T[] added, updated, removed;

        public override string ToString() {
            return $"TrackableChangesData<{nameof(T)}>, added: {added.Length}, updated: {updated.Length}, removed: {removed.Length}";
        }
    }
}
