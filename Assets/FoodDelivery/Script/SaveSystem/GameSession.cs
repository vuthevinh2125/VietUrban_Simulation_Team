using UnityEngine;

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
}
