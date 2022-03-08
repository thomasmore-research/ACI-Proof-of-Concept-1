using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;


namespace ARFoundationRemote.Editor {
    public class TextureAndDescriptor {
        [CanBeNull] Texture2D texture;
        XRTextureDescriptor _descriptor;

        
        public TextureAndDescriptor() {
        }

        public TextureAndDescriptor([NotNull] Texture2D solidColorTexture, int propNameId) {
            texture = solidColorTexture;
            updateDescriptor(propNameId);
        }

        public XRTextureDescriptor? descriptor {
            get {
                if (_descriptor.valid) {
                    return _descriptor;
                } else {
                    // using invalid descriptor will crash Unity Editor
                    return null;
                }
            }
        }

        public void Update(SerializedTextureAndPropId ser) {
            Update(ser.texture, ser.GetPropertyNameId());
        }

        public void Update(Texture2DSerializable texture2DSerializable, int propNameId) {
            texture2DSerializable.ResizeIfNeededAndDeserializeInto(ref texture);
            updateDescriptor(propNameId);
            #if ARFOUNDATION_4_0_2_OR_NEWER
                Assert.AreEqual(TextureDimension.Tex2D, _descriptor.dimension);
            #endif
        }

        void updateDescriptor(int propNameId) {
            _descriptor = new XRTextureDescriptorWrapper(texture, propNameId).Value;
        }

        public void DestroyTexture() {
            _descriptor.Reset();
            if (texture != null) {
                Object.Destroy(texture);
                texture = null;
            }
        }
    }
}
