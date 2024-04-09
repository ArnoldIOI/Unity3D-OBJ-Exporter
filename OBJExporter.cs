#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Text;
using System.Collections.Generic;

/*===============================================================================================================
 |      Unity3D Scene OBJ Exporter (Function Call Supported)
 |      Credicts to aaro4130 (https://assetstore.unity.com/packages/tools/utilities/scene-obj-exporter-22250)
 |      Modified by: 0xAC
 |==================================Usage========================================================================
 |      1. Place this script in your work directory.
 |      2. Call GameObjectExporter.ExportOBJ(savePath) to export the scene into .obj file.
 |==================================Parameters===================================================================
 |      savePath: string
 |         The path to save the .obj file.
 |      onlySelectedObjects: bool (default: false)
 |         Export only selected objects.
 |      applyPosition: bool (default: true)
 |         Apply the position of the object.
 |      applyRotation: bool (default: true)
 |         Apply the rotation of the object.
 |      applyScale: bool (default: true)
 |         Apply the scale of the object.
 |      generateMaterials: bool (default: true)
 |         Generate materials for the .obj file.
 |      exportTextures: bool (default: true)
 |         Export textures for the .obj file.
 |      splitObjects: bool (default: true)
 |         Split objects into different .obj files.
 |      autoMarkTexReadable: bool (default: true)
 |         Automatically mark textures as readable.
 |      objNameAddIdNum: bool (default: true)
 |         Add an id number to the object name.
 |
 |=================================Example=====================================
 |      GameObjectExporter.ExportOBJ("Assets/Exported/scene.obj");
 |=================================Notes from original author===================
 |      Project:  Unity3D Scene OBJ Exporter
 |
 |        Notes: Only works with meshes + meshRenderers. No terrain yet
 |
 |       Author:  aaro4130
 |
 |     DO NOT USE PARTS OF THIS CODE, OR THIS CODE AS A WHOLE AND CLAIM IT
 |     AS YOUR OWN WORK. USE OF CODE IS ALLOWED IF I (aaro4130) AM CREDITED
 |     FOR THE USED PARTS OF THE CODE.
 |
 *===========================================================================*/

public class GameObjectExporter
{
    public static void ExportOBJ(string exportPath, bool onlySelectedObjects=false, bool applyPosition=true, bool applyRotation=true, bool applyScale=true, bool generateMaterials=true, bool exportTextures=true, bool splitObjects=true, bool autoMarkTexReadable=true, bool objNameAddIdNum=true)
    { 
        //init stuff
        Dictionary<string, bool> materialCache = new Dictionary<string, bool>();
        var exportFileInfo = new System.IO.FileInfo(exportPath);
        string baseFileName = System.IO.Path.GetFileNameWithoutExtension(exportPath);
        EditorUtility.DisplayProgressBar("Exporting OBJ", "Please wait.. Starting export.", 0);

        //get list of required export things
        MeshFilter[] sceneMeshes;
        if (onlySelectedObjects)
        {
            List<MeshFilter> tempMFList = new List<MeshFilter>();
            foreach (GameObject g in Selection.gameObjects)
            {

                MeshFilter f = g.GetComponent<MeshFilter>();
                if (f != null)
                {
                    tempMFList.Add(f);
                }

            }
            sceneMeshes = tempMFList.ToArray();
        }
        else
        {
            sceneMeshes = GameObject.FindObjectsOfType(typeof(MeshFilter)) as MeshFilter[];

        }

        if (Application.isPlaying)
        {
            foreach (MeshFilter mf in sceneMeshes)
            {
                MeshRenderer mr = mf.gameObject.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (mr.isPartOfStaticBatch)
                    {
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("Error", "Static batched object detected. Static batching is not compatible with this exporter. Please disable it before starting the player.", "OK");
                        return;
                    }
                }
            }
        }
        
        //work on export
        StringBuilder sb = new StringBuilder();
        StringBuilder sbMaterials = new StringBuilder();
        sb.AppendLine("# Export of " + Application.loadedLevelName);
        if (generateMaterials)
        {
            sb.AppendLine("mtllib " + baseFileName + ".mtl");
        }
        float maxExportProgress = (float)(sceneMeshes.Length + 1);
        int lastIndex = 0;
        for(int i = 0; i < sceneMeshes.Length; i++)
        {
            string meshName = sceneMeshes[i].gameObject.name;
            float progress = (float)(i + 1) / maxExportProgress;
            EditorUtility.DisplayProgressBar("Exporting objects... (" + Mathf.Round(progress * 100) + "%)", "Exporting object " + meshName, progress);
            MeshFilter mf = sceneMeshes[i];
            MeshRenderer mr = sceneMeshes[i].gameObject.GetComponent<MeshRenderer>();

            if (splitObjects)
            {
                string exportName = meshName;
                if (objNameAddIdNum)
                {
                    exportName += "_" + i;
                }
                sb.AppendLine("g " + exportName);
            }
            if(mr != null && generateMaterials)
            {
                Material[] mats = mr.sharedMaterials;
                for(int j=0; j < mats.Length; j++)
                {
                    Material m = mats[j];
                    if (!materialCache.ContainsKey(m.name))
                    {
                        materialCache[m.name] = true;
                        sbMaterials.Append(MaterialToString(m, exportTextures, exportFileInfo.Directory.FullName, autoMarkTexReadable));
                        sbMaterials.AppendLine();
                    }
                }
            }

            //export the meshhh :3
            Mesh msh = mf.sharedMesh;
            int faceOrder = (int)Mathf.Clamp((mf.gameObject.transform.lossyScale.x * mf.gameObject.transform.lossyScale.z), -1, 1);
            
            //export vector data (FUN :D)!
            foreach (Vector3 vx in msh.vertices)
            {
                Vector3 v = vx;
                if (applyScale)
                {
                    v = MultiplyVec3s(v, mf.gameObject.transform.lossyScale);
                }
                
                if (applyRotation)
                {
  
                    v = RotateAroundPoint(v, Vector3.zero, mf.gameObject.transform.rotation);
                }

                if (applyPosition)
                {
                    v += mf.gameObject.transform.position;
                }
                v.x *= -1;
                sb.AppendLine("v " + v.x + " " + v.y + " " + v.z);
            }
            foreach (Vector3 vx in msh.normals)
            {
                Vector3 v = vx;
                
                if (applyScale)
                {
                    v = MultiplyVec3s(v, mf.gameObject.transform.lossyScale.normalized);
                }
                if (applyRotation)
                {
                    v = RotateAroundPoint(v, Vector3.zero, mf.gameObject.transform.rotation);
                }
                v.x *= -1;
                sb.AppendLine("vn " + v.x + " " + v.y + " " + v.z);

            }
            foreach (Vector2 v in msh.uv)
            {
                sb.AppendLine("vt " + v.x + " " + v.y);
            }

            for (int j=0; j < msh.subMeshCount; j++)
            {
                if(mr != null && j < mr.sharedMaterials.Length)
                {
                    string matName = mr.sharedMaterials[j].name;
                    sb.AppendLine("usemtl " + matName);
                }
                else
                {
                    sb.AppendLine("usemtl " + meshName + "_sm" + j);
                }

                int[] tris = msh.GetTriangles(j);
                for(int t = 0; t < tris.Length; t+= 3)
                {
                    int idx2 = tris[t] + 1 + lastIndex;
                    int idx1 = tris[t + 1] + 1 + lastIndex;
                    int idx0 = tris[t + 2] + 1 + lastIndex;
                    if(faceOrder < 0)
                    {
                        sb.AppendLine("f " + ConstructOBJString(idx2) + " " + ConstructOBJString(idx1) + " " + ConstructOBJString(idx0));
                    }
                    else
                    {
                        sb.AppendLine("f " + ConstructOBJString(idx0) + " " + ConstructOBJString(idx1) + " " + ConstructOBJString(idx2));
                    }
                    
                }
            }

            lastIndex += msh.vertices.Length;
        }

        //write to disk
        System.IO.File.WriteAllText(exportPath, sb.ToString());
        if (generateMaterials)
        {
            System.IO.File.WriteAllText(exportFileInfo.Directory.FullName + "/" + baseFileName + ".mtl", sbMaterials.ToString());
        }

        //export complete, close progress dialog
        EditorUtility.ClearProgressBar();
    }

    static Vector3 RotateAroundPoint(Vector3 point, Vector3 pivot, Quaternion angle)
    {
        return angle * (point - pivot) + pivot;
    }
    static Vector3 MultiplyVec3s(Vector3 v1, Vector3 v2)
    {
        return new Vector3(v1.x * v2.x, v1.y * v2.y, v1.z * v2.z);
    }

    static string TryExportTexture(string propertyName,Material m, string exportFolder = "Assets", bool autoMarkTexReadable = false)
    {
        if (m.HasProperty(propertyName))
        {
            Texture t = m.GetTexture(propertyName);
            if(t != null)
            {
                return ExportTexture((Texture2D)t);
            }
        }
        return "false";
    }
    static string ExportTexture(Texture2D t, string exportFolder = "Assets", bool autoMarkTexReadable = false)
    {
        try
        {
            if (autoMarkTexReadable)
            {
                string assetPath = AssetDatabase.GetAssetPath(t);
                var tImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (tImporter != null)
                {
                    tImporter.textureType = TextureImporterType.Default;

                    if (!tImporter.isReadable)
                    {
                        tImporter.isReadable = true;

                        AssetDatabase.ImportAsset(assetPath);
                        AssetDatabase.Refresh();
                    }
                }
            }
            string exportName = exportFolder + "/" + t.name + ".png";
            Texture2D exTexture = new Texture2D(t.width, t.height, TextureFormat.ARGB32, false);
            exTexture.SetPixels(t.GetPixels());
            System.IO.File.WriteAllBytes(exportName, exTexture.EncodeToPNG());
            return exportName;
        }
        catch (System.Exception ex)
        {
            Debug.Log("Could not export texture : " + t.name + ". is it readable?");
            return "null";
        }

    }

    private static string ConstructOBJString(int index)
    {
        string idxString = index.ToString();
        return idxString + "/" + idxString + "/" + idxString;
    }
    static string  MaterialToString(Material m, bool exportTextures = false, string exportFolder = "Assets", bool autoMarkTexReadable = false)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("newmtl " + m.name);


        //add properties
        if (m.HasProperty("_Color"))
        {
            sb.AppendLine("Kd " + m.color.r.ToString() + " " + m.color.g.ToString() + " " + m.color.b.ToString());
            if (m.color.a < 1.0f)
            {
                //use both implementations of OBJ transparency
                sb.AppendLine("Tr " + (1f - m.color.a).ToString());
                sb.AppendLine("d " + m.color.a.ToString());
            }
        }
        if (m.HasProperty("_SpecColor"))
        {
            Color sc = m.GetColor("_SpecColor");
            sb.AppendLine("Ks " + sc.r.ToString() + " " + sc.g.ToString() + " " + sc.b.ToString());
        }
        if (exportTextures) {
            //diffuse
            string exResult = TryExportTexture("_MainTex", m, exportFolder, autoMarkTexReadable);
            if (exResult != "false")
            {
                sb.AppendLine("map_Kd " + exResult);
            }
            //spec map
            exResult = TryExportTexture("_SpecMap", m, exportFolder, autoMarkTexReadable);
            if (exResult != "false")
            {
                sb.AppendLine("map_Ks " + exResult);
            }
            //bump map
            exResult = TryExportTexture("_BumpMap", m, exportFolder, autoMarkTexReadable);
            if (exResult != "false")
            {
                sb.AppendLine("map_Bump " + exResult);
            }

    }
        sb.AppendLine("illum 2");
        return sb.ToString();
    }
}
#endif