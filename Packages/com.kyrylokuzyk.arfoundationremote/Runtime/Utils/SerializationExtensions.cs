using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using JetBrains.Annotations;


namespace ARFoundationRemote.Runtime {
    public static class SerializationExtensions {
        public static byte[] SerializeToByteArray(this object obj, CompressionLevel compressionLevel = CompressionLevel.Fastest) {
            if (obj == null) {
                throw new ArgumentNullException(nameof(obj));
            }

            using (var memoryStream = new MemoryStream()) {
                using (var gZipStream = new GZipStream(memoryStream, compressionLevel)) {
                    new BinaryFormatter().Serialize(gZipStream, obj);
                }

                return memoryStream.ToArray();
            }
        }

        public static T Deserialize<T>([NotNull] this byte[] bytes) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }

            using (var memoryStream = new MemoryStream(bytes)) {
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress)) {
                    return (T) new BinaryFormatter().Deserialize(gZipStream);
                }
            }
        }
        
        public static bool TryDeserialize<T>([NotNull] this byte[] bytes, out T result) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }

            using (var memoryStream = new MemoryStream(bytes)) {
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress)) {
                    try {
                        result = (T) new BinaryFormatter().Deserialize(gZipStream);
                        return true;
                    } catch (SerializationException) {
                        result = default;
                        return false;
                    }
                }
            }
        }
    }
}
