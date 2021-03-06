﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class exampleUI : MonoBehaviour
{
    //This script will handle everything related to dialogue interface
    //It will use a VIDE_Data component to load dialogues and retrieve node data
    //to draw the text for the dialogue

    [System.NonSerialized]
    public VIDE_Data dialogue; //All you need for everything to work is to have a variable of this type in your custom UI script

    //These are just references to UI components in the scene
    public UnityEngine.UI.Text npcText;
    public UnityEngine.UI.Text npcName;
    public UnityEngine.UI.Text playerText;
    public GameObject itemText;
    public GameObject uiContainer;

    //We'll use these later
    bool gotItem = false;
    bool animatingText = false;

    //We'll be using this to store the current player dialogue options
    private List<UnityEngine.UI.Text> currentOptions = new List<UnityEngine.UI.Text>();

    //Here I'm assigning the variable a new component of its required type
    void Start()
    {
        dialogue = gameObject.AddComponent<VIDE_Data>();
        //Remember you can also manually add the VIDE_Data script as a component in the Inspector, 
        //then drag&drop it on your 'dialogue' variable slot
    }

    //Show-hide stuff will happen here
    void Update()
    {
        //Lets just store the Node Data variable for the sake of fewer words
        var data = dialogue.nodeData;

        //We'll be disabling the entire UI if there aren't any loaded conversations
        if (!dialogue.isLoaded)
        {
            uiContainer.SetActive(false);
        }
        else
        {
            uiContainer.SetActive(true);
            //Player-NPC conversation text will be visible depending on whose turn it is
            playerText.transform.parent.gameObject.SetActive(data.currentIsPlayer);
            npcText.transform.parent.gameObject.SetActive(!data.currentIsPlayer);

            //Color the Player options. Blue for the selected one
            for (int i = 0; i < currentOptions.Count; i++)
            {
                currentOptions[i].color = Color.black;
                if (i == data.selectedOption) currentOptions[i].color = Color.blue;
            }

            //Scroll through Player dialogue options
            if (!data.pausedAction)
            {
                if (Input.GetKeyDown(KeyCode.S))
                {
                    if (data.selectedOption < currentOptions.Count - 1)
                        data.selectedOption++;
                }
                if (Input.GetKeyDown(KeyCode.W))
                {
                    if (data.selectedOption > 0)
                        data.selectedOption--;
                }
            }
        }
    }

    //This begins the conversation (Called by examplePlayer script)
    public void Begin(VIDE_Assign diagToLoad)
    {
        //First step is to call BeginDialogue, passing the required VIDE_Assign component 
        //This will store the first Node data in dialogue.nodeData
        dialogue.BeginDialogue(diagToLoad);

        var data = dialogue.nodeData;

        //Safety check in case a null dialogue was sent
        if (dialogue.assigned == null)
        {
            dialogue.EndDialogue();
            return;
        }

        //Let's clean the NPC text variables
        npcText.text = "";
        npcName.text = "";

        //If we already talked to this NPC, lets modify the start of the conversation
        //Of course, this will be true for particular cases
        //Here, we are using the dialogueName variable of the VIDE_Assign to compare
        if (dialogue.assigned.interactionCount > 0)
        {
            string name = dialogue.assigned.dialogueName;
            switch (name)
            {
                case "Crazy Cap":
                    if (gotItem)
                    dialogue.nodeData = dialogue.SetNode(17); //SetNode allows you to jump to whichever node you want
                    break;
            }
        }

        //Let's specifically check for dynamic text change
        if (!data.currentIsPlayer && data.extraData == "itemLookUp")
            ItemLookUp(data);

        //Everytime dialogue.nodeData gets updated, we update our UI with the new data
        UpdateUI();
    }

    //This will handle what happens when we want next message to appear 
    //(Also called by examplePlayer script)
    public void NextNode()
    {
        var data = dialogue.nodeData;

        //Let's not go forward if text is currently being animated, but let's speed it up.
        if (animatingText) { animatingText = false; return; }

        //Check to see if there's extraData and if so, we do stuff
        if (!data.currentIsPlayer && data.extraData != "" && !data.pausedAction)
        {
            bool needsReturn = DoAction(data);
            if (needsReturn)
            {
                UpdateUI();
                return;
            }
        }

        //Let's specifically check for dynamic text change
        if (!data.currentIsPlayer && data.extraData == "itemLookUp" && !data.pausedAction)
            ItemLookUp(data);      

        //This will update the dialogue.nodeData with the next Node's data
        dialogue.Next();

        UpdateUI();
    }

    //A method to update the actual GUI with the new data
    public void UpdateUI()
    {
        //VIDE_Data.nodeData is all we need to retrieve data
        var data = dialogue.nodeData;

        //VIDE provides a bool variable that will turn TRUE when we attempt to call Next() on a node that leads to nowhere.
        //You are free to end the conversation whenever you desire. Just read the variable and call EndDialogue() on the VIDE_Data.
        if (data.isEnd)
        {
            //This is called when we have reached the end of the conversation
            dialogue.EndDialogue(); //VIDE_Data will get reset along with nodeData.
            return;
        }

        //If this new Node is a Player Node, set the selectable comments offered by the Node
        if (data.currentIsPlayer)
        {
            SetOptions(data.playerComments);
        }
        //If it's an NPC Node, let's just update NPC's text
        else
        {
            if (data.npcComment.Length > 0)
                if (npcText.text != data.npcComment[data.npcCommentIndex])
                {
                    npcText.text = "";
                    StartCoroutine(AnimateText());
                }

            npcName.text = data.tag;
        }
    }

    //This uses the returned string[] from nodeData.playerComments to create the UIs for each comment
    //It first cleans, then it instantiates new options
    public void SetOptions(string[] opts)
    {
        //Destroy the current options
        foreach (UnityEngine.UI.Text op in currentOptions)
            Destroy(op.gameObject);

        //Clean the variable
        currentOptions = new List<UnityEngine.UI.Text>();

        //Create the options
        for (int i = 0; i < opts.Length; i++)
        {
            //This is just one way of creating endless options for Unity's UI class
            //Normally, you'd have an absolute number of options and you wouldn't have the need of doing this
            GameObject newOp = Instantiate(playerText.gameObject, playerText.transform.position, Quaternion.identity) as GameObject;
            newOp.SetActive(true);
            newOp.transform.SetParent(playerText.transform.parent, true);
            newOp.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 20 - (20 * i));
            newOp.GetComponent<UnityEngine.UI.Text>().text = opts[i];
            currentOptions.Add(newOp.GetComponent<UnityEngine.UI.Text>());
        }
    }

    //A method to handle extraDatas and do something with them. 
    //Note that it is easier to call methods now with the Action node.
    public bool DoAction(VIDE_Data.NodeData data)
    {
        bool didAction = false; 
        switch (data.extraData)
        {
            case "item":
                //npcCommentIndex refers to the current NPC's comment when there are many in a single Node (when you use <br>)
                didAction = true;
                if (data.npcCommentIndex == 1)
                {
                    if (!itemText.activeSelf)
                    {
                        itemText.SetActive(true);
                        gotItem = true;
                    }
                    else
                    {
                        itemText.SetActive(false);
                        dialogue.Next();
                    }
                }
                else
                {
                    dialogue.Next();
                }
                break;

            case "insanity":
                //This will override the Dialogue's Start Node and use this one instead
                didAction = true;
                dialogue.assigned.overrideStartNode = 16;
                dialogue.Next();
                break;
        }
        return didAction;
    }

    //This will replace any "[NAME]" with the name of the gameobject holding the VIDE_Assign
    void ItemLookUp(VIDE_Data.NodeData data)
    {
        if (data.npcCommentIndex == 0)
        {
            data.npcComment[data.npcCommentIndex] = data.npcComment[data.npcCommentIndex].Replace("[NAME]", dialogue.assigned.gameObject.name);
        }
    }

    //Very simple text animation, not optimal
    //Use StringBuilder for better performance
    public IEnumerator AnimateText()
    {

        var data = dialogue.nodeData;
        animatingText = true;
        string c = data.npcComment[data.npcCommentIndex];

        if (!data.currentIsPlayer)
        {
            while (npcText.text != c)
            {
                if (!animatingText) break;
                string letterToAdd = c[npcText.text.Length].ToString();
                npcText.text += letterToAdd; //Actual text updates here
                yield return new WaitForSeconds(0.02f);
            }
        }

        npcText.text = data.npcComment[data.npcCommentIndex]; //And here		
        animatingText = false;
    }
}
