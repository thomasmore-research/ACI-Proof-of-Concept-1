using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class DontDestroyOnLoadSingleton : MonoBehaviour {
        public static List<string> runningCoroutineNames { get; } = new List<string>();

        static DontDestroyOnLoadSingleton instance;
        static bool isDestroyed;


        #if UNITY_EDITOR
            /// use <see cref="AddCoroutine"/> in player instead
            public static DontDestroyOnLoadSingleton Instance => getInstance();
        #endif

        static DontDestroyOnLoadSingleton getInstance() {
            Assert.IsFalse(isDestroyed);
            if (instance == null) {
                var go = new GameObject(nameof(DontDestroyOnLoadSingleton));
                DontDestroyOnLoad(go);
                instance = go.AddComponent<DontDestroyOnLoadSingleton>();
            }

            return instance;
        }
        
        public static void AddCoroutine(IEnumerator routine, string name) {
            getInstance().addCoroutine(routine, name);
        }

        void addCoroutine(IEnumerator routine, string debugName) {
            StartCoroutine(countCoroutinesNumber(routine, debugName));
        }

        IEnumerator countCoroutinesNumber(IEnumerator routine, string debugName) {
            runningCoroutineNames.Add(debugName);
            logRunningCoroutines();
            while (routine.MoveNext()) {
                yield return routine.Current;
            }
            
            var removed = runningCoroutineNames.Remove(debugName);
            Assert.IsTrue(removed);
            logRunningCoroutines();
        }

        void logRunningCoroutines() {
            log($"running coroutines {runningCoroutineNames.Count}: {string.Join(", ", runningCoroutineNames)}");
        }
        
        void OnDestroy() {
            isDestroyed = true;
        }
        
        [Conditional("_")]
        void log(string msg) {
            Debug.Log(msg);
        }
    }
}
