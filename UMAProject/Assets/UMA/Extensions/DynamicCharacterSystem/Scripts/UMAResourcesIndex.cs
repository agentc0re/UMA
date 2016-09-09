using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;

namespace UMA
{
	[System.Serializable]
	public class UMAResourcesIndex : MonoBehaviour, ISerializationCallbackReceiver
	{
		public static UMAResourcesIndex Instance;
		public UMAResourcesIndexData Index;
		public UnityEngine.Object indexAsset;
		public bool enableDynamicIndexing = false;
		public bool makePersistent = false;

		public UMAResourcesIndex()
		{

		}



		void Reset()
		{
			Debug.Log("UMARESCOURCESINDEX RESET HAPPENNED");
			LoadOrCreateData();
		}

		/*void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
				if(makePersistent)
					DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
				if (makePersistent)
					Destroy(gameObject);
				else
					Instance = this;
            }
            LoadOrCreateData();
        }*/

		void Start()
		{
			if (Instance == null)
			{
				Instance = this;
				if (makePersistent)
					DontDestroyOnLoad(gameObject);
			}
			else if (Instance != this)
			{
				if (Instance.makePersistent)
					Destroy(gameObject);
				else
					Instance = this;
			}
			else if (Instance == this)//OnAfterDeserialize() gets called in the editor but doesn't do anything with the makePersistent value
			{
				if (makePersistent)
					DontDestroyOnLoad(gameObject);
			}
			LoadOrCreateData();
		}

		void OnApplicationQuit()
		{
			Save();
		}

		public void OnBeforeSerialize()
		{

		}

		public void OnAfterDeserialize()
		{
			if (Instance == null)//make an Instance in the editor too
			{
				Instance = this;
			}
		}

		public void Add(UnityEngine.Object obj)
		{
#if UNITY_EDITOR
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
#endif
		}
		public void Add(UnityEngine.Object obj, string objName)
		{
#if UNITY_EDITOR
			if (obj == null || objName == "")
				return;
			Index.AddPath(obj, objName);
			Save();
#endif
		}
		public void Add(UnityEngine.Object obj, int objNameHash)
		{
#if UNITY_EDITOR
			if (obj == null)
				return;
			Index.AddPath(obj, objNameHash);
			Save();
#endif
		}
		/// <summary>
		/// Loads saved Index data from a file or creates new data object;
		/// </summary>
		/// <returns></returns>
		public void LoadOrCreateData()
		{
			var data = new UMAResourcesIndexData();
			if (indexAsset != null)
			{
				var rawData = ((TextAsset)indexAsset).text;
				data = JsonUtility.FromJson<UMAResourcesIndexData>(rawData);
			}
#if UNITY_EDITOR
			else
			{
				var dataAssetPath = System.IO.Path.Combine(Application.dataPath, "UMA/Extensions/DynamicCharacterSystem/Scripts/UMAResourcesIndex.txt");
				if (File.Exists(dataAssetPath))
				{
					var rawData = FileUtils.ReadAllText(dataAssetPath);
					data = JsonUtility.FromJson<UMAResourcesIndexData>(rawData);
				}
			}
#endif
			Index = data;
		}

		/// <summary>
		/// Saves any updates to the index to the data file
		/// </summary>
		public void Save()
		{
			//Currently Editor Only. But then since you cant add any assets to Resources in a build you should not be adding anything to the index either.
#if UNITY_EDITOR
			var dataAssetPath = System.IO.Path.Combine(Application.dataPath, "UMA/Extensions/DynamicCharacterSystem/Scripts/UMAResourcesIndex.txt");
			var jsonData = JsonUtility.ToJson(Index);
			FileUtils.WriteAllText(dataAssetPath, jsonData);
			EditorUtility.SetDirty(indexAsset);
			AssetDatabase.SaveAssets();
			//need to refresh the actual data?
			LoadOrCreateData();
#endif
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
		// slight issue here is that UMABonePose assets dont have a hash and expressions are called the same thing for every race (so we only end up with one set indexed). But since they are refrerenced in an expressionset this seems to work ok anyway.
		public void IndexAllResources()
		{
			if (Application.isPlaying)
			{
				Debug.Log("You can only create a full Resources index while the application is not playing.");
				return;
			}
			var paths = AssetDatabase.GetAllAssetPaths();
			int pathsAdded = 0;
			for (int i = 0; i < paths.Length; i++)
			{
				if (paths[i].IndexOf("Resources/") > -1)
				{
					//we need to split the path and only use the part after resources
					var objResourcesPathArray = paths[i].Split(new string[] { "Resources/" }, StringSplitOptions.RemoveEmptyEntries);
					var extension = Path.GetExtension(objResourcesPathArray[1]);
					var objResourcesPath = objResourcesPathArray[1];
					if (extension != "")
					{
						objResourcesPath = objResourcesPath.Replace(extension, "");
					}
					var tempObj = Resources.Load(objResourcesPath);
					if (tempObj != null)
					{
						pathsAdded++;
						string thisName = Path.GetFileNameWithoutExtension(paths[i]);
						int thisHash = UMAUtils.StringToHash(thisName);
						if (tempObj.GetType() == typeof(SlotDataAsset))
						{
							thisName = ((SlotDataAsset)tempObj).slotName;
							thisHash = ((SlotDataAsset)tempObj).nameHash;
						}
						if (tempObj.GetType() == typeof(OverlayDataAsset))
						{
							thisName = ((OverlayDataAsset)tempObj).overlayName;
							thisHash = ((OverlayDataAsset)tempObj).nameHash;
						}
						if (tempObj.GetType() == typeof(RaceData))
						{
							thisName = ((RaceData)tempObj).raceName;
							thisHash = UMAUtils.StringToHash(thisName);
						}
						Index.AddPath(tempObj, thisHash);
						if (tempObj.GetType() != typeof(UnityEngine.GameObject))
							Resources.UnloadAsset(tempObj);//TODO check if this is safe to do...
					}
				}
			}
			Debug.Log("[UMAResourcesIndex] Added/Updated " + Index.Count() + " assets in the Index");
			Save();
		}
#endif
	}
}
