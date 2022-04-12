using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ExtensionMethods
{
	public static float AtLeast(this float v, float min) => Mathf.Max(v, min);

	public static int AtLeast(this int v, int min) => Mathf.Max(v, min);

    public static bool ContainsIndex(this Array array, int index, int dimension)
    {
        if (index < 0)
            return false;

        return index < array.GetLength(dimension);
    }
}
