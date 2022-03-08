using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Assertions;


namespace ARFoundationRemote.Runtime {
    public class Reflector<T> where T : new() {
        static readonly Dictionary<string, FieldInfo> fields = typeof(T)
            .GetFields(BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic)
            .ToDictionary(_ => _.Name);

        static readonly Dictionary<string, PropertyInfo> props = typeof(T)
            .GetProperties(BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic)
            .ToDictionary(_ => _.Name);

        public ReflectorResult GetResultBuilder() {
            return new ReflectorResult();
        }

        /*public V GetField<V>(T obj, string name) {
            if (!fields.ContainsKey(name)) {
                throw new Exception($"{nameof(T)} has no field with name {name}");
            }
            
            var result = fields[name].GetValue(obj);
            Assert.IsTrue(result is V);
            return (V) result;
        }*/

        public V GetProperty<V>(T obj, string name) {
            var result = props[name].GetValue(obj);
            Assert.IsTrue(result is V);
            return (V) result;
        }

        public class ReflectorResult {
            object boxed = new T();

            public void SetField(string name, object val) {
                fields[name].SetValue(boxed, val);
            }

            public T Result => (T) boxed;
        }
    }
}
