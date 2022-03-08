#if UNITY_EDITOR
using System.Diagnostics;
using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.RuntimeEditor {
    internal class SimulateTouchWithMouse {
        public static SimulateTouchWithMouse Instance { get; } = new SimulateTouchWithMouse();
        float lastUpdateTime = -1;
        Vector3 prevMousePos;


        public void Update() {
            Assert.IsTrue(Defines.isLegacyInputManagerEnabled);
            var currentTime = Time.time;
            if (currentTime != lastUpdateTime) {
                lastUpdateTime = currentTime;

                var curMousePos = UnityEngine.Input.mousePosition;
                var delta = curMousePos - prevMousePos;
                prevMousePos = curMousePos;

                var touch = createTouch(getPhase(delta), delta);
                Input.simulatedMouseTouch = touch;
                if (touch.HasValue) {
                    #if AR_FOUNDATION_REMOTE_4_12_0_OR_NEWER
                    Connection.TryRecordMessage(new LegacyInputData {
                        legacyTouches = new []{TouchSerializable.Create(touch.Value) }
                    });
                    #endif
                }
            }
        }

        static TouchPhase? getPhase(Vector3 delta) {
            if (Settings.Instance.inputSimulationType != InputSimulationType.SimulateMouseWithTouches) {
                if (UnityEngine.Input.GetMouseButtonDown(0)) {
                    return TouchPhase.Began;
                } else if (UnityEngine.Input.GetMouseButton(0)) {
                    return delta.sqrMagnitude < 0.01f ? TouchPhase.Stationary : TouchPhase.Moved;
                } else if (UnityEngine.Input.GetMouseButtonUp(0)) {
                    return TouchPhase.Ended;
                }
            }

            return null;
        }

        static Touch? createTouch(TouchPhase? phase, Vector3 delta) {
            if (!phase.HasValue) {
                return null;
            }

            var curMousePos = LegacyInputReceiver.Instance.mousePosFromOnGUIEvent;
            var touch = new Touch {
                phase = phase.Value,
                type = TouchType.Indirect,
                position = curMousePos,
                rawPosition = curMousePos,
                fingerId = 11,
                tapCount = 1,
                deltaTime = Time.deltaTime,
                deltaPosition = delta
            };
            LogTouch(touch, "fake");
            return touch;
        }

        [Conditional("_")]
        public static void LogTouch(Touch t, string s) {
            Debug.Log(s + t.fingerId + t.phase + t.position);
        }
    }
}
#endif
