using UnityEngine;
using UMACharacterSystem;
using System.Collections.Generic;
using UnityEngine.UI;

public class SlotHandler : MonoBehaviour
{
    public DynamicCharacterAvatar Avatar;
    public GameObject WardrobePanel;
    public GameObject WardrobeButtonPrefab;
    public string SlotName;



    public void Setup(DynamicCharacterAvatar avatar, string slotName, GameObject wardrobePanel)
    {
        Avatar = avatar;
        SlotName = slotName;
        WardrobePanel = wardrobePanel;
    }

    public void OnClick()
    {
        // Get the available UMATextRecipes for this slot.
        List<UMATextRecipe> SlotRecipes = Avatar.AvailableRecipes[SlotName];
        // Cleanup old buttons
        Cleanup();

        // Add a "Take off" button
        /*
        GameObject go = GameObject.Instantiate(WardrobeButtonPrefab);
        WardrobeHandler wh = go.GetComponent<WardrobeHandler>();
        wh.Setup(Avatar, null, SlotName);
        Text txt = go.GetComponentInChildren<Text>();
        txt.text = "Remove";
        go.transform.SetParent(WardrobePanel.transform);
        */
        AddButton("Remove", SlotName);

        // Find all the wardrobe items for the current slot, and create a button for them.
        foreach (UMATextRecipe utr in SlotRecipes)
        {
            AddButton(utr.name, SlotName, utr);
         /*   go = GameObject.Instantiate(WardrobeButtonPrefab);
            wh = go.GetComponent<WardrobeHandler>();
            wh.Setup(Avatar, utr, SlotName);
            go.transform.SetParent(WardrobePanel.transform); */
        }
    }

    private void AddButton(string theText, string SlotName, UMATextRecipe utr = null)
    {
        GameObject go = GameObject.Instantiate(WardrobeButtonPrefab);
        WardrobeHandler wh = go.GetComponent<WardrobeHandler>();
        wh.Setup(Avatar, utr, SlotName);
        Text txt = go.GetComponentInChildren<Text>();
        txt.text = theText;
        go.transform.SetParent(WardrobePanel.transform);
    }

    private void Cleanup()
    {
        if (WardrobePanel.transform.childCount > 0)
        {
            foreach (Transform t in WardrobePanel.transform)
            {
                GameObject.Destroy(t.gameObject);
            }
        }
    }
}
