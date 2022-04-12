using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rules/Building")]
public class BuildingDependency : ScriptableObject
{
    public GameObject[] existing = null;
    public GameObject[] suggestion = null;
    public float minDist = 5;
    public float maxDist = 5;
    public float weight = 10;
    //public bool isExistingMandatory = false;
}
