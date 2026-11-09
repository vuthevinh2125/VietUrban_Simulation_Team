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
            Debug.LogWarning("nothing to save");
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
                Debug.Log($"loaded player, money: {data.money}");
                return data;

            }
            else
            {
                Debug.LogWarning("save file not found");
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
            Debug.LogWarning("no file to delete");
        }
    }
}
