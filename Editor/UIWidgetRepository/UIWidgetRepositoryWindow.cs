/*---------------------------------------------------------------------------------------
-- 负责人: onemt
-- 创建时间: 2024-08-08 09:46:47
-- 概述:
---------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace Editor.Tools.UIWidgetRepository
{
    public class UIWidgetRepositoryWindow : EditorWindow
    {
        private struct UIWidgetItem
        {
            public string prefabGUID;
            public string prefabName;
            public string prefabPath;
            public GameObject prefab;
            public int repositoryIndex;

            public string aliasName => UIWidgetRepositoryConfiguration.instance.GetUIWidgetPrefabName(prefabGUID);

            public UIWidgetItem(string guid,int index)
            {
                this.prefabGUID = guid;
                this.repositoryIndex = index;
                this.prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                this.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                this.prefabName = prefab.name;
            }
        }
        
        public const int kWindowWidth = 1300;
        public const int kWindowHeight = 636;
        
        [MenuItem("Tools/Window/UI组件库",false,1)]
        public static void ShowWindow()
        {
            UIWidgetRepositoryWindow window = (UIWidgetRepositoryWindow)GetWindow(typeof(UIWidgetRepositoryWindow));
            window.titleContent.text = "UI组件库";
            // int width = 1272 + 13 + 12;

            window.minSize = new Vector2(kWindowWidth, kWindowHeight);
            window.maxSize = window.minSize;
            window.Show();
        }

        private GUIContent m_DeleteGroupGUIContent = new GUIContent("删除组");
        private GUIContent m_PingPrefabGUIContent = new GUIContent("Ping组件");
        private GUIContent m_SetPrefabAliasContent = new GUIContent("设置别名");
        private GUIContent m_DeletePrefabAliasContent = new GUIContent("删除别名");
        private GUIContent m_DeletePrefabGUIContent = new GUIContent("删除组件");

        private readonly float k_PrefabsScaleMax = 1f;
        private readonly float k_PrefabsScaleMin = 0.5f;
        private float m_PrefabScale = 1f;
        private Vector2 m_PrefabSize = new Vector2(120, 120);
        private int m_PrefabPadding = 5;

        private const int k_LeftTypeScrollWidht = 200;
        private readonly Rect k_LeftTypeScrollRect = new Rect(0, 5, k_LeftTypeScrollWidht, kWindowHeight);
        private Vector2 m_LeftTypeScrollPos = new Vector2(0, 0);
        private readonly Vector2 k_LeftTypeBtnItemSize = new Vector2(204, 50);

        private readonly Rect k_RightPrefabScrollRect = new Rect(k_LeftTypeScrollWidht + 20, 30,
            kWindowWidth - k_LeftTypeScrollWidht - 20, kWindowHeight - 30);

        private Vector2 m_RightPrefabScrollPos = new Vector2(0, 0);
        private readonly int m_RightPrefabLabelHeight = 34;

        private bool m_RightContainerDrag;

        private string m_SelectType;
        private List<string> m_GroupTypeList;

        private int m_ChoosePrefabIndex;
        private int m_MousePrefabIndex;
        private GameObject m_LoadPrefab;
        private Texture m_LoadPrefabPreview;
        
        private List<string> m_PrefabGUIDs;
        private List<UIWidgetItem> m_UIWidgetItems = new List<UIWidgetItem>();
        private Dictionary<string, Texture> m_PrefabPreviewsByGUID = new Dictionary<string, Texture>();

        private bool m_IsFocus;
        
        private readonly GUIContent m_SubWindow_AddGroupContent = new GUIContent("输入添加的组名:");
        private readonly GUIContent m_SubWindow_AddGroupButtonContent = new GUIContent("添加");
        
        private readonly GUIContent m_SubWindow_SetAliasContent = new GUIContent("输入设置的别名:");
        private readonly GUIContent m_SubWindow_SetAliasButtonContent = new GUIContent("设置");
        
        //搜索功能
        private string m_SearchLabel;
        private List<UIWidgetItem> m_SearchUIWidgetItems = new List<UIWidgetItem>();
        private void OnEnable()
        {
            m_SelectType = UIWidgetRepositoryDefine.kAllGroupType;
            InitGroupData();
            InitPrefabData();

            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            UIWidgetRepositoryConfiguration.instance.Dispose();
        }

        #region CustomSceneView

        private Queue<Action<SceneView>> m_SceneViewDelegateList = new Queue<Action<SceneView>>();
        private HashSet<Action<SceneView>> m_SceneViewRemoveDelegateList = new HashSet<Action<SceneView>>();
        private void OnSceneGUI(SceneView sceneView)
        {
            int count = m_SceneViewDelegateList.Count;
            while (count-- > 0)
            {
                Action<SceneView> action = m_SceneViewDelegateList.Dequeue();
                action.Invoke(sceneView);
                if (m_SceneViewRemoveDelegateList.Contains(action))
                {
                    continue;
                }
                m_SceneViewDelegateList.Enqueue(action);
            }
        }

        private void AddSceneViewDelegate(Action<SceneView> action)
        {
            m_SceneViewDelegateList.Enqueue(action);
        }

        private void RemoveSceneViewDelegate(Action<SceneView> action)
        {
            if (!m_SceneViewRemoveDelegateList.Contains(action))
            {
                m_SceneViewRemoveDelegateList.Add(action);
            }
        }

        private void ClearSceneViewDelegate()
        {
            m_SceneViewDelegateList.Clear();
            m_SceneViewRemoveDelegateList.Clear();
        }

        #endregion
        
        private void DragPerformAsPrefab(Transform container, Vector3 localPos)
        {
            Undo.IncrementCurrentGroup();
            int groupIndex = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("DragPerformAsPrefab");
            GameObject currentPrefab = PrefabUtility.InstantiatePrefab(m_LoadPrefab) as GameObject;
            Undo.RegisterCreatedObjectUndo(currentPrefab,"Create DragPreform");
            Undo.SetTransformParent(currentPrefab.transform, container,"move item to container");
            // currentPrefab.transform.SetParent(container);
            currentPrefab.transform.localPosition = localPos;
            currentPrefab.transform.localScale = m_LoadPrefab.transform.localScale;
            Selection.activeObject = currentPrefab;
            Undo.CollapseUndoOperations(groupIndex);
        }
        
        private void InitDragState()
        {
            //Event数据
            // m_RightContainerDrag = false;
            m_LoadPrefab = null;
            // m_LoadPrefabPreview = null;
            //表现
            m_ChoosePrefabIndex = -1;
        }

        private void OnInspectorUpdate()
        {
            if(this.m_IsFocus)
            {
                //这里需要每次调用重绘，OnGui的刷新频率不固定。。
                this.Repaint();
            }
        }

        private void OnFocus()
        {
            m_IsFocus = true;
        }

        private void OnLostFocus()
        {
            m_IsFocus = false;
        }

        private void OnGUI()
        {
            m_MousePrefabIndex = GetPrefabItemUnderMouse();
            RefreshWindow();
            UpdateWindowEvent();
        }

        #region Event

        private void UpdateWindowEvent()
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            
            Event currentEvent = Event.current;
            EventType eventType = currentEvent.type;
            
            switch (eventType)
            {
                case EventType.MouseDown:
                    if (currentEvent.isMouse && (m_MousePrefabIndex >= 0 && m_MousePrefabIndex < m_SearchUIWidgetItems.Count))
                    {
                        string guid = m_SearchUIWidgetItems[m_MousePrefabIndex].prefabGUID;
                        //鼠标左键双击进入预制界面
                        if (currentEvent.button == 0 && currentEvent.clickCount == 2)
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                            PrefabStageUtility.OpenPrefab(assetPath);
                        }
                        //鼠标右键打开删除菜单
                        if (currentEvent.button == 1)
                        {
                            Event.current.Use();
                            GenericMenu genericMenu = new GenericMenu();
                            genericMenu.AddItem(m_PingPrefabGUIContent, false, () =>
                            {
                                GameObject gameObject = m_SearchUIWidgetItems[m_MousePrefabIndex].prefab;
                                EditorGUIUtility.PingObject(gameObject);
                            } );
                            genericMenu.AddItem(m_SetPrefabAliasContent,false, () =>
                            {
                                UIWidgetRepositoryInputNameWindow.ShowWindow(m_SubWindow_SetAliasContent,
                                    m_SubWindow_SetAliasButtonContent,
                                    (string alias) =>
                                    {
                                        UIWidgetRepositoryConfiguration.instance.SetUIWidgetPrefabAliasByGroup(guid, alias);
                                        EditorUtility.DisplayDialog("提示", $"设置别名成功，别名：{alias}", "ok");

                                        return true;
                                    },
                                    OnSubWindowClose);
                            });
                            genericMenu.AddItem(m_DeletePrefabAliasContent,false, () =>
                            {
                                UIWidgetRepositoryConfiguration.instance.SetUIWidgetPrefabAliasByGroup(guid,string.Empty);;
                                EditorUtility.DisplayDialog("提示", $"删除别名成功", "ok");
                            });
                            genericMenu.AddItem(m_DeletePrefabGUIContent, false, () =>
                            {
                                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                                string prefabName = Path.GetFileNameWithoutExtension(assetPath);
                                if (EditorUtility.DisplayDialog($"确定删除{prefabName}", "确定后在组件库内进行删除，目录内需要手动删除！", "确定",
                                        "取消"))
                                {
                                    RemovePrefabData(guid);
                                }
                            });
                            genericMenu.ShowAsContext();
                        }

                        //鼠标左键单击开启拖拽
                        if (currentEvent.button == 0 && currentEvent.clickCount == 1)
                        {
                            InitDragState();
                            m_ChoosePrefabIndex = m_MousePrefabIndex;
                        
                            // m_RightContainerDrag = true; //拖拽起始点在Item上
                            DragAndDrop.PrepareStartDrag();
                            DragAndDrop.StartDrag("prefab");
                            m_LoadPrefab = m_SearchUIWidgetItems[m_MousePrefabIndex].prefab;
                            m_LoadPrefabPreview = GetAssetPreview(guid);
                            Object[] obj = { m_LoadPrefab };
                            DragAndDrop.objectReferences = obj;
                            //开始拖拽事件检测
                            ClearSceneViewDelegate();
                            AddSceneViewDelegate(PrefabItemDragHandler);
                        }
                    }
                    break;
                //拖放操作已执行
                case EventType.DragPerform:
                    GameObject[] draggedObjects = this.draggedObjects;
                    //拖到场景里的事件在PrefabItemDragHandler处理了。
             
                    //从屏幕外拖进来
                    if (draggedObjects != null)
                    {
                        foreach (var gameObject in draggedObjects)
                        {
                            string assetPath = AssetDatabase.GetAssetPath(gameObject);
                            string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                            int dragPrefabIndex = m_PrefabGUIDs.IndexOf(assetGuid);
                            //不在组里面的加到最后面
                            if (dragPrefabIndex == -1)
                            {
                                AddPrefabData(assetGuid,m_PrefabGUIDs.Count);
                            }
                            //搜索了就不让移动位置了
                            else if(string.IsNullOrWhiteSpace(m_SearchLabel))
                            {
                                //位置相同则不发生改变
                                if(m_MousePrefabIndex == dragPrefabIndex)
                                    continue;
                                //移动位置
                                UIWidgetRepositoryConfiguration.instance.RemoveUIWidgetPrefabByGroup(m_SelectType,
                                    assetGuid);
                                AddPrefabData(assetGuid,m_MousePrefabIndex);
                            }
                        }
                    }
                    currentEvent.Use();
                    break;
            }
        }

        private void PrefabItemDragHandler(SceneView sceneView)
        {
            if (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                RemoveSceneViewDelegate(DrawTexture);
                AddSceneViewDelegate(DrawTexture);

                if (Event.current.type == EventType.DragPerform)
                {
                    Vector2 mousePos = Event.current.mousePosition;
                    Transform container = UIWidgetRepositoryHelper.GetObjectParent(Selection.gameObjects);

                    if (container != null)
                    {
                        mousePos.y = sceneView.camera.pixelHeight - mousePos.y;
                        Vector3 WorldPos = sceneView.camera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0));
                        Vector3 localPos = container.InverseTransformPoint(new Vector3(WorldPos.x, WorldPos.y, 0));
                        DragPerformAsPrefab(container, localPos);
                        EditorUtility.SetDirty(container);
                    }
                    RemoveSceneViewDelegate(DrawTexture);
                    RemoveSceneViewDelegate(PrefabItemDragHandler);
                    InitDragState();
                }
                Event.current.Use();
            }
        }
        
        private bool OutBounds(SceneView sceneView, float offset = 0f)
        {
            if (Event.current.mousePosition.y < sceneView.camera.pixelHeight + offset && Event.current.mousePosition.y > 0 && Event.current.mousePosition.x < sceneView.camera.pixelWidth && Event.current.mousePosition.x > 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        
        private void DrawTexture(SceneView sceneView)
        {
            if (OutBounds(sceneView))
            {
                RemoveSceneViewDelegate(DrawTexture);
            }

            Rect rect = new Rect(Event.current.mousePosition.x - m_LoadPrefabPreview.width / 2, 
                    Event.current.mousePosition.y - m_LoadPrefabPreview.height / 2, 
                    m_LoadPrefabPreview.width, m_LoadPrefabPreview.height);
            Handles.BeginGUI();
            GUI.DrawTexture(rect, m_LoadPrefabPreview);
            Handles.EndGUI();
            sceneView.Repaint();
            // Debug.Log($"绘制预览图！！！,x:{rect.x},y:{rect.y}");
        }
        
        private GameObject[] draggedObjects
        {
            get
            {
                if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
                    return null;
        
                return DragAndDrop.objectReferences.Where(x => x as GameObject).Cast<GameObject>().ToArray();
            }
            set
            {
                if (value != null)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = value;
                    // draggedObjectIsOurs = true;
                }
                else DragAndDrop.AcceptDrag();
            }
        }


        private int GetPrefabItemUnderMouse()
        {
            if (Event.current == null)
            {
                return -1;
            }
            Vector2 previewSize = m_PrefabSize * m_PrefabScale;
            float spacingX = previewSize.x + m_PrefabPadding;
            float spacingY = spacingX + m_RightPrefabLabelHeight;

            Vector2 pos = Event.current.mousePosition + m_RightPrefabScrollPos;

            float x = k_RightPrefabScrollRect.x, y = k_RightPrefabScrollRect.y;
            if (pos.y < y) return -1;

            float width = Screen.width - m_PrefabPadding + m_RightPrefabScrollPos.x;
            float height = Screen.height - m_PrefabPadding + m_RightPrefabScrollPos.y;
            int index = 0;

            while (true)
            {
                Rect rect = new Rect(x, y, spacingX, spacingY);
                if (rect.Contains(pos)) break;

                x += spacingX;

                if (x + spacingX > width)
                {
                    if (pos.x > x) return -1;
                    y += spacingY;
                    x = k_RightPrefabScrollRect.x;
                    if (y + spacingY > height) return -1;
                }

                index++;
            }

            // Debug.Log($"Click index:{index}");
            return index;
        }

        #endregion

        #region UI

        private void RefreshWindow()
        {
            RefreshTopContainer();
            RefreshLeftTypeList();
            RefreshRightPrefabContainer();
        }

        private void RefreshTopContainer()
        {
            float topPadding = 5f;
            float topHeight = 20f;
            //尺寸滑动条
            float sliderWidth = 150f;
            float xPos = this.minSize.x/2 - sliderWidth/2;
            Rect rect = new Rect(xPos, topPadding, sliderWidth, topHeight);
            m_PrefabScale = EditorGUI.Slider(rect, m_PrefabScale, k_PrefabsScaleMin, k_PrefabsScaleMax);
            //数量显示
            float countLabelWidth = 60f;
            Rect countLabelRect = new Rect(this.minSize.x - countLabelWidth, topPadding, countLabelWidth, topHeight);
            EditorGUI.LabelField(countLabelRect,$"数量:{m_PrefabGUIDs.Count}");
            //搜索框
            float findWidth = 200f;
            Rect findRect = new Rect(countLabelRect.x - 30f - findWidth, topPadding, findWidth, topHeight);
            string searchLabel = EditorGUI.TextField(findRect,m_SearchLabel);
            if (searchLabel != m_SearchLabel)
            {
                m_SearchLabel = searchLabel;
                SearchPrefabNameToPrefabs();
            }
        }

        private void SearchPrefabNameToPrefabs()
        {
            m_SearchUIWidgetItems.Clear();
            //空的就为全部
            if (string.IsNullOrWhiteSpace(m_SearchLabel))
            {
                m_SearchUIWidgetItems.AddRange(m_UIWidgetItems);
                return;
            }
            //不是空的就塞包含查找字符的
            foreach (var uiWidgetItem in m_UIWidgetItems)
            {
                if (uiWidgetItem.aliasName.Contains(m_SearchLabel))
                {
                    m_SearchUIWidgetItems.Add(uiWidgetItem);
                }   
            }
        }

        /// <summary>
        /// 更新左侧类型列表
        /// </summary>(
        private void RefreshLeftTypeList()
        {
            float spacing = 2.5f;
            float typeViewHeight = (k_LeftTypeBtnItemSize.y + spacing) * m_GroupTypeList.Count;
            Rect typeViewRect = new Rect(0, 0, 0, typeViewHeight);
            m_LeftTypeScrollPos = GUI.BeginScrollView(k_LeftTypeScrollRect, m_LeftTypeScrollPos, typeViewRect);
            for (int i = 0; i < m_GroupTypeList.Count; i++)
            {
                string typeStr = m_GroupTypeList[i];
                //设置选中和未选中的颜色
                if (typeStr == m_SelectType)
                    GUI.color = Color.green;
                else
                    GUI.color = Color.white;

                if (GUILayout.Button(typeStr, GUILayout.Width(k_LeftTypeBtnItemSize.x),
                        GUILayout.Height(k_LeftTypeBtnItemSize.y)))
                {
                    Event currentEvent = Event.current;

                    if (currentEvent.button == 0)
                    {
                        if (typeStr == UIWidgetRepositoryDefine.kAddType)
                        {
                            //添加组名的逻辑
                            UIWidgetRepositoryInputNameWindow.ShowWindow(m_SubWindow_AddGroupContent,m_SubWindow_AddGroupButtonContent,
                                (string groupType) =>
                                {
                                    //判断是否存在。
                                    if (UIWidgetRepositoryConfiguration.instance.HasUIWidgetGroup(groupType))
                                    {
                                        EditorUtility.DisplayDialog("提示", "添加的组名已经存在！重新命名！","ok");
                                        return false;
                                    }

                                    UIWidgetRepositoryConfiguration.instance.AddNewUIWidgetGroup(groupType);
                                    InitGroupData();
                                    this.Repaint();
                                    if (EditorUtility.DisplayDialog("提示", $"添加成功，组名：{groupType}", "ok"))
                                    {
                                        return true;
                                    }

                                    return true;
                                }, 
                                OnSubWindowClose);
                        }
                        else
                        {
                            m_SelectType = typeStr;
                            //切换展示的逻辑
                            m_ChoosePrefabIndex = -1;
                            InitPrefabData();
                            RefreshRightPrefabContainer();
                        }
                    }

                    if (currentEvent.button == 1 && typeStr != UIWidgetRepositoryDefine.kAddType &&
                        typeStr != UIWidgetRepositoryDefine.kAllGroupType)
                    {
                        GenericMenu genericMenu = new GenericMenu();
                        genericMenu.AddItem(m_DeleteGroupGUIContent, false, () =>
                        {
                            if (EditorUtility.DisplayDialog("确定删除", "确定删除组后整组的数据都会消失并且无法回复！", "确定", "取消"))
                            {
                                DeleteGroup(typeStr);
                            }
                        });
                        genericMenu.ShowAsContext();
                    }
                }
            }

            GUI.EndScrollView();
            GUI.color = GUI.backgroundColor;
        }

        private Color m_ItemMouseColor = new Color(0.31f, 0.56f, 1f, 0.5f);
        private Color m_ItemChooseColor = new Color(1f, 0f, 0f, 0.15f);

        /// <summary>
        /// 根据当前所选类型更新右侧Prefab列表
        /// </summary>
        /// <param name="type"></param>
        private void RefreshRightPrefabContainer()
        {
            // if (m_PrefabGUIDs == null)
            // {
            //     return;
            // }
            Vector2 previewSize = m_PrefabSize * m_PrefabScale;
            int oneLineCount = Mathf.FloorToInt(k_RightPrefabScrollRect.width / (previewSize.x + m_PrefabPadding));
            int lineCount = Mathf.CeilToInt((float)m_SearchUIWidgetItems.Count / oneLineCount);
            float viewHeight = lineCount * (previewSize.y + m_PrefabPadding + m_RightPrefabLabelHeight);
            Rect viewRect = new Rect(0, 0, k_RightPrefabScrollRect.width, viewHeight);
            m_RightPrefabScrollPos = GUI.BeginScrollView(k_RightPrefabScrollRect, m_RightPrefabScrollPos, viewRect);
            Rect previewRect = new Rect(2, 2, previewSize.x, previewSize.y);
            for (int i = 0; i < m_SearchUIWidgetItems.Count; i++)
            {
                UIWidgetItem uiWidgetItem = m_SearchUIWidgetItems[i];
                string guid = uiWidgetItem.prefabGUID;
                Texture prefabPreview = GetAssetPreview(guid);
                GUI.backgroundColor = Color.white;
                EditorGUI.DrawPreviewTexture(previewRect, prefabPreview);
                Object assetObj = AssetDatabase.LoadAssetAtPath(uiWidgetItem.prefabPath, typeof(Object));
                if (assetObj == null)
                {
                    GUI.EndScrollView();
                    //这里如果为空说明在界面打开的时候被删除了这时候重新触发下加载。
                    InitPrefabData();
                    this.Repaint();
                    return;
                }
                EditorStyles.label.fontSize = 14;
                EditorStyles.label.alignment = TextAnchor.UpperLeft;
                Rect indexRect = new Rect(previewRect.x, previewRect.y, previewRect.width, 16);
                EditorGUI.LabelField(indexRect, (uiWidgetItem.repositoryIndex+1).ToString());

                Rect labelRect = new Rect(previewRect.x, previewRect.y + previewRect.height, previewRect.width,
                    m_RightPrefabLabelHeight);
                EditorStyles.label.wordWrap = true;
                EditorStyles.label.alignment = TextAnchor.UpperCenter;
                EditorStyles.label.fontSize = 13;
                EditorGUI.LabelField(labelRect, uiWidgetItem.aliasName);

                //当前鼠标选中的状态
                if (m_MousePrefabIndex == i)
                {
                    EditorGUI.DrawRect(previewRect, m_ItemMouseColor);
                }
                //选中外框
                if (m_ChoosePrefabIndex == i)
                {
                    Rect chooseRect = new Rect(previewRect);
                    chooseRect.x -= 2;
                    chooseRect.y -= 2;
                    chooseRect.width += 4;
                    chooseRect.height += 4;
                    EditorGUI.DrawRect(chooseRect, m_ItemChooseColor);
                }
                
                previewRect.x += previewSize.x;
                previewRect.x += m_PrefabPadding;

                if ((previewRect.x + previewSize.x + m_PrefabPadding) >= k_RightPrefabScrollRect.width)
                {
                    previewRect.x = 2;
                    previewRect.y += (previewSize.y + m_RightPrefabLabelHeight + m_PrefabPadding);
                }
            }

            GUI.EndScrollView();
        }

        #endregion

        #region Data

        private void InitGroupData()
        {
            m_GroupTypeList = UIWidgetRepositoryConfiguration.instance.GetGroupTypeConfig();
        }

        private void OnSubWindowClose()
        {
            this.OnFocus();
        }

        private void DeleteGroup(string type)
        {
            UIWidgetRepositoryConfiguration.instance.RemoveUIWidgetGroup(type);
            InitGroupData();
            if (m_SelectType == type)
            {
                m_SelectType = UIWidgetRepositoryDefine.kAllGroupType;
                InitPrefabData();
            }

            this.Repaint();
        }

        private void InitPrefabData()
        {
            m_PrefabGUIDs = UIWidgetRepositoryConfiguration.instance.GetPrefabsConfigByGroup(m_SelectType);
            m_UIWidgetItems.Clear();
            for (int i = 0; i < m_PrefabGUIDs.Count; i++)
            {
                string guid = m_PrefabGUIDs[i];
                UIWidgetItem uiWidgetItem = new UIWidgetItem(guid, i);
                m_UIWidgetItems.Add(uiWidgetItem);
            }

            SearchPrefabNameToPrefabs();
        }

        private void AddPrefabData(string guid,int insertIndex)
        {
            //判断是否是Common目录下如果不是则不让导入！！弹提示框。
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.Contains(UIWidgetRepositoryDefine.kCommonPath))
            {
                EditorUtility.DisplayDialog("导入失败",
                    "拖入的预制不是Common路径下的，只有Common路径下的可以放入组件库内。 \n避免跨模块调用，如果有跨模块使用的需求则需要将预制放到Common路径下才合理。", "ok");
                return;
            }

            string fileName = Path.GetFileName(assetPath);
            if (!fileName.EndsWith(".prefab"))
            {
                EditorUtility.DisplayDialog("导入失败", "只能导入预制！", "ok");
                return;
            }

            UIWidgetRepositoryConfiguration.instance.AddNewUIWidgetPrefabByGroup(m_SelectType, guid,insertIndex);
            InitPrefabData();
            Debug.Log($"AddPrefab,type:{m_SelectType},guid:{guid}");
            this.Repaint();
        }

        private void RemovePrefabData(string guid)
        {
            UIWidgetRepositoryConfiguration.instance.RemoveUIWidgetPrefabByGroup(m_SelectType, guid);
            InitPrefabData();
            Debug.Log($"RemovePrefab,type:{m_SelectType},guid:{guid}");
            this.Repaint();
        }

        private Texture GetAssetPreview(string guid)
        {
            if (m_PrefabPreviewsByGUID.TryGetValue(guid, out Texture texture))
            {
                return texture;
            }

            texture = UIWidgetRepositoryHelper.GetAssetPreview(guid);
            m_PrefabPreviewsByGUID[guid] = texture;
            return texture;
        }

        #endregion
    }
}