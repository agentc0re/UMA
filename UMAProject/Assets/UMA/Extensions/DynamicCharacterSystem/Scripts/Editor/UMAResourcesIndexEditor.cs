using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using UMA;

namespace UMAEditor
{
    [CustomEditor(typeof(UMAResourcesIndex), true)]
    public class UMAResourcesIndexEditor : Editor
    {
        UMAResourcesIndex thisURI;

        string GetIndexInfo(SerializedProperty indexData)
        {
            int totalIndexedTypes = indexData.arraySize;
            int totalIndexedFiles = 0;
            List<string> typeNames = new List<string>();
            for(int i = 0; i < totalIndexedTypes; i++)
            {
                typeNames.Add(indexData.GetArrayElementAtIndex(i).FindPropertyRelative("type").stringValue);
                totalIndexedFiles += indexData.GetArrayElementAtIndex(i).FindPropertyRelative("typeFiles").arraySize;
            }
            string info = "Total files indexed: " + totalIndexedFiles + " in " + totalIndexedTypes + " Types"/*.\nIndexed Types: \n" + String.Join(", ", typeNames.ToArray())*/;
            return info;
        }

        public override void OnInspectorGUI()
        {
            thisURI = target as UMAResourcesIndex;
            //we need to get the inspector to update to show any data that was added while the game was running?
            thisURI.LoadOrCreateData();
            serializedObject.Update();
            //DrawDefaultInspector();
            DrawPropertiesExcluding(serializedObject,new string[] { "enableDynamicIndexing", "Index" });
            var data = serializedObject.FindProperty("Index").FindPropertyRelative("data");
            var info = GetIndexInfo(data);
            EditorGUILayout.HelpBox(info, MessageType.Info);
            EditorGUILayout.HelpBox("Dynamic Indexing will automatically add any new assets you add to your Resources folders to the index as they are requested. But this is slow. Before you build, click the 'Create/Update Index' button below and turn off Dynamic Indexing.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            serializedObject.FindProperty("enableDynamicIndexing").boolValue = EditorGUILayout.ToggleLeft("Enable Dynamic Indexing", serializedObject.FindProperty("enableDynamicIndexing").boolValue);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Create/Update Index"))
            {
                thisURI.IndexAllResources();
                serializedObject.Update();
            }
            if (GUILayout.Button("Clear Index"))
            {
                thisURI.ClearIndex();
                serializedObject.Update();
            }
            EditorGUILayout.EndHorizontal();
            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
