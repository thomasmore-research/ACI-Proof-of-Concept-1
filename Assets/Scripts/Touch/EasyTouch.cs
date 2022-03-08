using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class EasyTouch : MonoBehaviour
{
    private Touch oldTouch1; //Last touched point 1 (finger 1)  
    private Touch oldTouch2; //Last touched point 2 (finger 2)  
                             //This is the text in the scene, I used it for logging, you can delete it if you don’t need it
    public Text _text;//****************************
    private string text;//****************************

    void Update()
    {

        //No touch  
        if (Input.touchCount <= 0)
        {
            //Debug.Log("Input.touchCount = 0");

            return;
        }

        ////Single touch, move up and down horizontally
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved)
        {
            Debug.Log("Single touch, move up and down horizontally");
            text = "Single touch, move up and down horizontally";//****************************
            _text.text = text;//****************************
            var deltaposition = Input.GetTouch(0).deltaPosition;
            transform.Translate(new Vector3(deltaposition.x * 0.01f, deltaposition.y * 0.01f, 0f), Space.World);
        }
        //Single touch, rotate up and down horizontally  
        if (2 == Input.touchCount)
        {
            Debug.Log("Single-touch, rotate horizontally up and down");
            text = "Single touch, rotate horizontally up and down";//****************************
            _text.text = text;//****************************
            Touch touch = Input.GetTouch(0);
            Vector2 deltaPos = touch.deltaPosition;
            transform.Rotate(Vector3.down * deltaPos.x, Space.World);
            transform.Rotate(Vector3.right * deltaPos.y, Space.World);
        }

        //Multi-touch, zoom in and zoom out  
        Touch newTouch1 = Input.GetTouch(0);
        Touch newTouch2 = Input.GetTouch(1);

        //The second point is just beginning to touch the screen, only record, no processing  
        if (newTouch2.phase == TouchPhase.Began)
        {
            oldTouch2 = newTouch2;
            oldTouch1 = newTouch1;
            return;
        }

        //Calculate the distance between the old two points and the new two points, enlarge the model when larger, and zoom the model when smaller  
        float oldDistance = Vector2.Distance(oldTouch1.position, oldTouch2.position);
        float newDistance = Vector2.Distance(newTouch1.position, newTouch2.position);

        //The difference between the two distances, positive means zoom in gesture, negative means zoom out gesture  
        float offset = newDistance - oldDistance;

        //Magnification factor, one pixel is calculated as 0.01 times (100 adjustable)  
        float scaleFactor = offset / 100f;
        Vector3 localScale = transform.localScale;
        Vector3 scale = new Vector3(localScale.x + scaleFactor,
                                    localScale.y + scaleFactor,
                                    localScale.z + scaleFactor);

        //Minimum zoom to 0.3 times  
        if (scale.x > 0.3f && scale.y > 0.3f && scale.z > 0.3f)
        {
            transform.localScale = scale;
        }

        //Remember the latest touch point and use it next time  
        oldTouch1 = newTouch1;
        oldTouch2 = newTouch2;

    }
}
