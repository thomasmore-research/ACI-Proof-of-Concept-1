using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using TMPro;

public class ASAManager : ASABase
{
    #region Private Members
    private List<string> anchorIds = new List<string>();
    static IDictionary<string, Product> products = new Dictionary<string, Product>();
    private IDictionary<string, Product> anchorProduct = new Dictionary<string, Product>();

    private FirebaseFirestore db;
    private string currentAnchorIDSelected = string.Empty;
    private string currentObjectReference = string.Empty;
    #endregion

    #region Public Members
    public Text log;
    public Text ASAStatus;
    public Text previewTitle;

    public string[] productIds;
    public Button prevBtn;
    public Button nextBtn;
    public Button filterBtn;
    public Button placeBtn;
    public Button infoBtn;

    public Button watchButton;
    public Button clearButton;
    #endregion

    #region Unity Lifecycle
    public override void Start()
    {
        base.Start();

        db = FirebaseFirestore.DefaultInstance;
        feedbackBox = log;

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    void OnEnable()
    {
        //subscribe to event
        AnchorDetector.OnClickedEvent += AnchorIsSelected;
        ARSession.stateChanged += ARSession_stateChanged;
    }

    void OnDisable()
    {
        //Un-subscribe to event
        AnchorDetector.OnClickedEvent -= AnchorIsSelected;
        ARSession.stateChanged -= ARSession_stateChanged;
    }
    #endregion

    #region Public Methods
    public override void OnDestroy()
    {
        base.OnDestroy();

        if (CloudManager != null)
        {
            CloudManager.DestroySession();
        }

        CleanupSpawnedObjects();
    }

    public void RunWatcher()
    {
        StopWatcher();
        Log("Number of Anchors: " + anchorIds.Count);

        currentWatcher = CreateWatcher();
        if (currentWatcher == null)
        {
            Log("Either cloudmanager or session is null, should not be here!");
            feedbackBox.text = "YIKES - couldn't create watcher!";
        }
    }

    public void StopWatcher()
    {
        if (currentWatcher != null)
        {
            currentWatcher.Stop();
            currentWatcher = null;
        }
    }

    public void ShowFilters()
    {
        SceneController.LoadScene(1);
    }

    public void GoToPlace()
    {
        SceneController.LoadScene(4);
    }

    public void OnInformation()
    {
        if (currentObjectReference != string.Empty)
        {
            PlayerPrefs.SetString("DataSet", currentObjectReference);
            Debug.LogFormat("<color=green> OnInformation - Object Reference: {0} </color>", currentObjectReference);

            SceneController.LoadScene(3);
        }
        else
        {
            userFeed.UserFeedMessage("Please select a bike!");
            userFeed.StartAnimation(FadeAction.FadeInAndOut);
            Log("Please select a bike");
        }
    }

    public void Log(string message)
    {
        if (log != null)
        {
            string currentText = log.text;
            log.text = currentText += "\n" + message;
            Debug.Log(message);
        }
    }

    #endregion

    #region Event Handlers
    private void ARSession_stateChanged(ARSessionStateChangedEventArgs args)
    {
        if (args.state == ARSessionState.SessionTracking)
        {
            if (base.AnchoredPositionedProducts.Count == 0)
                InitializeAnchors();
        }
    }


    protected override void OnCloudAnchorLocated(AnchorLocatedEventArgs args)
    {
        base.OnCloudAnchorLocated(args);
        Log("--- Anchor located");
        switch (args.Status)
        {
            case LocateAnchorStatus.Located:
                UnityDispatcher.InvokeOnAppThread(() => { PlaceExistingAnchor(args.Anchor); });
                break;
            case LocateAnchorStatus.AlreadyTracked:
                Log("Already tracked: " + args.Anchor.Identifier);
                // This anchor has already been reported and is being tracked
                break;
            case LocateAnchorStatus.NotLocatedAnchorDoesNotExist:
                Log("Anchor NotLocatedAnchorDoesNotExist ");
                // The anchor was deleted or never existed in the first place
                // Drop it, or show UI to ask user to anchor the content anew
                break;
            case LocateAnchorStatus.NotLocated:
                Log("Anchor NotLocated");
                // The anchor hasn't been found given the location data
                // The user might in the wrong location, or maybe more data will help
                // Show UI to tell user to keep looking around
                break;
        }
    }
    #endregion

    #region Internal Methods
    private async Task InitializeAnchors()
    {
        try
        {
            await SyncWithDatabase();

            if (!CloudManager.IsSessionStarted)
            {
                await CloudManager.StartSessionAsync();
            }

            currentCloudAnchor = null;

            ConfigureSession();
            RunWatcher();
        }
        catch (System.Exception e)
        {
            Log(e.Message);
            throw;
        }

    }

    protected override void CleanupSpawnedObjects()
    {
        products.Clear();
        anchorProduct.Clear();

        Debug.Log("<color=green> CLean Up ASAManager Scene </color>");
    }

    //This will be called when invoked
    private void AnchorIsSelected(GameObject target)
    {
        GameObject parent = target.transform.parent.gameObject;
        string parentName = target.transform.parent.name;
        string title = parent.GetComponent<AnchorPrefab>().title.text;

        if (currentAnchorIDSelected == string.Empty)
        {
            currentAnchorIDSelected = parentName;
            Log(title + " Selected Anchor: " + currentAnchorIDSelected);
            userFeed.UserFeedMessage("Selected: " + title);
            userFeed.StartAnimation(FadeAction.FadeInAndOut);
        }
        else
        {
            if (currentAnchorIDSelected != parentName)
            {
                GameObject oldTarget;// = GameObject.Find(currentAnchorID);
                AnchoredPositionedProducts.TryGetValue(currentAnchorIDSelected, out oldTarget);
                Renderer rD = oldTarget.GetComponent<AnchorPrefab>().pointer.GetComponent<Renderer>();
                rD.material.SetColor("_Color", Color.white);

                currentAnchorIDSelected = parentName;

                Log(title + " Selected Anchor: " + currentAnchorIDSelected);
                userFeed.UserFeedMessage("Selected: " + title);
                userFeed.StartAnimation(FadeAction.FadeInAndOut);
            }
            else
            {
                Log(title + " Deselected Anchor: " + currentAnchorIDSelected);
                userFeed.UserFeedMessage("Deselected: " + title);
                userFeed.StartAnimation(FadeAction.FadeInAndOut);
                currentAnchorIDSelected = string.Empty;
            }
        }

        Debug.LogFormat("<color=green> OnInformation - Object Reference: {0} </color>", currentAnchorIDSelected);

        GetObjectReferenceFromProduct();
    }

    private void GetObjectReferenceFromProduct()
    {
        if (currentAnchorIDSelected != string.Empty)
        {
            Product p;
            anchorProduct.TryGetValue(currentAnchorIDSelected, out p);
            currentObjectReference = p.objectReference;

            Log("Got Object Reference");

            OnInformation();
        }
        else
            currentObjectReference = string.Empty;
    }

    private void ConfigureSession()
    {
        IEnumerable<string> anchorsToFind = products.Where(p => p.Value.AnchorID != null).Select(p => p.Value.AnchorID).ToArray();

        SetAnchorIdsToLocate(anchorsToFind);
    }

    private void PlaceExistingAnchor(CloudSpatialAnchor anchor)
    {
        try
        {
            currentCloudAnchor = anchor;
            Pose anchorPose = Pose.identity;
            anchorPose = anchor.GetPose();

            try
            {
                SpawnNewAnchoredObject(anchorPose.position, anchorPose.rotation, anchor);

                //Invoke Event 
                if (OnProductTitleEvent != null)
                {
                    Product p;
                    anchorProduct.TryGetValue(anchor.Identifier, out p);
                    OnProductTitleEvent(p);
                }
            }
            catch (System.Exception e)
            {
                Debug.Log("<color=yellow> SpawnOrMoveCurrentAnchoredObject: " + e.Message + "</color>");
                throw;
            }
        }
        catch (System.Exception e)
        {
            Debug.Log("<color=yellow> PlaceExistingAnchor: " + e.Message + "</color>");
            throw;
        }
    }


    private async Task SyncWithDatabase()
    {
        try
        {
            userFeed.UserFeedMessage("Synching with Database");
            userFeed.StartAnimation(FadeAction.FadeInAndOut);

            Log("Sync with database");
            products.Clear();
            anchorProduct.Clear();
            anchorIds.Clear();
            String idString = PlayerPrefs.GetString("filterOnProductIds");
            Log("Filter: " + idString);
            if (productIds.Length < 1) productIds = idString.Split(',');
            CollectionReference productsRef = db.Collection("products");
            Query query = productsRef.WhereIn("id", productIds);
            QuerySnapshot querySnapshot = await query.GetSnapshotAsync();
            foreach (DocumentSnapshot documentSnapshot in querySnapshot.Documents)
            {
                Log(documentSnapshot.Id);
                Product product = documentSnapshot.ConvertTo<Product>();
                products.Add(documentSnapshot.Id, product);
                anchorProduct.Add(product.AnchorID, product);
                anchorIds.Add(product.AnchorID);
            }
            return;
        }
        catch (System.Exception e)
        {
            Log(e.Message);
            throw;
        }
    }
    #endregion

    #region Public Events
    public delegate void ProductTitleEvent(Product product);
    public static event ProductTitleEvent OnProductTitleEvent;
    #endregion
}
