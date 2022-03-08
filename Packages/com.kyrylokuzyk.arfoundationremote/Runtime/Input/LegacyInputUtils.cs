using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;


namespace ARFoundationRemote.Runtime {
    [Serializable]
    public struct LegacyInputData {
        [NotNull] public TouchSerializable[] legacyTouches;
    }
    
    
    public class TouchesReceiver<T> where T: ITouch {
        readonly Queue<T[]> touchEventsQueue = new Queue<T[]>();
        T[] currentTouches = new T[0];
        float lastUpdateTime = -1;

            
        public T[] Touches {
            get {
                dequeueTouchesIfNewFrame();
                return currentTouches;
            }
        }
            
        void dequeueTouchesIfNewFrame() {
            var currentTime = Time.time;
            if (currentTime != lastUpdateTime) {
                lastUpdateTime = currentTime;
                currentTouches = touchEventsQueue.Any() ? touchEventsQueue.Dequeue() : new T[0];
            }
        }
            
        public void Receive([NotNull] T[] receivedTouches) {
            if (touchEventsQueue.Count == 0 || hasSingleFramePhase(receivedTouches) || hasSingleFramePhase(touchEventsQueue.Peek())) {
                touchEventsQueue.Enqueue(receivedTouches);
            } else {
                var combined = touchEventsQueue.Dequeue().ToList();
                foreach (var receivedTouch in receivedTouches) {
                    var i = combined.FindIndex(_ => _.Id() == receivedTouch.Id());
                    if (i != -1) {
                        combined[i] = receivedTouch;
                    } else {
                        combined.Add(receivedTouch);
                    }
                }
                    
                touchEventsQueue.Enqueue(combined.ToArray());
            }
        }
            
        static bool hasSingleFramePhase(T[] touches) => touches.Any(_ => _.IsSingleFramePhase());
    }
    
    
    public interface ITouch {
        bool IsSingleFramePhase();
        int Id();
    }
    
    
    public static class TouchUtils {
        public static Vector2Serializable NormalizeByScreenSize(Vector2 v) {
            var result = new Vector2(v.x / Screen.width, v.y / Screen.height);
            return Vector2Serializable.Create(result);
        }

        public static Vector2 FromNormalizedToScreenPos(Vector2Serializable ser) {
            var v = ser.Value;
            return new Vector2(v.x * Screen.width, v.y * Screen.height);            
        }
    }
}
