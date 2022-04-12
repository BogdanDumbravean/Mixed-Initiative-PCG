using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class GenerationWindow : EditorWindow
{
	[MenuItem("/Tools/Procedural Generation")]
	public static void OpenWindow() => GetWindow<GenerationWindow>();

	private const int buildingLayer = 5;
	private const int maxBuildingSuggestions = 10;
	private const int maxPlacementSuggestions = 10;
	private const int timeBetweenAnalysis = 5;

	public bool isBrushActive = false;
	public bool isAIActive = false;
	public float radius = 345;
	public int spawnCount = 10;
	public GameObject spawnPrefab = null;
	public Transform buildingParent;
	public Texture loadingIcon;
	public List<GameObject> prefabs;
	//public Material previewMaterial = null;
	public Vector3 centerPoint = Vector3.zero;

	SerializedObject so;
	SerializedProperty propIsBrushActive;
	SerializedProperty propIsAIActive;
	SerializedProperty propRadius;
	SerializedProperty propSpawnCount;
	SerializedProperty propLoadingIcon;
	SerializedProperty propBuildingParent;
	//SerializedProperty propPreviewMaterial;
	//SerializedProperty propCenterPoint;

	SpawnData[] randPoints;
	SpawnData[] suggestionPoints;
	Material materialHologram;
	SuggestionManager suggestionManager;
	TerrainTextureDetector terrainTextureDetector;
	List<GameObject> suggestions, filteredPrefabs;
	Vector2 scrollPosition1 = Vector2.zero, scrollPosition2 = Vector2.zero;
	string searchString;
	bool isPlacingManually, drawLoadingIcon;
	float timer;

	public struct SpawnData
	{
		public Vector2 pointInDisc;
		public Vector3 pointInWorld;
		public float rotationDegree;
		public GameObject prefab;

		public void SetRandomValues(GameObject spawnPrefab)
		{
			pointInDisc = Random.insideUnitCircle;
			rotationDegree = Random.value * 360;
			prefab = spawnPrefab;
		}
	}

	public class SpawnPoint	// instead of Pose
	{
		public Vector3 position;
		public Quaternion rotation;
		public SpawnData spawnData;
		public bool isValid = false;

		public Vector3 up => rotation * Vector3.up;

		public SpawnPoint(Vector3 position, Quaternion rotation, SpawnData spawnData)
		{
			this.position = position;
			this.rotation = rotation;
			this.spawnData = spawnData;

			if (spawnData.prefab != null)
			{
				SpawnableObject spawnableObject = spawnData.prefab.GetComponent<SpawnableObject>();
				if (spawnableObject == null)
				{
					isValid = true;
				}
				else
				{
					float h = spawnableObject.height;
					float r = spawnableObject.radius;
					//Ray ray = new Ray(position, up);
					bool sphereCast = Physics.OverlapCapsule(position, position + up * h, r, ~buildingLayer).Length != 0;
					isValid = sphereCast == false;
				}
			}
		}
	}

	private IEnumerable<string> GetPaths(string type, string folder)
	{
		string[] guids = AssetDatabase.FindAssets(type, new string[] { folder });
		return guids.Select(AssetDatabase.GUIDToAssetPath);
	}

	private void FindProperties()
	{
		so = new SerializedObject(this);
		propIsBrushActive = so.FindProperty("isBrushActive");
		propIsAIActive = so.FindProperty("isAIActive");
		propRadius = so.FindProperty("radius");
		propSpawnCount = so.FindProperty("spawnCount");
		propLoadingIcon = so.FindProperty("loadingIcon");
		propBuildingParent = so.FindProperty("buildingParent");
		//propPreviewMaterial = so.FindProperty("previewMaterial");
		//propCenterPoint = so.FindProperty("centerPoint");
	}

	private void SetGUIProperyFields()
	{
		EditorGUILayout.PropertyField(propIsBrushActive);
		EditorGUILayout.PropertyField(propIsAIActive);
		EditorGUILayout.PropertyField(propRadius);
		propRadius.floatValue = propRadius.floatValue.AtLeast(1);
		EditorGUILayout.PropertyField(propSpawnCount);
		propSpawnCount.intValue = propSpawnCount.intValue.AtLeast(1);
		EditorGUILayout.PropertyField(propLoadingIcon);
		EditorGUILayout.PropertyField(propBuildingParent);
		//EditorGUILayout.PropertyField(propPreviewMaterial);
		//EditorGUILayout.PropertyField(propCenterPoint);
		GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
		boxStyle.normal.textColor = Color.white;
		GUILayout.Box("Left ctrl + scrolling wheel - rotate buildings when placing manually", boxStyle);
		GUILayout.Box("Left ctrl in AI mode - manual building placement", boxStyle);
	}

	private void OnEnable()
	{
		FindProperties();

		//GenerateRandomPoints();
		SceneView.duringSceneGui += DuringSceneGUI;
		Undo.undoRedoPerformed += Repaint;

		Shader sh = Shader.Find("Unlit/GreenHologram");
		materialHologram = new Material(sh);

		// load prefabs
		prefabs = GetPaths("t:prefab", "Assets/Prefabs/Houses")
			.Select(AssetDatabase.LoadAssetAtPath<GameObject>).OrderBy(e => e.name).ToList();
		filteredPrefabs = new List<GameObject>(prefabs);

		Analyze();
	}

	private void OnInspectorUpdate()
	{
		if (!isBrushActive || !isAIActive)
			return;

		timer += Time.deltaTime;
	}

	private void RefreshTerrainData()
	{
		terrainTextureDetector = GameObject.FindObjectOfType<TerrainTextureDetector>();
		if (terrainTextureDetector == null)
			Debug.LogError("TerrainTextureDetector null");
		else
			suggestionManager = new SuggestionManager(this, terrainTextureDetector);

		suggestions = new List<GameObject>();
	}

	private void Analyze()
	{
		//drawLoadingIcon = true;
		RefreshTerrainData();
		suggestions = suggestionManager.EvaluateRules().OrderByDescending(item => item.weight + Random.value * 0.1f).Select(x => prefabs[x.prefabIndex]).ToList();
		//foreach (GameObject p in suggestions)
		//{
		//	Debug.Log(p.name);
		//}
		//drawLoadingIcon = false;
	}

	private void OnDisable()
	{
		SceneView.duringSceneGui -= DuringSceneGUI;
		Undo.undoRedoPerformed -= Repaint;
		DestroyImmediate(materialHologram);
	}

	void GenerateRandomPoints()
	{
		randPoints = new SpawnData[spawnCount];
		for (int i = 0; i < spawnCount; i++)
		{
			randPoints[i].SetRandomValues(spawnPrefab);
		}
	}

	private void OnGUI()
	{
		so.Update();
		SetGUIProperyFields();

		if (so.ApplyModifiedProperties())
		{
			if (isBrushActive && isAIActive)
			{
				isPlacingManually = false;
				Analyze();
				FindLocationSuggestions();
			}

			//GenerateRandomPoints();	// this should not happen for all props
			SceneView.RepaintAll();
		}

		if(Event.current.type == EventType.MouseDown && Event.current.button == 0)
		{
			GUI.FocusControl(null);
			Repaint();
		}
	}

	void DuringSceneGUI(SceneView sceneView)
	{
		EditorGUI.BeginChangeCheck();
		so.Update();
		//propCenterPoint.vector3Value = Handles.PositionHandle(centerPoint, Quaternion.identity);
		so.ApplyModifiedProperties();
		if (EditorGUI.EndChangeCheck())
		{
			Repaint();
		}

		DrawSugesstionToggleGUI();

		
		Handles.zTest = CompareFunction.LessEqual;


		if(Event.current.type == EventType.MouseMove)
		{
			sceneView.Repaint();
		}


		if (isBrushActive == false)
			return;

		if (isAIActive == false)
			isPlacingManually = true;

		if (timer >= timeBetweenAnalysis && Event.current.type != EventType.MouseDown)
		{
			//Debug.Log("timer");
			Analyze();
			timer = 0;
		}

		//ScrollToSizeCircle();


		Ray ray = new Ray(sceneView.camera.transform.position, centerPoint - sceneView.camera.transform.position); //HandleUtility.GUIPointToWorldRay(Event.current.mousePosition)
		
		if (Physics.Raycast(ray, out RaycastHit hit, 1000, buildingLayer))
		{
			Transform cameraTransform = sceneView.camera.transform;
			Vector3 hitNormal = Vector3.up;// hit.normal;
			Vector3 hitTangent = Vector3.Cross(hitNormal, cameraTransform.up).normalized;
			Vector3 hitBitangent = Vector3.Cross(hitNormal, hitTangent);

			List<SpawnPoint> hitPoints = GetSpawnPoints(hit.point, hitNormal, hitTangent, hitBitangent);

			

			if (Event.current.type == EventType.Repaint)
			{
				DrawCircle(hit.point, hitNormal, hitTangent, hitBitangent);

				DrawSpawnPreviews(hitPoints, sceneView.camera);

				if(suggestionPoints != null)
				for (int i = 0; i < suggestionPoints.Length; ++i)
				{
					DrawSphere(suggestionPoints[i].pointInWorld);
					Handles.DrawAAPolyLine(suggestionPoints[i].pointInWorld, suggestionPoints[i].pointInWorld + Vector3.up);
				}
			}

			// spawn on press
			//if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
			//{
			//	TrySpawnObjects(hitPoints);

			//}

			if (isAIActive)
			{
				if(Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.V)
					FindLocationSuggestions();
				if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.B)
					Analyze();
			}

			Quaternion randRot = Quaternion.identity;
			if (isAIActive && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.LeftControl)
			{
				isPlacingManually = true;
				randRot = Quaternion.Euler(0f, Random.value * 360, 0f);

				//Ray ptRay = new Ray(suggestionPoint.pointInWorld + Vector3.up * 3f, Vector3.down);

				//if (Physics.Raycast(ptRay, out RaycastHit ptHit, 100, buildingLayer))
				//{
				//	// calculate rotation and assign to pose together with position
				//	Quaternion randRot = Quaternion.Euler(0f, suggestionPoint.rotationDegree, 0f);
				//	Quaternion rot = Quaternion.LookRotation(ptHit.normal) * Quaternion.Euler(90f, 0f, 0f) * randRot;
				//	SpawnPoint pose = new SpawnPoint(ptHit.point, rot, suggestionPoint);
				//	hitPoses.Add(pose);
				//}
				//var sd = new SpawnData();
				//sd.
				//DrawSpawnPreviews(new List<SpawnPoint> { new SpawnPoint() }, sceneView.camera);
			}
			if (isAIActive && Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.LeftControl)
			{
				isPlacingManually = false;
				FindLocationSuggestions();
			}
			if(isPlacingManually)
			{
				//List<SpawnPoint> hitPoses = new List<SpawnPoint>();
				Ray cursorRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
				if (Physics.Raycast(cursorRay, out RaycastHit cursorHit, 10000, buildingLayer))
				{
					//Quaternion rot = Quaternion.LookRotation(cursorHit.normal) * Quaternion.Euler(90f, 0f, 0f) * randRot;
					if (suggestionPoints == null || suggestionPoints.Length != 1 || suggestionPoints[0].prefab != spawnPrefab)
					{
						var sd = new SpawnData();
						sd.rotationDegree = randRot.y;
						sd.prefab = spawnPrefab;
						sd.pointInWorld = cursorHit.point;
						suggestionPoints = new SpawnData[] { sd };
					}
					else
					{
						suggestionPoints[0].pointInWorld = cursorHit.point;
					}
					//sd.SetRandomValues(spawnPrefab);
					//SpawnPoint pose = new SpawnPoint(cursorHit.point, rot, sd);
					//hitPoses.Add(pose);
				}

				bool holdingCtrl = (Event.current.modifiers & EventModifiers.Control) != 0;
				if (Event.current.type == EventType.ScrollWheel && holdingCtrl)
				{
					float scrollDirection = Mathf.Sign(Event.current.delta.y);

					if (suggestionPoints != null && suggestionPoints.Length == 1)
					{
						suggestionPoints[0].rotationDegree += scrollDirection * 10f;
					}
					Repaint();
					Event.current.Use();
				}
			}

			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.C)
			{
				Ray cursorRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
				if (Physics.Raycast(cursorRay, out RaycastHit cursorHit)) 
				{
					foreach (SpawnPoint spawnPoint in hitPoints)
					{
						float spawnPointRadius = spawnPoint.spawnData.prefab.GetComponent<SpawnableObject>().radius;
						if (spawnPoint.isValid && Vector3.Distance(cursorHit.point, spawnPoint.position) < spawnPointRadius)
						{
							GameObject spawnedObject = (GameObject)PrefabUtility.InstantiatePrefab(spawnPrefab, buildingParent);
							Undo.RegisterCreatedObjectUndo(spawnedObject, "Spawn Objects");
							spawnedObject.transform.position = spawnPoint.position;
							spawnedObject.transform.rotation = spawnPoint.rotation;
							break;
						}
					}
				}
			}
		}
	}

	void DrawLoadingIcon()
	{
		if (loadingIcon == null)
			return;
		Rect iconRect = new Rect(100, 80, 20, 20);

		GUI.Button(iconRect, loadingIcon);
	}

	void DrawHorizontalScroll()
	{
		Rect iconRect = new Rect(110, 0, 64, 64);
		Rect labelRect = new Rect(0, 40, 64, 64);

		scrollPosition1 = GUI.BeginScrollView(new Rect(110, 8, 380, 90), scrollPosition1, new Rect(110, 8, 5 + maxBuildingSuggestions * (iconRect.width + 5), 15));
		for (int i = 0; i < suggestions.Count && i < maxBuildingSuggestions; ++i)
		{
			Texture icon = AssetPreview.GetAssetPreview(suggestions[i]);

			EditorGUI.BeginChangeCheck();
			if (GUI.Toggle(iconRect, spawnPrefab == suggestions[i], new GUIContent(icon)))
			{
				spawnPrefab = suggestions[i];
			}
			if (EditorGUI.EndChangeCheck() && isAIActive)
			{
				//GenerateRandomPoints();
				FindLocationSuggestions();
			}
			labelRect.x = iconRect.x + 10;
			GUI.Label(labelRect, new GUIContent(suggestions[i].name));

			iconRect.x += iconRect.width + 5;
		}

		GUI.EndScrollView();
	}

	void DrawVerticalScroll()
	{
		Rect iconRect = new Rect(8, 110, 64, 64);
		Rect labelRect = new Rect(8, 110, 100, 64);

		//scrollPosition = GUI.VerticalScrollbar(rect, scrollPosition, 2, 0, 10);
		scrollPosition2 = GUI.BeginScrollView(new Rect(8, 110, 120, 300), scrollPosition2, new Rect(8, 110, 15, 5 + filteredPrefabs.Count * (iconRect.height + 2)), false, true);

		for (int i = 0; i < filteredPrefabs.Count /*&& i < maxBuildingSuggestions*/; ++i)
		{
			Texture icon = AssetPreview.GetAssetPreview(filteredPrefabs[i]);

			EditorGUI.BeginChangeCheck();
			if (GUI.Toggle(iconRect, spawnPrefab == filteredPrefabs[i], new GUIContent(icon)))
			{
				spawnPrefab = filteredPrefabs[i];
			}
			if (EditorGUI.EndChangeCheck() && isAIActive)
			{
				//GenerateRandomPoints();
				FindLocationSuggestions();
			}
			labelRect.y = iconRect.y + iconRect.height / 2;
			GUI.Label(labelRect, new GUIContent(filteredPrefabs[i].name));

			iconRect.y += iconRect.height + 2;
		}

		GUI.EndScrollView();
	}

	void DrawSugesstionToggleGUI()
	{
		Handles.BeginGUI();


		if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.LeftShift)
			drawLoadingIcon = true;
		if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.LeftShift)
			drawLoadingIcon = false;
		if (drawLoadingIcon)
			DrawLoadingIcon();

		if (isAIActive)
			DrawHorizontalScroll();

		DrawVerticalScroll();

		var iconRect = new Rect(8, 8, 90, 35);
		if(GUI.Button(iconRect, "Analyze"))
		{
			Analyze();
		}
		iconRect.y += iconRect.height + 2;
		if (GUI.Button(iconRect, "Refresh\nPlacements") && isAIActive)
		{
			FindLocationSuggestions();
			//suggestionManager.AnalyzeTerrain();
		}


		iconRect.y += iconRect.height + 10;
		GUILayout.BeginArea(new Rect(iconRect));
		var aux = GUILayout.TextField(searchString, GUI.skin.FindStyle("ToolbarSeachTextField"), GUILayout.Width(100));
		if(aux != null && aux.Equals(searchString) == false)
		{
			aux = aux.ToLower();
			filteredPrefabs = prefabs.FindAll(e => e.name.ToLower().Contains(aux));
			searchString = aux;
		}
		GUILayout.EndArea();

		Handles.EndGUI();
	}

	void FindLocationSuggestions()
	{//TODO: if all top suggestions are in similar location
	 //TODO: loading icon
		drawLoadingIcon = true;

		if (spawnPrefab == null)
			return;
		var l = suggestionManager.FindLocationSuggestions(spawnPrefab).OrderByDescending(item => item.Value + Random.value).ToList();
		
		if (l.Count == 0)
		{
			suggestionPoints = new SpawnData[0];
			Debug.LogWarning("No placement suggestions to show!");
			return;
		}
		if (l.Count > maxPlacementSuggestions)
		{
			l = l.GetRange(0, maxPlacementSuggestions);
		}
		suggestionPoints = new SpawnData[l.Count];
		for (int i = 0; i < suggestionPoints.Length; ++i)
		{
			suggestionPoints[i].SetRandomValues(spawnPrefab);
			suggestionPoints[i].pointInWorld = l[i].Key.location;
			//Debug.Log(suggestionPoints[i].pointInWorld);
		}
		drawLoadingIcon = false;
	}

	//void ScrollToSizeCircle()
	//{
	//	bool holdingCtrl = (Event.current.modifiers & EventModifiers.Control) != 0;
	//	if (Event.current.type == EventType.ScrollWheel && holdingCtrl)
	//	{
	//		float scrollDirection = Mathf.Sign(Event.current.delta.y);

	//		so.Update();
	//		propRadius.floatValue *= 1f + scrollDirection * 0.1f;
	//		so.ApplyModifiedProperties();
	//		Repaint();
	//		Event.current.Use();
	//	}
	//}

	void DrawSpawnPreviews(List<SpawnPoint> spawnPoints, Camera cam)
	{
		foreach(SpawnPoint spawnPoint in spawnPoints)
		{
			if (spawnPoint.spawnData.prefab != null && spawnPoint.isValid)
			{
				Matrix4x4 poseToWorld = Matrix4x4.TRS(spawnPoint.position, spawnPoint.rotation, Vector3.one);
				DrawMesh(spawnPoint.spawnData.prefab, poseToWorld, cam, spawnPoint.isValid);
			}
			else
			{
				// draw sphere and normal on surface
				DrawSphere(spawnPoint.position);
				Handles.DrawAAPolyLine(spawnPoint.position, spawnPoint.position + spawnPoint.up);
			}
		}
	}

	void DrawSphere(Vector3 pos)
	{
		Handles.SphereHandleCap(-1, pos, Quaternion.identity, 0.1f, EventType.Repaint);
	}

	void DrawMesh(GameObject spawnPrefab, Matrix4x4 poseToWorld, Camera cam, bool valid)
	{
		MeshFilter[] filters = spawnPrefab.GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter filter in filters)
		{
			Matrix4x4 childToPose = filter.transform.localToWorldMatrix;
			Matrix4x4 childToWorld = poseToWorld * childToPose;
			
			Mesh mesh = filter.sharedMesh;

			Material mat = materialHologram; //filter.GetComponent<MeshRenderer>().sharedMaterial
			for (int i = 0; i < mesh.subMeshCount; ++i)
			{
				Graphics.DrawMesh(mesh, childToWorld, mat, 0, cam, i);
			}
		}
	}

	void DrawCircle(Vector3 hitPoint, Vector3 hitNormal, Vector3 hitTangent, Vector3 hitBitangent)
	{
		int circleDetail = 128;
		Vector3[] ringPoints = new Vector3[circleDetail];
		for (int i = 0; i < circleDetail; i++)
		{
			float t = i / ((float)circleDetail - 1);
			const float TAU = 6.28318530718f;
			float angRad = t * TAU;
			Vector2 dir = new Vector2(Mathf.Cos(angRad), Mathf.Sin(angRad));
			Ray r = GetTangentRay(dir, hitPoint, hitNormal, hitTangent, hitBitangent);

			if (Physics.Raycast(r, out RaycastHit cHit, 100, buildingLayer))
			{
				ringPoints[i] = cHit.point + cHit.normal * 0.02f;
			}
			else
			{
				ringPoints[i] = r.origin;
			}
		}
		Handles.DrawAAPolyLine(ringPoints);
	}

	List<SpawnPoint> GetSpawnPoints(Vector3 hitPoint, Vector3 hitNormal, Vector3 hitTangent, Vector3 hitBitangent)
	{
		List<SpawnPoint> hitPoses = new List<SpawnPoint>();
		// drawing points
		if (suggestionPoints == null)
			return hitPoses;
		foreach (SpawnData suggestionPoint in suggestionPoints)
		{
			//Ray ptRay = GetTangentRay(rndDataPoint.pointInDisc, hitPoint, hitNormal, hitTangent, hitBitangent);
			Ray ptRay = new Ray(suggestionPoint.pointInWorld + Vector3.up * 300f, Vector3.down);

			if (Physics.Raycast(ptRay, out RaycastHit ptHit, 500, buildingLayer))
			{
				// calculate rotation and assign to pose together with position
				Quaternion randRot = Quaternion.Euler(0f, suggestionPoint.rotationDegree, 0f);
				Quaternion rot = Quaternion.LookRotation(/*ptHit.normal*/Vector3.up) * Quaternion.Euler(90f, 0f, 0f) * randRot;
				SpawnPoint pose = new SpawnPoint(ptHit.point, rot, suggestionPoint);
				hitPoses.Add(pose);
			}
		}
		return hitPoses;
	}
	
	//void TrySpawnObjects(List<SpawnPoint> spawnPoints)
	//{
	//	if (spawnPrefab == null)
	//		return;

	//	foreach (SpawnPoint spawnPoint in spawnPoints)
	//	{
	//		//spawnPoint.spawnData.prefab.GetComponent<SpawnableObject>().radius;
	//		if (spawnPoint.isValid == false)
	//			continue;

	//		GameObject spawnedObject = (GameObject)PrefabUtility.InstantiatePrefab(spawnPrefab);
	//		Undo.RegisterCreatedObjectUndo(spawnedObject, "Spawn Objects");
	//		spawnedObject.transform.position = spawnPoint.position;
	//		spawnedObject.transform.rotation = spawnPoint.rotation;
	//	}
	//	GenerateRandomPoints();
	//}

	Ray GetTangentRay(Vector3 tangentSpacePos, Vector3 hitPoint, Vector3 hitNormal, Vector3 hitTangent, Vector3 hitBitangent)
	{
		Vector3 rayOrigin = hitPoint + (hitTangent * tangentSpacePos.x + hitBitangent * tangentSpacePos.y) * radius;
		rayOrigin += hitNormal * 50; // offset margin
		Vector3 rayDirection = -hitNormal;
		return new Ray(rayOrigin, rayDirection);
	}
}
