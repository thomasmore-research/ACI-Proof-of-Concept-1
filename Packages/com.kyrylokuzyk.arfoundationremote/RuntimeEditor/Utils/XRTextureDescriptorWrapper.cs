#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;


public readonly struct XRTextureDescriptorWrapper {
    readonly IntPtr nativeTexture;
    readonly int width;
    readonly int height;
    readonly int mipmapCount;
    readonly TextureFormat format;
    readonly int propertyNameId;
    #if ARFOUNDATION_4_0_2_OR_NEWER
    readonly int depth;
    readonly TextureDimension dimension;
    #endif

    static readonly Dictionary<string, FieldInfo> fields = typeof(XRTextureDescriptor).GetFields(BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic)
        .ToDictionary(_ => _.Name);

    public XRTextureDescriptorWrapper(Texture2D tex, int propertyNameId) {
        nativeTexture = tex.GetNativeTexturePtr();
        width = tex.width;
        height = tex.height;
        mipmapCount = tex.mipmapCount;
        format = tex.format;
        this.propertyNameId = propertyNameId;
        #if ARFOUNDATION_4_0_2_OR_NEWER
            depth = 1;
            dimension = tex.dimension;
        #endif
    }

    public XRTextureDescriptor Value {
        get {
            object boxed = new XRTextureDescriptor();
            setValue(boxed, "m_NativeTexture", nativeTexture);
            setValue(boxed, "m_Width", width);
            setValue(boxed, "m_Height", height);
            setValue(boxed, "m_MipmapCount", mipmapCount);
            setValue(boxed, "m_Format", format);
            setValue(boxed, "m_PropertyNameId", propertyNameId);
            #if ARFOUNDATION_4_0_2_OR_NEWER
                setValue(boxed, "m_Depth", depth);
                setValue(boxed, "m_Dimension", dimension);
            #endif
            var result = (XRTextureDescriptor) boxed;
            Assert.IsTrue(result.valid);
            return result;
        }
    }

    static void setValue(object obj, string fieldName, object val) {
        fields[fieldName].SetValue(obj, val);
    }
}
#endif
