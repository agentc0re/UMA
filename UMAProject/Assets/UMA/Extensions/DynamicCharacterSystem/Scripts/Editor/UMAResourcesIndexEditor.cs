using UnityEngine;
using UnityEditor;
using System.Collections;
using UMA;

namespace UMAEditor
{
    [CustomEditor(typeof(UMAResourcesIndex), true)]
    public class UMAResourcesIndexEditor : Editor
    {
        UMAResourcesIndex thisURI;
        public override void OnInspectorGUI()
        {
            thisURI = target as UMAResourcesIndex;
            //we need to get the inspector to update to show any data that was added while the game was running?
            thisURI.LoadOrCreateData();
            serializedObject.Update();
            DrawDefaultInspector();
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Add/Update all Resources"))
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
