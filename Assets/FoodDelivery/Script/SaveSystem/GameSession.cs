using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSession
{
    private static PlayerData current;
    public static PlayerData Current
    {
        get
        {
            if (current == null)
            {
                if (SaveSystem.HasSave() == true)
                {
                    current = SaveSystem.LoadGame();
                }
                if (current == null)
                {
                    current = new PlayerData();
                }
            }
            return current;
        }
    }

    public static void StartNewGame()
    {
        SaveSystem.DeleteGame();
        current = new PlayerData();
    }

    public static void SetCurrent(PlayerData data)
    {
        if (data != null)
        {
            current = data;
        }
    }

    public static void RestartCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        Time.timeScale = 1f;
        StartNewGame();
        // Persist fresh defaults locally, then replace any in-flight cloud save
        // and wait for the fresh profile upload before reloading the scene.
        SaveSystem.SaveLocalOnly(Current);
        FirebaseCloudSaveService.SaveImmediately(Current, delegate(bool saved)
        {
            if (!saved)
            {
                Debug.LogWarning("Fresh profile is saved locally, but cloud sync failed.");
            }
            SceneManager.LoadScene(sceneName);
        });
    }
}
