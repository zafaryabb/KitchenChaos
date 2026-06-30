using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class KitchenObjectSO : ScriptableObject
{
    public GameObject prefab;
    public Sprite icon;
    public string objectName;

    [Tooltip("If true, this item can be given to the pet at the cat station (e.g. fish skeletons, crab shells).")]
    public bool petFood;
}
