using System;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using Touch = UnityEngine.Touch;


namespace ARFoundationRemote.Runtime {
    public class LegacyInputSender : MonoBehaviour {
        readonly Throttler throttler = new Throttler(15);
        [NotNull] TouchSerializable[] prevTouches = new TouchSerializable[0];
        
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void register() {
            Utils.TryCreatePersistentCompanionAppObject<LegacyInputSender>();   
        }

        void Update() {
            if (!Sender.isConnected) {
                return;
            }

            if (!Defines.isLegacyInputManagerEnabled) {
                return;
            }
            
            var touches = UnityEngine.Input.touches.Select(TouchSerializable.Create).ToArray();
            if (!prevTouches.SequenceEqual(touches)) {
                prevTouches = touches;
                if (touches.Any(_ => _.IsSingleFramePhase() || throttler.CanSendNonCriticalMessage)) {
                    TouchSerializable.log(touches, "sent");
                    Connection.Send(new LegacyInputData {
                        legacyTouches = touches
                    });
                }    
            }
        }
    }

    
    [Serializable]
    public struct TouchSerializable : ITouch {
        int fingerId;
        TouchPhase phase;
        Vector2Serializable position;
        float deltaTime;
        TouchType type;
        float radius;
        float pressure;
        int tapCount;
        Vector2Serializable rawPosition;
        float azimuthAngle;
        float altitudeAngle;
        Vector2Serializable deltaPosition;
        float radiusVariance;
        float maximumPossiblePressure;
       

        public static TouchSerializable Create(Touch t) {
            return new TouchSerializable {
                fingerId = t.fingerId,
                phase = t.phase,
                position = TouchUtils.NormalizeByScreenSize(t.position),
                type = t.type,
                radius = t.radius,
                pressure = t.pressure,
                tapCount = t.tapCount,
                deltaTime = t.deltaTime,
                rawPosition = TouchUtils.NormalizeByScreenSize(t.rawPosition),
                azimuthAngle = t.azimuthAngle,
                altitudeAngle = t.altitudeAngle,
                deltaPosition = TouchUtils.NormalizeByScreenSize(t.deltaPosition),
                radiusVariance = t.radiusVariance,
                maximumPossiblePressure = t.maximumPossiblePressure
            };
        }

        public Touch Deserialize() {
            return new Touch {
                fingerId = fingerId,
                phase = phase,
                position = TouchUtils.FromNormalizedToScreenPos(position),
                deltaTime = deltaTime,
                type = type,
                radius = radius,
                pressure = pressure,
                tapCount = tapCount,
                rawPosition = TouchUtils.FromNormalizedToScreenPos(rawPosition),
                azimuthAngle = azimuthAngle,
                altitudeAngle = altitudeAngle,
                deltaPosition = TouchUtils.FromNormalizedToScreenPos(deltaPosition),
                radiusVariance = radiusVariance,
                maximumPossiblePressure = maximumPossiblePressure
            };
        }

        public override string ToString() {
            return fingerId.ToString() + phase + position.Value;
        }

        public bool IsSingleFramePhase() {
            switch (phase) {
                case TouchPhase.Began:
                case TouchPhase.Canceled:
                case TouchPhase.Ended:
                    return true;
                default:
                    return false;
            }
        }

        int ITouch.Id() {
            return fingerId;
        }

        [Conditional("_")]
        public static void log(TouchSerializable[] array, string msg) {
            var str = msg + ": " + array.Length + "\n";
            foreach (var touchSerializable in array) {
                str += touchSerializable.phase + "\n";
            }
            
            UnityEngine.Debug.Log(str);
        }
    }
}
