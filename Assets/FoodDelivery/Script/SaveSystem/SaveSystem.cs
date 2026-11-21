using UnityEngine;
using System.IO;
public class SaveSystem
{
    private static string SaveFileName = "savegame.json";
    public static string SaveFilePath
    {
        get
        {
            return Path.Combine(Application.persistentDataPath, SaveFileName);
        }
    }
    public static bool HasSave()
    {
        return File.Exists(SaveFilePath);
    }

    public static void SaveGame(PlayerData data)
    {
        if (data == null)
        {
            Debug.Log("nothing to save");
            return;
        }
        SaveLocalOnly(data);
        FirebaseCloudSaveService.QueueSave(data);
    }

    public static void SaveLocalOnly(PlayerData data)
    {
        if (data == null)
        {
            return;
        }
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SaveFilePath, json);
        Debug.Log(SaveFilePath);
    }
    public static PlayerData LoadGame()
    {
        try
        {
            if (File.Exists(SaveFilePath))
            {
                string json = File.ReadAllText(SaveFilePath);
                PlayerData data = JsonUtility.FromJson<PlayerData>(json);
                if (data != null && !json.Contains("\"reputation\""))
                {
                    data.reputation = 50;
                }
                if (data != null && !json.Contains("\"stamina\""))
                {
                    data.stamina = 100f;
                }
                if (data != null && !json.Contains("\"businessRegistered\""))
                {
                    data.businessRegistered = true;
                }
                Debug.Log($"loaded player, money: {data.money}");
                return data;

            }
            else
            {
                Debug.Log("save file not found");
                return null;
            }
        }
        catch(System.Exception e)
        {
            Debug.LogError($"load failed: {e.Message}");
            return null;
        }

    }

    public static void DeleteGame()
    {
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
            Debug.Log("deleted successfully");
        }
        else
        {
            Debug.Log("no file to delete");
        }
    }
}
