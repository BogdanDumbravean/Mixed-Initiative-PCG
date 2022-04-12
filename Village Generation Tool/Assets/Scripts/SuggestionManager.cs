using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class SuggestionManager
{
    const float raySpacing = 15f;

    public struct WeightedPrefab
	{
        public float weight;
        public int prefabIndex;
    }

    public class SimilarLocation : IEquatable<SimilarLocation>
    {
        private const float similarDistance = 2f;

        public float weight;
		public Vector3 location;

		public SimilarLocation()
		{
            weight = 0;
            location = Vector3.zero;
        }

		public SimilarLocation(float weight, Vector3 location)
		{
			this.weight = weight;
			this.location = location;
		}

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public bool Equals(SimilarLocation other)
		{
            return other != null && Vector3.Distance(location, other.location) < similarDistance;
        }

        public override int GetHashCode()
		{
			return HashCode.Combine(location);
		}
	}

    GenerationWindow generationWindow;
    TerrainTextureDetector terrainTextureDetector;

    public SuggestionManager(GenerationWindow _generationWindow, TerrainTextureDetector _terrainTextureDetector)
	{
        generationWindow = _generationWindow;
        terrainTextureDetector = _terrainTextureDetector;
        LoadAssets();
    }

    BuildingDependency[] buildingRules;
    TerrainDependency[] terrainRules;
    List<GameObject> prefabs => generationWindow.prefabs;
    WeightedPrefab[] weightedPrefabs;
    Dictionary<SimilarLocation, float> weightedLocations;
    int[] terrainIdxs;

    private IEnumerable<string> GetPaths(string type, string folder)
    {
        string[] guids = AssetDatabase.FindAssets(type, new string[] { folder });
        return guids.Select(AssetDatabase.GUIDToAssetPath);
    }

    void LoadAssets()
    {
        buildingRules = GetPaths("t:BuildingDependency", "Assets/Prefabs/Rules")
            .Select(AssetDatabase.LoadAssetAtPath<BuildingDependency>).ToArray();
        terrainRules = GetPaths("t:TerrainDependency", "Assets/Prefabs/Rules")
            .Select(AssetDatabase.LoadAssetAtPath<TerrainDependency>).ToArray();
    }

    public void AnalyzeTerrain()
	{
		Debug.Log("AnalyzeTerrain");
		terrainTextureDetector.RecacheData();
        var radius = (int)generationWindow.radius;
        terrainIdxs = new int[(int)(radius * radius * 4 / (2 * raySpacing))];
        int idx = 0;
        Vector3 pos;
        for (float posY = -generationWindow.radius; posY < generationWindow.radius; posY += raySpacing)
        {
            for (float posX = -generationWindow.radius; posX < generationWindow.radius; posX += raySpacing)
            {
                pos = generationWindow.centerPoint + posX * Vector3.right + posY * Vector3.back;
                terrainIdxs[idx++] = terrainTextureDetector.GetDominantTextureIndexAt(pos);
                //Debug.LogWarning((idx - 1) + " " + terrainIdxs[idx - 1] + " " + posX + " " + posY);
            }
        }
    }

    bool CheckNeighbours(int idx, Texture texture, float minDist, float maxDist)
    {
        var n = (int)(generationWindow.radius * 2 / raySpacing);
        int[] di = { -n, -n, -n, 0, 0, n, n, n };
        int[] dj = { -1, 0, 1, -1, 1, -1, 0, 1 };
        for (int i = 0; i < di.Length; ++i)
		{
            int deltaIdx = idx + di[i] + dj[i];
            int safetyNumber = 0;
            while (Vector3.Distance(IdxToWorldPos(idx), IdxToWorldPos(deltaIdx)) <= maxDist && safetyNumber < 10)
            {
                safetyNumber++;
                if (idx % n == 0 && (i == 0 || i == 3 || i == 5))
                    continue;
                if (idx % n == n - 1 && (i == 2 || i == 4 || i == 7))
                    continue;
                if (deltaIdx < 0 || deltaIdx >= terrainIdxs.Length)
                    continue;
                if (Vector3.Distance(IdxToWorldPos(idx), IdxToWorldPos(deltaIdx)) < minDist
                    || Vector3.Distance(IdxToWorldPos(idx), IdxToWorldPos(deltaIdx)) > maxDist)
                {
                    continue;
                }

                if (terrainIdxs[deltaIdx] != -1 && texture == TerrainIdxToTexture(terrainIdxs[deltaIdx]))
                {
                    //Debug.Log(idx + " " + deltaIdx + " " + texture);
                    return true;
                }
                deltaIdx += di[i] + dj[i];
            }
		}
        return false;
    }

    Texture TerrainIdxToTexture(int idx)
	{
        //Debug.Log("idx: " + idx + ", texture: " + Terrain.activeTerrain.terrainData.terrainLayers[0].diffuseTexture.name);
        if (idx < 0 || idx >= Terrain.activeTerrain.terrainData.terrainLayers.Length)
            return null;
        return Terrain.activeTerrain.terrainData.terrainLayers[idx].diffuseTexture;
    }

    Vector3 IdxToWorldPos(int idx)
    {
        //new NotImplementedException("IdxToTexture not implemented");
        var n = (int)(generationWindow.radius * 2 / raySpacing);
        var line = idx / n;
        var col = idx % n;
        var posX = (col - n / 2) * raySpacing;
        var posY = (line - n / 2) * raySpacing;
        //Debug.LogError(idx + " " + line + " " + col + " " + posX + " " + posY + " " + (generationWindow.centerPoint + posX * Vector3.right + posY * Vector3.back));
        return generationWindow.centerPoint + posX * Vector3.right + posY * Vector3.back;
    }

    List<TerrainDependency> GetTerrainRules(GameObject prefab)
    {
        return terrainRules.Where(x => x.suggestion == prefab).ToList();
    }
    
    List<BuildingDependency> GetBuildingRules(GameObject prefab)
    {
        return buildingRules.Where(x => x.suggestion.Contains(prefab)).ToList();
    }

    public List<KeyValuePair<SimilarLocation, float>> FindLocationSuggestions(GameObject prefab)
	{
        LoadAssets();
        Debug.Log("Searching Suggestions");
        
        weightedLocations = new Dictionary<SimilarLocation, float>();
        
        if(terrainIdxs != null)
        // maxTries random (with retry button)
            for (int i = 0; i < terrainIdxs.Length; ++i)
            {
                //Debug.Log("FindLocationSuggestions for1 " + i);
                var tr = GetTerrainRules(prefab);
                //var textures = tr.Select(x => x.terrain);
			    foreach (TerrainDependency td in tr)
                {
                    //Debug.Log("FindLocationSuggestions for2 " + td.terrain.name + " " + td.suggestion.name);
                    if (terrainIdxs[i] != -1)
                    {
					    //Debug.Log("FindLocationSuggestions terrain found " + i);
                        if(td.minDist != 0 && td.terrain == TerrainIdxToTexture(terrainIdxs[i]))
					    {
                            continue;
					    }
					    if (td.minDist == 0 && td.terrain == TerrainIdxToTexture(terrainIdxs[i]))
                        {
                            //Debug.Log("FindLocationSuggestions maxDist == 0 " + IdxToWorldPos(i));
                            var location = new SimilarLocation(td.weight, IdxToWorldPos(i));

                            if (weightedLocations.ContainsKey(location))
                                weightedLocations[location] += td.weight;
                            else
                                weightedLocations.Add(location, td.weight);
                        }
                        else if (td.maxDist > 0 && CheckNeighbours(i, td.terrain, td.minDist, td.maxDist))
                        {
                            //Debug.Log("FindLocationSuggestions maxDist != 0");

                            var location = new SimilarLocation(td.weight, IdxToWorldPos(i));

                            if (weightedLocations.ContainsKey(location))
                                weightedLocations[location] += td.weight;
                            else
                                weightedLocations.Add(location, td.weight);
                        }
                    }
                }
		    }
        // Check building rules
        var br = GetBuildingRules(prefab);
        var prefabRadius = prefab.GetComponent<SpawnableObject>().radius;
        foreach (BuildingDependency bd in br)
        {
            if (bd.existing.Length == 0)
                continue;

            float[] di = { -1, -1, -1, 0, 0, 1, 1, 1 };
            float[] dj = { -1, 0, 1, -1, 1, -1, 0, 1 };
            for (int i = 0; i < di.Length; ++i)
            {
                foreach(var bdExisting in bd.existing)
                    foreach (var go in GameObject.FindGameObjectsWithTag(bdExisting.tag))
                        if(go.name == bdExisting.name)
                        {
                            var origin = go.transform.position;
                            origin.x += di[i] * bd.minDist;
                            origin.z += dj[i] * bd.minDist;
                            for (float delta = 0; delta * prefabRadius + bd.minDist < bd.maxDist; delta++)
                            {
                                origin.x += di[i] * delta * prefabRadius;
                                origin.z += dj[i] * delta * prefabRadius;
                                if (Vector3.Distance(origin, generationWindow.centerPoint) <= generationWindow.radius 
                                    && !Physics.SphereCast(origin, prefabRadius, Vector3.down, out RaycastHit hit))
                                {
                                    var location = new SimilarLocation(bd.weight, origin);

                                    if (weightedLocations.ContainsKey(location))
                                        weightedLocations[location] += bd.weight;
                                    else
                                        weightedLocations.Add(location, bd.weight);
                                }
                            }
                        }
            }
        }
        
        return weightedLocations.Where(x => x.Value > 0).ToList();
	}

    public IEnumerable<WeightedPrefab> EvaluateRules()
    {
        AnalyzeTerrain();
        weightedPrefabs = new WeightedPrefab[prefabs.Count];
        for(int i = 0; i < prefabs.Count; ++i)
		{
            weightedPrefabs[i].prefabIndex = i;
		}
        EvaluateBuildingRules();
        EvaluateTerrainRules();
        //Array.Sort(weightedPrefabs, (x, y) => y.weight.CompareTo(x.weight));
        return weightedPrefabs;
    }

    private void EvaluateBuildingRules()
	{
        for (int i = 0; i < buildingRules.Length; ++i)
        {
            if (buildingRules[i].existing != null /* && GameObject.Find(buildingRules[i].existing.name) != null*/)
            {
                //var count =  GameObject.FindGameObjectsWithTag("Building")
                int count = 0;
                foreach (Transform child in generationWindow.buildingParent)
                {
                    foreach (var bre in buildingRules[i].existing)
                        if (child.name.Equals(bre.name))
                            count++;
                }
                foreach (var brs in buildingRules[i].suggestion)
                    if(brs != null)
                        weightedPrefabs[prefabs.IndexOf(brs)].weight += count * buildingRules[i].weight;
            }
        }
    }

    private void EvaluateTerrainRules()
	{
        for (int i = 0; i < terrainRules.Length; ++i)
            if(terrainIdxs.Any(x => TerrainIdxToTexture(x) == terrainRules[i].terrain))
            {
                weightedPrefabs[prefabs.IndexOf(terrainRules[i].suggestion)].weight += terrainRules[i].weight;
            }
    }
}
