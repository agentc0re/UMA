using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UMAAssetBundleManager;

namespace UMA
{
    public class DynamicAssetLoader : MonoBehaviour
    {
        static DynamicAssetLoader _instance;

        [Tooltip("Set the server URL that assetbundles can be loaded from. Used in a live build and when the LocalAssetServer is turned off.")]
        public string remoteServerURL = "";
		[Tooltip("Use the JSON version of the assetBundleIndex rather than the assetBundleVersion.")]
		public bool useJsonIndex = false;
		[Tooltip("Set the server URL for the AssetBundleIndex json data. You can use this to make a server request that could generate an index on the fly for example. Used in a live build and when the LocalAssetServer is turned off. TIP use [PLATFORM] to use the current platform name in the URL")]
		public string remoteServerIndexURL = "";
		[Tooltip("A list of assetbundles to preload when the game starts. After these have completed loading any GameObject in the gameObjectsToActivate field will be activated.")]
        public List<string> assetBundlesToPreLoad = new List<string>();
        [Tooltip("GameObjects that will be activated after the list of assetBundlesToPreLoad has finished downloading.")]
        public List<GameObject> gameObjectsToActivate = new List<GameObject>();
        [Space]
        public GameObject loadingMessageObject;
        public Text loadingMessageText;
        public string loadingMessage = "";
        [HideInInspector]
        [System.NonSerialized]
        public float percentDone = 0f;
        [HideInInspector]
        [System.NonSerialized]
        public bool assetBundlesDownloading;
        bool canCheckDownloadingBundles;
        bool isInitializing = false;
        bool isInitialized = false;
        bool gameObjectsActivated;
        [Space]
        //Default assets fields
        public RaceData placeholderRace;//temp race based on UMAMale with a baseRecipe to generate a temp umaMale TODO: Could have a female too and search the required racename to see if it contains female...
        public UMATextRecipe placeholderWardrobeRecipe;//empty temp wardrobe recipe
        public SlotDataAsset placeholderSlot;//empty temp slot
        public OverlayDataAsset placeholderOverlay;//empty temp overlay. Would be nice if there was some way we could have a shader on this that would 'fill up' as assets loaded maybe?
        [HideInInspector]
        [System.NonSerialized]
        public UMAAvatarBase requestingUMA;
        //TODO: Just visible for dev
        //[HideInInspector]
        //[System.NonSerialized]
        public DownloadingAssetsList downloadingAssets = new DownloadingAssetsList();

        //Because searching Resources for UMA Assets is so slow we will cache the results as we get them
        //[System.NonSerialized]
        //public Dictionary<Type, Dictionary<int, string>> UMAResourcesIndex = new Dictionary<Type, Dictionary<int, string>>();

        int? _currentBatchID = null;

        /// <summary>
        /// Gets the currentBatchID or generates a new one if it is null. 
        /// Tip: Rather than setting this explicily, consider calling GenerateBatchID which will provide a unique random id number and set this property at the same time.
        /// </summary>
        public int CurrentBatchID
        {
            get
            {
                if (_currentBatchID == null)
                    _currentBatchID = GenerateBatchID();
                return (int)_currentBatchID;
            }
            set
            {
                _currentBatchID = value;
            }
        }

        public static DynamicAssetLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindInstance();
                }
                return _instance;
            }
            set { _instance = value; }
        }

        #region BASE METHODS
        void OnEnable()
        {
            if (_instance == null) _instance = this;//TODO check whether we can use multiple DALs by making this always set on start.
            if (!isInitialized)
            {
                StartCoroutine(Initialize());
            }
        }

        IEnumerator Start()
        {
            if (_instance == null) _instance = this;//TODO check whether we can use multiple DALs by making this always set on start.
            if (!isInitialized)
            {
                yield return StartCoroutine(Initialize());
            }

            //Load any preload asset bundles if there are any
            if (assetBundlesToPreLoad.Count > 0)
            {
                yield return StartCoroutine(LoadAssetBundlesAsync(assetBundlesToPreLoad));
            }
        }

        void Update()
        {
#if UNITY_EDITOR
            if (AssetBundleManager.SimulateAssetBundleInEditor)
            {
                if (!gameObjectsActivated)
                {
                    if (gameObjectsToActivate.Count > 0)
                    {
                        foreach (GameObject go in gameObjectsToActivate)
                        {
                            if (!go.activeSelf)
                            {
                                go.SetActive(true);
                            }
                        }
                    }
                    gameObjectsActivated = true;
                }
            }
#endif
            if (downloadingAssets.downloadingItems.Count > 0)
                downloadingAssets.Update();
            if (downloadingAssets.areDownloadedItemsReady == false)
                assetBundlesDownloading = true;
            if ((assetBundlesDownloading || downloadingAssets.areDownloadedItemsReady == false) && canCheckDownloadingBundles == true)
            {
                if (!AssetBundleManager.AreBundlesDownloading() && downloadingAssets.areDownloadedItemsReady == true)
                {
                    assetBundlesDownloading = false;
                    if (!gameObjectsActivated)
                    {
                        if (gameObjectsToActivate.Count > 0)
                        {
                            foreach (GameObject go in gameObjectsToActivate)
                            {
                                if (!go.activeSelf)
                                {
                                    go.SetActive(true);
                                }
                            }
                        }
                        gameObjectsActivated = true;
                    }
                }
            }
        }
        /// <summary>
        /// Finds the DynamicAssetLoader in the scene and treats it like a singleton.
        /// </summary>
        /// <returns>The DynamicAssetLoader.</returns>
        public static DynamicAssetLoader FindInstance()
        {
            if (_instance == null)
            {
                DynamicAssetLoader[] dynamicAssetLoaders = FindObjectsOfType(typeof(DynamicAssetLoader)) as DynamicAssetLoader[];
                if (dynamicAssetLoaders[0] != null)
                {
                    _instance = dynamicAssetLoaders[0];
                }
            }
            return _instance;
        }
        #endregion


        #region DOWNLOAD METHODS

        /// <summary>
        /// Initialize the downloading URL. eg. local server / iOS ODR / or the download URL as defined in the component settings if Simulation Mode and Local Asset Server is off
        /// </summary>
        void InitializeSourceURL()
        {
            string URLToUse = "";
            if (SimpleWebServer.ServerURL != "")
            {
#if UNITY_EDITOR
                if(SimpleWebServer.serverStarted)//this is not true in builds no matter what- but we in the editor we need to know
#endif
                URLToUse = SimpleWebServer.ServerURL;
                Debug.Log("[DynamicAssetLoader] SimpleWebServer.ServerURL = " + URLToUse);
            }
            else
            {
                URLToUse = remoteServerURL;
            }
//#endif
            if (URLToUse != "")
                AssetBundleManager.SetSourceAssetBundleURL(URLToUse);
            else
            {
                string errorString = "LocalAssetBundleServer was off and no remoteServerURL was specified. One of these must be set in order to use any AssetBundles!";
#if UNITY_EDITOR
                errorString = "Switched to Simulation Mode because LocalAssetBundleServer was off and no remoteServerURL was specified in the Scenes' DynamicAssetLoader. One of these must be set in order to actually use your AssetBundles.";
                AssetBundleManager.SimulateOverride = true;
#endif
                Debug.LogWarning(errorString);
            }
            return;

        }
        /// <summary>
        /// Initializes AssetBundleManager which loads the AssetBundleManifest object and the AssetBundleIndex object.
        /// </summary>
        /// <returns></returns>
        protected IEnumerator Initialize()
        {
#if UNITY_EDITOR
            if (AssetBundleManager.SimulateAssetBundleInEditor)
            {
                isInitialized = true;
                yield break;
            }
#endif
            if (isInitializing == false)
            {
                isInitializing = true;
                InitializeSourceURL();//in the editor this might set AssetBundleManager.SimulateAssetBundleInEditor to be true aswell so check that
#if UNITY_EDITOR
                if (AssetBundleManager.SimulateAssetBundleInEditor)
                {
                    isInitialized = true;
                    yield break;
                }
#endif
                var request = AssetBundleManager.Initialize(useJsonIndex, remoteServerIndexURL);
                if (request != null)
                {
                    while (AssetBundleManager.IsOperationInProgress(request))
                    {
                        yield return null;
                    }
                    isInitializing = false;
                    if (/*AssetBundleManager.AssetBundleManifestObject != null && */AssetBundleManager.AssetBundleIndexObject != null)
                    {
                        isInitialized = true;
                    }
                    else
                    {
                        //if we are in the editor this can only have happenned because the asset bundles were not built and by this point
                        //an error will have already been shown about that and AssetBundleManager.SimulationOverride will be true so we can just continue.
#if UNITY_EDITOR
                        if (/*AssetBundleManager.AssetBundleManifestObject == null ||*/ AssetBundleManager.AssetBundleIndexObject == null)
                        {
                            isInitialized = true;
                            yield break;
                        }
#endif
                    }
                }
                else
                {
                    Debug.LogWarning("AssetBundleManager failed to initialize correctly");
                }
            }
        }
        /// <summary>
        /// Generates a batch ID for use when grouping assetbundle asset requests together so they can be processed in the same cycle (to avoid UMA Generation errors).
        /// </summary>
        /// <returns></returns>
        public int GenerateBatchID()
        {
            CurrentBatchID = UnityEngine.Random.Range(1000000, 2000000);
            return CurrentBatchID;
        }
        /// <summary>
        /// Load a single assetbundle (and its dependencies) asynchroniously and sets the Loading Messages.
        /// </summary>
        /// <param name="assetBundleToLoad"></param>
        /// <param name="loadingMsg"></param>
        /// <param name="loadedMsg"></param>
        public void LoadAssetBundle(string assetBundleToLoad, string loadingMsg = "", string loadedMsg = "")
        {
            var assetBundlesToLoadList = new List<string>();
            assetBundlesToLoadList.Add(assetBundleToLoad);
            LoadAssetBundles(assetBundlesToLoadList, loadingMsg, loadedMsg);
        }
        /// <summary>
        /// Load multiple assetbundles (and their dependencies) asynchroniously and sets the Loading Messages.
        /// </summary>
        /// <param name="assetBundlesToLoad"></param>
        /// <param name="loadingMsg"></param>
        /// <param name="loadedMsg"></param>
        public void LoadAssetBundles(string[] assetBundlesToLoad, string loadingMsg = "", string loadedMsg = "")
        {
            var assetBundlesToLoadList = new List<string>(assetBundlesToLoad);
            LoadAssetBundles(assetBundlesToLoadList, loadingMsg, loadedMsg);
        }
        /// <summary>
        /// Load multiple assetbundles (and their dependencies) asynchroniously and sets the Loading Messages.
        /// </summary>
        /// <param name="assetBundlesToLoad"></param>
        /// <param name="loadingMsg"></param>
        /// <param name="loadedMsg"></param>
        public void LoadAssetBundles(List<string> assetBundlesToLoad, string loadingMsg = "", string loadedMsg = "")
        {
#if UNITY_EDITOR
            if (AssetBundleManager.SimulateAssetBundleInEditor)
            {
                //Actually we DO still need to do something here
                foreach (string requiredBundle in assetBundlesToLoad)
                {
                    SimulateLoadAssetBundle(requiredBundle);
                }
                return;
            }
#endif
            List<string> assetBundlesToReallyLoad = new List<string>();
            foreach (string requiredBundle in assetBundlesToLoad)
            {
                if (!AssetBundleManager.IsAssetBundleDownloaded(requiredBundle))
                {
                    assetBundlesToReallyLoad.Add(requiredBundle);
                }
            }
            if (assetBundlesToReallyLoad.Count > 0)
            {
                AssetBundleLoadingIndicator.Instance.Show(assetBundlesToReallyLoad, loadingMsg, "", loadedMsg);
                assetBundlesDownloading = true;
                canCheckDownloadingBundles = false;
                StartCoroutine(LoadAssetBundlesAsync(assetBundlesToReallyLoad));
            }
        }
        /// <summary>
        /// Loads a list of asset bundles and their dependencies asynchroniously
        /// </summary>
        /// <param name="assetBundlesToLoad"></param>
        /// <returns></returns>
        protected IEnumerator LoadAssetBundlesAsync(List<string> assetBundlesToLoad)
        {
#if UNITY_EDITOR
            if (AssetBundleManager.SimulateAssetBundleInEditor)
                yield break;
#endif
            if (!isInitialized)
            {
                if (!isInitializing)
                {
                    Debug.LogWarning("[DynamicAssetLoader] isInitialized was false");
                    yield return StartCoroutine(Initialize());
                }
                else
                {
                    Debug.Log("Waiting for Initializing to complete...");
                    while (isInitialized == false)
                    {
                        yield return null;
                    }
                }
            }
            string[] bundlesInManifest = AssetBundleManager.AssetBundleIndexObject.GetAllAssetBundles();
            foreach (string assetBundleName in assetBundlesToLoad)
            {
                foreach (string bundle in bundlesInManifest)
                {
                    if ((bundle == assetBundleName || bundle.IndexOf(assetBundleName + "/") > -1))
                    {
                        StartCoroutine(LoadAssetBundleAsync(bundle));
                    }
                }
            }
            canCheckDownloadingBundles = true;
            assetBundlesDownloading = true;
            yield return null;
        }
        /// <summary>
        /// Loads an asset bundle and its dependencies asynchroniously
        /// </summary>
        /// <param name="bundle"></param>
        /// <returns></returns>
        //DOS NOTES: if the local server is turned off after it was on when AssetBundleManager was initialized 
        //(like could happen in the editoror if you run a build that uses the local server but you have not started Unity and turned local server on)
        //then this wrongly says that the bundle has downloaded
#pragma warning disable 0219 //remove the warning that we are not using loadedBundle- since we want the error
        protected IEnumerator LoadAssetBundleAsync(string bundle)
        {
            float startTime = Time.realtimeSinceStartup;
            AssetBundleManager.LoadAssetBundle(bundle, false);
            while (AssetBundleManager.IsAssetBundleDownloaded(bundle) == false)
            {
                yield return null;
            }
            string error = null;
            LoadedAssetBundle loadedBundle = AssetBundleManager.GetLoadedAssetBundle(bundle, out error);
            float elapsedTime = Time.realtimeSinceStartup - startTime;
            Debug.Log(bundle + (error != null ? " was not" : " was") + " loaded successfully in " + elapsedTime + " seconds");
            if (error != null)
            {
                Debug.LogError("[DynamicAssetLoader] Bundle Load Error: " +error);
            }
            yield return true;
        }
#pragma warning restore 0219

        #endregion

        #region LOAD ASSETS METHODS
        List<Type> deepResourcesScanned = new List<Type>();
        //we do really need a unified AddAssets method really because now its not a simple two stage process 
        //i.e. we check the resources index then asset bundles then deep scan resources (slow)
        //but we still want people to be able to turn resources/assetbundles scanning on and off
        //its gonna be a monster to call...
        public bool AddAssets<T>(ref Dictionary<string, List<string>> assetBundlesUsedDict, bool searchResources, bool searchBundles, bool downloadAssetsEnabled, string bundlesToSearch = "", string resourcesFolderPath = "", int? assetNameHash = null, string assetName = "", Action<T[]> callback = null) where T : UnityEngine.Object
        {
            bool found = false;
            List<T> assetsToReturn = new List<T>();
            string[] resourcesFolderPathArray = SearchStringToArray(resourcesFolderPath);
            string[] bundlesToSearchArray = SearchStringToArray(bundlesToSearch);
            bool doDeepSearch = (assetName == "" && assetNameHash == null && deepResourcesScanned.Contains(typeof(T)) == false);
            //first do the quick resourcesIndex search if searchResources and we have either a name or a hash
            if (searchResources)
            {
                if (UMAResourcesIndex.Instance != null)
                {
                    doDeepSearch = doDeepSearch == true ? UMAResourcesIndex.Instance.enableDynamicIndexing : doDeepSearch;
                    found = AddAssetsFromResourcesIndex<T>(ref assetsToReturn, resourcesFolderPathArray, assetNameHash, assetName);
                    if ((assetName != "" || assetNameHash != null) && found)
                        doDeepSearch = false;
                }
            }
            //if we can and want to search asset bundles
            if ((AssetBundleManager.AssetBundleIndexObject != null || AssetBundleManager.SimulateAssetBundleInEditor == true) || Application.isPlaying == false)
                if (searchBundles && (found == false || (assetName == "" && assetNameHash == null)))
                {
                    bool foundHere = AddAssetsFromAssetBundles<T>(ref assetBundlesUsedDict, ref assetsToReturn, downloadAssetsEnabled, bundlesToSearchArray, assetNameHash, assetName);
                    found = foundHere == true ? true : found;
                    if ((assetName != "" || assetNameHash != null) && found)
                        doDeepSearch = false;
                }
            //if enabled and we have not found anything yet or we are getting all items of a type do a deep resources search (slow)
            if (searchResources && (found == false || doDeepSearch))
            {
                Debug.Log("DID DEEP SEARCH");
                bool foundHere = AddAssetsFromResources<T>(ref assetsToReturn, resourcesFolderPathArray, assetNameHash, assetName);
                found = foundHere == true ? true : found;
                if (assetName == "" && assetNameHash == null && deepResourcesScanned.Contains(typeof(T)) == false)
                    deepResourcesScanned.Add(typeof(T));
            }
            if (callback != null)
            {
                callback(assetsToReturn.ToArray());
            }
            return found;
        }

        public bool AddAssets<T>(bool searchResources, bool searchBundles, bool downloadAssetsEnabled, string bundlesToSearch = "", string resourcesFolderPath = "", int? assetNameHash = null, string assetName = "", Action<T[]> callback = null) where T : UnityEngine.Object
        {
            var dummyDict = new Dictionary<string, List<string>>();
            return AddAssets<T>(ref dummyDict, searchResources, searchBundles, downloadAssetsEnabled, bundlesToSearch, resourcesFolderPath, assetNameHash, assetName, callback);
        }

        public bool AddAssetsFromResourcesIndex<T>(ref List<T> assetsToReturn, string[] resourcesFolderPathArray, int? assetNameHash = null, string assetName = "") where T : UnityEngine.Object
        {
            bool found = false;
            if (UMAResourcesIndex.Instance == null)
                return found;
            if (assetNameHash != null || assetName != "")
            {
                string foundAssetPath = "";
                if (assetNameHash != null)
                {
                    foundAssetPath = UMAResourcesIndex.Instance.Index.GetPath<T>((int)assetNameHash, resourcesFolderPathArray);
                }
                else if (assetName != "")
                {
                    foundAssetPath = UMAResourcesIndex.Instance.Index.GetPath<T>(assetName, resourcesFolderPathArray);
                }
                if (foundAssetPath != "")
                {
                    T foundAsset = Resources.Load<T>(foundAssetPath);
                    if (foundAsset != null)
                    {
                        assetsToReturn.Add(foundAsset);
                        found = true;
                    }
                }
            }
            else if (assetNameHash == null && assetName == "")
            {
                foreach (string path in UMAResourcesIndex.Instance.Index.GetPaths<T>(resourcesFolderPathArray))
                {
                    T foundAsset = Resources.Load<T>(path);
                    if (foundAsset != null)
                    {
                        assetsToReturn.Add(foundAsset);
                        found = true;
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// Generic Library function to search Resources for a type of asset, optionally filtered by folderpath and asset assetNameHash or assetName. 
        /// Optionally sends the found assets to the supplied callback for processing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="resourcesFolderPath"></param>
        /// <param name="assetNameHash"></param>
        /// <param name="assetName"></param>
        /// <param name="callback"></param>
        /// <returns>Returns true if a assetNameHash or assetName were specified and an asset with that assetNameHash or assetName is found. Else returns false.</returns>
        public bool AddAssetsFromResources<T>(ref List<T> assetsToReturn, string[] resourcesFolderPathArray, int? assetNameHash = null, string assetName = "") where T : UnityEngine.Object
        {
            bool found = false;
            //If resources had an index like the one we made we could skip this (it does but because nobody understands the need to access it you cant)
            foreach (string path in resourcesFolderPathArray)
            {
                T[] foundAssets = new T[0];
                var pathPrefix = path == "" ? "" : path + "/";
                if ((typeof(T) == typeof(SlotDataAsset)) || (typeof(T) == typeof(OverlayDataAsset)) || (typeof(T) == typeof(RaceData)))
                {
                    //This is hugely expensive but we have to do this as we dont know the asset name, only the race/slot/overlayName which may not be the same. 
                    //This will only happen once now that I added the UMAResourcesDictionary
                    foundAssets = Resources.LoadAll<T>(path);
                }
                else
                {
                    if (assetName == "")
                        foundAssets = Resources.LoadAll<T>(path);
                    else
                    {
                        if (pathPrefix != "")
                        {
                            T foundAsset = Resources.Load<T>(pathPrefix + assetName);
                            if (foundAsset != null)
                            {
                                if (UMAResourcesIndex.Instance != null)
                                    UMAResourcesIndex.Instance.Add(foundAsset);
                                assetsToReturn.Add(foundAsset);
                                found = true;
                            }
                            else
                            {
                                foundAssets = Resources.LoadAll<T>(path);
                            }
                        }
                        else
                        {
                            foundAssets = Resources.LoadAll<T>(path);
                        }
                    }
                }
                if (found == false)
                {
                    for (int i = 0; i < foundAssets.Length; i++)
                    {
                        if (assetNameHash != null)
                        {
                            int foundHash = UMAUtils.StringToHash(foundAssets[i].name);
                            if (typeof(T) == typeof(SlotDataAsset))
                            {
                                foundHash = (foundAssets[i] as SlotDataAsset).nameHash;
                            }
                            if (typeof(T) == typeof(OverlayDataAsset))
                            {
                                //foundHash = UMAUtils.StringToHash((foundAssets[i] as OverlayDataAsset).nameHash);
                                foundHash = (foundAssets[i] as OverlayDataAsset).nameHash;
                            }
                            if (typeof(T) == typeof(RaceData))
                            {
                                foundHash = UMAUtils.StringToHash((foundAssets[i] as RaceData).raceName);
                            }
                            if (foundHash == assetNameHash)
                            {
                                if (UMAResourcesIndex.Instance != null)
                                    UMAResourcesIndex.Instance.Add(foundAssets[i], foundHash);
                                assetsToReturn.Add(foundAssets[i]);
                                found = true;
                            }
                        }
                        else if (assetName != "")
                        {
                            string foundName = foundAssets[i].name;
                            if (typeof(T) == typeof(OverlayDataAsset))
                            {
                                foundName = (foundAssets[i] as OverlayDataAsset).overlayName;
                            }
                            if (typeof(T) == typeof(SlotDataAsset))
                            {
                                foundName = (foundAssets[i] as SlotDataAsset).slotName;
                            }
                            if (typeof(T) == typeof(RaceData))
                            {
                                foundName = (foundAssets[i] as RaceData).raceName;
                            }
                            if (foundName == assetName)
                            {
                                if (UMAResourcesIndex.Instance != null)
                                    UMAResourcesIndex.Instance.Add(foundAssets[i], foundName);
                                assetsToReturn.Add(foundAssets[i]);
                                found = true;
                            }

                        }
                        else
                        {
                            if (UMAResourcesIndex.Instance != null)
                                UMAResourcesIndex.Instance.Add(foundAssets[i]);
                            assetsToReturn.Add(foundAssets[i]);
                            found = true;
                        }
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// Generic Library function to search AssetBundles for a type of asset, optionally filtered by bundle name, and asset assetNameHash or assetName. 
        /// Optionally sends the found assets to the supplied callback for processing.
        /// Automatically performs the operation in SimulationMode if AssetBundleManager.SimulationMode is enabled or if the Application is not playing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bundlesToSearch"></param>
        /// <param name="assetNameHash"></param>
        /// <param name="assetName"></param>
        /// <param name="callback"></param>
        public bool AddAssetsFromAssetBundles<T>(ref Dictionary<string, List<string>> assetBundlesUsedDict, ref List<T> assetsToReturn, bool downloadAssetsEnabled, string[] bundlesToSearchArray, int? assetNameHash = null, string assetName = "", Action<T[]> callback = null) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (AssetBundleManager.SimulateAssetBundleInEditor)
            {
                return SimulateAddAssetsFromAssetBundlesNew<T>(ref assetBundlesUsedDict, ref assetsToReturn, bundlesToSearchArray, assetNameHash, assetName, callback);
            }
            else
            {
#endif
                if (AssetBundleManager.AssetBundleIndexObject == null)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("[DynamicAssetLoader] No AssetBundleManager.AssetBundleManifestObject found. Do you need to rebuild your AssetBundles and/or upload the platform manifest bundle?");
                    AssetBundleManager.SimulateOverride = true;
                    return SimulateAddAssetsFromAssetBundlesNew<T>(ref assetBundlesUsedDict, ref assetsToReturn, bundlesToSearchArray, assetNameHash, assetName, callback);
#else
					Debug.LogError("[DynamicAssetLoader] No AssetBundleManager.AssetBundleManifestObject found. Do you need to rebuild your AssetBundles and/or upload the platform manifest bundle?");
                    return false;
#endif
                }
                if (AssetBundleManager.AssetBundleIndexObject == null)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("[DynamicAssetLoader] No AssetBundleManager.AssetBundleIndexObject found. Do you need to rebuild your AssetBundles and/or upload the platform index bundle?");
                    AssetBundleManager.SimulateOverride = true;
                    return SimulateAddAssetsFromAssetBundlesNew<T>(ref assetBundlesUsedDict, ref assetsToReturn, bundlesToSearchArray, assetNameHash, assetName, callback);
#else
					Debug.LogError("[DynamicAssetLoader] No AssetBundleManager.AssetBundleIndexObject found. Do you need to rebuild your AssetBundles and/or upload the platform index bundle?");
                    return false;
#endif
                }
                string[] allAssetBundleNames = AssetBundleManager.AssetBundleIndexObject.GetAllAssetBundleNames();
                string[] assetBundleNamesArray = allAssetBundleNames;
                Type typeParameterType = typeof(T);
                var typeString = typeParameterType.FullName;
                if (bundlesToSearchArray.Length > 0 && bundlesToSearchArray[0] != "")
                {
                    List<string> processedBundleNamesArray = new List<string>();
                    for (int i = 0; i < bundlesToSearchArray.Length; i++)
                    {
                        for (int ii = 0; ii < allAssetBundleNames.Length; ii++)
                        {
                            if (allAssetBundleNames[ii].IndexOf(bundlesToSearchArray[i]) > -1 && !processedBundleNamesArray.Contains(allAssetBundleNames[ii]))
                            {
                                processedBundleNamesArray.Add(allAssetBundleNames[ii]);
                            }
                        }
                    }
                    assetBundleNamesArray = processedBundleNamesArray.ToArray();
                }
                bool assetFound = false;
                for (int i = 0; i < assetBundleNamesArray.Length; i++)
                {
                    string error = "";
                    if (assetNameHash != null && assetName == "")
                    {
                        assetName = AssetBundleManager.AssetBundleIndexObject.GetAssetNameFromHash(assetBundleNamesArray[i], assetNameHash, typeString);
                    }
                    if (assetName != "" || assetNameHash != null)
                    {
                        if (assetName == "" && assetNameHash != null)
                        {
                            continue;
                        }
                        bool assetBundleContains = AssetBundleManager.AssetBundleIndexObject.AssetBundleContains(assetBundleNamesArray[i], assetName, typeString);
                        if (!assetBundleContains && typeof(T) == typeof(SlotDataAsset))
                        {
                            //try the '_Slot' version
                            assetBundleContains = AssetBundleManager.AssetBundleIndexObject.AssetBundleContains(assetBundleNamesArray[i], assetName + "_Slot", typeString);
                        }
                        if (assetBundleContains)
                        {
                            if (AssetBundleManager.IsAssetBundleDownloaded(assetBundleNamesArray[i]))
                            {
                                T target = (T)AssetBundleManager.GetLoadedAssetBundle(assetBundleNamesArray[i], out error).m_AssetBundle.LoadAsset<T>(assetName);
                                if (target == null && typeof(T) == typeof(SlotDataAsset))
                                {
                                    target = (T)AssetBundleManager.GetLoadedAssetBundle(assetBundleNamesArray[i], out error).m_AssetBundle.LoadAsset<T>(assetName + "_Slot");
                                }
                                if (target != null)
                                {
                                    assetFound = true;
                                    if (!assetBundlesUsedDict.ContainsKey(assetBundleNamesArray[i]))
                                    {
                                        assetBundlesUsedDict[assetBundleNamesArray[i]] = new List<string>();
                                    }
                                    if (!assetBundlesUsedDict[assetBundleNamesArray[i]].Contains(assetName))
                                    {
                                        assetBundlesUsedDict[assetBundleNamesArray[i]].Add(assetName);
                                    }
                                    assetsToReturn.Add(target);
                                    if (assetName != "")
                                        break;
                                }
                                else
                                {
                                    if (error != "")
                                    {
                                        Debug.LogWarning(error);
                                    }
                                }
                            }
                            else if (downloadAssetsEnabled)
                            {
                                //Here we return a temp asset and wait for the bundle to download
                                //We dont want to create multiple downloads of the same bundle so check its not already downloading
                                if (AssetBundleManager.AreBundlesDownloading(assetBundleNamesArray[i]) == false)
                                {
                                    LoadAssetBundle(assetBundleNamesArray[i]);
                                }
                                else
                                {
                                    //do nothing its already downloading
                                }
                                if (assetNameHash == null)
                                {
                                    assetNameHash = AssetBundleManager.AssetBundleIndexObject.GetAssetHashFromName(assetBundleNamesArray[i], assetName, typeString);
                                }
                                T target = downloadingAssets.AddDownloadItem<T>(CurrentBatchID, assetName, assetNameHash, assetBundleNamesArray[i], requestingUMA);
                                if (target != null)
                                {
                                    assetFound = true;
                                    if (!assetBundlesUsedDict.ContainsKey(assetBundleNamesArray[i]))
                                    {
                                        assetBundlesUsedDict[assetBundleNamesArray[i]] = new List<string>();
                                    }
                                    if (!assetBundlesUsedDict[assetBundleNamesArray[i]].Contains(assetName))
                                    {
                                        assetBundlesUsedDict[assetBundleNamesArray[i]].Add(assetName);
                                    }
                                    assetsToReturn.Add(target);
                                    if (assetName != "")
                                        break;
                                }
                            }
                        }
                    }
                    else //we are just loading in all assets of type from the downloaded bundles- only realistically possible when the bundles have been downloaded already because otherwise this would trigger the download of all possible assetbundles that contain anything of type T...
                    {
                        if (AssetBundleManager.IsAssetBundleDownloaded(assetBundleNamesArray[i]))
                        {
                            string[] assetsInBundle = AssetBundleManager.AssetBundleIndexObject.GetAllAssetsOfTypeInBundle(assetBundleNamesArray[i], typeString);
                            if (assetsInBundle.Length > 0)
                            {
                                foreach (string asset in assetsInBundle)
                                {
                                    T target = (T)AssetBundleManager.GetLoadedAssetBundle(assetBundleNamesArray[i], out error).m_AssetBundle.LoadAsset<T>(asset);
                                    if (target == null && typeof(T) == typeof(SlotDataAsset))
                                    {
                                        target = (T)AssetBundleManager.GetLoadedAssetBundle(assetBundleNamesArray[i], out error).m_AssetBundle.LoadAsset<T>(asset + "_Slot");
                                    }
                                    if (target != null)
                                    {
                                        assetFound = true;
                                        if (!assetBundlesUsedDict.ContainsKey(assetBundleNamesArray[i]))
                                        {
                                            assetBundlesUsedDict[assetBundleNamesArray[i]] = new List<string>();
                                        }
                                        if (!assetBundlesUsedDict[assetBundleNamesArray[i]].Contains(asset))
                                        {
                                            assetBundlesUsedDict[assetBundleNamesArray[i]].Add(asset);
                                        }
                                        assetsToReturn.Add(target);
                                    }
                                    else
                                    {
                                        if (error != "")
                                        {
                                            Debug.LogWarning(error);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (!assetFound && assetName != "")
                {
                    string[] assetIsInArray = AssetBundleManager.AssetBundleIndexObject.FindContainingAssetBundle(assetName, typeString);
                    string assetIsIn = assetIsInArray.Length > 0 ? " but it was in " + assetIsInArray[0] : ". Do you need to reupload you platform manifest and index?";
                    Debug.LogWarning("Dynamic" + typeof(T).Name + "Library (" + typeString + ") could not load " + assetName + " from any of the AssetBundles searched" + assetIsIn);
                }
                if (assetsToReturn.Count > 0 && callback != null)
                {
                    callback(assetsToReturn.ToArray());
                }

                return assetFound;
#if UNITY_EDITOR
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Simulates the loading of assets when AssetBundleManager is set to 'SimulationMode'
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bundlesToSearch"></param>
        /// <param name="assetNameHash"></param>
        /// <param name="assetName"></param>
        /// <param name="callback"></param>
        bool SimulateAddAssetsFromAssetBundlesNew<T>(ref Dictionary<string, List<string>> assetBundlesUsedDict, ref List<T> assetsToReturn, string[] bundlesToSearchArray, int? assetNameHash = null, string assetName = "", Action<T[]> callback = null) where T : UnityEngine.Object
        {
            Type typeParameterType = typeof(T);
            var typeString = typeParameterType.FullName;
            if (assetNameHash != null)
            {
                //actually this is not true. We could load all assets of type, iterate over them and get the hash and see if it matches...But then that would be as slow as loading from resources was
                Debug.Log("It is not currently possible to search for assetBundle assets in SimulationMode using the assetNameHash. " + typeString + " is trying to do this with assetNameHash " + assetNameHash);
            }
            string[] allAssetBundleNames = AssetDatabase.GetAllAssetBundleNames();
            string[] assetBundleNamesArray;
            if (bundlesToSearchArray.Length > 0 && bundlesToSearchArray[0] != "")
            {
                List<string> processedBundleNamesArray = new List<string>();
                for (int i = 0; i < bundlesToSearchArray.Length; i++)
                {
                    for (int ii = 0; ii < allAssetBundleNames.Length; ii++)
                    {
                        if (allAssetBundleNames[ii].IndexOf(bundlesToSearchArray[i]) > -1 && !processedBundleNamesArray.Contains(allAssetBundleNames[ii]))
                        {
                            processedBundleNamesArray.Add(allAssetBundleNames[ii]);
                        }
                    }
                }
                assetBundleNamesArray = processedBundleNamesArray.ToArray();
            }
            else
            {
                assetBundleNamesArray = allAssetBundleNames;
            }
            bool assetFound = false;
            List<string> dependencies = new List<string>();
            for (int i = 0; i < assetBundleNamesArray.Length; i++)
            {
                if (assetFound && assetName != "")//Do we want to break actually? What if the user has named two overlays the same? Or would this not work anyway?
                    break;
                string[] possiblePaths = new string[0];
                if (assetName != "")
                {
                    //if this is looking for SlotsDataAssets then the asset name has _Slot after it usually even if the slot name doesn't have that-but the user might have renamed it so cover both cases
                    if (typeof(T) == typeof(SlotDataAsset))
                    {
                        string[] possiblePathsTemp = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleNamesArray[i], assetName);
                        string[] possiblePaths_SlotTemp = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleNamesArray[i], assetName + "_Slot");
                        List<string> possiblePathsList = new List<string>(possiblePathsTemp);
                        foreach (string path in possiblePaths_SlotTemp)
                        {
                            if (!possiblePathsList.Contains(path))
                            {
                                possiblePathsList.Add(path);
                            }
                        }
                        possiblePaths = possiblePathsList.ToArray();
                    }
                    else
                    {
                        possiblePaths = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleNamesArray[i], assetName);
                    }
                }
                else
                {
                    //Ideally we should load all the dependent assets too but when we are simulating we dont have access 
                    //to this data because its in the manifest, which is not there, otherwise we would not be simulating in the first place
                    if (!Application.isPlaying)
                    {
                        possiblePaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleNamesArray[i]);
                    }
                }
                foreach (string path in possiblePaths)
                {
                    T target = (T)AssetDatabase.LoadAssetAtPath(path, typeof(T));
                    if (target != null)
                    {
                        assetFound = true;
                        if (!assetBundlesUsedDict.ContainsKey(assetBundleNamesArray[i]))
                        {
                            assetBundlesUsedDict[assetBundleNamesArray[i]] = new List<string>();
                        }
                        if (!assetBundlesUsedDict[assetBundleNamesArray[i]].Contains(assetName))
                        {
                            assetBundlesUsedDict[assetBundleNamesArray[i]].Add(assetName);
                        }
                        assetsToReturn.Add(target);
                        //if the application is not playing we want to load ALL the assets from the bundle this asset will be in
                        if (Application.isPlaying)
                        {
                            var thisAssetBundlesAssets = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleNamesArray[i]);
                            for (int ii = 0; ii < thisAssetBundlesAssets.Length; ii++)
                            {
                                if (!dependencies.Contains(thisAssetBundlesAssets[ii]) && thisAssetBundlesAssets[ii] != path)
                                {
                                    dependencies.Add(thisAssetBundlesAssets[ii]);
                                }
                            }
                        }
                        if (assetName != "")
                            break;
                    }
                }
            }
            if (!assetFound && assetName != "")
            {
                Debug.LogWarning("Dynamic" + typeString + "Library could not simulate the loading of " + assetName + " from any AssetBundles");
            }
            if (assetsToReturn.Count > 0 && callback != null)
            {
                callback(assetsToReturn.ToArray());
            }
            if (dependencies.Count > 0)
            {
                //we need to load ALL the assets from every Assetbundle that has a dependency in it.
                List<string> AssetBundlesToFullyLoad = new List<string>();
                for (int i = 0; i < assetBundleNamesArray.Length; i++)
                {
                    var allAssetBundlePaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleNamesArray[i]);
                    bool processed = false;
                    for (int ii = 0; ii < allAssetBundlePaths.Length; ii++)
                    {
                        for (int di = 0; di < dependencies.Count; di++)
                        {
                            if (allAssetBundlePaths[ii] == dependencies[di])
                            {
                                if (!AssetBundlesToFullyLoad.Contains(assetBundleNamesArray[i]))
                                {
                                    AssetBundlesToFullyLoad.Add(assetBundleNamesArray[i]);
                                }
                                processed = true;
                                break;
                            }
                        }
                        if (processed) break;
                    }
                }
                foreach (string assetBundleName in AssetBundlesToFullyLoad)
                {
                    var allAssetBundlePaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
                    for (int ai = 0; ai < allAssetBundlePaths.Length; ai++)
                    {
                        UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(allAssetBundlePaths[ai]);
                        //we actually only seem to need to do DCS stuff...
                        if (obj.GetType() == typeof(UMATextRecipe))
                        {
                            if (requestingUMA as UMACharacterSystem.DynamicCharacterAvatar)
                            {
                                (requestingUMA as UMACharacterSystem.DynamicCharacterAvatar).dynamicCharacterSystem.AddRecipe(obj as UMATextRecipe);
                            }
                        }
                    }
                }
                if (requestingUMA as UMACharacterSystem.DynamicCharacterAvatar)
                {
                    (requestingUMA as UMACharacterSystem.DynamicCharacterAvatar).dynamicCharacterSystem.Refresh();
                }
            }
            return assetFound;
        }
#endif
        
#if UNITY_EDITOR
        public void SimulateLoadAssetBundle(string assetBundleToLoad)
        {
            var allAssetBundlePaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleToLoad);
            UMACharacterSystem.DynamicCharacterSystem thisDCS = null;
            for (int i = 0; i < allAssetBundlePaths.Length; i++)
            {
                UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(allAssetBundlePaths[i]);
                //we could really do with UMAContext having a ref for DynamicCharacterSystem
                thisDCS = GameObject.Find("DynamicCharacterSystem").GetComponent<UMACharacterSystem.DynamicCharacterSystem>();
                if (obj.GetType() == typeof(UMATextRecipe))
                {
                    if (thisDCS)
                    {
                        thisDCS.AddRecipe(obj as UMATextRecipe);
                    }
                }
            }
            if (thisDCS)
            {
                thisDCS.Refresh();
            }
        }
#endif
        /// <summary>
        /// Splits the 'ResourcesFolderPath(s)' and 'AssetBundleNamesToSearch' fields up by comma if the field is using that functionality...
        /// </summary>
        /// <param name="searchString"></param>
        /// <returns></returns>
        string[] SearchStringToArray(string searchString = "")
        {
            string[] searchArray;
            if (searchString == "")
            {
                searchArray = new string[] { "" };
            }
            else
            {
                searchString.Replace(" ,", ",").Replace(", ", ",");
                if (searchString.IndexOf(",") == -1)
                {
                    searchArray = new string[1] { searchString };
                }
                else
                {
                    searchArray = searchString.Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
            return searchArray;
        }

#endregion

#region SPECIAL TYPES
       //DownloadingAssetsList and DownloadingAssetItem moved into their own scripts to make this one a bit more manageable!        
#endregion
    }
}
