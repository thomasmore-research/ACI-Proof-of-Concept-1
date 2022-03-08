using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FilterManager : MonoBehaviour
{
    public UniWebView webView;
    string[] productIds;

    // Start is called before the first frame update
    void Start()
    {
        webView.OnMessageReceived += (view, message) =>
        {
            if (String.Compare(message.Path, "showProducIds") != 0) PlayerPrefs.SetString("filterOnProductIds", message.Args["ids"]);
            Debug.Log(message.Args["ids"]);
            SceneController.LoadScene(2);
        };
    }

    // Update is called once per frame
    void Update()
    {

    }

}
