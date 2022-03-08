using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;


public class MeshDetector : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public float delay = 0.1f;

    #region Private Members

    private string fullText;
    private string currentText = "";
    private TaskCoroutine taskDisplayText;

    private LookAtCamera atCamera;
    private Animator haloAnimator;
    private bool allowDragandDrop = false;

    // Time management
    private float downClickTime;
    private float singleClickDeltaTime = 0.2F;

    private Transform parentTransform;
    private Color greenColor;
    private Color whiteColor;

    [SerializeField]
    private SpriteRenderer pointerCircle;
    
    [SerializeField]
    private SpriteShapeRenderer descriptionSprite;

    [SerializeField]
    private TextMeshPro descriptionText;

    [SerializeField]
    private GameObject container;

    #endregion

    #region Unity Lifecycle
    void Start()
    {
        addPhysicsRaycaster();
        addPhysics2DRaycaster();

        fullText = descriptionText.text;

        greenColor = ConvertColor(104, 180, 45);
        whiteColor = Color.white;
        container.gameObject.SetActive(false);
        pointerCircle.color = whiteColor;//ConvertColor(224, 176, 0);//E0B000 //Goldenrod //Color.red;
        parentTransform = transform.parent;

        atCamera = parentTransform.GetComponent<LookAtCamera>();
        haloAnimator = gameObject.GetComponentInChildren<Animator>();
    }
    void OnEnable()
    {
        //subscribe to event
        ModelTargetsManager.OnAdminModeEvent += TriggerAdminMode;
    }

    void OnDisable()
    {
        //Un-subscribe to event
        ModelTargetsManager.OnAdminModeEvent -= TriggerAdminMode;
    }

    #endregion

    #region Private Methods
    void TriggerAdminMode(bool value)
    {
        allowDragandDrop = value;
    }

    void addPhysics2DRaycaster()
    {
        Physics2DRaycaster physicsRaycaster = GameObject.FindObjectOfType<Physics2DRaycaster>();
        if (physicsRaycaster == null)
        {
            Camera.main.gameObject.AddComponent<Physics2DRaycaster>();
        }
    }

    void addPhysicsRaycaster()
    {
        PhysicsRaycaster physicsRaycaster = GameObject.FindObjectOfType<PhysicsRaycaster>();
        if (physicsRaycaster == null)
        {
            Camera.main.gameObject.AddComponent<PhysicsRaycaster>();
        }
    }

    void HasTextFinished(bool value)
    {
        taskDisplayText = null;
        Destroy(GameObject.FindObjectOfType<TaskManagerCoroutine>());
    }

    IEnumerator ShowText()
    {
        for (int i = 0; i < fullText.Length; i++)
        {
            currentText = fullText.Substring(0, i);
            descriptionText.text = currentText;
            yield return new WaitForSeconds(delay);
        }
    }

    Color ConvertColor(int r, int g, int b) {

        return new Color(r/255.0f, g/255.0f, b/255.0f);
    }

    #endregion

    #region Input Touches
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("Clicked: " + eventData.pointerCurrentRaycast.gameObject.name);
        downClickTime = Time.time;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (Time.time - downClickTime <= singleClickDeltaTime)
        {
            if (!container.gameObject.activeSelf)
            {
                container.gameObject.SetActive(true);

                if (taskDisplayText == null)
                {
                    currentText = "";
                    taskDisplayText = new TaskCoroutine(ShowText());
                    taskDisplayText.Finished += HasTextFinished;
                }
                else
                    taskDisplayText.Unpause();

                Color greyWeb = ConvertColor(124, 132, 131); //7C8483 //Grey Web
                descriptionSprite.materials[0].SetColor("_Color", greyWeb);//Color.grey);
                descriptionSprite.materials[1].SetColor("_Color", greyWeb);//Color.grey);

                pointerCircle.color = greenColor;//ConvertColor(20, 83, 123); //USAFA Blue //Color.blue;

                //haloAnimator.enabled = false;
                haloAnimator.gameObject.SetActive(false);
            }
            else
            {
                descriptionText.text = "";
                container.gameObject.SetActive(false);
                pointerCircle.color = whiteColor;//ConvertColor(224, 176, 0);//E0B000 //Goldenrod //Color.red;

                taskDisplayText.Pause();

                //haloAnimator.enabled = true;
                haloAnimator.gameObject.SetActive(true);
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Stop thez look at function of the camera;
        atCamera.triggerLookAt = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (allowDragandDrop)
        {
            // Get the ray from mouse position
            Ray R = Camera.main.ScreenPointToRay(eventData.position);
            // Take current position of this draggable object as Plane's Origin
            Vector3 PO = parentTransform.position;
            // Take current negative camera's forward as Plane's Normal
            Vector3 PN = -Camera.main.transform.forward;
            // plane vs. line intersection in algebric form. It find t as distance from the camera of the new point in the ray's direction.
            float t = Vector3.Dot(PO - R.origin, PN) / Vector3.Dot(R.direction, PN);
            // Find the new point.
            Vector3 P = R.origin + R.direction * t;

            parentTransform.position = P;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        //Invoke Event 
        if (OnClickedEvent != null)
        {
            OnClickedEvent(parentTransform.gameObject);
        }

        atCamera.triggerLookAt = true;
    }

    #endregion

    #region Public Events
    public delegate void SyncWithDatabaseEvent(GameObject target);
    public static event SyncWithDatabaseEvent OnClickedEvent;
    #endregion
}