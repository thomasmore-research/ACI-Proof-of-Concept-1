using UnityEngine;
using UnityEngine.Assertions;


namespace ARFoundationRemote.Runtime {
    public class ScriptableObjectSingleton<T> : ScriptableObject where T: ScriptableObjectSingleton<T> {
        static T instance;
        
        public static T Instance {
            get {
                if (instance == null) {
                    var typeName = typeof(T).Name;
                    instance = Resources.Load<T>(typeName);
                    Assert.IsNotNull(instance, $"{Constants.packageName}: please check that the file exists: 'Assets/Plugins/ARFoundationRemoteInstaller/Resources/{typeName}.asset'");
                }
                
                return instance;
            }
        }
    }
}
