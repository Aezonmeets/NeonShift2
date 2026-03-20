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
    public string date;         
    public int    avatarIndex;  
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
    static Sprite[] _cachedAvatarSprites = new Sprite[8];

    [HideInInspector] public Sprite[] avatarSprites
    {
        get  => _cachedAvatarSprites;
        set  { _cachedAvatarSprites = value; }
    }

    public void SetAvatarSprites(Sprite[] sprites)
    {
        if (sprites == null) return;
        for (int i = 0; i < Mathf.Min(sprites.Length, 8); i++)
            _cachedAvatarSprites[i] = sprites[i];
    }

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
    }

    // ── PUBLIC API ────────────────────────────────────────────────────────

    public void TrySubmit(string mode, int score, int maxCombo, float accuracy)
    {
        string playerName = PlayerPrefs.GetString("PlayerName", "PLAYER");
        if (playerName.Length > 12) playerName = playerName.Substring(0, 12);

        int avatarIdx = PlayerPrefs.GetInt("AvatarIndex", 0);
        int existingIndex = entries.FindIndex(e => e.name == playerName && e.mode == mode);

        if (existingIndex >= 0)
        {
            if (score > entries[existingIndex].score)
            {
                var entry = entries[existingIndex];
                entry.score = score;
                entry.maxCombo = maxCombo;
                entry.accuracy = $"{accuracy:F1}%";
                entry.date = DateTime.Now.ToString("MM/dd HH:mm");
                entry.avatarIndex = avatarIdx;
                entries[existingIndex] = entry; 
            }
        }
        else
        {
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
        }

        // GUARANTEE: Force Sort strictly descending
        entries.Sort((a, b) => b.score.CompareTo(a.score));
        
        if (entries.Count > MAX_ENTRIES)
            entries.RemoveRange(MAX_ENTRIES, entries.Count - MAX_ENTRIES);

        Save();
    }

    public void TrySubmit(string mode, int score, float accuracy)
        => TrySubmit(mode, score, 0, accuracy);

    public List<LeaderboardEntry> GetAll() 
    {
        var list = new List<LeaderboardEntry>(entries);
        // Guarantee sorted on fetch
        list.Sort((a, b) => b.score.CompareTo(a.score));
        return list;
    }

    public List<LeaderboardEntry> GetForMode(string mode)
    {
        var list = new List<LeaderboardEntry>();
        foreach (var e in entries)
            if (string.Equals(e.mode, mode, StringComparison.OrdinalIgnoreCase))
                list.Add(e);
                
        // Guarantee sorted descending by score natively
        list.Sort((a, b) => b.score.CompareTo(a.score));
        return list;
    }

    public void ReloadFromDisk() { Load(); }

    public void ClearAll()
    {
        entries.Clear();
        Save();
    }

    // ── PERSISTENCE ───────────────────────────────────────────────────────
    void Save()
    {
        string json = JsonUtility.ToJson(new LBData { entries = entries.ToArray() });
        PlayerPrefs.SetString(KEY, json);
        PlayerPrefs.Save();
    }

    void Load()
    {
        string json = PlayerPrefs.GetString(KEY, "");
        if (string.IsNullOrEmpty(json))
            json = PlayerPrefs.GetString("Leaderboard_v1", "");

        entries.Clear();
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var data = JsonUtility.FromJson<LBData>(json);
                if (data?.entries != null)
                {
                    entries = new List<LeaderboardEntry>(data.entries);
                    // FORCE sort immediately on load to prevent any past desync bugs from carrying over
                    entries.Sort((a, b) => b.score.CompareTo(a.score));
                }
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