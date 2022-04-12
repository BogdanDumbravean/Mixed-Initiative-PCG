using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rules/Terrain")]
public class TerrainDependency : ScriptableObject
{
    public Texture terrain = null;
    public GameObject suggestion = null;
    public float minDist = 5;
    public float maxDist = 5;
    public float weight = 10;
    //public bool isTerrainMandatory = false;
}
