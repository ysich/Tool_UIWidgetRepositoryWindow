/*---------------------------------------------------------------------------------------
-- 负责人: onemt
-- 创建时间: 2024-09-02 14:45:39
-- 概述:
---------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;
using UnityEditor;
using UnityEngine;

namespace Editor.Tools.UIWidgetRepository
{
    public class UIWidgetRepositoryConfiguration:IDisposable
    {
        private readonly Dictionary<string, string> m_AliasMap = new Dictionary<string, string>();
        private readonly List<string> m_GroupTypeList = new List<string>();
        private readonly Dictionary<string, List<string>> m_GroupMap = new Dictionary<string, List<string>>();
        
        #region 单例

        private static UIWidgetRepositoryConfiguration s_Instance;
        public static UIWidgetRepositoryConfiguration instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = new UIWidgetRepositoryConfiguration();
                }

                return s_Instance;
            }
        }

        public void Dispose()
        {
            this.SaveConfig();
            s_Instance = null;
        }

        #endregion
        

        private UIWidgetRepositoryConfiguration()
        {
            InitConfig();
        }

        private void InitConfig()
        {
            JSONClass jsonClass = GetUIWidgetRepositoryJsonConfig();
            //解析alias数据
            JSONArray aliasArray = jsonClass[UIWidgetRepositoryDefine.kAliasConfigKey].AsArray;
            for (int i = 0; i < aliasArray.Count ; i++)
            {
                JSONNode node = aliasArray[i];
                string guid = node[UIWidgetRepositoryDefine.kAliasConfigGuidKey];
                string name = node[UIWidgetRepositoryDefine.kAliasConfigNameKey];
                m_AliasMap.Add(guid,name);
            }
            //解析group数据
            JSONArray groupArray = jsonClass[UIWidgetRepositoryDefine.kGroupConfigKey].AsArray;
            for (int i = 0; i < groupArray.Count; i++)
            {
                List<string> prefabList = new List<string>();
                JSONNode node = groupArray[i];
                string nodeGroupType = node[UIWidgetRepositoryDefine.kGroupConfigGroupNameKey];
                JSONArray prefabsConfig = node[UIWidgetRepositoryDefine.kGroupConfigGroupPrefabKey].AsArray;
                for (int j = 0; j < prefabsConfig.Count; j++)
                {
                    JSONNode prefabGuid = prefabsConfig[j];
                    prefabList.Add(prefabGuid);
                }
                m_GroupMap.Add(nodeGroupType,prefabList);
                m_GroupTypeList.Add(nodeGroupType);
            }
        }

        private void SaveConfig()
        {
            JSONArray aliasArray = new JSONArray();
            foreach (var alias in m_AliasMap)
            {
                JSONClass aliasConfig = new JSONClass();
                aliasConfig[UIWidgetRepositoryDefine.kAliasConfigGuidKey] = alias.Key;
                aliasConfig[UIWidgetRepositoryDefine.kAliasConfigNameKey] = alias.Value;
                aliasArray.Add(aliasConfig);
            }
            JSONArray groupArray = new JSONArray();
            foreach (var group in m_GroupMap)
            {
                JSONClass groupConfig = new JSONClass();
                groupConfig[UIWidgetRepositoryDefine.kGroupConfigGroupNameKey] = group.Key;
                JSONArray prefabArray = new JSONArray();
                foreach (var prefabGuid in group.Value)
                {
                    prefabArray.Add(prefabGuid);
                }

                groupConfig[UIWidgetRepositoryDefine.kGroupConfigGroupPrefabKey] = prefabArray;
                groupArray.Add(groupConfig);
            }
            JSONClass jsonClass = new JSONClass()
            {
                {UIWidgetRepositoryDefine.kAliasConfigKey,aliasArray},
                {UIWidgetRepositoryDefine.kGroupConfigKey,groupArray}
            };
            SaveUIWidgetRepositoryJsonConfig(jsonClass);
        }

        private void SaveUIWidgetRepositoryJsonConfig(JSONClass config)
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

        private JSONClass GetUIWidgetRepositoryJsonConfig()
        {
            JSONClass config = null;
            if (!File.Exists(UIWidgetRepositoryDefine.kConfigPath))
            {
                string dirPath = Path.GetDirectoryName(UIWidgetRepositoryDefine.kConfigPath);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                config = new JSONClass()
                {
                    { UIWidgetRepositoryDefine.kAliasConfigKey, new JSONArray() },
                    {
                        UIWidgetRepositoryDefine.kGroupConfigKey, new JSONArray()
                        {
                            new JSONClass()
                            {
                                { "name", UIWidgetRepositoryDefine.kAllGroupType },
                                { "prefabs", new JSONArray() }
                            }
                        }
                    }
                };
            }
            else
            {
                config = JSONNode.LoadFromFile(UIWidgetRepositoryDefine.kConfigPath) as JSONClass;
            }

            return config;
        }

        public List<string> GetGroupTypeConfig()
        {
            List<string> groupTypes = new List<string>();
            foreach (var VARIABLE in m_GroupMap)
            {
                groupTypes.Add(VARIABLE.Key);
            }
            //+号不序列化进数据里
            groupTypes.Add(UIWidgetRepositoryDefine.kAddType);
            return groupTypes;
        }

        public List<string> GetPrefabsConfigByGroup(string groupType)
        {
            List<string> groupConfig = m_GroupMap[groupType];
            for (int i = groupConfig.Count-1; i >= 0 ; i--)
            {
                string prefabGuid = groupConfig[i];
                string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
                //如果预制不存在了则进行删除
                if (!File.Exists(path))
                {
                    groupConfig.RemoveAt(i);
                    Debug.Log($"UI组件库存在已经被删除的预制，已删除。guid:{prefabGuid}");
                }
            }

            return groupConfig;
        }
        
        public string GetUIWidgetPrefabName(string prefabGuid)
        {
            if (m_AliasMap.TryGetValue(prefabGuid, out string alias))
            {
                return alias;
            }

            string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
            return Path.GetFileNameWithoutExtension(path);
        }
        
        public bool HasUIWidgetGroup(string groupType)
        {
            return m_GroupMap.ContainsKey(groupType);
        }
        

        #region 数据变更

        public void AddNewUIWidgetGroup(string newGroupType)
        {
            if (m_GroupMap.ContainsKey(newGroupType))
            {
                throw new Exception("当前组已存在");
            }

            m_GroupMap[newGroupType] = new List<string>();
        }

        public void RemoveUIWidgetGroup(string groupType)
        {
            m_GroupMap.Remove(groupType);
        }

        public void AddNewUIWidgetPrefabByGroup(string groupType, string prefabGuid,int insertIndex)
        {
            List<string> groupConfig = m_GroupMap[groupType];
            insertIndex = insertIndex > groupConfig.Count ? groupConfig.Count : insertIndex;
            groupConfig.Insert(insertIndex,prefabGuid);
        }

        public void RemoveUIWidgetPrefabByGroup(string groupType, string prefabGuid)
        {
            List<string> groupConfig = m_GroupMap[groupType];
            groupConfig.Remove(prefabGuid);
        }

        /// <summary>
        /// 给UI组件取别名
        /// </summary>
        /// <returns></returns>
        public void SetUIWidgetPrefabAliasByGroup(string prefabGuid, string prefabAlias)
        {
            if (prefabAlias == string.Empty)
            {
                if (m_AliasMap.ContainsKey(prefabGuid))
                {
                    m_AliasMap.Remove(prefabGuid);
                    SaveConfig();
                }
                return;
            }
            m_AliasMap[prefabGuid] = prefabAlias;
        }

        #endregion

    }
}