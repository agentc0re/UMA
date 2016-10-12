using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UMACharacterSystem;

public class SampleCode : MonoBehaviour {

    public DynamicCharacterAvatar Avatar;
    public GameObject SlotPrefab;
    public GameObject WardrobePrefab;
    public GameObject SlotPanel;
    public GameObject WardrobePanel;
    public GameObject ColorPrefab;
    public GameObject HelpText;

    /// <summary>
    /// Remove any buttons from the panels
    /// </summary>
    private void Cleanup()
    {
        foreach (Transform t in SlotPanel.transform)
        {
            GameObject.Destroy(t.gameObject);
        }
        foreach (Transform t in WardrobePanel.transform)
        {
            GameObject.Destroy(t.gameObject);
        }
    }

    public void HelpClick()
    {
        if (HelpText.activeSelf)
        {
            HelpText.SetActive(false);
        }
        else
        {
            Cleanup();
            HelpText.SetActive(true);
        }
    }
    
    /// <summary>
    /// Colors button event handler
    /// </summary>
    public void ColorsClick()
    {
        // get all the shared colors.
        // get 
        Cleanup();

        foreach(UMA.OverlayColorData ocd in Avatar.CurrentSharedColors )
        {
            GameObject go = GameObject.Instantiate(ColorPrefab);
            AvailableColorsHandler ch = go.GetComponent<AvailableColorsHandler>();
            ch.Setup(Avatar, ocd.name, WardrobePanel);

            Text txt = go.GetComponentInChildren<Text>();
            txt.text = ocd.name;
            go.transform.SetParent(SlotPanel.transform);
        }
    }

    /// <summary>
    /// Wardrobe Button event handler
    /// </summary>
    public void WardrobeClick()
    {
        Cleanup();

        List<string> Slots = Avatar.CurrentWardrobeSlots;
        Dictionary<string, List<UMATextRecipe>> recipes = Avatar.AvailableRecipes;

        foreach (string s in recipes.Keys)
        {
            GameObject go = GameObject.Instantiate(SlotPrefab);
            SlotHandler sh = go.GetComponent<SlotHandler>();
            sh.Setup(Avatar, s,WardrobePanel);
            Text txt = go.GetComponentInChildren<Text>();
            txt.text = s;
            go.transform.SetParent(SlotPanel.transform);
        }
    }
}
