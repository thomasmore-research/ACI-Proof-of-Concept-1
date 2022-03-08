using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;


namespace ARFoundationRemote.Runtime {
    public static class Utils {
        #if UNITY_EDITOR
        [CanBeNull]
        public static T TryCreatePersistentEditorObject<T>() where T : Component {
            if (Global.IsPluginEnabled()) {
                return CreatePersistentObject<T>();
            } else {
                return null;
            }
        }
        #endif

        public static void TryCreatePersistentCompanionAppObject<T>() where T : Component {
            if (Defines.isARCompanionDefine && !Application.isEditor) {
                CreatePersistentObject<T>();
            }
        }

        [NotNull]
        public static T CreatePersistentObject<T>() where T : Component {
            Assert.IsNull(Object.FindObjectOfType<T>());
            var go = new GameObject(typeof(T).Name); // todoo place all plugin objects under one parent
            Object.DontDestroyOnLoad(go);
            return go.AddComponent<T>();
        }

        public static void TryCreateCompanionAppObjectOnSceneLoad<T>() where T : Component {
            if (Defines.isARCompanionDefine && !Application.isEditor) {
                SceneManager.sceneLoaded += delegate {
                    Assert.IsNull(Object.FindObjectOfType<T>());
                    new GameObject(typeof(T).Name).AddComponent<T>();
                };
            }
        }
    }
}
