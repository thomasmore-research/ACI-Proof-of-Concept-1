using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine;

public class SceneController : MonoBehaviour
{
    public Image fader;
    public Image spinner;
    private static SceneController instance;
   
    void Awake()
    {
        if(instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); //persistent within the scenes

            fader.rectTransform.sizeDelta = new Vector2(Screen.width + 20 , Screen.height + 20);
            fader.gameObject.SetActive(false);
            spinner.gameObject.SetActive(false);

            PlayerPrefs.DeleteAll();

            // Load filter scene
            LoadScene(1);            
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void LoadScene(int index, float duration = 1, float waitTime = 0)
    {
        instance.StartCoroutine(instance.FadeScene(index, duration, waitTime));
    }

    private IEnumerator FadeScene(int index, float duration, float waitTime)
    {
        fader.gameObject.SetActive(true);
        spinner.gameObject.SetActive(true);

        RectTransform rectComponent = spinner.GetComponent<RectTransform>();

        for(float t = 0; t < 1; t += Time.deltaTime / duration)
        {
            fader.color = new Color(0, 0, 0, Mathf.Lerp(0, 1, t));
            rectComponent.Rotate(0f, 0f, (5 * t));

            yield return null;
        }
        
        //Load scene async and wait 
        AsyncOperation ao = SceneManager.LoadSceneAsync(index);

        while(!ao.isDone)
            yield return null;

        yield return new WaitForSeconds(waitTime); //works only with courutine

        for (float t = 0; t < 1; t += Time.deltaTime / duration)
        {
            fader.color = new Color(0, 0, 0, Mathf.Lerp(1, 0, t));
            rectComponent.Rotate(0f, 0f, (5 * t));

            yield return null;
        }

        fader.gameObject.SetActive(false);
        spinner.gameObject.SetActive(false);
    }
}
