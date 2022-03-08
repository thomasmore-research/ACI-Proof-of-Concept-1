using System;
using System.Reflection;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;


public class OriginDataReceiver : MonoBehaviour {
    ARSessionOrigin origin;


           
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void register() {
        Utils.TryCreateCompanionAppObjectOnSceneLoad<OriginDataReceiver>();
    }
    
    void Awake() {
        origin = Sender.Instance.origin;
        Assert.IsNotNull(origin);
    }

    void OnEnable() {
        Connection.Register<SessionOriginData>(receive);
    }

    void OnDisable() {
        Connection.UnRegister<SessionOriginData>();
    }

    void receive(SessionOriginData originData) {
        // Debug.Log("receive origin data\n" + originData);
        var originTransform = origin.transform;
        originTransform.localScale = originData.scale.Value;

        var pose = originData.pose.Value;
        var originDataContentOffsetPosition = originData.contentOffsetPosition;
        if (originDataContentOffsetPosition.HasValue) {
            origin.GetOrCreateContentOffsetTransform().position = originDataContentOffsetPosition.Value.Value;
        }

        originTransform.localPosition = pose.position;
        originTransform.localRotation = pose.rotation;

        var cam = origin.camera;
        var originDataNearClippingPlane = originData.nearClippingPlane;
        if (originDataNearClippingPlane.HasValue) {
            cam.nearClipPlane = originDataNearClippingPlane.Value;
        }

        var originDataFarClippingPlane = originData.farClippingPlane;
        if (originDataFarClippingPlane.HasValue) {
            cam.farClipPlane = originDataFarClippingPlane.Value;
        }
    }
}


[Serializable]
public struct SessionOriginData {
    public PoseSerializable pose { get; private set; }
    public Vector3Serializable scale { get; private set; }
    public Vector3Serializable? contentOffsetPosition { get; private set; }
    public float? nearClippingPlane { get; private set; }
    public float? farClippingPlane { get; private set; }


    public static SessionOriginData Create([NotNull] ARSessionOrigin origin) {
        var transform = origin.transform;
        var contentOffsetGoViaReflection = origin.GetContentOffsetGameObject();
        Vector3Serializable? contentOffsetPosition = null;
        if (contentOffsetGoViaReflection != null) {
            contentOffsetPosition = Vector3Serializable.Create(contentOffsetGoViaReflection.transform.position);
        }

        var camera = origin.camera;
        return new SessionOriginData {
            pose = PoseSerializable.Create(transform.LocalPose()),
            scale = Vector3Serializable.Create(transform.localScale),
            contentOffsetPosition = contentOffsetPosition,
            nearClippingPlane = camera != null ? new float?(camera.nearClipPlane) : null,
            farClippingPlane = camera != null ? new float?(camera.farClipPlane) : null,
        };
    }

    public override string ToString() {
        var contentOffsetPositionStr = contentOffsetPosition.HasValue ? contentOffsetPosition.Value.Value.ToString() : "NULL";
        return "Pose: " + pose + "; scale: " + scale.Value + "; contentOffsetPosition: " + contentOffsetPositionStr;
    }
}


public static class ARSessionOriginExtensions {
    [CanBeNull]
    public static GameObject GetContentOffsetGameObject([NotNull] this ARSessionOrigin origin) {
        var field = origin.GetType().GetField("m_ContentOffsetGameObject", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
        Assert.IsNotNull(field);
        return field.GetValue(origin) as GameObject;
    }

    [NotNull]
    public static Transform GetOrCreateContentOffsetTransform([NotNull] this ARSessionOrigin origin) {
        var property = origin.GetType().GetProperty("contentOffsetTransform", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty);
        Assert.IsNotNull(property);
        var result = property.GetValue(origin) as Transform;
        Assert.IsNotNull(result);
        return result;
    }
}
