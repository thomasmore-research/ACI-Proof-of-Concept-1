using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class AnchorPrefab : MonoBehaviour
{
    public TextMeshPro title;
    public GameObject pointer;

    void OnEnable()
    {
        //subscribe to event
        ASAManager.OnProductTitleEvent += UpdateAnchorTitle;
    }

    void OnDisable()
    {
        //Un-subscribe to event
        ASAManager.OnProductTitleEvent -= UpdateAnchorTitle;
    }

    void UpdateAnchorTitle(Product p)
    {
        if (p != null && (p.AnchorID == name))
        {
            string titleFormat = p.title;
            if (title.text != titleFormat)
                title.text = titleFormat;
        }
        
    }
}
