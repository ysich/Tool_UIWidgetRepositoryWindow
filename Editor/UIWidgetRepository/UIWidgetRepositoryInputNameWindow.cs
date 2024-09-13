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

namespace Editor.Tools.UIWidgetRepository
{
    public class UIWidgetRepositoryInputNameWindow:EditorWindow
    {
        public GUIContent titleContent;
        public GUIContent buttonContent;
        public Func<string,bool> onButtonAction;
        public Action onCloseAction;
        public static UIWidgetRepositoryInputNameWindow ShowWindow(GUIContent titleContent,GUIContent buttonContent,Func<string,bool> buttonAction,Action closeAction)
        {
            UIWidgetRepositoryInputNameWindow window = GetWindow<UIWidgetRepositoryInputNameWindow>();
            window.minSize = new Vector2(400, 200);
            window.maxSize = new Vector2(400, 200);
            window.titleContent = titleContent;
            window.buttonContent = buttonContent;
            window.onButtonAction = buttonAction;
            window.onCloseAction = closeAction;
            window.Show();
            window.Focus();
            return window;
        }

        private string m_AddLabelStr;
        
        private void OnDisable()
        {
            this.onCloseAction();
        }

        private void OnGUI()
        {
            GUILayout.Label(titleContent,GUILayout.Height(20));
            GUILayout.Space(10);
            m_AddLabelStr = GUILayout.TextField(m_AddLabelStr,GUILayout.Height(60));
            GUILayout.Space(10);
            GUI.color = Color.green;
            if (GUILayout.Button(buttonContent,GUILayout.Height(60)))
            {
                if (string.IsNullOrWhiteSpace(m_AddLabelStr))
                {
                    EditorUtility.DisplayDialog("提示", "输入的内容为空！重新输入！","ok");
                    return;
                }

                if (onButtonAction(m_AddLabelStr))
                {
                    this.Close();
                }
            }
        }
    }
}