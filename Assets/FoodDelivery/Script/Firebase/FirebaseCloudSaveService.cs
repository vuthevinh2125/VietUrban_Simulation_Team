using System;
using System.Collections;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseCloudSaveService : MonoBehaviour
{
    [SerializeField] private string databaseUrl;
    private static FirebaseCloudSaveService instance;
    private Coroutine pendingSave;

    [Serializable]
    public class LeaderboardEntry
    {
        public string uid;
        public string username;
        public int money;
    }

    public static void LoadLeaderboard(Action<LeaderboardEntry[]> completed)
    {
        if (instance == null || string.IsNullOrWhiteSpace(instance.databaseUrl))
        {
            completed?.Invoke(Array.Empty<LeaderboardEntry>());
            return;
        }
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null)
        {
            completed?.Invoke(Array.Empty<LeaderboardEntry>());
            return;
        }
        instance.StartCoroutine(instance.LoadLeaderboardRoutine(user, completed));
    }

    public static void QueueLeaderboardEntry(PlayerData data)
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (instance == null || data == null || user == null || string.IsNullOrWhiteSpace(instance.databaseUrl)) return;
        instance.StartCoroutine(instance.WriteLeaderboardRoutine(user, data));
    }

    private IEnumerator LoadLeaderboardRoutine(FirebaseUser user, Action<LeaderboardEntry[]> completed)
    {
        Task<string> tokenTask = user.TokenAsync(false);
        while (!tokenTask.IsCompleted) yield return null;
        if (tokenTask.IsFaulted || tokenTask.IsCanceled)
        {
            completed?.Invoke(Array.Empty<LeaderboardEntry>());
            yield break;
        }

        string url = databaseUrl.TrimEnd('/') + "/leaderboard.json?auth=" + UnityWebRequest.EscapeURL(tokenTask.Result);
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("Leaderboard download failed: " + request.error);
                completed?.Invoke(Array.Empty<LeaderboardEntry>());
                yield break;
            }

            string json = request.downloadHandler.text;
            var entries = new System.Collections.Generic.List<LeaderboardEntry>();
            MatchCollection matches = Regex.Matches(json, "\\\"(?<uid>[^\\\"]+)\\\"\\s*:\\s*\\{(?<body>[^{}]*)\\}");
            foreach (Match match in matches)
            {
                string body = match.Groups["body"].Value;
                string username = Regex.Match(body, "\\\"username\\\"\\s*:\\s*(?<value>\\\"(?:\\\\.|[^\\\"])*\\\")").Groups["value"].Value;
                string moneyValue = Regex.Match(body, "\\\"money\\\"\\s*:\\s*(?<value>-?\\d+)").Groups["value"].Value;
                if (string.IsNullOrEmpty(moneyValue)) continue;
                string wrapped = "{\"username\":" + username + "}";
                string decodedName = JsonUtility.FromJson<NameWrapper>(wrapped)?.username;
                int.TryParse(moneyValue, out int money);
                entries.Add(new LeaderboardEntry { uid = match.Groups["uid"].Value, username = decodedName, money = money });
            }
            completed?.Invoke(entries.ToArray());
        }
    }

    [Serializable] private class NameWrapper { public string username; }

    private IEnumerator WriteLeaderboardRoutine(FirebaseUser user, PlayerData data)
    {
        Task<string> tokenTask = user.TokenAsync(false);
        while (!tokenTask.IsCompleted) yield return null;
        if (tokenTask.IsFaulted || tokenTask.IsCanceled) yield break;

        LeaderboardEntry entry = new LeaderboardEntry
        {
            uid = user.UserId,
            username = string.IsNullOrWhiteSpace(user.DisplayName) ? "Player" : user.DisplayName,
            money = data.money
        };
        byte[] body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(entry));
        string url = databaseUrl.TrimEnd('/') + "/leaderboard/" + UnityWebRequest.EscapeURL(user.UserId) + ".json?auth=" + UnityWebRequest.EscapeURL(tokenTask.Result);
        using (UnityWebRequest request = new UnityWebRequest(url, "PUT"))
        {
            request.timeout = 15;
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) Debug.LogWarning("Leaderboard upload failed: " + request.error);
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void LoadForUser(FirebaseUser user, Action<PlayerData> completed, bool startFreshIfMissing = false)
    {
        if (instance == null || user == null || string.IsNullOrWhiteSpace(instance.databaseUrl))
        {
            completed(null);
            return;
        }
        instance.StartCoroutine(instance.LoadRoutine(user, completed, startFreshIfMissing));
    }

    public static void QueueSave(PlayerData data)
    {
        if (instance == null || data == null)
        {
            return;
        }
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null || string.IsNullOrWhiteSpace(instance.databaseUrl))
        {
            return;
        }
        if (instance.pendingSave != null)
        {
            instance.StopCoroutine(instance.pendingSave);
        }
        instance.pendingSave = instance.StartCoroutine(instance.SaveRoutine(user, data, null));
    }

    public static void SaveImmediately(PlayerData data, Action<bool> completed)
    {
        if (instance == null || data == null)
        {
            Debug.LogError("Cloud save unavailable: service instance or player data is missing.");
            completed(false);
            return;
        }
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null || string.IsNullOrWhiteSpace(instance.databaseUrl))
        {
            Debug.LogError(user == null
                ? "Cloud save unavailable: no Firebase user is signed in."
                : "Cloud save unavailable: Realtime Database URL is empty.");
            completed(false);
            return;
        }
        if (instance.pendingSave != null)
        {
            instance.StopCoroutine(instance.pendingSave);
        }
        instance.pendingSave = instance.StartCoroutine(instance.SaveRoutine(user, data, completed));
    }

    private IEnumerator LoadRoutine(FirebaseUser user, Action<PlayerData> completed, bool startFreshIfMissing)
    {
        Task<string> tokenTask = user.TokenAsync(false);
        while (!tokenTask.IsCompleted)
        {
            yield return null;
        }
        if (tokenTask.IsFaulted || tokenTask.IsCanceled)
        {
            completed(null);
            yield break;
        }

        if (startFreshIfMissing)
        {
            PlayerData freshData = new PlayerData();
            SaveSystem.SaveLocalOnly(freshData);
            StartCoroutine(SaveRoutine(user, freshData, null));
            completed(freshData);
            yield break;
        }

        string url = BuildSaveUrl(user.UserId, tokenTask.Result);
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("cloud save download failed");
                completed(null);
                yield break;
            }

            string json = request.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(json) || json == "null")
            {
                PlayerData localData = SaveSystem.LoadGame();
                if (localData == null)
                {
                    localData = new PlayerData();
                }
                SaveSystem.SaveLocalOnly(localData);
                StartCoroutine(SaveRoutine(user, localData, null));
                completed(localData);
                yield break;
            }

            PlayerData cloudData = JsonUtility.FromJson<PlayerData>(json);
            if (cloudData == null)
            {
                completed(null);
                yield break;
            }
            SaveSystem.SaveLocalOnly(cloudData);
            QueueLeaderboardEntry(cloudData);
            completed(cloudData);
        }
    }

    private IEnumerator SaveRoutine(FirebaseUser user, PlayerData data, Action<bool> completed)
    {
        Task<string> tokenTask = user.TokenAsync(false);
        while (!tokenTask.IsCompleted)
        {
            yield return null;
        }
        if (tokenTask.IsFaulted || tokenTask.IsCanceled)
        {
            pendingSave = null;
            if (completed != null) completed(false);
            yield break;
        }

        string json = JsonUtility.ToJson(data);
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
        using (UnityWebRequest request = new UnityWebRequest(BuildSaveUrl(user.UserId, tokenTask.Result), "PUT"))
        {
            request.timeout = 15;
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Cloud save upload failed. HTTP " + request.responseCode + ": " + request.error +
                    " Response: " + request.downloadHandler.text);
                pendingSave = null;
                if (completed != null) completed(false);
                yield break;
            }
        }
        pendingSave = null;
        QueueLeaderboardEntry(data);
        if (completed != null) completed(true);
    }

    private string BuildSaveUrl(string userId, string token)
    {
        string root = databaseUrl.TrimEnd('/');
        return root + "/players/" + UnityWebRequest.EscapeURL(userId) + "/save.json?auth=" + UnityWebRequest.EscapeURL(token);
    }
}
