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
    public GameObject DnaPrefab;
    public GameObject LabelPrefab;
    public GameObject GeneralHelpText;
    public GameObject WardrobeHelpText;
    public GameObject ColorsHelpText;
    public GameObject DnaHelpText;

    /// <summary>
    /// Remove any controls from the panels
    /// </summary>
    private void Cleanup()
    {
        GeneralHelpText.SetActive(false);
        DnaHelpText.SetActive(false);
        WardrobeHelpText.SetActive(false);
        ColorsHelpText.SetActive(false);

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
        if (GeneralHelpText.activeSelf)
        {
            GeneralHelpText.SetActive(false);
        }
        else
        {
            Cleanup();
            GeneralHelpText.SetActive(true);
        }
    }

    public void WardrobeHelpClick()
    {
        if (WardrobeHelpText.activeSelf)
        {
            WardrobeHelpText.SetActive(false);
        }
        else
        {
            Cleanup();
            WardrobeHelpText.SetActive(true);
        }
    }

    public void ColorsHelpClick()
    {
        if (ColorsHelpText.activeSelf)
        {
            ColorsHelpText.SetActive(false);
        }
        else
        {
            Cleanup();
            ColorsHelpText.SetActive(true);
        }
    }

    public void DNAHelpClick()
    {
        if (DnaHelpText.activeSelf)
        {
            DnaHelpText.SetActive(false);
        }
        else
        {
            Cleanup();
            DnaHelpText.SetActive(true);
        }
    }
    /// <summary>
    /// DNA Button event Handler
    /// </summary>
    public void DnaClick()
    {
        Cleanup();
        List<DnaSetter> AllDNA = Avatar.GetDNA();
        foreach(DnaSetter ds in AllDNA)
        {
            // create a button. 
            // set set the dna setter on it.
            GameObject go = GameObject.Instantiate(DnaPrefab);
            DNAHandler ch = go.GetComponent<DNAHandler>();
            ch.Setup(Avatar, ds, WardrobePanel);

            Text txt = go.GetComponentInChildren<Text>();
            txt.text = ds.Name;
            go.transform.SetParent(SlotPanel.transform);
        }
    }

    /// <summary>
    /// Colors Button event handler
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
