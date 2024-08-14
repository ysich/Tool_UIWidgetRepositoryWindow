/*---------------------------------------------------------------------------------------
-- 负责人: onemt
-- 创建时间: 2024-08-08 16:33:08
-- 概述:
---------------------------------------------------------------------------------------*/

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

using SimpleJSON;
using UnityEditor.SceneManagement;
using UnityEngine.UI;

namespace OnemtEditor.Tools.UIWidgetRepository
{
    public static class UIWidgetRepositoryHelper
    {
        public static Texture GetAssetPreview(string guid,int previewSize = 128)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object assetObj = AssetDatabase.LoadAssetAtPath(path, typeof(Object));
            GameObject obj = assetObj as GameObject;
            if (obj == null)
            {
                Debug.LogError($"Prefab Is NULL,GUID:{guid}");
                return null;
            }
            GameObject canvas = new GameObject("RenderCanvas", typeof(Canvas));
            GameObject cameraObj = new GameObject("RenderCamera", typeof(Camera));
            canvas.transform.position = new Vector3(10000, 10000, 10000);
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(Screen.currentResolution.width, Screen.currentResolution.height);

            GameObject go = GameObject.Instantiate(obj, canvas.transform);

            Bounds bound = GetBounds(go);

            cameraObj.transform.position = new Vector3((bound.max.x + bound.min.x) / 2, (bound.max.y + bound.min.y) / 2, (bound.max.z + bound.min.z) / 2 - 100);
            cameraObj.transform.LookAt(cameraObj.transform.position);

            Camera camera = cameraObj.GetComponent<Camera>();
            camera.cameraType = CameraType.SceneView;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0f);

            float width = bound.max.x - bound.min.x;
            float height = bound.max.y - bound.min.y;
            float max_camera_size = (width > height ? width : height) + 10;
            camera.orthographicSize = max_camera_size / 2;

            RenderTexture rt = RenderTexture.GetTemporary(previewSize, previewSize, 24);
            camera.targetTexture = rt;
            camera.RenderDontRestore();

            RenderTexture tex = new RenderTexture(previewSize, previewSize, 0, RenderTextureFormat.Default);
            Graphics.Blit(rt, tex);

            RenderTexture.active = null;
            camera.targetTexture = null;
            rt.Release();
            RenderTexture.ReleaseTemporary(rt);

            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(cameraObj);

            return tex;
        }

        public static Bounds GetBounds(GameObject obj)
        {
            Vector3 Min = new Vector3(99999, 99999, 99999);
            Vector3 Max = new Vector3(-99999, -99999, -99999);
            MeshRenderer[] renders = obj.GetComponentsInChildren<MeshRenderer>();
            if (renders.Length > 0)
            {
                for (int i = 0; i < renders.Length; i++)
                {
                    if (renders[i].bounds.min.x < Min.x)
                        Min.x = renders[i].bounds.min.x;
                    if (renders[i].bounds.min.y < Min.y)
                        Min.y = renders[i].bounds.min.y;
                    if (renders[i].bounds.min.z < Min.z)
                        Min.z = renders[i].bounds.min.z;

                    if (renders[i].bounds.max.x > Max.x)
                        Max.x = renders[i].bounds.max.x;
                    if (renders[i].bounds.max.y > Max.y)
                        Max.y = renders[i].bounds.max.y;
                    if (renders[i].bounds.max.z > Max.z)
                        Max.z = renders[i].bounds.max.z;
                }
            }
            else
            {
                RectTransform[] rectTrans = obj.GetComponentsInChildren<RectTransform>();
                Vector3[] corner = new Vector3[4];
                for (int i = 0; i < rectTrans.Length; i++)
                {
                    //获取节点的四个角的世界坐标，分别按顺序为左下左上，右上右下
                    rectTrans[i].GetWorldCorners(corner);
                    if (corner[0].x < Min.x)
                        Min.x = corner[0].x;
                    if (corner[0].y < Min.y)
                        Min.y = corner[0].y;
                    if (corner[0].z < Min.z)
                        Min.z = corner[0].z;

                    if (corner[2].x > Max.x)
                        Max.x = corner[2].x;
                    if (corner[2].y > Max.y)
                        Max.y = corner[2].y;
                    if (corner[2].z > Max.z)
                        Max.z = corner[2].z;
                }
            }

            Vector3 center = (Min + Max) / 2;
            Vector3 size = new Vector3(Max.x - Min.x, Max.y - Min.y, Max.z - Min.z);
            return new Bounds(center, size);
        }
        
        /// <summary>
        /// 只选中一个非 Root的节点时,拖动出来的节点应该和该节点同层级
        /// 未选中或者选中 根Canvas 节点，拖动出来的节点都在 根Canvas 子节点层级
        /// 选中多个时 拖动出来的节点在 根Canvas 子节点层级
        /// </summary>
        public static Transform GetObjectParent(GameObject[] selection)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                //Prefab编辑模式下,需要额外区分是否是Canvas (Environment)
                if (selection.Length == 1
                    && !selection[0].name.Equals("Canvas (Environment)")
                    && selection[0].transform != prefabStage.prefabContentsRoot.transform)
                {
                    return selection[0].transform.parent.transform;
                }
                else
                {
                    return prefabStage.prefabContentsRoot.transform;
                }
            }
            else
            {
                if (selection.Length == 1)
                {
                    if (selection[0].transform == selection[0].transform.root)
                    {
                        return selection[0].transform.root;
                    }
                    else
                    {
                        return selection[0].transform.parent.transform;
                    }
                }
                else
                {
                    if (Object.FindObjectsOfType<Canvas>().Length == 0)
                    {
                        new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    }
                    return Object.FindObjectsOfType<Canvas>()[0].transform;
                }
            }
        }

        #region config

         private static void SaveUIWidgetRepositoryJsonConfig(JSONArray config)
        {
            using (var fs = new FileStream(UIWidgetRepositoryDefine.kConfigPath, FileMode.OpenOrCreate))
            {
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Flush();
                    config.Serialize(bw);
                    bw.Close();
                    fs.Close();
                }
            }
        }

        private static JSONArray GetUIWidgetRepositoryJsonConfig()
        {
            JSONArray config = null;
            if (!File.Exists(UIWidgetRepositoryDefine.kConfigPath))
            {
                string dirPath = Path.GetDirectoryName(UIWidgetRepositoryDefine.kConfigPath);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }
                //初始化
                config = new JSONArray()
                {
                    new JSONClass()
                    {
                        {"name",UIWidgetRepositoryDefine.kAllGroupType},
                        {"prefabs",new JSONArray()}
                    }
                };
                SaveUIWidgetRepositoryJsonConfig(config);
            }
            else
            {
                config = JSONNode.LoadFromFile(UIWidgetRepositoryDefine.kConfigPath) as JSONArray;
            }

            return config;
        } 

        public static List<string> GetGroupTypeConfig()
        {
            List<string> groupTypes = new List<string>();
            JSONArray allNode = GetUIWidgetRepositoryJsonConfig();
            for (int i = 0; i < allNode.Count; i++)
            {
                JSONNode node = allNode[i];
                string groupType = node["name"];
                groupTypes.Add(groupType);
            }
            //+号不序列化进数据里
            groupTypes.Add(UIWidgetRepositoryDefine.kAddType);
            return groupTypes;
        }

        public static List<string> GetPrefabsConfigByGroup(string groupType)
        {
            List<string> prefabs = new List<string>();
            JSONArray allNode = GetUIWidgetRepositoryJsonConfig();
            bool isDirty = false;
            for (int i = 0; i < allNode.Count; i++)
            {
                JSONNode node = allNode[i];
                string nodeGroupType = node["name"];
                if (groupType == nodeGroupType)
                {
                    JSONArray prefabsConfig = node["prefabs"].AsArray;
                    for (int j = 0; j < prefabsConfig.Count; j++)
                    {
                        JSONNode prefabGuid = prefabsConfig[j];
                        string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
                        //如果预制不存在了则进行删除
                        if (!File.Exists(path))
                        {
                            prefabsConfig.Remove(j);
                            isDirty = true;
                            Debug.Log($"UI组件库存在已经被删除的预制，已删除。guid:{prefabGuid}");
                            continue;   
                        }
                        prefabs.Add(prefabGuid);
                    }
                }
            }
            if(isDirty)
                SaveUIWidgetRepositoryJsonConfig(allNode);

            return prefabs;
        }

        public static void AddNewUIWidgetGroup(string newGroupType)
        {
            JSONArray jsonArray = GetUIWidgetRepositoryJsonConfig();
            jsonArray.Add(new JSONClass()
            {
                {"name",newGroupType},
                {"prefabs",new JSONArray()}
            });
            SaveUIWidgetRepositoryJsonConfig(jsonArray);
        }

        public static bool RemoveUIWidgetGroup(string groupType)
        {
            JSONArray jsonArray = GetUIWidgetRepositoryJsonConfig();
            foreach (JSONNode node in jsonArray.Childs)
            {
                string type = node["name"];
                if (type == groupType)
                {
                    jsonArray.Remove(node);
                    SaveUIWidgetRepositoryJsonConfig(jsonArray);
                    return true;
                }
            }

            return false;
        }

        public static void AddNewUIWidgetPrefabByGroup(string groupType, string prefabGuid)
        {
            JSONArray jsonArray = GetUIWidgetRepositoryJsonConfig();
            foreach (JSONNode node in jsonArray.Childs)
            {
                string type = node["name"];
                if (type == groupType)
                {
                    JSONArray prefabJsonArray = node["prefabs"].AsArray;
                    prefabJsonArray.Add(prefabGuid);
                    SaveUIWidgetRepositoryJsonConfig(jsonArray);
                    return;
                }
            }
        }

        public static void RemoveUIWidgetPrefabByGroup(string groupType, string prefabGuid)
        {
            JSONArray jsonArray = GetUIWidgetRepositoryJsonConfig();
            foreach (JSONNode node in jsonArray.Childs)
            {
                string type = node["name"];
                if (type == groupType)
                {
                    JSONArray prefabJsonArray = node["prefabs"].AsArray;
                    foreach (JSONNode guid in prefabJsonArray)
                    {
                        if (guid.Value == prefabGuid)
                        {
                            prefabJsonArray.Remove(guid);
                            SaveUIWidgetRepositoryJsonConfig(jsonArray);
                            return;
                        }
                    }
                }
            }
        }

        #endregion
        
       
    }
}