using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class LiveScoreManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    static LiveScoreManager _instance;
    public static LiveScoreManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("LiveScoreManager");
                _instance = go.AddComponent<LiveScoreManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public read-only properties ───────────────────────────────────────────

    public long PlayerScore { get; private set; } = 0L;
    public int PlayerRank { get; private set; } = 0; 

    string _currentPlayerName = "Guest";

    // ── API for Game Logic ────────────────────────────────────────────────────

    public void RegisterPlayer(string playerName, long startingScore = 0)
    {
        _currentPlayerName = string.IsNullOrEmpty(playerName) ? "Guest" : playerName;
        PlayerScore = startingScore;
        RecalculateRank();
    }

    public void AddScore(long points)
    {
        PlayerScore += points;
        RecalculateRank();
    }

    public void AddScore(long points, HitResult result) { AddScore(points); }
    public void AddScore(long points, string text) { AddScore(points); } 
    public void AddScore(long points, int multiplier) { AddScore(points); }
    public void AddScore(long points, float multiplier) { AddScore(points); }

    public void TrySubmit(string mode, int score, int maxCombo, float accuracy)
    {
        CallLeaderboardSafe(mode, score, maxCombo, accuracy, accuracy.ToString("0.0") + "%");
    }

    public void TrySubmit(string mode, int score, int maxCombo, string accuracy)
    {
        float accFloat = 0f;
        float.TryParse(accuracy.Replace("%", ""), out accFloat);
        CallLeaderboardSafe(mode, score, maxCombo, accFloat, accuracy);
    }

    void CallLeaderboardSafe(string mode, int score, int maxCombo, float accFloat, string accString)
    {
        if (LeaderboardManager.Instance == null) return;

        var floatMethod = typeof(LeaderboardManager).GetMethod("TrySubmit", new System.Type[] { typeof(string), typeof(int), typeof(int), typeof(float) });
        if (floatMethod != null)
        {
            floatMethod.Invoke(LeaderboardManager.Instance, new object[] { mode, score, maxCombo, accFloat });
            return;
        }
        
        var stringMethod = typeof(LeaderboardManager).GetMethod("TrySubmit", new System.Type[] { typeof(string), typeof(int), typeof(int), typeof(string) });
        if (stringMethod != null)
        {
            stringMethod.Invoke(LeaderboardManager.Instance, new object[] { mode, score, maxCombo, accString });
        }
    }

    // ── Internal Logic ────────────────────────────────────────────────────────

    void RecalculateRank()
    {
        if (PlayerScore <= 0)
        {
            PlayerRank = 0; // Show the dash on the HUD until they score points
            return;
        }

        int rank = 1;
        
        // Dynamically fetch from the actual leaderboard depending on the active game mode
        if (LeaderboardManager.Instance != null)
        {
            int modeIdx = PlayerPrefs.GetInt("SelectedMode", 0);
            string[] modes = { "Easy", "Medium", "Hard", "Endless" };
            string currentMode = modes[Mathf.Clamp(modeIdx, 0, modes.Length - 1)];

            var lbList = LeaderboardManager.Instance.GetForMode(currentMode);
            foreach (var entry in lbList)
            {
                if (entry.name == _currentPlayerName) continue; // Don't rank against your own past score
                if (entry.score > PlayerScore) rank++;
            }
        }
        
        PlayerRank = rank;
    }
}