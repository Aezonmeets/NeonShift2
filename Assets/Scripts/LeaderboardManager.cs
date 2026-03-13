using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[System.Serializable]
public struct LeaderboardEntry
{
    public string name;
    public int score;
    public string mode;
    public string accuracy;
}

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    const int MAX_ENTRIES = 10;
    const string KEY = "Leaderboard_v1";

    static readonly Color CYAN = new Color(0f, 0.92f, 1f);
    static readonly Color MAGENTA = new Color(1f, 0.15f, 0.75f);

    List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // ── PUBLIC API ──────────────────────────────────────────────────────
    public void TrySubmit(string mode, int score, float accuracy)
    {
        string name = PlayerPrefs.GetString("PlayerName", "PLAYER").ToUpper();
        if (name.Length > 8) name = name.Substring(0, 8);

        entries.Add(new LeaderboardEntry { name = name, score = score, mode = mode, accuracy = $"{accuracy:F1}%" });
        entries.Sort((a, b) => b.score.CompareTo(a.score));
        if (entries.Count > MAX_ENTRIES) entries.RemoveRange(MAX_ENTRIES, entries.Count - MAX_ENTRIES);
        Save();
    }

    public List<LeaderboardEntry> GetAll() => entries;
    public List<LeaderboardEntry> GetForMode(string mode)
    {
        var list = new List<LeaderboardEntry>();
        foreach (var e in entries) if (e.mode == mode) list.Add(e);
        return list;
    }

    // ── PERSISTENCE ─────────────────────────────────────────────────────
    void Save()
    {
        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(new LBData { entries = entries.ToArray() }));
        PlayerPrefs.Save();
    }
    void Load()
    {
        string json = PlayerPrefs.GetString(KEY, "");
        if (!string.IsNullOrEmpty(json))
            try { var d = JsonUtility.FromJson<LBData>(json); if (d?.entries != null) entries = new List<LeaderboardEntry>(d.entries); }
            catch { entries = new List<LeaderboardEntry>(); }
    }

    // ── UI BUILDER ───────────────────────────────────────────────────────
    public void BuildLeaderboardUI(GameObject parent, string filterMode = null)
    {
        foreach (Transform c in parent.transform) Destroy(c.gameObject);

        var list = filterMode != null ? GetForMode(filterMode) : GetAll();

        Lbl(parent, "LEADERBOARD", 22, new Vector2(0, 95), CYAN, FontStyles.Bold);

        if (list.Count == 0)
        {
            Lbl(parent, "No scores yet — play to get ranked!", 14, new Vector2(0, 40), new Color(.6f, .7f, .8f));
            return;
        }

        Lbl(parent, "#    NAME        SCORE         ACC      MODE", 11, new Vector2(0, 65), new Color(.5f, .6f, .7f));
        Div(parent, new Vector2(0, 50), CYAN);

        for (int i = 0; i < Mathf.Min(list.Count, MAX_ENTRIES); i++)
        {
            var e = list[i]; float y = 30f - i * 22f;
            Color rowCol = i == 0 ? new Color(1f, .92f, .15f) : i == 1 ? new Color(.8f, .8f, .8f) : i == 2 ? new Color(.85f, .55f, .25f) : Color.white;
            string line = $"{(i + 1).ToString().PadRight(4)}{e.name.PadRight(12)}{e.score.ToString("N0").PadRight(14)}{e.accuracy.PadRight(9)}{e.mode}";
            Lbl(parent, line, 13, new Vector2(0, y), rowCol);
        }
    }

    static void Lbl(GameObject p, string txt, int sz, Vector2 pos, Color col, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("L"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, .5f); rt.anchorMax = new Vector2(1f, .5f);
        rt.anchoredPosition = new Vector2(0, pos.y); rt.sizeDelta = new Vector2(0, 30);
        rt.offsetMin = new Vector2(10, rt.offsetMin.y); rt.offsetMax = new Vector2(-10, rt.offsetMax.y);
        var tmp = go.AddComponent<TextMeshProUGUI>(); tmp.text = txt; tmp.fontSize = sz; tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = col; tmp.enableWordWrapping = false;
    }
    static void Div(GameObject p, Vector2 pos, Color col)
    {
        var go = new GameObject("D"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, .5f); rt.anchorMax = new Vector2(1f, .5f);
        rt.anchoredPosition = new Vector2(0, pos.y); rt.sizeDelta = new Vector2(0, 1.5f);
        rt.offsetMin = new Vector2(10, rt.offsetMin.y); rt.offsetMax = new Vector2(-10, rt.offsetMax.y);
        go.AddComponent<Image>().color = new Color(col.r, col.g, col.b, .35f);
    }

    [System.Serializable] class LBData { public LeaderboardEntry[] entries; }
}