using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;

using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;


public class AnnotationManager : MonoBehaviour
{
    #region Public Members
    public GameObject annotation;
    public GameObject annotationPrefab;
    public List<GameObject> annotationGOList;
    public Text LogField;
    #endregion

    #region Private Members
    [SerializeField]
    private ModelTargetsManager modelTargetsManager;

    [SerializeField] private float radius;

    private int snapshotCount;
    private IDictionary<string, Annotation> annotationsList = new Dictionary<string, Annotation>();
    #endregion

    #region Unity Lifecycle
    void OnEnable()
    {
        //subscribe to event
        MeshDetector.OnClickedEvent += SaveAnnotationPosition;
    }

    void OnDisable()
    {
        //Un-subscribe to event
        MeshDetector.OnClickedEvent -= SaveAnnotationPosition;
    }
    #endregion

    #region Private Methods
    async private void SaveAnnotationPosition(GameObject annotationGameObject)
    {
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        string currentAnnotationKey = annotationGameObject.name;

        try
        {
            Log("Saving to db");
            if (currentAnnotationKey != null)
            {
                DocumentReference docRef = db.Collection("annotations").Document(currentAnnotationKey);
                Annotation annotation = annotationsList[currentAnnotationKey];
                annotation.x = annotationGameObject.transform.position.x;
                annotation.y = annotationGameObject.transform.position.y;
                annotation.z = annotationGameObject.transform.position.z;
                await docRef.SetAsync(annotation);
                Log("Annotation Saved " + annotation.title);
            }
        }
        catch (System.Exception e)
        {
            Log(e.Message);
            throw;
        }
    }
    private Vector3 SpawnAroundPoint(int i)
    {
        try
        {
            if (modelTargetsManager.TrackableBox == null)
            {
                Debug.Log("TrackableBox not Detected");
                Log("TrackableBox not Detected");
            }    
            Vector3 point = modelTargetsManager.TrackableBox.center;

            /* Distance around the HALF circle */
            float radians = 2 * Mathf.PI / snapshotCount * i; //2 * for FULL circle

            /* Get the vector direction */
            float vertical = Mathf.Sin(radians);
            float horizontal = Mathf.Cos(radians);

            Vector3 h = new Vector3(0, modelTargetsManager.TrackableBox.extents.y, 0);
            float height = h.magnitude;

            Vector3 spawnDir = new Vector3(horizontal, height, vertical);

            Vector3 w = new Vector3(modelTargetsManager.TrackableBox.extents.x, 0, 0);
            radius = w.magnitude;

            /* Get the spawn position */
            Vector3 spawnPos = point + spawnDir * radius; // Radius is just the distance away from the point

            return spawnPos;
        }
        catch(Exception e)
        {
            Log("SpawnAroundPoint:" + e.Message);
            return Vector3.zero;
        }
    }
    #endregion

    #region Public Methods
    public void AddAnnotation(Annotation annotation, string key, int index)
    {
        try
        {
            if (annotation == null)
                Log("ANNOTATION NULL");
            else
            {
                Vector3 position = new Vector3(annotation.x, annotation.y, annotation.z);
                if (annotation.x == 0.0f && annotation.y == 0.0f && annotation.z == 0.0f)
                    position = SpawnAroundPoint(index);

                Log("Add annotation: " + annotation.ToString() + "Pos: " + position.x + " " + position.y + " " + position.z);

                GameObject annotationGO = Instantiate(annotationPrefab, position, Quaternion.identity, this.annotation.transform);

                AnnotationPrefab apf = annotationGO.GetComponent<AnnotationPrefab>();
                apf.title = annotation.title;
                apf.description = annotation.description;

                annotationGO.name = key;
                annotationGO.SetActive(true);
                annotationGOList.Add(annotationGO);
            }
        }
        catch (Exception e)
        {
            print(e);
            Log("AddAnnotation:" + e.Message);
        }
    }


    async public void GetAnnotations(string productID)
    {
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        Query query = db.Collection("annotations").WhereEqualTo("productId", productID);
        QuerySnapshot snapshot = await query.GetSnapshotAsync();

        int index = 0;

        try
        {
            Log("Sync with database");
            annotationsList.Clear();
            snapshotCount = snapshot.Count;

            //Clear the previous annotations
            foreach ( GameObject obj in annotationGOList)
            {
                if (obj)
                    Destroy(obj);
            }
            annotationGOList.Clear();
                

            foreach (DocumentSnapshot documentSnapshot in snapshot.Documents)
            {
                Log(documentSnapshot.Id);
                Annotation annotation = documentSnapshot.ConvertTo<Annotation>();
                AddAnnotation(annotation, documentSnapshot.Id, index);
                annotationsList.Add(documentSnapshot.Id, annotation);

                index++;
            }
            return;
        }
        catch (System.Exception e)
        {
            Log("GetAnnotations: " + e.Message);
            throw;
        }
    }

    public void Log(string message)
    {
        if (LogField != null)
        {
            string currentText = LogField.text;
            LogField.text = currentText += "\n" + message;
            Debug.Log(message);
        }
    }
    #endregion
}
