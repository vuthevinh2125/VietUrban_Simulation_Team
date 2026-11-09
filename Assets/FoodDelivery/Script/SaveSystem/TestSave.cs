using UnityEngine;

public class TestSave : MonoBehaviour
{
    PlayerData testData = new PlayerData();

    void Start()
    {
        Debug.Log(SaveSystem.SaveFilePath);
        Debug.Log(SaveSystem.HasSave());
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("save pressed");
            testData.AddItem("banh_pho", 500);
            testData.AddItem("banh_pho", 500);
            testData.money = 500;
            SaveSystem.SaveGame(testData);

        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("load pressed");
          PlayerData result = SaveSystem.LoadGame();
          if (result == null)
            {
                Debug.Log("fAIL");
            }
            else
            {
                Debug.Log(result.inventory.Count);
                Debug.Log(result.money);
                foreach (var item in result.inventory)
                {
                    Debug.Log(item.itemId);
                    Debug.Log(item.amount);
                }
            }
        }
    }
}
