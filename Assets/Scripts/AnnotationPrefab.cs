using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class AnnotationPrefab : MonoBehaviour
{

    public string title;
    public string description;
    // Start is called before the first frame update
    void Start()
    {
        try
        {
            GameObject tmp = transform.Find("Title").gameObject;
            TextMeshPro textmeshPro = tmp.GetComponent<TextMeshPro>();
            textmeshPro.SetText(title);
        }
        catch(Exception e)
        {
            Debug.Log("<color=yellow>" + e.Message + "</color>");
        }

        try 
        { 
            GameObject dsc = transform.Find("Container").gameObject;
            dsc.transform.GetChild(0).GetComponent<TextMeshPro>().SetText(description);
        }
        catch(Exception e)
        {
            Debug.Log("<color=yellow>" + e.Message + "</color>");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
