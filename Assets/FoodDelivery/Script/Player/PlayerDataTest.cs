using UnityEngine;
using System;
public class PlayerDataTest : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    PlayerData test = new PlayerData();
    void Start()
    {
        test.AddItem("Pho", 2);
        test.AddItem("Pho", 2);
        Debug.Log(test.inventory.Count);
        Debug.Log(test.inventory[0].itemId);
        Debug.Log(test.inventory[0].amount);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
