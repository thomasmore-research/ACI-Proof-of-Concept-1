#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.Assertions;
#if UNITY_2019_2
    using UnityEngine.Experimental.LowLevel;
#else
    using UnityEngine.LowLevel;
#endif


namespace ARFoundationRemote.RuntimeEditor {
    public class LegacyInputReceiver: MonoBehaviour {
        public static LegacyInputReceiver Instance { get; private set; }
        public Vector2 mousePosFromOnGUIEvent { get; private set; }
        readonly TouchesReceiver<TouchSerializable> legacyTouchesReceiver = new TouchesReceiver<TouchSerializable>();
        static InputSimulationType inputSimulationType => Settings.Instance.inputSimulationType;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void register() {
            Assert.IsNull(Instance);
            Instance = Utils.TryCreatePersistentEditorObject<LegacyInputReceiver>();
        }
        
        void Awake() {
            setupInputSimulation();
        }

        void OnEnable() {
            Connection.Register<LegacyInputData>(data => legacyTouchesReceiver.Receive(data.legacyTouches));
        }

        void OnDisable() {
            Connection.UnRegister<LegacyInputData>();
        }

        void OnDestroy() {
            for (int i = 0; i < UnityEngine.Input.touchCount; i++) {
                var t = UnityEngine.Input.GetTouch(i);
                t.phase = TouchPhase.Canceled;
                simulate(t);
            }
        }

        void Update() {
            Input.remoteTouches = legacyTouchesReceiver.Touches.Select(_ => _.Deserialize()).ToArray();
        }

        static bool inputSimulationDidSetup;
        
        static void setupInputSimulation() {
            if (!Global.IsPluginEnabled()) {
                return;
            }
            
            if (!Defines.isLegacyInputManagerEnabled) {
                return;    
            }
            
            Assert.IsFalse(inputSimulationDidSetup);
            inputSimulationDidSetup = true;
            
            if (inputSimulationType == InputSimulationType.SimulateSingleTouchWithMouseLegacy) {
                return;
            }

            if (inputSimulationType == InputSimulationType.SimulateSingleTouchWithMouse) {
                UnityEngine.Input.simulateMouseWithTouches = false;
            }

            insertTouchSimulationUpdateIntoPlayerLoop();
        }

        static void insertTouchSimulationUpdateIntoPlayerLoop() {
            var loop = PlayerLoop.
                #if UNITY_2019_2
                    GetDefaultPlayerLoop();
                #else
                    GetCurrentPlayerLoop();
                #endif
            logPlayerLoop(loop);
            var index = Array.FindIndex(loop.subSystemList, _ => {
                var typeFullName = _.type.FullName;
                Assert.IsNotNull(typeFullName);
                return typeFullName.Contains(".PlayerLoop.EarlyUpdate");
            });
            Assert.AreNotEqual(-1, index);
            var earlyUpdateLoop = loop.subSystemList[index];
            var list = earlyUpdateLoop.subSystemList.ToList();
            list.Insert(0, new PlayerLoopSystem {type = typeof(TouchSimulationUpdate), updateDelegate = TouchSimulationUpdate.Update});
            earlyUpdateLoop.subSystemList = list.ToArray();
            loop.subSystemList[index] = earlyUpdateLoop;
            PlayerLoop.SetPlayerLoop(loop);
            logPlayerLoop(loop);
        }

        static void logPlayerLoop(PlayerLoopSystem loop) {
            /*Debug.Log("________logPlayerLoop");
            foreach (var loopSystem in loop.subSystemList) {
                Debug.Log(loopSystem.type);
                foreach (var subsystem in loopSystem.subSystemList) {
                    Debug.Log(subsystem.type);
                }
            }*/
        }
        
        void OnGUI() {
            // calling SimulateTouch is overriding Input.mouse position in Windows Unity Editor.
            // so we get mouse position from Event.current
            var mousePosition = Event.current.mousePosition;
            mousePosFromOnGUIEvent = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
        }

        struct TouchSimulationUpdate {
            public static bool enabled = true;
            
            public static void Update() {
                if (!Application.isPlaying) {
                    // PlayerLoop will still contain TouchSimulationUpdate after stopping the scene in Editor
                    return;
                }
                
                SimulateTouchWithMouse.Instance.Update();
                if (enabled) {
                    if (inputSimulationType == InputSimulationType.SimulateSingleTouchWithMouse && UnityEngine.Input.simulateMouseWithTouches) {
                        enabled = false;
                        Debug.LogError(Constants.packageName + ": UnityEngine.Input.simulateMouseWithTouches is required to be false to be able to simulate single touch with mouse in Editor.");
                        return;
                    }

                    simulateReceivedAndMouseTouches();
                }
            }
        }

        /// todo possible flaw: "com.unity.ugui" receives two inputs simultaneously: one from the real mouse, and the other one from <see cref="Input.simulatedMouseTouch"/>
        /// todo input is not working correctly in ar foundation samples. Button get stuck and sometimes it's required to press them several times
        static void simulateReceivedAndMouseTouches() {
            for (int i = 0; i < Input.touchCount; i++) {
                var touch = Input.GetTouch(i);
                simulate(touch);
            }
        }

        static void simulate(Touch touch) {
            var simulateTouchMethod = typeof(UnityEngine.Input).GetMethod("SimulateTouch", BindingFlags.NonPublic | BindingFlags.Static);
            if (simulateTouchMethod == null) {
                Debug.LogError(Constants.packageName + ": to enable touch input remoting and simulation in Unity 2019.2, please add this line on top of every script that uses UnityEngine.Input:\nusing Input = ARFoundationRemote.Input;\n");
                TouchSimulationUpdate.enabled = false;
                return;
            }
                
            SimulateTouchWithMouse.LogTouch(touch, "simulate");
            simulateTouchMethod.Invoke(null, new object[] { touch.fingerId, touch.position, touch.phase });
            InputRemotingUtils.CheckGameViewIsFocused();
        }
    }
}
#endif
