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
		private UMAResourcesIndexData index = new UMAResourcesIndexData();
		public UnityEngine.Object indexAsset;
		public bool enableDynamicIndexing = false;
		public bool makePersistent = false;
		public bool initialized = false;
		bool initializing = false;

		//Index (with a capital I) need to be a property that calls LoadOrCreateData if this is not initialized
		public UMAResourcesIndexData Index
		{
			get
			{
				if (!Application.isPlaying)
				{
					Instance = this;// makes no fucking difference - in fact because racelibrary is setting a 'allStartingAssetsAdded ' bool this just makes things worse...
				}
				if (initialized == false)
					LoadOrCreateData();
				return index;
			}
		}


		public UMAResourcesIndex()
		{
		}

		//Awake has to be here because if its not there is no resources index untill the actual object is viewed
		//ACTUALLY THIS MAKES NO DIFFERENCE
		/*void Awake()
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
			if(!Instance.initialized && !Instance.initializing)
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
			if (!Instance.initialized && !Instance.initializing)
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
				initialized = false;//this seems to fix things
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
			index.AddPath(obj, thisName);
			Save();
#endif
		}
		public void Add(UnityEngine.Object obj, string objName)
		{
#if UNITY_EDITOR
			if (obj == null || objName == "")
				return;
			index.AddPath(obj, objName);
			Save();
#endif
		}
		public void Add(UnityEngine.Object obj, int objNameHash)
		{
#if UNITY_EDITOR
			if (obj == null)
				return;
			index.AddPath(obj, objNameHash);
			Save();
#endif
		}
		/// <summary>
		/// Loads saved Index data from a file or creates new data object;
		/// </summary>
		/// <returns></returns>
		public void LoadOrCreateData()
		{
			if (initialized || initializing)
				return;
			initializing = true;
			var data = new UMAResourcesIndexData();
			bool saveNewAsset = false;
			if (indexAsset != null)
			{
				Debug.Log("[UMAResourcesIndex] indexAsset was not null");
				var rawData = ((TextAsset)indexAsset).text;
				data = JsonUtility.FromJson<UMAResourcesIndexData>(rawData);
			}
#if UNITY_EDITOR
			else
			{
				Debug.Log("[UMAResourcesIndex] indexAsset was null");
				var dataAssetPath = System.IO.Path.Combine(Application.dataPath, "UMA/Extensions/DynamicCharacterSystem/Scripts/UMAResourcesIndex.txt");
				if (File.Exists(dataAssetPath))
				{
					Debug.Log("[UMAResourcesIndex] BUT we found it");
					var rawData = FileUtils.ReadAllText(dataAssetPath);
					data = JsonUtility.FromJson<UMAResourcesIndexData>(rawData);
					indexAsset = AssetDatabase.LoadAssetAtPath("Assets/UMA/Extensions/DynamicCharacterSystem/Scripts/UMAResourcesIndex.txt", typeof(TextAsset));
				}
				else
				{
					Debug.Log("ResourcesIndex No Index Existed");
					saveNewAsset = true;
				}
			}
#endif
			index = data;
#if UNITY_EDITOR
			if (saveNewAsset)
			{
				Save();
			}

#endif
			initialized = true;
			initializing = false;
		}
		public string GetIndexInfo()
		{
			int totalIndexedTypes = 0;
			int totalIndexedFiles = 0;
			if (index.data != null)
			{
				totalIndexedTypes = index.data.Length;
				totalIndexedFiles = 0;
				List<string> typeNames = new List<string>();
				for (int i = 0; i < totalIndexedTypes; i++)
				{
					typeNames.Add(index.data[i].type);
					totalIndexedFiles += index.data[i].typeFiles.Length;
				}
			}
			string info = "Total files indexed: " + totalIndexedFiles + " in " + totalIndexedTypes + " Types"/*.\nIndexed Types: \n" + String.Join(", ", typeNames.ToArray())*/;
			return info;
		}

		/// <summary>
		/// Saves any updates to the index to the data file
		/// </summary>
		public void Save()
		{
			//Currently Editor Only. But then since you cant add any assets to Resources in a build you should not be adding anything to the index either.
#if UNITY_EDITOR
			var dataAssetPath = System.IO.Path.Combine(Application.dataPath, "UMA/Extensions/DynamicCharacterSystem/Scripts/UMAResourcesIndex.txt");
			var jsonData = JsonUtility.ToJson(index);
			FileUtils.WriteAllText(dataAssetPath, jsonData);
			EditorUtility.SetDirty(indexAsset);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			//set the indexAsset to be this file
			indexAsset = AssetDatabase.LoadAssetAtPath("Assets/UMA/Extensions/DynamicCharacterSystem/Scripts/UMAResourcesIndex.txt", typeof(TextAsset));
#endif
		}

#if UNITY_EDITOR

		/// <summary>
		/// Clears the Index of all data.
		/// </summary>
		public void ClearIndex()
		{
			index = new UMAResourcesIndexData();
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
						index.AddPath(tempObj, thisHash);
						if (tempObj.GetType() != typeof(UnityEngine.GameObject))
							Resources.UnloadAsset(tempObj);//TODO check if this is safe to do...
					}
				}
			}
			Debug.Log("[UMAResourcesIndex] Added/Updated " + index.Count() + " assets in the Index");
			Save();
		}
#endif
	}
}
