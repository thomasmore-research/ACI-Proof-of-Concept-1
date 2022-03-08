using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;


public class AnchorDetector : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Material material;
    private Color whiteColor;

    void Start()
    {
        addPhysicsRaycaster();
        addPhysics2DRaycaster();

        material = gameObject.GetComponent<Renderer>().material;
        material.SetColor("_Color", Color.white);
        whiteColor = material.color;
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

    #region Public Methods
    public void OnPointerDown(PointerEventData eventData)
    {
        if (OnClickedEvent != null)
        {
            OnClickedEvent(this.gameObject);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Color greenColor = ConvertColor(104, 180, 45);
        Color newColor = (material.color == greenColor) ?  whiteColor : greenColor;
        material.SetColor("_Color", newColor);
    }

    Color ConvertColor(int r, int g, int b)
    {

        return new Color(r / 255.0f, g / 255.0f, b / 255.0f);
    }
    #endregion

    #region Public Events
    public delegate void ClickedActionEvent(GameObject target);
    public static event ClickedActionEvent OnClickedEvent;
    #endregion

}