using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Terrain))][ExecuteAlways]
public class TerrainTextureDetector : MonoBehaviour
{
    Terrain ThisTerrain;
    TerrainData ThisTerrainData => ThisTerrain.terrainData;

    float[,,] CachedTerrainAlphamapData;

    void OnEnable()
    {
        ThisTerrain = GetComponent<Terrain>();

        CachedTerrainAlphamapData = ThisTerrainData.GetAlphamaps(0, 0, ThisTerrainData.alphamapWidth, ThisTerrainData.alphamapHeight);
    }

    public void RecacheData()
	{
        CachedTerrainAlphamapData = ThisTerrainData.GetAlphamaps(0, 0, ThisTerrainData.alphamapWidth, ThisTerrainData.alphamapHeight);
    }

    /// <summary>
    /// Gets the index of the most visible texture on the terrain at the specified point in world space.
    /// These texture indexes are assigned in the "paint textures" tab of the terrain inspector.
    /// If the supplied position is outside the bounds of the terrain, this function will return -1.
    /// </summary>
    public int GetDominantTextureIndexAt(Vector3 worldPosition)
    {
        Vector3Int alphamapCoordinates = ConvertToAlphamapCoordinates(worldPosition);

        if (!CachedTerrainAlphamapData.ContainsIndex(alphamapCoordinates.x, dimension: 1))
            return -1;

        if (!CachedTerrainAlphamapData.ContainsIndex(alphamapCoordinates.z, dimension: 0))
            return -1;


        int mostDominantTextureIndex = 0;
        float greatestTextureWeight = float.MinValue;

        int textureCount = CachedTerrainAlphamapData.GetLength(2);
        for (int textureIndex = 0; textureIndex < textureCount; textureIndex++)
        {
            // I am really not sure why the x and z coordinates are out of order here, I think it's just Unity being lame and weird
            float textureWeight = CachedTerrainAlphamapData[alphamapCoordinates.z, alphamapCoordinates.x, textureIndex];

            if (textureWeight > greatestTextureWeight)
            {
                greatestTextureWeight = textureWeight;
                mostDominantTextureIndex = textureIndex;
            }
        }

        return mostDominantTextureIndex;


        Vector3Int ConvertToAlphamapCoordinates(Vector3 _worldPosition)
        {
            Vector3 relativePosition = _worldPosition - transform.position;
            // Important note: terrains cannot be rotated, so we don't have to worry about rotation

            return new Vector3Int
            (
                x: Mathf.RoundToInt((relativePosition.x / ThisTerrainData.size.x) * ThisTerrainData.alphamapWidth),
                y: 0,
                z: Mathf.RoundToInt((relativePosition.z / ThisTerrainData.size.z) * ThisTerrainData.alphamapHeight)
            );
        }
    }
}
