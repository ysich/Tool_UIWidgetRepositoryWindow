/*---------------------------------------------------------------------------------------
-- 负责人: onemt
-- 创建时间: 2024-08-08 15:49:33
-- 概述:
---------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace OnemtEditor.Tools.UIWidgetRepository
{
    public class UIWidgetRepositoryAddGroupWindow:EditorWindow
    {
        public Action<string> onSureAddAction;
        public static void ShowWindow(Action<string> sureAddAction)
        {
            UIWidgetRepositoryAddGroupWindow window = GetWindow<UIWidgetRepositoryAddGroupWindow>();
            window.minSize = new Vector2(400, 200);
            window.maxSize = new Vector2(400, 200);
            window.onSureAddAction = sureAddAction;
            window.Show();
        }
        
        private readonly GUIContent m_AddGroupContentGUIContent = new GUIContent("输入添加的组名:");
        private readonly GUIContent m_AddGroupButtonGUIContent = new GUIContent("添加");

        private string m_AddLabelStr;
        private void OnGUI()
        {
            GUILayout.Label(m_AddGroupContentGUIContent,GUILayout.Height(20));
            GUILayout.Space(10);
            m_AddLabelStr = GUILayout.TextField(m_AddLabelStr,GUILayout.Height(60));
            GUILayout.Space(10);
            GUI.color = Color.green;
            if (GUILayout.Button(m_AddGroupButtonGUIContent,GUILayout.Height(60)))
            {
                if (string.IsNullOrWhiteSpace(m_AddLabelStr))
                {
                    EditorUtility.DisplayDialog("提示", "输入的组名称为空！重新输入！","ok");
                    return;
                }
                //判断是否存在。
                List<string> groups = UIWidgetRepositoryHelper.GetGroupTypeConfig();
                if (groups.IndexOf(m_AddLabelStr) != -1)
                {
                    EditorUtility.DisplayDialog("提示", "添加的组名已经存在！重新命名！","ok");
                    return;
                }

                onSureAddAction(m_AddLabelStr);
                if (EditorUtility.DisplayDialog("提示", $"添加成功，组名：{m_AddLabelStr}", "ok"))
                {
                    this.Close();
                }
            }
        }
    }
}