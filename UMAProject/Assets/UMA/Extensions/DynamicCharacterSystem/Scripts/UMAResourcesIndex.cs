using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace UMA
{
    [System.Serializable]
    public class UMAResourcesIndex : MonoBehaviour
    {
        public static UMAResourcesIndex Instance;
        public UMAResourcesIndexData Index;

        public UMAResourcesIndex()
        {

        }

        void Reset()
        {
            LoadOrCreateData();
        }

        void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
            LoadOrCreateData();
        }

        void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if(Instance != this)
            {
                Destroy(gameObject);
            }
            LoadOrCreateData();
        }

        void OnApplicationQuit()
        {
            Save();
        }

        public void Add(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            string thisName = obj.name;
            if (obj.GetType() == typeof(SlotDataAsset))
            {
                thisName = ((SlotDataAsset)obj).slotName;
            }
            if (obj.GetType() == typeof(OverlayDataAsset))
            {
                thisName = ((OverlayDataAsset)obj).overlayName;
            }
            if (obj.GetType() == typeof(RaceData))
            {
                thisName = ((RaceData)obj).raceName;
            }
            Index.AddPath(obj, thisName);
            Save();
        }
        public void Add(UnityEngine.Object obj, string objName)
        {
            if (obj == null || objName == "")
                return;
            Index.AddPath(obj, objName);
            Save();
        }
        /// <summary>
        /// Loads saved Index data from a file or creates new data object;
        /// </summary>
        /// <returns></returns>
        public void LoadOrCreateData()
        {
            var data = new UMAResourcesIndexData();
            var dataAssetPath = System.IO.Path.Combine(Application.dataPath, "UMA/Extensions/DynamicCharacterSystem/Scripts/UMAResourcesIndex.dat");
            if (File.Exists(dataAssetPath))
            {
                var rawData = FileUtils.ReadAllText(dataAssetPath);
                data = JsonUtility.FromJson<UMAResourcesIndexData>(rawData);
            }
            Index = data;
        }

        /// <summary>
        /// Saves any updates to the index to the data file
        /// </summary>
        public void Save()
        {
            var dataAssetPath = System.IO.Path.Combine(Application.dataPath, "UMA/Extensions/DynamicCharacterSystem/Scripts/UMAResourcesIndex.dat");
            var jsonData = JsonUtility.ToJson(Index);
            FileUtils.WriteAllText(dataAssetPath, jsonData);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Clears the Index of all data.
        /// </summary>
        public void ClearIndex()
        {
            Index = new UMAResourcesIndexData();
            Save();
        }
        /// <summary>
        /// Method to generate a full index of every file in Resources
        /// </summary>
        public void IndexAllResources()
        {
            if (Application.isPlaying)
            {
                Debug.Log("You can only create a full Resources index while the application is not playing.");
                return;
            }
            var paths = AssetDatabase.GetAllAssetPaths();
            int pathsAdded = 0;
            for(int i = 0; i < paths.Length; i++)
            {
                if (paths[i].IndexOf("Resources/") > -1)
                {
                    //we need to split the path and only use the part after resources
                    var objResourcesPathArray = paths[i].Split(new string[] { "Resources/" }, StringSplitOptions.RemoveEmptyEntries);
                    var extension = Path.GetExtension(objResourcesPathArray[1]);
                    var objResourcesPath = objResourcesPathArray[1];
                    if(extension != "")
                    {
                        objResourcesPath = objResourcesPath.Replace(extension, "");
                    }
                    var tempObj = Resources.Load(objResourcesPath);
                    if(tempObj != null)
                    {
                        pathsAdded++;
                        string thisName = Path.GetFileNameWithoutExtension(paths[i]);
                        if (tempObj.GetType() == typeof(SlotDataAsset))
                        {
                            thisName = ((SlotDataAsset)tempObj).slotName;
                        }
                        if (tempObj.GetType() == typeof(OverlayDataAsset))
                        {
                            thisName = ((OverlayDataAsset)tempObj).overlayName;
                        }
                        if (tempObj.GetType() == typeof(RaceData))
                        {
                            thisName = ((RaceData)tempObj).raceName;
                        }
                        Index.AddPath(tempObj, thisName);
                        if(tempObj.GetType() != typeof(UnityEngine.GameObject))
                            Resources.UnloadAsset(tempObj);//TODO check if this is safe to do...
                    }
                }
            }
            Debug.Log("[UMAResourcesIndex] Added or updated " + pathsAdded + " to the Index");
            Save();
        }
#endif
    }
}
