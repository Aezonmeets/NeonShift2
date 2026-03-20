using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System;

[System.Serializable]
public struct LeaderboardEntry
{
    public string name;
    public int    score;
    public int    maxCombo;
    public string mode;
    public string accuracy;
    public string date;         // "MM/dd HH:mm"
    public int    avatarIndex;  // 0-7, matches the slot the player chose in the main menu
}

public class LeaderboardManager : MonoBehaviour
{
    // ── SINGLETON ─────────────────────────────────────────────────────────
    private static LeaderboardManager _instance;
    public static LeaderboardManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<LeaderboardManager>();
                if (_instance == null)
                {
                    var go = new GameObject("LeaderboardManager");
                    _instance = go.AddComponent<LeaderboardManager>();
                }
            }
            return _instance;
        }
    }

    const int    MAX_ENTRIES = 50;
    const string KEY         = "Leaderboard_v2";

    List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

    // ── AVATAR SPRITE CACHE ───────────────────────────────────────────────
    // Static so it survives even if the MonoBehaviour instance is recreated.
    // MainMenuManager.Build() calls SetAvatarSprites() to populate this.
    // LeaderboardScene reads from it via GetCachedAvatarSprite().
    static Sprite[] _cachedAvatarSprites = new Sprite[8];

    // Non-static backing kept for compatibility with old code that wrote to this directly.
    [HideInInspector] public Sprite[] avatarSprites
    {
        get  => _cachedAvatarSprites;
        set  { _cachedAvatarSprites = value; }
    }

    /// <summary>
    /// Called by MainMenuManager.Build() to register the 8 inspector sprites.
    /// Uses a static cache so the sprites survive across scene loads.
    /// </summary>
    public void SetAvatarSprites(Sprite[] sprites)
    {
        if (sprites == null) return;
        for (int i = 0; i < Mathf.Min(sprites.Length, 8); i++)
            _cachedAvatarSprites[i] = sprites[i];
    }

    /// <summary>Returns the cached avatar sprite for slot index (0-7).</summary>
    public static Sprite GetCachedAvatarSprite(int index)
    {
        if (_cachedAvatarSprites == null || index < 0 || index >= _cachedAvatarSprites.Length)
            return null;
        return _cachedAvatarSprites[index];
    }

    // ── LIFECYCLE ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
        Debug.Log($"[LeaderboardManager] Awake — loaded {entries.Count} entries from disk.");
    }

    // ── PUBLIC API ────────────────────────────────────────────────────────

    /// <summary>
    /// Submit a score entry. Call this from GameManager when the round ends.
    /// Example: LeaderboardManager.Instance.TrySubmit("Easy", score, maxCombo, accuracy);
    /// </summary>
    public void TrySubmit(string mode, int score, int maxCombo, float accuracy)
    {
        string playerName = PlayerPrefs.GetString("PlayerName", "PLAYER");
        if (playerName.Length > 12) playerName = playerName.Substring(0, 12);

        int avatarIdx = PlayerPrefs.GetInt("AvatarIndex", 0);

        var entry = new LeaderboardEntry
        {
            name        = playerName,
            score       = score,
            maxCombo    = maxCombo,
            mode        = mode,
            accuracy    = $"{accuracy:F1}%",
            date        = DateTime.Now.ToString("MM/dd HH:mm"),
            avatarIndex = avatarIdx
        };

        entries.Add(entry);
        entries.Sort((a, b) => b.score.CompareTo(a.score));
        if (entries.Count > MAX_ENTRIES)
            entries.RemoveRange(MAX_ENTRIES, entries.Count - MAX_ENTRIES);

        Save();
        Debug.Log($"[LeaderboardManager] Score saved — Player: {playerName} | Mode: {mode} | Score: {score} | Accuracy: {accuracy:F1}% | Avatar: {avatarIdx}");
    }

    /// <summary>Overload without maxCombo.</summary>
    public void TrySubmit(string mode, int score, float accuracy)
        => TrySubmit(mode, score, 0, accuracy);

    public List<LeaderboardEntry> GetAll() => new List<LeaderboardEntry>(entries);

    public List<LeaderboardEntry> GetForMode(string mode)
    {
        var list = new List<LeaderboardEntry>();
        foreach (var e in entries)
            if (string.Equals(e.mode, mode, StringComparison.OrdinalIgnoreCase))
                list.Add(e);
        return list;
    }

    /// <summary>
    /// Force a fresh read from PlayerPrefs.
    /// Called by LeaderboardScene on Start() to guarantee the latest data is shown.
    /// </summary>
    public void ReloadFromDisk()
    {
        Load();
        Debug.Log($"[LeaderboardManager] ReloadFromDisk — {entries.Count} entries available.");
    }

    public void ClearAll()
    {
        entries.Clear();
        Save();
        Debug.Log("[LeaderboardManager] All entries cleared.");
    }

    // ── PERSISTENCE ───────────────────────────────────────────────────────
    void Save()
    {
        string json = JsonUtility.ToJson(new LBData { entries = entries.ToArray() });
        PlayerPrefs.SetString(KEY, json);
        PlayerPrefs.Save();
        Debug.Log($"[LeaderboardManager] Saved {entries.Count} entries to PlayerPrefs.");
    }

    void Load()
    {
        string json = PlayerPrefs.GetString(KEY, "");

        // Migrate from v1 key if v2 has nothing
        if (string.IsNullOrEmpty(json))
            json = PlayerPrefs.GetString("Leaderboard_v1", "");

        entries.Clear();
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var data = JsonUtility.FromJson<LBData>(json);
                if (data?.entries != null)
                    entries = new List<LeaderboardEntry>(data.entries);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardManager] Failed to parse saved data: {ex.Message}");
                entries = new List<LeaderboardEntry>();
            }
        }
    }

    // ── SCENE NAVIGATION ──────────────────────────────────────────────────
    public static void OpenLeaderboardScene(string returnScene)
    {
        PlayerPrefs.SetString("LB_ReturnScene", returnScene);
        SceneManager.LoadScene("LeaderboardScene");
    }

    // ── INLINE LEADERBOARD UI (Game Over panel) ───────────────────────────
    public void BuildLeaderboardUI(GameObject parent, string filterMode = null)
    {
        foreach (Transform c in parent.transform) Destroy(c.gameObject);
        var list = filterMode != null ? GetForMode(filterMode) : GetAll();

        Lbl(parent, "LEADERBOARD", 24, new Vector2(0, 95), CYAN, FontStyles.Bold);

        if (list.Count == 0)
        {
            Lbl(parent, "No scores yet!", 16, new Vector2(0, 40), new Color(.6f, .7f, .8f));
            return;
        }

        Lbl(parent, "#    NAME            SCORE        ACC", 12, new Vector2(0, 65), new Color(.5f, .6f, .7f));
        Div(parent, new Vector2(0, 50), CYAN);

        for (int i = 0; i < Mathf.Min(list.Count, 5); i++)
        {
            var e = list[i];
            float y = 25f - i * 26f;
            Color rowCol = i == 0 ? new Color(1f, .92f, .15f)
                         : i == 1 ? new Color(.8f, .8f, .8f)
                         : i == 2 ? new Color(.85f, .55f, .25f)
                         : Color.white;
            string line = $"{(i + 1).ToString().PadRight(4)}{e.name.PadRight(14)}{e.score.ToString("N0").PadRight(13)}{e.accuracy}";
            Lbl(parent, line, 14, new Vector2(0, y), rowCol);
        }
    }

    // ── UI HELPERS ────────────────────────────────────────────────────────
    static readonly Color CYAN = new Color(0f, 0.92f, 1f);

    static void Lbl(GameObject p, string txt, int sz, Vector2 pos, Color col, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("L"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, .5f); rt.anchorMax = new Vector2(1f, .5f);
        rt.anchoredPosition = new Vector2(0, pos.y); rt.sizeDelta = new Vector2(0, 30);
        rt.offsetMin = new Vector2(10, rt.offsetMin.y); rt.offsetMax = new Vector2(-10, rt.offsetMax.y);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = txt; tmp.fontSize = sz; tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = col;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    static void Div(GameObject p, Vector2 pos, Color col)
    {
        var go = new GameObject("D"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, .5f); rt.anchorMax = new Vector2(1f, .5f);
        rt.anchoredPosition = new Vector2(0, pos.y); rt.sizeDelta = new Vector2(0, 2f);
        rt.offsetMin = new Vector2(10, rt.offsetMin.y); rt.offsetMax = new Vector2(-10, rt.offsetMax.y);
        go.AddComponent<Image>().color = new Color(col.r, col.g, col.b, .35f);
    }

    [System.Serializable] class LBData { public LeaderboardEntry[] entries; }
}