using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public class RollingFilter<T> {
        [PublicAPI] public IReadOnlyCollection<TimeAndData> entries => queue;
        readonly Queue<TimeAndData> queue = new Queue<TimeAndData>();
        readonly float timeSpan;
        
        
        public RollingFilter(float timeSpan) {
            this.timeSpan = timeSpan;
        }
        
        public void AddNewEntryAndRemoveOldEntries(T entry) {
            var curTime = Time.unscaledTime;
            queue.Enqueue(new TimeAndData(curTime, entry));

            while (queue.Count > 0) {
                if (curTime - queue.Peek().time > timeSpan) {
                    queue.Dequeue();
                } else {
                    break;
                }
            }
        }

        public class TimeAndData {
            public readonly float time;
            [PublicAPI]
            public readonly T data;

                    
            public TimeAndData(float time, T data) {
                this.time = time;
                this.data = data;
            }
        }
    }
}
