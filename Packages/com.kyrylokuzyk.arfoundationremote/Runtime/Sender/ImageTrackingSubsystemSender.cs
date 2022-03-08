using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class ImageTrackingSubsystemSender : MonoBehaviour, IEditorEventSubscriber {
        [SerializeField] ARTrackedImageManager manager = null;
        
        readonly Dictionary<Guid, Guid> allGuids = new Dictionary<Guid, Guid>();
        readonly Dictionary<Guid, Guid> currentGuids = new Dictionary<Guid, Guid>();
        bool canAddImages;
        readonly Queue<ImageDataEditor> messageQueue = new Queue<ImageDataEditor>();
        bool wasEnabledOnce; 

        
        void Awake() {
            Assert.IsNull(manager.referenceLibrary);
            manager.trackedImagesChanged += args => {
                Connection.Send(new ImageData {
                    trackedImagesData = new TrackableChangesData<XRTrackedImageSerializable> {
                        added = filterAndSerialize(args.added),
                        updated = filterAndSerialize(args.updated),
                        removed = filterAndSerialize(args.removed)
                    }
                });

                XRTrackedImageSerializable[] filterAndSerialize(IEnumerable<ARTrackedImage> _images) {
                    return _images
                        .Select(image => new {image, guid = getEditorImageGuid(image.referenceImage.guid)})
                        .Where(_ => _.guid.HasValue)
                        .Select(_ => XRTrackedImageSerializable.Create(_.image, _.guid.Value))
                        .ToArray();
                }
                
                // When we ScheduleAddImageJob to MutableRuntimeReferenceImageLibrary, a new guid will be generated for the image.
                // I save the original guid to send it send back to the Editor.
                Guid? getEditorImageGuid(Guid guid) {
                    if (allGuids.TryGetValue(guid, out var result)) {
                        return result;
                    } else {
                        if (!loggedNotFoundGuids.Contains(guid)) {
                            loggedNotFoundGuids.Add(guid);
                            Debug.LogError($"editor image guid not found {guid}");
                        }
                
                        return null;
                    }
                }
            };
        }

        void OnEnable() {
            Assert.IsFalse(wasEnabledOnce, $"{nameof(OnEnable)} was called twice on {nameof(ImageTrackingSubsystemSender)}. This will stop the {nameof(waitForSessionInitialisingIfNeeded)} coroutine.");
            wasEnabledOnce = true;
            StartCoroutine(waitForSessionInitialisingIfNeeded());
            Sender.RegisterEditorEventSubscriber(this);
            Connection.Register<ImageDataEditor>(receiveImageData);
        }

        void OnDisable() {
            Sender.UnRegisterEditorEventSubscriber(this);
            Connection.UnRegister<ImageDataEditor>();
        }

        IEnumerator waitForSessionInitialisingIfNeeded() {
            if (Defines.isAndroid) {
                while (ARSession.state < ARSessionState.SessionInitializing) {
                    yield return null;
                }
            }

            log("canAddImages = true;");
            canAddImages = true;
            foreach (var _ in messageQueue) {
                receiveImageData(_);
            }
            messageQueue.Clear();
        }


        void IEditorEventSubscriber.EditorEventReceived(EditorToPlayerMessageType message) {
            if (message.IsStop()) {
                reset();
            }
        }

        void receiveImageData(ImageDataEditor data) {
            if (canAddImages) {
                processImageData(data);
            } else {
                log("messageQueue.Enqueue(data) because !canAddImages");
                messageQueue.Enqueue(data);
            }
        }

        void processImageData(ImageDataEditor imageData) {
            var guids = imageData.referenceImageGuids;
            if (guids != null) {
                log($"receive image guids {string.Join(",", guids)}");
                var embedded = findEmbeddedLibraryWithSameImages(new HashSet<Guid>(guids));
                if (embedded != null) {
                    log($"using embedded library {embedded.guid}");
                    reset();
                    manager.referenceLibrary = manager.CreateRuntimeLibrary(embedded);
                    setManagerEnabled(true);
                    foreach (var _ in guids) {
                        addGuid(_, _);
                    }
                }

                Connection.Send(new ImageData {
                    embeddedImageLibDidSetSuccessfully = embedded != null
                });
                
                return;
            }
            
            var imageLibraryContainer = imageData.imageLibrary;
            if (imageLibraryContainer != null) {
                setNewImageLibrary(imageLibraryContainer);
                return;
            }
            
            var imageToAdd = imageData.imageToAdd;
            if (imageToAdd != null) {
                addImageAndDestroyTexture(imageToAdd.Deserialize());
                return;
            }

            throw new Exception("Empty ImageDataEditor received!");
        }

        void reset() {
            log("reset()");
            currentGuids.Clear();
            setManagerEnabled(false);
            manager.referenceLibrary = null;
        }

        void setManagerEnabled(bool isEnabled) {
            Sender.Instance.SetManagerEnabled(manager, isEnabled);
        }
        
        void setNewImageLibrary([NotNull] ImageLibrarySerializableContainer imageLibraryContainer) {
            log("setNewImageLibrary");
            reset();
            
            var serializedLibrary = imageLibraryContainer.library;
            if (serializedLibrary == null) {
                log("receive image library NULL");
                return;
            }

            log("receive image library, count: " + serializedLibrary.count);
            manager.referenceLibrary = manager.CreateRuntimeLibrary();
            setManagerEnabled(true);
            
            for (int i = 0; i < serializedLibrary.count; i++) {
                addImageAndDestroyTexture(serializedLibrary.DeserializeImage(i));
            }

            if (getCurrentImages().Any()) {
                log("all remote images: " + string.Join(", ", getCurrentImages().Select(_ => _.name)));
            }
        }

        /// There is no way to get the guid of <see cref="XRImageTrackingSubsystem.imageLibrary"/>, so we search for a ref lib with the same set of ref images
        [CanBeNull]
        XRReferenceImageLibrary findEmbeddedLibraryWithSameImages([NotNull] HashSet<Guid> guids) {
            return Settings.Instance.embeddedImageLibraries
                .Where(_ => _ != null)
                .FirstOrDefault(_ => _.GetImageGuids().SetEquals(guids));
        }

        IEnumerable<XRReferenceImage> getCurrentImages() {
            var imageLibrary = manager.referenceLibrary;
            for (int i = 0; i < imageLibrary.count; i++) {
                yield return imageLibrary[i];
            }
        }

        void addImageAndDestroyTexture(XRReferenceImage image) {
            addImage(image);
            Destroy(image.texture);
        }

        void addImage(XRReferenceImage image) {
            var library = manager.referenceLibrary;
            if (library == null) {
                Debug.LogError("ARTrackedImageManager.referenceLibrary is null");
                return;
            }

            if (!(library is MutableRuntimeReferenceImageLibrary mutableLibrary)) {
                Debug.LogError("this platform does not support adding reference images at runtime");
                return;
            }

            var editorImageGuid = image.guid;
            if (currentGuids.Values.Contains(editorImageGuid)) {
                Debug.LogError($"{image.name} image already added");
                return;
            } 
            
            log($"ScheduleAddImageJob {image.name}, {editorImageGuid}, {image.size}, {image.textureGuid}");
            var oldGuids = mutableLibrary.GetImageGuids();
            bool isSuccess = true;
                if (Defines.AR_FOUNDATION_4_1_0_PREVIEW_11_OR_NEWER && supportsValidation(mutableLibrary)) {
                    #if AR_FOUNDATION_4_1_0_PREVIEW_11_OR_NEWER
                        var jobState = mutableLibrary.ScheduleAddImageWithValidationJob(image.texture, image.name, image.width);
                        jobState.jobHandle.Complete();
                        if (jobState.status != AddReferenceImageJobStatus.Success) {
                            isSuccess = false;
                            Debug.LogError($"ScheduleAddImageWithValidationJob {image.name} failed\n" +
                                           $"with status {jobState.status}");
                        }
                    #endif
                } else {
                #pragma warning disable 618
                mutableLibrary.ScheduleAddImageJob(image.texture, image.name, image.width).Complete();
                #pragma warning restore
            }

            bool supportsValidation(MutableRuntimeReferenceImageLibrary lib) {
                #if AR_SUBSYSTEMS_4_1_0_PREVIEW_11_OR_NEWER
                    return lib.supportsValidation;
                #else
                    return false;
                #endif
            }

            if (isSuccess) {
                var addedGuids = mutableLibrary.GetImageGuids().Except(oldGuids).ToArray();
                if (addedGuids.Length == 1) {
                    var addedGuid = addedGuids.First();
                    log("addedGuid " + addedGuid);
                    addGuid(addedGuid, editorImageGuid);
                } else {
                    Debug.LogError($"ScheduleAddImageJob failed\nGuids count: {addedGuids.Length}, image: {image.name}");
                }
            }
        }

        void addGuid(Guid companionAppGuid, Guid editorGuid) {
            allGuids[companionAppGuid] = editorGuid;
            currentGuids[companionAppGuid] = editorGuid;
        }

        readonly HashSet<Guid> loggedNotFoundGuids = new HashSet<Guid>();

        [Conditional("_")]
        public static void log(string s) {
            Debug.Log(nameof(ImageTrackingSubsystemSender) + ":: " + s);
        }
    }


    [Serializable]
    public class XRTrackedImageSerializable: ISerializableTrackable<XRTrackedImage> {
        TrackableIdSerializable trackableIdSer;
        Guid sourceImageId;
        PoseSerializable pose;
        Vector2Serializable size;
        TrackingState trackingState;


        public static XRTrackedImageSerializable Create(ARTrackedImage i, Guid guid) {
            return new XRTrackedImageSerializable {
                trackableIdSer = TrackableIdSerializable.Create(i.trackableId),
                sourceImageId = guid,
                pose = PoseSerializable.Create(i.transform.LocalPose()),
                size = Vector2Serializable.Create(i.size),
                trackingState = i.trackingState,
            };
        }
        
        public TrackableId trackableId => trackableIdSer.Value;
        public XRTrackedImage Value => new XRTrackedImage(trackableId, sourceImageId, pose.Value, size.Value, trackingState, IntPtr.Zero);
    }


    [Serializable]
    public class ImageLibrarySerializableContainer {
        [CanBeNull] public ImageLibrarySerializable library;
    }
    
    
    [Serializable]
    public class ImageLibrarySerializable {
        [NotNull] readonly List<XRReferenceImageSerializable> images;

  
        public ImageLibrarySerializable([NotNull] List<XRReferenceImageSerializable> images) {
            this.images = images;
        }

        public int count => images.Count;
        
        public XRReferenceImage DeserializeImage(int index) => images[index].Deserialize(); 

        public HashSet<Guid> GetGuids() {
            return new HashSet<Guid>(images.Select(_ => _.guid.guid));
        }
    }

    
    [Serializable]
    public class XRReferenceImageSerializable {
        static readonly FieldInfo
            m_SerializedGuid = getField("m_SerializedGuid"),
            m_SerializedTextureGuid = getField("m_SerializedTextureGuid");
        
        public SerializableGuid guid { get; private set; }
        SerializableGuid textureGuid;
        Vector2Serializable? size;
        string name;
        Texture2DSerializable texture;


        public XRReferenceImage Deserialize() {
            return new XRReferenceImage(guid, textureGuid, size?.Value, name, texture.DeserializeTexture());
        }

        public static XRReferenceImageSerializable Create(XRReferenceImage i, [CanBeNull] Texture2D textureOverride) {
            /*if (Defines.isAndroid) {
                // ARCore image tracking works with images less than 300x300, tested with 256x256 image
                var size = i.size;
                const int minSize = 300;
                if (size.x < minSize || size.y < minSize) {
                    Debug.LogError($"{Constants.packageName}: the minimum image size for ARCore is 300x300. More info here: https://developers.google.com/ar/develop/c/augmented-images");
                }
            }*/
            
            Texture2D tex;
            var imageTexture = i.texture;
            if (textureOverride != null) {
                Assert.IsNull(imageTexture);
                tex = textureOverride;
            } else {
                Assert.IsNotNull(imageTexture);
                tex = imageTexture;
            }
            
            Assert.IsNotNull(tex);
            return new XRReferenceImageSerializable {
                guid = getGuid(i, m_SerializedGuid),
                textureGuid = getGuid(i, m_SerializedTextureGuid),
                size = i.specifySize ? Vector2Serializable.Create(i.size) : (Vector2Serializable?) null,
                name = i.name,
                texture = Texture2DSerializable.SerializeToPNG(tex, 1, null)
            };
        }

        static SerializableGuid getGuid(XRReferenceImage i, FieldInfo field) {
            return (SerializableGuid) field.GetValue(i);
        }

        static FieldInfo getField(string name) {
            var result = typeof(XRReferenceImage)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);
            Assert.IsNotNull(result);
            return result;
        }
    }
    
    
    public static class XRReferenceImageLibraryExtensions {
        public static HashSet<Guid> GetImageGuids([NotNull] this IReferenceImageLibrary imageLib) {
            var result = new HashSet<Guid>();
            for (int i = 0; i < imageLib.count; i++) {
                result.Add(imageLib[i].guid);
            }

            return result;
        }
    }
    
    
    [Serializable]
    public struct ImageData {
        public bool? embeddedImageLibDidSetSuccessfully;
        public TrackableChangesData<XRTrackedImageSerializable>? trackedImagesData;
    }

    
    [Serializable]
    public struct ImageDataEditor {
        [CanBeNull] public Guid[] referenceImageGuids;
        [CanBeNull] public ImageLibrarySerializableContainer imageLibrary;
        [CanBeNull] public XRReferenceImageSerializable imageToAdd;


        public override string ToString() {
            return 
                // $"referenceImageGuids: {string.Join(",", referenceImageGuids ?? new Guid[0])}, " +
                    $"referenceImageGuids count: {referenceImageGuids?.Length}, " +
                   // $"imageLibrary guids: {string.Join(",", imageLibrary?.library?.GetGuids() ?? new HashSet<Guid>())}, " +
                   $"imageLibrary count: {imageLibrary?.library?.count}, " +
                   $"imageToAdd: {imageToAdd?.guid}";
        }
    }
}
