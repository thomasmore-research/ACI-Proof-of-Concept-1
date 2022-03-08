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
public class Product
{
    [FirestoreProperty]
    public string id { get; set; }
    [FirestoreProperty]
    public string title { get; set; }
    [FirestoreProperty]
    public string description { get; set; }
    [FirestoreProperty]
    public string objectReference { get; set; }
    [FirestoreProperty]
    public string[] type { get; set; }
    [FirestoreProperty]
    public string brand { get; set; }
    [FirestoreProperty]
    public string[] colors { get; set; }
    [FirestoreProperty]
    public string AnchorID { get; set; }
    [FirestoreProperty]
    public string[] distanceToWork { get; set; }
    [FirestoreProperty]
    public string[] purpose { get; set; }
    [FirestoreProperty]
    public string intensity { get; set; }
    [FirestoreProperty]
    public float distanceInKm { get; set; }
    [FirestoreProperty]
    public string[] bikeStorage { get; set; }
    [FirestoreProperty]
    public string[] environment { get; set; }
    [FirestoreProperty]
    public string[] bikeStance { get; set; }
    [FirestoreProperty]
    public bool active { get; set; }

}