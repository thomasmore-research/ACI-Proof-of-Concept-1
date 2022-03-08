using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;


namespace ARFoundationRemote.Runtime {
    public class EditorViewReceiver : MonoBehaviour, IEditorEventSubscriber {
        [SerializeField] RawImage rawImage = null;
        [SerializeField] Material opaqueMaterial = null; 

        [CanBeNull] Texture2D colorTexture;
        readonly int colorTexPropId = Shader.PropertyToID("_MainTex");


        void Awake() {
            rawImage.enabled = false;
        }

        void OnEnable() {
            Sender.RegisterEditorEventSubscriber(this);
            Connection.Register<EditorViewData>(receive);
        }

        void OnDisable() {
            Sender.UnRegisterEditorEventSubscriber(this);
            Connection.UnRegister<EditorViewData>();
        }

        public void EditorEventReceived(EditorToPlayerMessageType message) {
            if (message.IsStop()) {
                rawImage.enabled = false;
            }
        }

        void receive(EditorViewData editorViewData) {
            // disable raw image, then enable it back to support resizing
            rawImage.enabled = false;
            editorViewData.colorTexture.ResizeIfNeededAndDeserializeInto(ref colorTexture);

            var material = opaqueMaterial;
            Assert.IsNotNull(material, "material != null");
            rawImage.material = material;
            Assert.AreEqual(rawImage.materialForRendering, material);
            material.SetTexture(colorTexPropId, colorTexture);

            rawImage.enabled = true;
        }

        void OnDestroy() {
            if (colorTexture != null) {
                Destroy(colorTexture);
            }
        }
    }


    [Serializable]
    public struct EditorViewData {
        public Texture2DSerializable colorTexture;
    }
}
