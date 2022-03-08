using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    [Serializable]
    public struct Vector2Serializable {
        float x, y;

        public static Vector2Serializable Create(Vector2 v) {
            return new Vector2Serializable {
                x = v.x,
                y = v.y,
            };
        }

        public Vector2 Value => new Vector3(x, y);
    }


    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3Serializable {
        float x, y, z;

        public static Vector3Serializable Create(Vector3 v) {
            return new Union {nonSerializable = v}.serializable;
        }

        public Vector3 Value => new Union {serializable = this}.nonSerializable;
               
        [StructLayout(LayoutKind.Explicit)]
        struct Union {
            [FieldOffset(0)] public Vector3Serializable serializable;
            [FieldOffset(0)] public Vector3 nonSerializable;
        }
    }
    
    
    [Serializable]
    public struct QuaternionSerializable {
        float x, y, z, w;

        public static QuaternionSerializable Create(Quaternion q) {
            return new QuaternionSerializable {
                x = q.x,
                y = q.y,
                z = q.z,
                w = q.w
            };
        }

        public Quaternion Quaternion => new Quaternion(x, y, z, w);
    }


    [Serializable]
    public struct PoseSerializable {
        Vector3Serializable position;
        QuaternionSerializable rotation;


        public static PoseSerializable Create(Pose pose) {
            return new PoseSerializable {
                position = Vector3Serializable.Create(pose.position),
                rotation = QuaternionSerializable.Create(pose.rotation)
            };
        }

        public Pose Value => new Pose(position.Value, rotation.Quaternion);

        public override string ToString() {
            return position.Value + ", " + rotation.Quaternion;
        }
    }

    [Serializable]
    public class SphericalHarmonicsL2Serializable {
        const int bands = 3, coefficients = 9;
        float[] floats;
        

        public static SphericalHarmonicsL2Serializable Create(SphericalHarmonicsL2 o) {
            var array = new float[bands * coefficients];
            for (int i = 0; i < bands; i++) {
                for (int j = 0; j < coefficients; j++) {
                    array[i * coefficients + j] = o[i, j];
                }
            }
            
            return new SphericalHarmonicsL2Serializable {
                floats = array
            };
        }

        public SphericalHarmonicsL2 Value {
            get {
                var result = new SphericalHarmonicsL2();
                for (int i = 0; i < bands; i++) {
                    for (int j = 0; j < coefficients; j++) {
                        result[i, j] = floats[i * coefficients + j];
                    }
                }

                return result;
            }
        }
    }
    
    /// Unity 2019.2 accepts only lists
    public static class Extensions {
        public static List<Vector3> ToNonSerializableList([NotNull] this IEnumerable<Vector3Serializable> collection) {
            return collection.Select(_ => _.Value).ToList();
        }
      
        public static List<Vector4> ToNonSerializableList([NotNull] this IEnumerable<Vector4Serializable> collection) {
            return collection.Select(_ => _.Value).ToList();
        }
     
        public static List<Vector2> ToNonSerializableList([NotNull] this IEnumerable<Vector2Serializable> collection) {
            return collection.Select(_ => _.Value).ToList();
        }
      
        public static List<Color> ToNonSerializableList([NotNull] this IEnumerable<ColorSerializable> collection) {
            return collection.Select(_ => _.Value).ToList();
        }
    }
}
