
/*
 * EasyRoads3D Runtime API example
 * 
 * This script is used in the scenes:
 * 
 * /Assets/EasyRoads3D scenes/scene runtime - new road network
 * /Assets/EasyRoads3D scenes/scene runtime - existing road network
 * 
 * Switch the Unity editor to Play mode to run the scripts
 * 
 * 
 * Important:
 * 
 * It has occured that the terrain was not restored leaving the road shape in the terrain 
 * after exiting Play Mode. This happened after putting focus on the Scene View 
 * window while in Play Mode! In another occasion this consistently happened until the 
 * Scene View window got focus! 
 * 
 * Please check the above using a test terrain and / or backup your terrains before using this code!
 * 
 * You can backup your terrain by simply duplicating the terrain object 
 * in the project folder
 * 
 * Also, check the OnDestroy() code, without this code the shape of the generated roads
 * will certainly remain in the terrain object after you exit Play Mode!
 * 
 * 
 * 
 * 
 * */


using UnityEngine;
using System.Collections;
using EasyRoads3Dv3;

public class runtimeScript : MonoBehaviour {


	public ERRoadNetwork roadNetwork;
	
	public ERRoad road;
	
	public GameObject go;
	public int currentElement = 0;
	public float distance = 0; 
	public float speed = 5f;


	void Start () {
	
		Debug.Log("Please read the comments at the top of the runtime script (/Assets/EasyRoads3D/Scripts/runtimeScript) before using the runtime API!");

		if(FindAnyObjectByType<ERModularBase>() != null)
        {
			// A road network object already exists in the scene
			// Create a small road network with road objects and intersections
			CreateRoadNetwork();
		}
        else
        {
			// Create a new road network object and a single road object
			CreateNewRoadNetwork();
		}

		// create dummy object and move it along the road in Update() 
		go = GameObject.CreatePrimitive(PrimitiveType.Cube);
	}

	void CreateNewRoadNetwork()
    {
		// Create Road Network object
		roadNetwork = new ERRoadNetwork();

		// Create a road object
		//	ERRoad road = roadNetwork.CreateRoad(string name);
		//	ERRoad road = roadNetwork.CreateRoad(string name, Vector3[] markers);
		//	ERRoad road = roadNetwork.CreateRoad(string name, ERRoadType roadType);
		//	ERRoad road = roadNetwork.CreateRoad(string name, ERRoadType roadType, Vector3[] markers);

		// create a new road type
		ERRoadType roadType = new ERRoadType();
		roadType.roadWidth = 6;
		roadType.roadMaterial = Resources.Load("Materials/roads/road material") as Material;
		// optional
		roadType.layer = 1;
		roadType.tag = "Untagged";
		//   roadType.hasMeshCollider = false; // default is true

		// create a new road
		Vector3[] markers = new Vector3[4];
		markers[0] = new Vector3(200, 5, 200);
		markers[1] = new Vector3(250, 5, 200);
		markers[2] = new Vector3(250, 5, 250);
		markers[3] = new Vector3(300, 5, 250);

		road = roadNetwork.CreateRoad("road 1", roadType, markers);

		// road.SetResolution(float value):void;

		// Add Marker: ERRoad.AddMarker(Vector3);
		road.AddMarker(new Vector3(300, 5, 300));

		// Add Marker: ERRoad.InsertMarker(Vector3);
		road.InsertMarker(new Vector3(275, 5, 235));
		// road.InsertMarkerAt(Vector3 pos, int index): void;

		// Delete Marker: ERRoad.DeleteMarker(int index);
		road.DeleteMarker(2);

		// Set the road width : ERRoad.SetWidth(float width);
		//	road.SetWidth(10);

		// Set the road material : ERRoad.SetMaterial(Material path);
		// Material mat = Resources.Load("Materials/roads/single lane") as Material;
		// road.SetMaterial(mat);

		// Add / remove a meshCollider component
		// road.SetMeshCollider(bool value):void;

		// Set the position of a marker
		// road.SetMarkerPosition(int index, Vector3 position):void;
		// road.SetMarkerPositions(Vector3[] position):void;
		// road.SetMarkerPositions(Vector3[] position, int index):void;

		// Get the position of a marker
		// road.GetMarkerPosition(int index):Vector3;

		// Get the position of a marker
		//   road.GetMarkerPositions():Vector3[];

		// Set the layer
		// road.SetLayer(int value):void;

		// Set the tag
		// road.SetTag(string value):void;

		// Set marker control type
		// road.SetMarkerControlType(int marker, ERMarkerControlType type) : bool; // Spline, StraightXZ, StraightXZY, Circular

		// Find a road object
		//  public static function ERRoadNetwork.GetRoadByName(string name) : ERRoad;

		// Get all road objects
		// public static function ERRoadNetwork.GetRoads() : ERRoad[];  

		// Snap vertices to the terrain (no terrain deformation)
		// road.SnapToTerrain(true);

		// Build the Road Network 
		roadNetwork.BuildRoadNetwork();

		// Remove EasyRoads3D script components from the game objects
		// roadNetwork.Finalize();

		// Restore the Road Network 
		// roadNetwork.RestoreRoadNetwork();

		// Show / Hide the white surfaces surrounding roads
		// public function roadNetwork.HideWhiteSurfaces(bool value) : void;

		// road.GetConnectionAtStart(): GameObject;
		// road.GetConnectionAtStart(out int connection): GameObject; // connections: 0 = bottom, 1= tip, 2 = left, 3 = right (the same for T crossings)

		// road.GetConnectionAtEnd(): GameObject;
		// road.GetConnectionAtEnd(out int connection): GameObject; // connections: 0 = bottom, 1= tip, 2 = left, 3 = right (the same for T crossings)

		// Snap the road vertices to the terrain following the terrain shape (no terrain deformation)
		// road.SnapToTerrain(bool value): void;
		// road.SnapToTerrain(bool value, float yOffset): void;

		// Get the road length
		// road.GetLength() : float;

		
	}

	void CreateRoadNetwork()
    {
		// This example shows how to use the scripting API with a road network object that is already in the scene
		// Create a reference to road objects and intersection sin the scene
		// Connect road objects with intersections
		// Attach new intersections to a road object

		// Only one Road Network object can exist in the scene
		// When a road network object already exists the ERRoadNetwork constructor will create
		// a reference to Road Network object that is already in the scene
		roadNetwork = new ERRoadNetwork();

		// Get a reference to road object "Default Road 0001" in the scene
		ERRoad road1 = roadNetwork.GetRoadByName("Default Road 001");

		// Get a reference to connection object "Default X Crossing 0001" in the scene
		ERConnection conn1 = roadNetwork.GetConnectionByName("Default X Crossing 001");

		// First find the nearest connection index to the start of road1 by passing the first marker index position
		int index = conn1.FindNearestConnectionIndex(road1.GetMarkerPosition(0));
		// Attach the start of road1 to this connection index
		road1.ConnectToStart(conn1, index);

		// Load a new connection object from the Resources folder
		ERConnection connPrefab = roadNetwork.GetConnectionPrefabByName("Default X Crossing");

		// Create a new instance of this prefab and align and connect it to the end of road object 1
		ERConnection conn2 = road1.AttachToEnd(connPrefab);

		// An instance of a connection prefab can also be added directly to the scene
		// public ERConnection InstantiateConnection(ERConnection connectionPrefab, string name, Vector3 position, Vector3 eulerAngles)

		// Create a new road object based on a road type available in the road network object

		// Get the available road types from the road network object in the scene
		// ERRoadType[] roadTypes = roadNetwork.GetRoadTypes();
		ERRoadType roadType = roadNetwork.GetRoadTypeByName("Default Road");

		// Set the road marker positions for the new road
		Vector3[] roadMarkers = new Vector3[2] {new Vector3(410f, 0f, 555f), new Vector3(465f, 0f, 555f)};

		// Create the road object
		ERRoad road2 = roadNetwork.CreateRoad("Default Road 002", roadType, roadMarkers);

		

		// Find the nearest connection index on conn2 to the start of road2 by passing the first marker index position
		index = conn2.FindNearestConnectionIndex(road2.GetMarkerPosition(0));

		// Attach the new road object to conn2
		road2.ConnectToStart(conn2, index);

		// Build the Road Network 
		roadNetwork.BuildRoadNetwork();

		// Restore the Road Network to continue adding road objects and intersections
		// roadNetwork.RestoreRoadNetwork();

	}

	void Update () {
	
		if(road != null){
			float deltaT = Time.deltaTime;
			float rSpeed = (deltaT * speed);
		
			distance += rSpeed;

			// pass the current distance to get the position on the road
//			Debug.Log(road);
			Vector3 v = road.GetPosition(distance, ref currentElement);
			v.y += 1;
		
			go.transform.position = v;
			go.transform.forward = road.GetLookatSmooth(distance, currentElement);;
		}

        // spline point info center of the road
        //      public function ERRoad.GetSplinePointsCenter() : Vector3[];

        // spline point info center of the road
        //      public function ERRoad.GetSplinePointsRightSide() : Vector3[];

        // spline point info center of the road
        //      public function ERRoad.GetSplinePointsLeftSide() : Vector3[];

        // Get the selected road in the Unity Editor
        //  public static function EREditor.GetSelectedRoad() : ERRoad;   



	}
	
	void OnDestroy(){

		// Restore road networks that are in Build Mode
		// This is very important otherwise the shape of roads will still be visible inside the terrain!

		if(roadNetwork != null){
			if(roadNetwork.isInBuildMode){
				roadNetwork.RestoreRoadNetwork();
				Debug.Log("Restore Road Network");
			}
		}
	}
}
