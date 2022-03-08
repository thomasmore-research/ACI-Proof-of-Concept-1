// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using TMPro;

public abstract class ASABase : MonoBehaviour
{
    #region Member Variables
    protected bool isErrorActive = false;
    protected Text feedbackBox;
    protected readonly List<string> anchorIdsToLocate = new List<string>();
    protected AnchorLocateCriteria anchorLocateCriteria = null;
    protected CloudSpatialAnchor currentCloudAnchor;
    protected CloudSpatialAnchorWatcher currentWatcher;
    protected GameObject spawnedObject = null;
    protected Material spawnedObjectMat = null;

    private IDictionary<string, GameObject> positionedProducts = new Dictionary<string, GameObject>();
    #endregion // Member Variables

    public ImageFadeImproved userFeed;
    #region Unity Inspector Variables
    [SerializeField]
    [Tooltip("The prefab used to represent an anchored object.")]
    private GameObject anchoredObjectPrefab = null;

    [SerializeField]
    [Tooltip("SpatialAnchorManager instance to use for this demo. This is required.")]
    private SpatialAnchorManager cloudManager = null;
    #endregion // Unity Inspector Variables

    /// <summary>
    /// Destroying the attached Behaviour will result in the game or Scene
    /// receiving OnDestroy.
    /// </summary>
    /// <remarks>OnDestroy will only be called on game objects that have previously been active.</remarks>
    public virtual void OnDestroy()
    {
        if (CloudManager != null)
        {
            CloudManager.StopSession();
        }

        if (currentWatcher != null)
        {
            currentWatcher.Stop();
            currentWatcher = null;
        }

        CleanupSpawnedObjects();
    }

    public virtual bool SanityCheckAccessConfiguration()
    {
        if (string.IsNullOrWhiteSpace(CloudManager.SpatialAnchorsAccountId)
            || string.IsNullOrWhiteSpace(CloudManager.SpatialAnchorsAccountKey)
            || string.IsNullOrWhiteSpace(CloudManager.SpatialAnchorsAccountDomain))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Start is called on the frame when a script is enabled just before any
    /// of the Update methods are called the first time.
    /// </summary>
    public virtual void Start()
    {
        try
        {
            if (CloudManager == null)
            {
                Debug.Break();
                feedbackBox.text = $"{nameof(CloudManager)} reference has not been set. Make sure it has been added to the scene and wired up to {this.name}.";
                return;
            }

            if (!SanityCheckAccessConfiguration())
            {
                feedbackBox.text = $"{nameof(SpatialAnchorManager.SpatialAnchorsAccountId)}, {nameof(SpatialAnchorManager.SpatialAnchorsAccountKey)} and {nameof(SpatialAnchorManager.SpatialAnchorsAccountDomain)} must be set on {nameof(SpatialAnchorManager)}";
            }


            if (AnchoredObjectPrefab == null)
            {
                feedbackBox.text = "CreationTarget must be set";
                return;
            }

            CloudManager.SessionUpdated += CloudManager_SessionUpdated;
            CloudManager.AnchorLocated += CloudManager_AnchorLocated;
            CloudManager.LocateAnchorsCompleted += CloudManager_LocateAnchorsCompleted;
            CloudManager.LogDebug += CloudManager_LogDebug;
            CloudManager.Error += CloudManager_Error;

            anchorLocateCriteria = new AnchorLocateCriteria();
        }
        catch (System.Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Cleans up spawned objects.
    /// </summary>
    protected virtual void CleanupSpawnedObjects()
    {
        if (spawnedObject != null)
        {
            Destroy(spawnedObject);
            spawnedObject = null;
        }

        if (spawnedObjectMat != null)
        {
            Destroy(spawnedObjectMat);
            spawnedObjectMat = null;
        }

        foreach (KeyValuePair<string, GameObject> go in positionedProducts)
        {
            Destroy(go.Value);
        }
        positionedProducts.Clear();
    }

    protected CloudSpatialAnchorWatcher CreateWatcher()
    {
        if ((CloudManager != null) && (CloudManager.Session != null))
        {
            return CloudManager.Session.CreateWatcher(anchorLocateCriteria);
        }
        else
        {
            return null;
        }
    }

    protected void SetAnchorIdsToLocate(IEnumerable<string> anchorIds)
    {
        if (anchorIds == null)
        {
            throw new ArgumentNullException(nameof(anchorIds));
        }

        anchorLocateCriteria.NearAnchor = new NearAnchorCriteria();

        anchorIdsToLocate.Clear();
        anchorIdsToLocate.AddRange(anchorIds);

        anchorLocateCriteria.Identifiers = anchorIdsToLocate.ToArray();
    }

    protected void ResetAnchorIdsToLocate()
    {
        anchorIdsToLocate.Clear();
        anchorLocateCriteria.Identifiers = new string[0];
    }

    protected void SetNearbyAnchor(CloudSpatialAnchor nearbyAnchor, float DistanceInMeters, int MaxNearAnchorsToFind)
    {
        if (nearbyAnchor == null)
        {
            anchorLocateCriteria.NearAnchor = new NearAnchorCriteria();
            return;
        }

        NearAnchorCriteria nac = new NearAnchorCriteria();
        nac.SourceAnchor = nearbyAnchor;
        nac.DistanceInMeters = DistanceInMeters;
        nac.MaxResultCount = MaxNearAnchorsToFind;

        anchorLocateCriteria.NearAnchor = nac;
    }

    protected void SetNearDevice(float DistanceInMeters, int MaxAnchorsToFind)
    {
        NearDeviceCriteria nearDeviceCriteria = new NearDeviceCriteria();
        nearDeviceCriteria.DistanceInMeters = DistanceInMeters;
        nearDeviceCriteria.MaxResultCount = MaxAnchorsToFind;

        anchorLocateCriteria.NearDevice = nearDeviceCriteria;
    }

    protected void SetGraphEnabled(bool UseGraph, bool JustGraph = false)
    {
        anchorLocateCriteria.Strategy = UseGraph ?
                                        (JustGraph ? LocateStrategy.Relationship : LocateStrategy.AnyStrategy) :
                                        LocateStrategy.VisualInformation;
    }

    /// <summary>
    /// Bypassing the cache will force new queries to be sent for objects, allowing
    /// for refined poses over time.
    /// </summary>
    /// <param name="BypassCache"></param>
    public void SetBypassCache(bool BypassCache)
    {
        anchorLocateCriteria.BypassCache = BypassCache;
    }


    /// <summary>
    /// Moves the specified anchored object.
    /// </summary>
    /// <param name="objectToMove">The anchored object to move.</param>
    /// <param name="worldPos">The world position.</param>
    /// <param name="worldRot">The world rotation.</param>
    /// <param name="cloudSpatialAnchor">The cloud spatial anchor.</param>
    protected virtual void MoveAnchoredObject(GameObject objectToMove, Vector3 worldPos, Quaternion worldRot, CloudSpatialAnchor cloudSpatialAnchor = null)
    {
        // Get the cloud-native anchor behavior
        CloudNativeAnchor cna = objectToMove.GetComponent<CloudNativeAnchor>();

        // Warn and exit if the behavior is missing
        if (cna == null)
        {
            Debug.LogWarning($"The object {objectToMove.name} is missing the {nameof(CloudNativeAnchor)} behavior.");
            return;
        }

        // Is there a cloud anchor to apply
        if (cloudSpatialAnchor != null)
        {
            // Yes. Apply the cloud anchor, which also sets the pose.
            cna.CloudToNative(cloudSpatialAnchor);
        }
        else
        {
            // No. Just set the pose.
            cna.SetPose(worldPos, worldRot);
        }
    }

    /// <summary>
    /// Called when a cloud anchor is located.
    /// </summary>
    /// <param name="args">The <see cref="AnchorLocatedEventArgs"/> instance containing the event data.</param>
    protected virtual void OnCloudAnchorLocated(AnchorLocatedEventArgs args)
    {
        // To be overridden.
    }

    /// <summary>
    /// Called when cloud anchor location has completed.
    /// </summary>
    /// <param name="args">The <see cref="LocateAnchorsCompletedEventArgs"/> instance containing the event data.</param>
    protected virtual void OnCloudLocateAnchorsCompleted(LocateAnchorsCompletedEventArgs args)
    {
        Debug.Log("Locate pass complete");
    }

    /// <summary>
    /// Called when the current cloud session is updated.
    /// </summary>
    protected virtual void OnCloudSessionUpdated()
    {
        // To be overridden.
    }


    /// <summary>
    /// Called when a cloud anchor is not saved successfully.
    /// </summary>
    /// <param name="exception">The exception.</param>
    protected virtual void OnSaveCloudAnchorFailed(Exception exception)
    {
        // we will block the next step to show the exception message in the UI.
        isErrorActive = true;
        Debug.LogException(exception);
        Debug.Log("Failed to save anchor " + exception.ToString());

        UnityDispatcher.InvokeOnAppThread(() => this.feedbackBox.text = string.Format("Error: {0}", exception.ToString()));
    }

    /// <summary>
    /// Called when a cloud anchor is saved successfully.
    /// </summary>
    protected virtual Task OnSaveCloudAnchorSuccessfulAsync()
    {
        // To be overridden.
        return Task.CompletedTask;
    }


    /// <summary>
    /// Saves the current object anchor to the cloud.
    /// </summary>
    protected virtual async Task SaveCurrentObjectAnchorToCloudAsync()
    {
        // Get the cloud-native anchor behavior
        CloudNativeAnchor cna = spawnedObject.GetComponent<CloudNativeAnchor>();

        // If the cloud portion of the anchor hasn't been created yet, create it
        if (cna.CloudAnchor == null)
        {
            await cna.NativeToCloud();
        }

        // Get the cloud portion of the anchor
        CloudSpatialAnchor cloudAnchor = cna.CloudAnchor;

        // In this sample app we delete the cloud anchor explicitly, but here we show how to set an anchor to expire automatically
        //cloudAnchor.Expiration = DateTimeOffset.Now.AddDays(7);

        if (!CloudManager.IsReadyForCreate)
            userFeed.StartAnimation(FadeAction.FadeIn);

        while (!CloudManager.IsReadyForCreate)
        {
            await Task.Delay(330);
            float createProgress = CloudManager.SessionStatus.RecommendedForCreateProgress;
            feedbackBox.text = $"Move your device to capture more environment data: {createProgress:0%}";

            userFeed.UserFeedMessage($"Move your device: {createProgress:0%}");
        }

        bool success = false;

        feedbackBox.text = "Saving...";

        //userFeed.UserFeedMessage("Saving...");
        //userFeed.StartAnimation(FadeAction.FadeOut);

        try
        {
            // Actually save
            await CloudManager.CreateAnchorAsync(cloudAnchor);

            // Store
            currentCloudAnchor = cloudAnchor;

            // Success?
            success = currentCloudAnchor != null;

            if (success && !isErrorActive)
            {
                // Await override, which may perform additional tasks
                // such as storing the key in the AnchorExchanger
                await OnSaveCloudAnchorSuccessfulAsync();
            }
            else
            {
                OnSaveCloudAnchorFailed(new Exception("Failed to save, but no exception was thrown."));
            }
        }
        catch (Exception ex)
        {
            OnSaveCloudAnchorFailed(ex);
        }
    }

    /// <summary>
    /// Spawns a new anchored object.
    /// </summary>
    /// <param name="worldPos">The world position.</param>
    /// <param name="worldRot">The world rotation.</param>
    /// <returns><see cref="GameObject"/>.</returns>
    protected virtual GameObject SpawnNewAnchoredObject(Vector3 worldPos, Quaternion worldRot)
    {
        // Create the prefab
        GameObject newGameObject = GameObject.Instantiate(AnchoredObjectPrefab, worldPos, worldRot);

        // Attach a cloud-native anchor behavior to help keep cloud
        // and native anchors in sync.
        newGameObject.AddComponent<CloudNativeAnchor>();

        // Set the color
        //newGameObject.GetComponent<MeshRenderer>().material.color = GetStepColor();

        // Return created object
        return newGameObject;
    }

    /// <summary>
    /// Spawns a new object.
    /// </summary>
    /// <param name="worldPos">The world position.</param>
    /// <param name="worldRot">The world rotation.</param>
    /// <param name="cloudSpatialAnchor">The cloud spatial anchor.</param>
    /// <returns><see cref="GameObject"/>.</returns>
    protected virtual GameObject SpawnNewAnchoredObject(Vector3 worldPos, Quaternion worldRot, CloudSpatialAnchor cloudSpatialAnchor)
    {
        // Create the object like usual
        GameObject newGameObject = SpawnNewAnchoredObject(worldPos, worldRot);

        // If a cloud anchor is passed, apply it to the native anchor
        if (cloudSpatialAnchor != null)
        {
            CloudNativeAnchor cloudNativeAnchor = newGameObject.GetComponent<CloudNativeAnchor>();
            cloudNativeAnchor.CloudToNative(cloudSpatialAnchor);

            //Set the Identifier as Name of the object
            newGameObject.name = currentCloudAnchor.Identifier;
            positionedProducts.Add(currentCloudAnchor.Identifier, newGameObject);
        }

        // Set color
        //newGameObject.GetComponent<MeshRenderer>().material.color = GetStepColor();

        // Return newly created object
        return newGameObject;
    }

    /// <summary>
    /// Spawns a new anchored object and makes it the current object or moves the
    /// current anchored object if one exists.
    /// </summary>
    /// <param name="worldPos">The world position.</param>
    /// <param name="worldRot">The world rotation.</param>
    protected virtual void SpawnOrMoveCurrentAnchoredObject(Vector3 worldPos, Quaternion worldRot)
    {
        // Create the object if we need to, and attach the platform appropriate
        // Anchor behavior to the spawned object
        if (spawnedObject == null)
        {
            // Use factory method to create
            spawnedObject = SpawnNewAnchoredObject(worldPos, worldRot, currentCloudAnchor);

            // Update color
            spawnedObjectMat = spawnedObject.GetComponent<MeshRenderer>().material;
        }
        else
        {
            // Use factory method to move
            MoveAnchoredObject(spawnedObject, worldPos, worldRot, currentCloudAnchor);
        }
    }

    private void CloudManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        Debug.LogFormat("Anchor recognized as a possible anchor {0} {1}", args.Identifier, args.Status);
        if (args.Status == LocateAnchorStatus.Located)
        {
            OnCloudAnchorLocated(args);
        }
    }

    private void CloudManager_LocateAnchorsCompleted(object sender, LocateAnchorsCompletedEventArgs args)
    {
        OnCloudLocateAnchorsCompleted(args);
    }

    private void CloudManager_SessionUpdated(object sender, SessionUpdatedEventArgs args)
    {
        OnCloudSessionUpdated();
    }

    private void CloudManager_Error(object sender, SessionErrorEventArgs args)
    {
        isErrorActive = true;
        Debug.Log(args.ErrorMessage);

        UnityDispatcher.InvokeOnAppThread(() => this.feedbackBox.text = string.Format("Error: {0}", args.ErrorMessage));
    }

    private void CloudManager_LogDebug(object sender, OnLogDebugEventArgs args)
    {
        Debug.Log(args.Message);
    }


    #region Public Properties
    /// <summary>
    /// Gets the prefab used to represent an anchored object.
    /// </summary>
    public GameObject AnchoredObjectPrefab { get { return anchoredObjectPrefab; } }

    /// <summary>
    /// Gets the <see cref="SpatialAnchorManager"/> instance used by this demo.
    /// </summary>
    public SpatialAnchorManager CloudManager { get { return cloudManager; } }

    /// <summary>
    /// Gets and set the new Anchor Locate Criteria
    /// </summary>
    public AnchorLocateCriteria AnchorLocateCriteria { get { return this.anchorLocateCriteria; } set { this.anchorLocateCriteria = value; } }

    /// <summary>
    /// Get the Dictionary of all the Anchored Positioned Products
    /// </summary>
    public IDictionary<string, GameObject> AnchoredPositionedProducts { get { return positionedProducts; } }
    #endregion // Public Properties
}
