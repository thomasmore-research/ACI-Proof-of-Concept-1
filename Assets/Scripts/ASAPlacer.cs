using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;

public class ASAPlacer : ASABase
{

    #region Private Members
    private FirebaseFirestore db;
    private static IDictionary<string, Product> products = new Dictionary<string, Product>();
    private int currentIndex = 0;
    private string currentProductKey;
    private string currentAnchorId = "";
    #endregion

    #region Public Members
    public Text log;
    public string[] productIds;
    public Text ASAStatus;
    public Text previewTitle;

    public Button prevBtn;
    public Button nextBtn;
    public Button filterBtn;
    public Button placeBtn;
    #endregion

    #region Unity Lifecycle
    public override void Start()
    {
        base.Start();

        db = FirebaseFirestore.DefaultInstance;
        feedbackBox = log;

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
    public override void OnDestroy()
    {
        base.OnDestroy();
    }

    private void Update()
    {
        CheckNavigation();
    }

    void OnEnable()
    {
        //subscribe to event
        ARSession.stateChanged += ARSession_stateChanged;
    }

    void OnDisable()
    {
        //Un-subscribe to event
        ARSession.stateChanged -= ARSession_stateChanged;
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
            currentAnchorId = "";
            currentCloudAnchor = null;
        }
        catch (System.Exception e)
        {
            Log(e.Message);
            throw;
        }

    }

    protected virtual async Task PlaceNewAnchor()
    {
        try
        {
            Quaternion rotation = Quaternion.AngleAxis(0, Vector3.up);
            Vector3 newPosition = Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
            SpawnOrMoveCurrentAnchoredObject(newPosition, rotation);
        }
        catch (System.Exception e)
        {
            Log(e.Message);
            throw;
        }
    }

    protected override void OnSaveCloudAnchorFailed(Exception exception)
    {
        base.OnSaveCloudAnchorFailed(exception);
        Log("Anchor fail");
        currentAnchorId = string.Empty;
    }

    protected override async Task OnSaveCloudAnchorSuccessfulAsync()
    {
        await base.OnSaveCloudAnchorSuccessfulAsync();

        currentAnchorId = currentCloudAnchor.Identifier;
        feedbackBox.text = "Created: " + currentAnchorId;
        // Sanity check that the object is still where we expect
        Pose anchorPose = currentCloudAnchor.GetPose();
        SpawnOrMoveCurrentAnchoredObject(anchorPose.position, anchorPose.rotation);

        try
        {
            Log("Saving to db");
            userFeed.UserFeedMessage("Saving Anchor to Database");
            userFeed.StartAnimation(FadeAction.FadeInAndOut);

            if (currentProductKey != null)
            {
                DocumentReference docRef = db.Collection("products").Document(currentProductKey);
                Product product = products[currentProductKey];
                product.AnchorID = currentCloudAnchor.Identifier;
                await docRef.SetAsync(product);
            }
            else
            {
                Product product = new Product();
                product.AnchorID = currentAnchorId;
                await db.Collection("products").AddAsync(product);
            }
            spawnedObject = null;
            currentAnchorId = "";
            currentCloudAnchor = null;
        }
        catch (System.Exception e)
        {
            Log(e.Message);
            throw;
        }
    }

    private async Task SyncWithDatabase()
    {
        try
        {
            Log("Sync with database"); 
            userFeed.UserFeedMessage("Synching with Database");
            userFeed.StartAnimation(FadeAction.FadeInAndOut);

            products.Clear();
            String idString = PlayerPrefs.GetString("filterOnProductIds");
            if (productIds.Length < 1) productIds = idString.Split(',');
            CollectionReference productsRef = db.Collection("products");
            Query query = productsRef;//.WhereIn("id", productIds);
            QuerySnapshot querySnapshot = await query.GetSnapshotAsync();
            foreach (DocumentSnapshot documentSnapshot in querySnapshot.Documents)
            {
                Log(documentSnapshot.Id);
                Product product = documentSnapshot.ConvertTo<Product>();
                products.Add(documentSnapshot.Id, product);
            }
            return;
        }
        catch (System.Exception e)
        {
            Log(e.Message);
            throw;
        }

    }

    private void ChangeCurrentProduct(int i)
    {
        try
        {
            currentIndex = currentIndex + i;
            if (currentIndex > products.Count - 1) currentIndex = 0;
            if (currentIndex < 0) currentIndex = products.Count - 1;
            if (products.Count > 0)
            {
                currentProductKey = products.ElementAt(currentIndex).Key;
                Product product = products[currentProductKey];
                //previewTitle.text = product.title;
                previewTitle.text = product.title + "\n" + (currentIndex + 1) + "/" + products.Count;
            }
        }
        catch (Exception e)
        {
            Log(e.Message);
        }
    }
    private void CheckNavigation()
    {
        bool showNavigation = products.Count > 1;
        nextBtn.interactable = showNavigation;
        prevBtn.interactable = showNavigation;
        if (currentProductKey == null) ChangeCurrentProduct(0);
        //infoBtn.interactable = distance < 1;
    }
    #endregion

    #region Public Methods
    public async void PlaceAnchorAsync()
    {
        await PlaceNewAnchor();
        await SaveCurrentObjectAnchorToCloudAsync();
    }

    public void ShowFilters()
    {
        SceneController.LoadScene(1);
    }

    public void OnPlace()
    {
        PlaceAnchorAsync();
    }

    public void OnNext()
    {
        ChangeCurrentProduct(+1);
    }

    public void OnPrevious()
    {
        ChangeCurrentProduct(-1);
    }


    public void Log(string message)
    {
        string currentText = log.text;
        log.text = currentText += "\n" + message;
        Debug.Log(message);
    }
    #endregion

    #region Event Handlers
    private void ARSession_stateChanged(ARSessionStateChangedEventArgs args)
    {
        if (args.state == ARSessionState.SessionTracking)
        {
            InitializeAnchors();
        }
    }
    #endregion
}
