using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SpawnableObject : MonoBehaviour
{
	public float height = 1f;
	public float radius = 1f;

	void OnDrawGizmosSelected()
	{
		Vector3 bottom = transform.position;
		Vector3 top = transform.position + transform.up * height;
		Handles.DrawAAPolyLine(bottom, top);
		void DrawSphere(Vector3 pos) => Gizmos.DrawSphere(pos, HandleUtility.GetHandleSize(pos) * 0.01f);

		DrawSphere(bottom);
		DrawSphere(top);

		Handles.DrawWireDisc(transform.position, Vector3.up, radius);
	}
}
