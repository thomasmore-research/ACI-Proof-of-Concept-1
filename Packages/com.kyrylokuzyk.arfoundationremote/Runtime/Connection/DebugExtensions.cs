using System;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public static class DebugExtensions {
        public static string AllFieldsAndPropsToString([CanBeNull] this object subject, bool includePrivate = false, bool logErrors = true) {
            if (subject == null) {
                return "NULL";
            }
            
            if (subject is string str) {
                return str;
            }
            
            var sb = new StringBuilder();
            Action<string, object> addFieldOrProp = (name, value) => {
                var fieldsValStr = value == null ? "[NULL]" : value.ToString();
                sb.Append(name).Append(": ").Append(fieldsValStr).Append("\n");
            };

            var flags = BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.NonPublic;

            if (includePrivate) {
                flags |= BindingFlags.NonPublic;
            }

            var t = subject.GetType();
            sb.Append($"TYPE: {t.Name}\n");
            var fields = t.GetFieldsX(flags.AppendGetField());
            foreach (var field in fields) {
                try {
                    addFieldOrProp(field.Name, field.GetValue(subject));
                } catch (Exception e) {
                    if (logErrors)
                        Debug.LogError(e);
                }
            }

            var props = t.GetPropertiesX(flags.AppendGetProperty());
            foreach (var prop in props) {
                try {
                    addFieldOrProp(prop.Name, prop.GetValue(subject, null));
                } catch (Exception e) {
                    if (logErrors)
                        Debug.LogError(e);
                }
            }

            return sb.ToString();
        }

        public static FieldInfo[] GetFieldsX(this Type t, BindingFlags flags) {
#if NETFX_CORE_8_1
        return TypeWinRT.GetFields(t, flags);
#else
            return t.GetFields(flags);
#endif
        }

        public static BindingFlags AppendGetField(this BindingFlags flags) {
            return flags.AppendBindingFlag(
#if !UNITY_WSA_10_0
                BindingFlags.GetField
#endif
            );
        }

        private static BindingFlags AppendBindingFlag(this BindingFlags flags, BindingFlags flag = 0) {
            return flags | flag;
        }

        public static PropertyInfo[] GetPropertiesX(this Type t, BindingFlags flags) {
#if NETFX_CORE_8_1
	        return TypeWinRT.GetProperties(t, flags);
#else
	        return t.GetProperties(flags);
#endif
        }

        public static BindingFlags AppendGetProperty(this BindingFlags flags) {
            return flags.AppendBindingFlag(
#if !UNITY_WSA_10_0
                BindingFlags.GetProperty
#endif
            );
        }
    }
}
