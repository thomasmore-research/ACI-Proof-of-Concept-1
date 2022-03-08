
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using Vuforia;

public class ModelTargetsManager : MonoBehaviour
{

    #region Private Members

    // For private serialized fields, we assign references in the Inspector, so disable assignment warnings.
    // Disable: CS0649: Field '' is never assigned to, and will always have its default value false
#pragma warning disable 649
    [Header("Model Target Shared Augmentation")]
    [SerializeField] GameObject annotation;

    [Header("Annotation Manager Object")]
    [SerializeField] AnnotationManager annotationManager;

    [Header("Text Field")]
    [SerializeField] Text LogField;

    [Header("Mobile")]
    [Tooltip("Guide Views Button only for Mobile")]
    [SerializeField] Button guideViewsButton;
    [SerializeField]
    bool triggerShaderAdjustment = false;

    [Header("MTG")]
    [Tooltip("RunTime MTG")]
    [SerializeField] //GameObject ModelTarget;
    private ModelTargetBehaviour modelStandard;

    private string DataSetStandard = "";
    private string DataSetAnnotation = "";
    //private ModelTargetBehaviour modelStandard;
    private Bounds trackableBox;
    private Shader transparentShader;
    [SerializeField]
    private ImageFadeImproved userFeed;
    private bool adminModeTrigger = false;
    private bool onDestroyCall = false;

    #endregion // PRIVATE_MEMBERS

#pragma warning restore 649

    #region Unity Lifecycle

    private void Awake()
    {
        DataSetAnnotation = PlayerPrefs.GetString("DataSet");
        annotationManager.GetAnnotations(DataSetAnnotation);

        if (DataSetAnnotation.Contains(":"))
            DataSetStandard = DataSetAnnotation.Split(':')[0];
        else
            DataSetStandard = DataSetAnnotation;

        transparentShader = Shader.Find("Unlit/Transparent Cutout");

        userFeed.text.fontSize = 15;

        //Portrait//PortraitUpsideDown//LandscapeRight//AutoRotation
        Screen.orientation = ScreenOrientation.AutoRotation;

    }

    private void Start()
    {
        VuforiaApplication.Instance.OnVuforiaStarted += OnVuforiaStarted;
        
        userFeed.UserFeedMessage("Align the Guide View with the Bike");
        userFeed.StartAnimation(FadeAction.FadeInAndOut);
    }
    private void FixedUpdate()
    {
        if (!triggerShaderAdjustment)
        {
            triggerShaderAdjustment = AdjustShader();
        }
    }

    private void OnDestroy()
    {
        VuforiaApplication.Instance.OnVuforiaStarted -= OnVuforiaStarted;

        onDestroyCall = true;

        Debug.Log("<color=green> CLean Up ModelTargetScene </color>");
    }

    #endregion // MONOBEHAVIOUR_METHODS


    #region Vuforia Callbacks

    private void OnVuforiaStarted()
    {
        CreateModelTarget(DataSetStandard);
    }

    private void OnTrackableStateChanged(ObserverBehaviour oBev, TargetStatus status)
    {
        Status sts = status.Status;

        switch (status.Status)
        {
            case Status.TRACKED:
                OnTrackingFound(sts);
                break;
            case Status.EXTENDED_TRACKED:
                OnTrackingFound(sts);
                break;
            default:
                OnTrackingLost(sts);
                break;
        }
    }

    private void OnTrackingLost(Status status)
    {
        if (!onDestroyCall)
        {
            if (Status.NO_POSE == status)
            {
                userFeed.UserFeedMessage("Align the Guide View with the Bike");
                userFeed.StartAnimation(FadeAction.FadeInAndOut);

                //VuforiaBehaviour.Instance.DevicePoseBehaviour.Reset();
                //annotationManager.GetAnnotations(DataSetAnnotation);
                this.annotation.SetActive(false);
            }
            if (Status.LIMITED == status) { }
        }
    }

    private void OnTrackingFound(Status status)
    {
        userFeed.UserFeedMessage("Click an annotation for more details");
        userFeed.StartAnimation(FadeAction.FadeInAndOut);

        this.annotation.SetActive(true);
    }

    #endregion // VUFORIA_CALLBACKS


    #region Public Buttons Methods

    public void LoadAnchorScreen()
    {
        userFeed.text.fontSize = 45;
        SceneController.LoadScene(2);
    }

    public void LoadAdminMode()
    {
        adminModeTrigger = !adminModeTrigger;

        //Invoke Event 
        OnAdminModeEvent?.Invoke(adminModeTrigger);

        if (adminModeTrigger)
        {
            userFeed.UserFeedMessage("Admin mode: ACTIVE");
            userFeed.StartAnimation(FadeAction.FadeIn);
        }
        else
        {
            userFeed.UserFeedMessage("Admin mode: NOT ACTIVE");
            userFeed.StartAnimation(FadeAction.FadeOut);
        }

    }

    public void LoadModelTargetUser()
    {
        SceneController.LoadScene(3);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    /// <summary>
    /// Cycles through guide views for Standard Model Targets with multiple views.
    /// </summary>
    public void CycleGuideView()
    {
        VLog.Log("cyan", "CycleGuideView() called.");
        Log("CycleGuideView() called.");

        if (this.modelStandard != null)
        {
            int activeView = this.modelStandard.GetActiveGuideViewIndex();
            int totalViews = this.modelStandard.GetNumGuideViews();

            if (totalViews > 1 && activeView > -1)
            {
                int guideViewIndexToActivate = (activeView + 1) % totalViews;

                VLog.Log("yellow",
                    this.modelStandard.TargetName + ": Activating Guide View Index " +
                    guideViewIndexToActivate.ToString() + " of " +
                    totalViews.ToString() + " total Guide Views.");

                Log(this.modelStandard.TargetName + ": Activating Guide View Index " +
                    guideViewIndexToActivate.ToString() + " of " +
                    totalViews.ToString() + " total Guide Views.");

                this.modelStandard.SetActiveGuideViewIndex(guideViewIndexToActivate);
            }
            else
            {
                VLog.Log("yellow",
                    "GuideView was not cycled." +
                    "\nActive Guide View Index = " + activeView +
                    "\nNumber of Guide Views = " + totalViews);

                Log("GuideView was not cycled." +
                    "\nActive Guide View Index = " + activeView +
                    "\nNumber of Guide Views = " + totalViews);
            }
        }

        triggerShaderAdjustment = false;
    }

    #endregion // PUBLIC_BUTTON_METHODS


    #region Private Methods
    private  void SetModelTargetGuideView(ModelTargetBehaviour mtBehaviour, int guideViewIndex)
    {
        string targetName = mtBehaviour.TargetName;
        string guideViewName = mtBehaviour.GetGuideView(guideViewIndex).Name;

        Debug.Log("Setting Guide View " + guideViewName + " for " + targetName);

        mtBehaviour.SetActiveGuideViewIndex(guideViewIndex);

    }

    private void CreateModelTarget(string datasetName)
    {
        string dataSetPath = "Vuforia/" + datasetName + ".xml";

        //Create the new model target 
        this.modelStandard = VuforiaBehaviour.Instance.ObserverFactory.CreateModelTarget(
            dataSetPath,
            datasetName,
            ModelTargetBehaviour.ModelTargetTrackingMode.DEFAULT,
            DataSetTrackableBehaviour.TargetMotionHint.STATIC
        );

        this.modelStandard.OnTargetStatusChanged += OnTrackableStateChanged;

        // add the Default Observer Event Handler to the newly created game object
        this.modelStandard.gameObject.AddComponent<DefaultObserverEventHandler>();
        this.modelStandard.enabled = true;
        this.modelStandard.name = "ModelTarget - " + datasetName;
        this.modelStandard.GuideViewMode = ModelTargetBehaviour.GuideViewDisplayMode.GuideView2D;

        Vector3 trackablePotition = this.modelStandard.GetBoundingBox().center;
        trackableBox = this.modelStandard.GetBoundingBox();

        VLog.Log("yellow",
            this.modelStandard.TargetName + ": Active Guide View Index " +
            this.modelStandard.GetActiveGuideViewIndex().ToString() + " of " +
            this.modelStandard.GetNumGuideViews().ToString() + " total Guide Views.");

        Log(this.modelStandard.TargetName + ": Active Guide View Index " +
            this.modelStandard.GetActiveGuideViewIndex().ToString() + " of " +
            this.modelStandard.GetNumGuideViews().ToString() + " total Guide Views.");

        ResetAugmentationTransform(this.modelStandard.transform);
    }

    private bool AdjustShader()
    {
        GuideView2DBehaviour mGuideView2DBehaviour = FindObjectOfType<GuideView2DBehaviour>();

        if (mGuideView2DBehaviour != null)
        {
            //UI/Unlit/Transparent "Unlit/Transparent Cutout" "Unlit/Transparent"

            MeshRenderer meshRenderer = mGuideView2DBehaviour.gameObject.GetComponent<MeshRenderer>();

            meshRenderer.material.shader = transparentShader;

            return true;
        }

        return false;
    }

    private void ResetAugmentationTransform(Transform targetTransform)
    {
        this.annotation.transform.parent = targetTransform;
        this.annotation.transform.localPosition = Vector3.zero;
        this.annotation.transform.localRotation = Quaternion.identity;
        this.annotation.transform.localScale = Vector3.one;
        this.annotation.SetActive(false);

        Log(this.annotation.name + " Parent: " +
            this.annotation.transform.parent.name + " Position: " +
            "X: " + this.annotation.transform.position.x + "Y: " + this.annotation.transform.position.y + "Z: " + this.annotation.transform.position.z);

        VLog.Log("yellow",
            this.annotation.name + " Parent: " +
            this.annotation.transform.parent.name + " Position: " +
            "X: " + this.annotation.transform.position.x + "Y: " + this.annotation.transform.position.y + "Z: " + this.annotation.transform.position.z);
    }

    #endregion // PRIVATE_METHODS


    #region Public Methods

    public void Log(string message)
    {
        string currentText = LogField.text;
        LogField.text = currentText += "\n" + message;
        Debug.Log(message);
    }

    public Bounds TrackableBox { get { return trackableBox; } }
    #endregion // UTILITY_METHODS


    #region Public Events
    public delegate void AdminModeEvent(bool value);
    public static event AdminModeEvent OnAdminModeEvent;
    #endregion
}