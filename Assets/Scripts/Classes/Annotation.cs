using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

using Firebase;
using Firebase.Database;
using Firebase.Firestore;
using Firebase.Extensions;

[FirestoreData]
public class Annotation
{
    [FirestoreProperty]
    public string id { get; set; }
    [FirestoreProperty]
    public string title { get; set; }

    [FirestoreProperty]
    public string description { get; set; }
        
    [FirestoreProperty]
    public string productId { get; set; }

    [FirestoreProperty]
    public float x { get; set; }

    [FirestoreProperty]
    public float y { get; set; }

    [FirestoreProperty]
    public float z { get; set; }
}