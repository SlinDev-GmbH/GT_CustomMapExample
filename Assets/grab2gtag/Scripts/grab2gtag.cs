using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if (UNITY_EDITOR)
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.Networking;
using Google.Protobuf;
#endif

public class grab2gtag : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}


#if (UNITY_EDITOR)
public class LevelDetails
{
    public int iteration;
}

[CustomEditor(typeof(grab2gtag))]
public class grab2gtagEditor : Editor
{
    public GameObject gameObject;

    public GameObject accessDoorPrefab;
    public GameObject levelObject;

    public PhysicMaterial iceMaterial;
    public PhysicMaterial bounceMaterial;

    string levelURL = "https://grabvr.quest/levels/viewer/?level=29st9jdcbxxotp0t9d8en:1727536250";//"URL to GRAB level";

    public override void OnInspectorGUI()
    {
        GUILayout.Label("Convert levels from the best game to the second best game!");

        levelURL = GUILayout.TextField(levelURL);
        if (GUILayout.Button("Generate"))
        {
           grab2gtag script = (grab2gtag)target;
           gameObject = script.gameObject;

           string[] levelIDParts = levelURL.Split("=");
           string levelID = levelIDParts[1];
           levelID = levelID.Replace(":", "/");

            byte[] resultData = DownloadLevel("https://api.slin.dev/grab/v1/details/" + levelID);
            string jsonText = System.Text.Encoding.UTF8.GetString(resultData, 0, resultData.Length);

            LevelDetails jsonData = JsonUtility.FromJson<LevelDetails>(jsonText);

            levelID += "/" + jsonData.iteration;
            byte[] levelData = DownloadLevel("https://api.slin.dev/grab/v1/download/" + levelID);

            //Remove any existing level data
            for(int i = gameObject.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(gameObject.transform.GetChild(i).gameObject);
            }

            //Create a new level container object
            GameObject levelParent = new GameObject("LevelObjects");
            levelParent.transform.parent = gameObject.transform;

            //Create the actual level
            COD.Level.Level level = COD.Level.Level.Parser.ParseFrom(levelData);
            CreateSun(level.AmbienceSettings, levelParent.transform);
            foreach(COD.Types.LevelNode node in level.LevelNodes)
            {
               CreateLevelNode(node, levelParent.transform);
            }

            //Add a teleporter below the level to teleport back to start
            float lowestY = 1000000000.0f;
            foreach(Transform child in gameObject.transform.GetComponentsInChildren<Transform>())
            {
                if(child.position.y - child.lossyScale.y < lowestY) lowestY = child.position.y - child.lossyScale.y;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MapPrefabs/Teleporter.prefab");
            GameObject newObject = Instantiate(prefab, gameObject.transform);
            newObject.transform.localPosition = new Vector3(0, lowestY - 20, 0);
            newObject.transform.localScale = new Vector3(100000, 10, 100000);
            newObject.isStatic = true;
        }
    }

    private byte[] DownloadLevel(string levelurl)
    {
        UnityWebRequest www = new UnityWebRequest(levelurl);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SendWebRequest();

        while(!www.isDone) {}

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
            return www.downloadHandler.data;
        }
        else
        {
            // Or retrieve results as binary data
            return www.downloadHandler.data;
        }
    }

    private void CreateSun(COD.Types.AmbienceSettings settings, Transform parent)
    {
        GameObject sun = new GameObject("sun");
        sun.transform.parent = parent;
        sun.transform.eulerAngles = new Vector3(settings.SunAzimuth, -settings.SunAltitude, 0);

        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.lightmapBakeType = LightmapBakeType.Baked;
        light.shadows = LightShadows.Soft;

        sun.isStatic = true;
    }

    private void CreateLevelNode(COD.Types.LevelNode node, Transform parent)
    {
        switch(node.ContentCase)
        {
            case COD.Types.LevelNode.ContentOneofCase.LevelNodeGroup:
            {
                CreateLevelNodeGroup(node.LevelNodeGroup, parent);
                break;
            }

            case COD.Types.LevelNode.ContentOneofCase.LevelNodeStart:
            {
                CreateLevelNodeStart(node.LevelNodeStart, parent);
                break;
            }

            case COD.Types.LevelNode.ContentOneofCase.LevelNodeStatic:
            {
                CreateLevelNodeStatic(node.LevelNodeStatic, parent);
                break;
            }
        }
    }

    private void CreateLevelNodeGroup(COD.Types.LevelNodeGroup node, Transform parent)
    {
        GameObject group = new GameObject("group");
        group.transform.parent = parent;
        group.transform.localPosition = new Vector3(node.Position.X, node.Position.Y, -node.Position.Z);
        group.transform.localRotation = new Quaternion(node.Rotation.X, node.Rotation.Y, -node.Rotation.Z, -node.Rotation.W);
        group.transform.localScale = new Vector3(node.Scale.X, node.Scale.Y, node.Scale.Z);

        group.isStatic = true;

        foreach(COD.Types.LevelNode other in node.ChildNodes)
        {
            CreateLevelNode(other, group.transform);
        }
    }

    private void CreateLevelNodeStart(COD.Types.LevelNodeStart node, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MapPrefabs/AccessDoorPlaceholder.prefab");
        GameObject newObject = Instantiate(prefab, parent);
        newObject.transform.localPosition = new Vector3(node.Position.X, node.Position.Y, -node.Position.Z);
        newObject.transform.localRotation = new Quaternion(node.Rotation.X, node.Rotation.Y, -node.Rotation.Z, -node.Rotation.W);
        newObject.transform.parent = gameObject.transform;
        newObject.isStatic = true;

        GameObject prefabRespawn = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MapPrefabs/TeleportPoint.prefab");
        GameObject teleportPoint = Instantiate(prefabRespawn, parent);
        teleportPoint.transform.localPosition = new Vector3(node.Position.X, node.Position.Y + 0.5F, -node.Position.Z);
        teleportPoint.transform.localRotation = new Quaternion(node.Rotation.X, node.Rotation.Y, -node.Rotation.Z, -node.Rotation.W);
        teleportPoint.transform.parent = gameObject.transform;
        teleportPoint.isStatic = true;
        //teleportPoint.GetComponent<TeleporterSettings>();
    }

    private void CreateLevelNodeStatic(COD.Types.LevelNodeStatic node, Transform parent)
    {
        string objectShapeFile = "Assets/grab2gtag/Meshes/cube.gltf";
        if(node.Shape == COD.Types.LevelNodeShape.Cube) {}
        else if(node.Shape == COD.Types.LevelNodeShape.Sphere) objectShapeFile = "Assets/grab2gtag/Meshes/sphere.gltf";
        else if(node.Shape == COD.Types.LevelNodeShape.Cylinder) objectShapeFile = "Assets/grab2gtag/Meshes/cylinder.gltf";
        else if(node.Shape == COD.Types.LevelNodeShape.Pyramid) objectShapeFile = "Assets/grab2gtag/Meshes/pyramid.gltf";
        else if(node.Shape == COD.Types.LevelNodeShape.Prism) objectShapeFile = "Assets/grab2gtag/Meshes/prism.gltf";
        else if(node.Shape == COD.Types.LevelNodeShape.Cone) objectShapeFile = "Assets/grab2gtag/Meshes/cone.gltf";
        else return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(objectShapeFile);
        GameObject newObject = Instantiate(prefab, parent);
        newObject.transform.localPosition = new Vector3(node.Position.X, node.Position.Y, -node.Position.Z);
        newObject.transform.localRotation = new Quaternion(node.Rotation.X, node.Rotation.Y, -node.Rotation.Z, -node.Rotation.W);
        newObject.transform.localScale = new Vector3(node.Scale.X, node.Scale.Y, node.Scale.Z);
        newObject.isStatic = true;

        MeshCollider collider = newObject.AddComponent<MeshCollider>();
        collider.convex = true;

        MeshRenderer renderer = newObject.GetComponent<MeshRenderer>();
        if(!renderer) return;

        if(node.Material == COD.Types.LevelNodeMaterial.Default) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/grab2gtag/Materials/GRAB-Default.mat");
        else if(node.Material == COD.Types.LevelNodeMaterial.Grabbable) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/grab2gtag/Materials/GRAB-Grabbable.mat");
        else if(node.Material == COD.Types.LevelNodeMaterial.Ice)
        {
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/grab2gtag/Materials/GRAB-Ice.mat");
            newObject.name = "ice";
            collider.material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>("Assets/PhysicsMaterials/Slippery.physicMaterial");
        }
        else if(node.Material == COD.Types.LevelNodeMaterial.Lava)
        {
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/grab2gtag/Materials/GRAB-Lava.mat");
            newObject.name = "lava";
            collider.isTrigger = true;
            //newObject.AddComponent<TeleporterSettings>();
        }
        else if(node.Material == COD.Types.LevelNodeMaterial.Wood) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/grab2gtag/Materials/GRAB-Wood.mat");
        else if(node.Material == COD.Types.LevelNodeMaterial.Grapplable) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/grab2gtag/Materials/GRAB-Grapplable.mat");
        else if(node.Material == COD.Types.LevelNodeMaterial.GrapplableLava) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/grab2gtag/Materials/GRAB-Grapplable_Lava.mat");
        else if(node.Material == COD.Types.LevelNodeMaterial.GrabbableCrumbling) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/grab2gtag/Materials/GRAB-Grabbable_Breaking.mat");
        else if(node.Material == COD.Types.LevelNodeMaterial.DefaultColored) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/grab2gtag/Materials/GRAB-Colored.mat");
        else if(node.Material == COD.Types.LevelNodeMaterial.Bouncing)
        {
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/grab2gtag/Materials/GRAB-Bouncing.mat");
            newObject.name = "bouncing";
            collider.material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>("Assets/PhysicsMaterials/GorillaSurfaceBouncy.physicMaterial");
        }
        else return;

        if(node.Material == COD.Types.LevelNodeMaterial.DefaultColored && node.Color1 != null)
        {
            renderer.sharedMaterial = new Material(renderer.sharedMaterial);
            Color color = new Color(node.Color1.R, node.Color1.G, node.Color1.B);
            renderer.sharedMaterial.SetColor("baseColorFactor", color);
        }
    }
}
#endif