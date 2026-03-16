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
    public int score;
    public int maxCombo;
    public string mode;
    public string accuracy;
    public string date;       // "MM/dd HH:mm"
}

public class LeaderboardManager : MonoBehaviour
{
    private static LeaderboardManager _instance;
    public static LeaderboardManager Instance
    {
        get
        {
            // LAZY INITIALIZATION: Fixes the bug where the manager doesn't exist on the first playthrough.
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<LeaderboardManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("LeaderboardManager");
                    _instance = go.AddComponent<LeaderboardManager>();
                }
            }
            return _instance;
        }
    }

    const int MAX_ENTRIES = 50;
    const string KEY = "Leaderboard_v2";

    List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

    void Awake()
    {
        // Enforce Singleton
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // ── PUBLIC API ───────────────────────────────────────────────────────
    public void TrySubmit(string mode, int score, int maxCombo, float accuracy)
    {
        // --- FIX: Grab EXACTLY what they typed, keep lowercase/uppercase, and allow 12 characters! ---
        string name = PlayerPrefs.GetString("PlayerName", "PLAYER");
        if (name.Length > 12) name = name.Substring(0, 12);

        entries.Add(new LeaderboardEntry
        {
            name = name,
            score = score,
            maxCombo = maxCombo,
            mode = mode,
            accuracy = $"{accuracy:F1}%",
            date = DateTime.Now.ToString("MM/dd HH:mm")
        });

        entries.Sort((a, b) => b.score.CompareTo(a.score));
        if (entries.Count > MAX_ENTRIES) entries.RemoveRange(MAX_ENTRIES, entries.Count - MAX_ENTRIES);

        Save();
        Debug.Log($"[Leaderboard] Successfully saved score: {score} for mode: {mode}");
    }

    public void TrySubmit(string mode, int score, float accuracy)
        => TrySubmit(mode, score, 0, accuracy);

    public List<LeaderboardEntry> GetAll() => entries;

    public List<LeaderboardEntry> GetForMode(string mode)
    {
        var list = new List<LeaderboardEntry>();
        foreach (var e in entries) if (e.mode == mode) list.Add(e);
        return list;
    }

    // ── PERSISTENCE ──────────────────────────────────────────────────────
    void Save()
    {
        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(new LBData { entries = entries.ToArray() }));
        PlayerPrefs.Save();
    }

    void Load()
    {
        string json = PlayerPrefs.GetString(KEY, "");
        if (string.IsNullOrEmpty(json)) json = PlayerPrefs.GetString("Leaderboard_v1", "");

        entries.Clear();
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var d = JsonUtility.FromJson<LBData>(json);
                if (d?.entries != null) entries = new List<LeaderboardEntry>(d.entries);
            }
            catch { entries = new List<LeaderboardEntry>(); }
        }
    }

    public void ClearAll() { entries.Clear(); Save(); }

    // ── SCENE NAVIGATION ─────────────────────────────────────────────────
    public static void OpenLeaderboardScene(string returnScene)
    {
        PlayerPrefs.SetString("LB_ReturnScene", returnScene);
        SceneManager.LoadScene("LeaderboardScene");
    }

    // ── SMALL INLINE UI (used on Game Over panel) ────────────────────────
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
            var e = list[i]; float y = 25f - i * 26f;
            Color rowCol = i == 0 ? new Color(1f, .92f, .15f) : i == 1 ? new Color(.8f, .8f, .8f) : i == 2 ? new Color(.85f, .55f, .25f) : Color.white;
            
            // Padded strings dynamically so the score always lines up nicely even with longer names
            string line = $"{(i + 1).ToString().PadRight(4)}{e.name.PadRight(14)}{e.score.ToString("N0").PadRight(13)}{e.accuracy}";
            Lbl(parent, line, 14, new Vector2(0, y), rowCol);
        }
    }

    static readonly Color CYAN = new Color(0f, 0.92f, 1f);

    static void Lbl(GameObject p, string txt, int sz, Vector2 pos, Color col, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("L"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, .5f); rt.anchorMax = new Vector2(1f, .5f);
        rt.anchoredPosition = new Vector2(0, pos.y); rt.sizeDelta = new Vector2(0, 30);
        rt.offsetMin = new Vector2(10, rt.offsetMin.y); rt.offsetMax = new Vector2(-10, rt.offsetMax.y);
        var tmp = go.AddComponent<TextMeshProUGUI>(); tmp.text = txt; tmp.fontSize = sz; tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = col; tmp.textWrappingMode = TextWrappingModes.NoWrap;
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