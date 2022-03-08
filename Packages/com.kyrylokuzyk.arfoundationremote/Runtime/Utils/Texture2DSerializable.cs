using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;


namespace ARFoundationRemote.Runtime {
    [Serializable]
    public struct Texture2DSerializable {
        readonly byte[] data;
        readonly int width;
        readonly int height;
        readonly TextureFormat format;
        readonly bool compressed;


        public Texture2DSerializable(byte[] data, int width, int height, TextureFormat format, bool compressed) {
            this.data = data;
            this.width = width;
            this.height = height;
            this.format = format;
            this.compressed = compressed;
        }
        
        public static Texture2DSerializable SerializeToJPG([NotNull] RenderTexture rt, int quality = 75) {
            const TextureFormat format = TextureFormat.RGB24;
            return new Texture2DSerializable(
                downsize(rt, format).EncodeToJPG(quality),
                rt.width,
                rt.height,
                format,
                true
            );
        }
      
        public static Texture2DSerializable SerializeToPNG([NotNull] RenderTexture rt) {
            const TextureFormat format = TextureFormat.ARGB32;
            return new Texture2DSerializable(
                downsize(rt, format).EncodeToPNG(),
                rt.width,
                rt.height,
                format,
                true
            );
        }
    
        public static Texture2DSerializable SerializeToRaw([NotNull] RenderTexture rt, TextureFormat format) {
            return new Texture2DSerializable(
                downsize(rt, format).GetRawTextureData(),
                rt.width,
                rt.height,
                format,
                false
            );
        }

        static Texture2D downsize(RenderTexture rt, TextureFormat textureFormat) {
            Assert.AreEqual(RenderTexture.active, rt, "AreEqual(rt, RenderTexture.active)");
            var downsized = GetCachedTexture(rt.width, rt.height, textureFormat);
            downsized.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            Assert.AreEqual(textureFormat, downsized.format, "AreEqual(tex.format, downsized.format)");
            return downsized;
        }

        public static Texture2DSerializable SerializeToPNG([NotNull] Texture2D tex, float resolutionMultiplier, [CanBeNull] Material materialForTextureBlit) {
            return serializeCompressed(tex, resolutionMultiplier, materialForTextureBlit, TextureFormat.ARGB32, _ => _.EncodeToPNG());
        }

        public static Texture2DSerializable SerializeToJPG([NotNull] Texture2D tex, float resolutionMultiplier, [CanBeNull] Material materialForTextureBlit, int quality = 75) {
            return serializeCompressed(tex, resolutionMultiplier, materialForTextureBlit, TextureFormat.RGB24, _ => _.EncodeToJPG(quality));
        }

        static Texture2DSerializable serializeCompressed([NotNull] Texture2D tex, float resolutionMultiplier, [CanBeNull] Material materialForTextureBlit, TextureFormat format, [NotNull] Func<Texture2D, byte[]> getBytes) {
            Assert.IsTrue(0f < resolutionMultiplier && resolutionMultiplier <= 1f);
            var w = Mathf.RoundToInt(tex.width * resolutionMultiplier);
            var h = Mathf.RoundToInt(tex.height * resolutionMultiplier);
            var downsized = downsize(tex, w, h, materialForTextureBlit, format);
            Assert.AreEqual(format, downsized.format);
            return new Texture2DSerializable(
                getBytes(downsized),
                w,
                h,
                format,
                true
            );
        }

        static Texture2D downsize(Texture2D tex, int width, int height, [CanBeNull] Material materialForTextureBlit, TextureFormat format) {
            var rt = RenderTexture.GetTemporary(width, height, 0);
            var prevRt = RenderTexture.active;
            blit(tex, rt, materialForTextureBlit);
            Assert.AreEqual(RenderTexture.active, rt, "AreEqual(rt, RenderTexture.active)");
            var downsized = GetCachedTexture(rt.width, rt.height, format);
            downsized.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            RenderTexture.active = prevRt;
            RenderTexture.ReleaseTemporary(rt);
            return downsized;
        }

        /// Graphics.Blit sets RenderTexture.active to dest RenderTexture.
        /// Graphics.Blit sets sourceTexture to _MainTex property and performs a copy.
        /// If there is no _MainTex property, the result is undefined (produces green texture with ARKitBackground.shader).
        static void blit([NotNull] Texture2D sourceTexture, [NotNull] RenderTexture destRt, [CanBeNull] Material materialForTextureBlit) {
            Assert.IsNotNull(sourceTexture, "sourceTexture != null");
            Assert.IsNotNull(destRt, "destRt != null");
            if (materialForTextureBlit != null) {
                Assert.IsTrue(materialForTextureBlit.HasProperty("_MainTex"));
                // Assert.AreEqual(materialForTextureBlit.mainTexture, sourceTexture); // this assertion can fail on Android when session is initializing or changing the camera config
                Graphics.Blit(sourceTexture, destRt, materialForTextureBlit);
            } else {
                Graphics.Blit(sourceTexture, destRt);
            }
        }

        public static void ClearCache() {
            foreach (var _ in textures) {
                UnityEngine.Object.Destroy(_);
            }

            textures.Clear();
        }

        static List<Texture2D> textures = new List<Texture2D>();

        public static Texture2D GetCachedTexture(int w, int h, TextureFormat format) {
            var existing = textures.Find(_ => _.width == w && _.height == h && _.format == format);
            if (existing != null) {
                return existing;
            } else {
                var isLinear = !Application.isEditor && QualitySettings.activeColorSpace == ColorSpace.Linear; // magic I don't currently understand
                var newTex = createEmpty(w, h, format, isLinear);
                textures.Add(newTex);
                return newTex;
            }
        }

        public Texture2D DeserializeTexture() {
            Texture2D result = null;
            ResizeIfNeededAndDeserializeInto(ref result);
            return result;
        }

        public bool CanDeserializeInto([NotNull] Texture2D tex) {
            return tex.width == width && tex.height == height && tex.format == format;
        }

        public void ResizeIfNeededAndDeserializeInto([CanBeNull] ref Texture2D tex) {
            if (tex != null && !CanDeserializeInto(tex)) {
                UnityEngine.Object.Destroy(tex);
                tex = null;
            }
            
            if (tex == null) {
                const bool isLinear = false; // magic I don't currently understand
                tex = createEmpty(width, height, format, isLinear);
            }
            
            deserializeInto(tex);
        }

        void deserializeInto([NotNull] Texture2D tex) {
            Assert.IsTrue(CanDeserializeInto(tex));
            if (compressed) {
                var isLoaded = tex.LoadImage(data);
                Assert.IsTrue(isLoaded);
            } else {
                tex.LoadRawTextureData(data);
                tex.Apply();
            }
            
            Assert.IsTrue(CanDeserializeInto(tex));
        }

        static Texture2D createEmpty(int w, int h, TextureFormat format, bool isLinear) {
            return new Texture2D(w, h, format, false, isLinear);
        }

        public override string ToString() {
            return $"{width}, {height}";
        }
    }
}
