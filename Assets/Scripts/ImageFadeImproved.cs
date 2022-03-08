using UnityEngine;
using UnityEngine.UI;
using System.Collections;


public enum FadeAction
{
    FadeIn,
    FadeOut,
    FadeInAndOut,
    FadeOutAndIn
}


public class ImageFadeImproved : MonoBehaviour
{
    [Tooltip("the image you want to fade, assign in inspector")]
    [SerializeField] private Image img;

    [Tooltip("The Text you want to fade, assign in inspector")]
    [SerializeField] public Text text;

    Color m_AlphaWhite = new Color(1, 1, 1, 0);
    Color m_White = new Color(1, 1, 1, 1);

    private FadeAction lastFadeAction;

    public FadeAction LastFadeAction { get {return lastFadeAction; } } 

    public void Start()
    {
        img.color = m_AlphaWhite;
        text.color = m_AlphaWhite;
    }

    public void UserFeedMessage(string message)
    {
        text.text = message;
    }

    public void StartAnimation(FadeAction fadeType, float seconds = 1.0f)
    {
        if (fadeType == FadeAction.FadeIn)
        {

            StartCoroutine(FadeIn(seconds));

        }

        else if (fadeType == FadeAction.FadeOut)
        {

            StartCoroutine(FadeOut(seconds));

        }

        else if (fadeType == FadeAction.FadeInAndOut)
        {

            StartCoroutine(FadeInAndOut(seconds));

        }

        else if (fadeType == FadeAction.FadeOutAndIn)
        {

            StartCoroutine(FadeOutAndIn(seconds));

        }

        lastFadeAction = fadeType;
    }

    // fade from transparent to opaque
    IEnumerator FadeIn(float seconds)
    {

        // loop over 1 second
        for (float i = 0; i <= seconds; i += Time.deltaTime)
        {
            // set color with i as alpha
            img.color = new Color(1, 1, 1, i);
            text.color = new Color(1, 1, 1, i);
            yield return null;
        }

    }

    // fade from opaque to transparent
    IEnumerator FadeOut(float seconds)
    {
        // loop over 1 second backwards
        for (float i = seconds; i >= 0; i -= Time.deltaTime)
        {
            // set color with i as alpha
            img.color = new Color(1, 1, 1, i);
            text.color = new Color(1, 1, 1, i);
            yield return null;
        }
    }

    IEnumerator FadeInAndOut(float seconds)
    {
        // loop over 1 second
        for (float i = 0; i <= seconds; i += Time.deltaTime)
        {
            // set color with i as alpha
            img.color = new Color(1, 1, 1, i);
            text.color = new Color(1, 1, 1, i);
            yield return null;
        }

        //Temp to Fade Out
        yield return new WaitForSeconds(1);

        // loop over 1 second backwards
        for (float i = seconds; i >= 0; i -= Time.deltaTime)
        {
            // set color with i as alpha
            img.color = new Color(1, 1, 1, i);
            text.color = new Color(1, 1, 1, i);
            yield return null;
        }
    }

    IEnumerator FadeOutAndIn(float seconds)
    {
        // loop over 1 second backwards
        for (float i = seconds; i >= 0; i -= Time.deltaTime)
        {
            // set color with i as alpha
            img.color = new Color(1, 1, 1, i);
            text.color = new Color(1, 1, 1, i);
            yield return null;
        }

        //Temp to Fade In
        yield return new WaitForSeconds(1);

        // loop over 1 second
        for (float i = 0; i <= seconds; i += Time.deltaTime)
        {
            // set color with i as alpha
            img.color = new Color(1, 1, 1, i);
            text.color = new Color(1, 1, 1, i);
            yield return null;
        }
    }

}