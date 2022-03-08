using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointerScript : MonoBehaviour
{
    public GameObject target;
    void Update()
    {
        if (target != null)
        {
            //gameObject.active = true;
            transform.LookAt(target.transform);
        }
        else
        {
            //gameObject.active = false;
        }
    }
}
