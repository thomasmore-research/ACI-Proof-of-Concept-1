using System;
using System.Collections.Generic;
using System.Linq;
using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Input = ARFoundationRemoteExamples.InputWrapper;


namespace ARFoundationRemoteExamples {
    public class AnchorsExample : MonoBehaviour {
        [SerializeField] ARAnchorManager anchorManager = null;
        [SerializeField] ARRaycastManager raycastManager = null;
        [SerializeField] ARSessionOrigin origin = null;
        [SerializeField] ARPlaneManager planeManager = null;
        [SerializeField] TrackableType raycastMask = TrackableType.PlaneWithinPolygon;

        AnchorTestType type = AnchorTestType.Add;


        void OnEnable() {
            anchorManager.anchorsChanged += anchorsChanged;
        }

        void OnDisable() {
            anchorManager.anchorsChanged -= anchorsChanged;
        }

        void anchorsChanged(ARAnchorsChangedEventArgs args) {
            AnchorSubsystemSender.LogChangedAnchors(args);
        }

        void Update() {
            for (int i = 0; i < Input.touchCount; i++) {
                var touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) {
                    continue;
                }
                
                var ray = origin.camera.ScreenPointToRay(touch.position);
                var hits = new List<ARRaycastHit>();
                var hasHit = raycastManager.Raycast(ray, hits, raycastMask);
                if (hasHit) {
                    switch (type) {
                        case AnchorTestType.Add: {
                            #pragma warning disable 618
                            var anchor = anchorManager.AddAnchor(hits.First().pose);
                            #pragma warning restore
                            print($"anchor added: {anchor != null}");
                            break;
                        }
                        case AnchorTestType.AttachToPlane: {
                            var attachedToPlane = tryAttachToPlane(hits);
                            print($"anchor attached successfully: {attachedToPlane}");
                            break;
                        }
                        default:
                            throw new Exception();
                    }
                } else {
                    // print("no hit");
                }
            }
        }

        bool tryAttachToPlane(List<ARRaycastHit> hits) {
            foreach (var hit in hits) {
                var plane = planeManager.GetPlane(hit.trackableId);
                if (plane != null) {
                    var anchor = anchorManager.AttachAnchor(plane, hit.pose);
                    if (anchor != null) {
                        return true;
                    }
                }
            }

            return false;
        }

        void OnGUI() {
            var h = 200;
            var y = Screen.height;

            y -= h;
            if (GUI.Button(new Rect(0, y,400,h), $"Current type: {type}")) {
                type = type == AnchorTestType.Add ? AnchorTestType.AttachToPlane : AnchorTestType.Add;
            }

            y -= h;
            if (GUI.Button(new Rect(0, y, 400, h), "Remove all anchors")) {
                removeAllAnchors();
            }
        }

        void removeAllAnchors() {
            var copiedAnchors = new HashSet<ARAnchor>();
            foreach (var _ in anchorManager.trackables) {
                copiedAnchors.Add(_);
            }

            foreach (var anchor in copiedAnchors) {
                if (anchor == null) {
                    continue;
                }
                
                if (Defines.arSubsystems_4_1_0_preview_11_or_newer) {
                    DestroyImmediate(anchor.gameObject);
                } else {
                    #pragma warning disable 618
                    var removed = anchorManager.RemoveAnchor(anchor);
                    #pragma warning restore
                    Debug.Log($"Anchor removed {anchor.trackableId}: {removed}");
                }
            }
        }

        enum AnchorTestType {
            Add,
            AttachToPlane
        }
    }
}
