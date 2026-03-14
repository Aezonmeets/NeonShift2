using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class LeaderboardScene : MonoBehaviour
{
    // ── Palette (Darkened BG for contrast, brightened Neon) ──────
    static readonly Color DARK_BG = new Color(0.02f, 0.02f, 0.04f, 1f);
    static readonly Color PANEL_BG = new Color(0.04f, 0.05f, 0.08f, 0.95f);
    static readonly Color CYAN = new Color(0f, 1f, 1f, 1f);
    static readonly Color MAGENTA = new Color(1f, 0f, 0.8f, 1f);
    static readonly Color DIM = new Color(0.5f, 0.6f, 0.7f, 1f);
    static readonly Color GOLD = new Color(1f, 0.85f, 0.1f, 1f);
    static readonly Color SILVER = new Color(0.85f, 0.90f, 1f, 1f);
    static readonly Color BRONZE = new Color(0.95f, 0.45f, 0.25f, 1f);

    static readonly Color[] TabColors = {
        new Color(0.7f, 0.7f, 0.8f),  // ALL
        new Color(0.1f, 1.0f, 0.3f),  // EASY
        new Color(1.0f, 0.9f, 0.1f),  // MEDIUM
        new Color(1.0f, 0.3f, 0.1f),  // HARD
        new Color(1.0f, 0.1f, 0.4f),  // ENDLESS
    };
    static readonly string[] TabLabels = { "ALL", "EASY", "MEDIUM", "HARD", "ENDLESS" };
    static readonly string[] TabFilters = { null, "Easy", "Medium", "Hard", "Endless" };

    static readonly (string h, float x, float w, TextAlignmentOptions a)[] Cols = {
        ("#",      -380f,  60f,  TextAlignmentOptions.Center),
        ("NAME",   -230f, 180f,  TextAlignmentOptions.Left),
        ("SCORE",  -70f,  140f,  TextAlignmentOptions.Right),
        ("ACC",     50f,  100f,  TextAlignmentOptions.Right),
        ("MODE",    170f, 120f,  TextAlignmentOptions.Center),
        ("DATE",    310f, 160f,  TextAlignmentOptions.Center),
    };

    const float ROW_H = 44f;
    const float ROW_GAP = 4f;
    const int PAGE_SIZE = 10;
    const float ROW_W = 880f;

    int currentTab = 0;
    int currentPage = 0;
    List<LeaderboardEntry> displayList = new List<LeaderboardEntry>();

    GameObject canvas;
    GameObject rowContainer;
    TextMeshProUGUI pageLabel;
    Button prevBtn, nextBtn;
    Button[] tabButtons;
    bool clearPending = false;

    void Start()
    {
        Camera.main.backgroundColor = DARK_BG;
        Camera.main.clearFlags = CameraClearFlags.SolidColor;

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        var _ = LeaderboardManager.Instance;
        BuildUI();
        SelectTab(0);
    }

    // High Intensity HDR setup - This is what the Bloom volume actually sees!
    Color GetHDR(Color c, float boost = 1f)
    {
        float intensity = 3.5f;
        if (Camera.main != null)
        {
            var camSettings = Camera.main.GetComponent<CameraSettings>();
            if (camSettings != null) intensity = camSettings.globalGlowIntensity;
        }
        intensity *= boost;
        return new Color(c.r * intensity, c.g * intensity, c.b * intensity, c.a);
    }

    void BuildUI()
    {
        canvas = new GameObject("Canvas");
        var cv = canvas.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceCamera;
        cv.worldCamera = Camera.main;
        cv.planeDistance = 5f;
        cv.sortingOrder = 10;

        var sc = canvas.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight = 0.5f;
        canvas.AddComponent<GraphicRaycaster>();

        var bg = Rect("BG", canvas); Fill(bg);
        bg.AddComponent<Image>().color = DARK_BG;

        // ── HEADER ──
        var header = Rect("Header", canvas);
        AnchorStretchTop(header, 80f);
        header.AddComponent<Image>().color = PANEL_BG;

        var hLine = Rect("HLine", header);
        var hlRT = hLine.GetComponent<RectTransform>();
        hlRT.anchorMin = new Vector2(0, 0); hlRT.anchorMax = new Vector2(1, 0);
        hlRT.offsetMin = Vector2.zero; hlRT.offsetMax = new Vector2(0, 3f);
        hLine.AddComponent<Image>().color = GetHDR(CYAN, 1.2f); // Glow line

        // ── TITLE ──
        var titleContainer = Rect("TitleContainer", header);
        SetAP(titleContainer, new Vector2(0, -32), new Vector2(.5f, 1f), new Vector2(400, 50));

        // Feed HDR directly into the text color. Bloom handles the rest.
        var neon = TMP("NEON", titleContainer, "NEON", 46, GetHDR(MAGENTA, 1.5f), FontStyles.Bold, TextAlignmentOptions.Right);
        var shift = TMP("SHIFT", titleContainer, "SHIFT", 46, GetHDR(CYAN, 1.5f), FontStyles.Bold, TextAlignmentOptions.Left);
        SetAP(neon, new Vector2(-5, 0), new Vector2(.5f, .5f), new Vector2(190, 50));
        SetAP(shift, new Vector2(5, 0), new Vector2(.5f, .5f), new Vector2(190, 50));

        var sub = TMP("Sub", header, "LEADERBOARD", 14, GetHDR(DIM, 0.8f), FontStyles.Bold, TextAlignmentOptions.Center);
        SetAP(sub, new Vector2(0, -68), new Vector2(.5f, 1f), new Vector2(360, 20));
        sub.GetComponent<TextMeshProUGUI>().characterSpacing = 8f;

        // ── BUTTONS ──
        var backBtn = Btn("◀  BACK", CYAN, header, Vector2.zero, new Vector2(120, 42), GoBack);
        var backRT = backBtn.GetComponent<RectTransform>();
        backRT.anchorMin = backRT.anchorMax = new Vector2(0, 1);
        backRT.anchoredPosition = new Vector2(80, -40);

        var clrBtn = Btn("CLEAR ALL", MAGENTA, header, Vector2.zero, new Vector2(120, 42), TryClear);
        var clrRT = clrBtn.GetComponent<RectTransform>();
        clrRT.anchorMin = clrRT.anchorMax = new Vector2(1, 1);
        clrRT.anchoredPosition = new Vector2(-80, -40);
        clrBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 13;

        // ── TABS ──
        var tabBar = Rect("TabBar", canvas);
        var tbRT = tabBar.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = new Vector2(1, 1);
        tbRT.offsetMin = new Vector2(0, -80 - 50); tbRT.offsetMax = new Vector2(0, -80);
        tabBar.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.95f);

        tabButtons = new Button[TabLabels.Length];
        float tabW = 130f, gap = 8f;
        float totalW = TabLabels.Length * tabW + (TabLabels.Length - 1) * gap;
        float sx = -totalW / 2f + tabW / 2f;

        for (int i = 0; i < TabLabels.Length; i++)
        {
            int idx = i;
            Color tc = TabColors[i];

            var tab = Rect("Tab" + i, tabBar);
            var tabRT = tab.GetComponent<RectTransform>();
            tabRT.anchorMin = tabRT.anchorMax = new Vector2(.5f, .5f);
            tabRT.anchoredPosition = new Vector2(sx + i * (tabW + gap), 0);
            tabRT.sizeDelta = new Vector2(tabW, 40f);

            var tabImg = tab.AddComponent<Image>();

            var acc = Rect("Acc", tab);
            var accRT = acc.GetComponent<RectTransform>();
            accRT.anchorMin = new Vector2(0, 0); accRT.anchorMax = new Vector2(1, 0);
            accRT.offsetMin = Vector2.zero; accRT.offsetMax = new Vector2(0, 4);
            acc.AddComponent<Image>().color = tc;

            var lbl = TMP("L", tab, TabLabels[i], 14, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Fill(lbl);

            var btn = tab.AddComponent<Button>(); btn.targetGraphic = tabImg;
            var bc = btn.colors;
            bc.normalColor = new Color(tc.r * .1f, tc.g * .1f, tc.b * .1f, 0f);
            bc.highlightedColor = new Color(tc.r * .2f, tc.g * .2f, tc.b * .2f, 1f);
            bc.pressedColor = new Color(tc.r * .4f, tc.g * .4f, tc.b * .4f, 1f);
            btn.colors = bc;
            btn.onClick.AddListener(() => { clearPending = false; SelectTab(idx); });
            tabButtons[i] = btn;
        }

        // ── COLUMNS ──
        var colHeader = Rect("ColHeaders", canvas);
        var chRT = colHeader.GetComponent<RectTransform>();
        chRT.anchorMin = new Vector2(.5f, 1f); chRT.anchorMax = new Vector2(.5f, 1f);
        chRT.anchoredPosition = new Vector2(0, -(80f + 50f + 16f));
        chRT.sizeDelta = new Vector2(ROW_W, 30f);
        PlaceRow(colHeader, null, GetHDR(DIM, 0.5f), 12, FontStyles.Bold);

        var hul = Rect("HUL", canvas);
        var hulRT = hul.GetComponent<RectTransform>();
        hulRT.anchorMin = new Vector2(.5f, 1f); hulRT.anchorMax = new Vector2(.5f, 1f);
        hulRT.anchoredPosition = new Vector2(0, -(80f + 50f + 30f + 4f));
        hulRT.sizeDelta = new Vector2(ROW_W, 2f);
        hul.AddComponent<Image>().color = GetHDR(CYAN, 0.7f);

        rowContainer = Rect("Rows", canvas);
        var rcRT = rowContainer.GetComponent<RectTransform>();
        rcRT.anchorMin = new Vector2(.5f, 0f); rcRT.anchorMax = new Vector2(.5f, 1f);
        rcRT.sizeDelta = new Vector2(ROW_W, 0f);
        rcRT.offsetMin = new Vector2(-ROW_W / 2f, 54f); rcRT.offsetMax = new Vector2(ROW_W / 2f, -170f);

        // ── PAGINATION ──
        var pageBar = Rect("PageBar", canvas);
        var pbRT = pageBar.GetComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(0, 0); pbRT.anchorMax = new Vector2(1, 0);
        pbRT.offsetMin = Vector2.zero; pbRT.offsetMax = new Vector2(0, 54f);
        pageBar.AddComponent<Image>().color = PANEL_BG;

        var pLine = Rect("PLine", pageBar);
        var plRT = pLine.GetComponent<RectTransform>();
        plRT.anchorMin = new Vector2(0, 1); plRT.anchorMax = new Vector2(1, 1);
        plRT.offsetMin = Vector2.zero; plRT.offsetMax = new Vector2(0, 2f);
        pLine.AddComponent<Image>().color = GetHDR(CYAN, 0.7f);

        prevBtn = Btn("◀", CYAN, pageBar, new Vector2(-120, 0), new Vector2(40, 36), () => { currentPage--; RefreshRows(); });
        nextBtn = Btn("▶", CYAN, pageBar, new Vector2(120, 0), new Vector2(40, 36), () => { currentPage++; RefreshRows(); });

        var pgLbl = TMP("PgLbl", pageBar, "Page 1 / 1", 16, DIM, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAP(pgLbl, new Vector2(0, 0), new Vector2(.5f, .5f), new Vector2(200, 30));
        pageLabel = pgLbl.GetComponent<TextMeshProUGUI>();
    }

    void SelectTab(int idx)
    {
        currentTab = idx;
        currentPage = 0;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            Color tc = TabColors[i];
            var img = tabButtons[i].GetComponent<Image>();
            var acc = tabButtons[i].transform.Find("Acc").GetComponent<Image>();

            if (i == currentTab)
            {
                img.color = new Color(tc.r * .2f, tc.g * .2f, tc.b * .2f, 1f);
                acc.color = GetHDR(tc, 1.5f); // Smooth glowing line at the bottom
            }
            else
            {
                img.color = new Color(tc.r * .05f, tc.g * .05f, tc.b * .05f, 0f);
                acc.color = new Color(tc.r, tc.g, tc.b, 0.3f);
            }
        }

        string filter = TabFilters[currentTab];
        displayList = filter == null ? LeaderboardManager.Instance.GetAll() : LeaderboardManager.Instance.GetForMode(filter);
        RefreshRows();
    }

    void RefreshRows()
    {
        foreach (Transform c in rowContainer.transform) Destroy(c.gameObject);

        int total = displayList.Count;
        int pages = Mathf.Max(1, Mathf.CeilToInt((float)total / PAGE_SIZE));
        currentPage = Mathf.Clamp(currentPage, 0, pages - 1);
        int start = currentPage * PAGE_SIZE;
        int end = Mathf.Min(start + PAGE_SIZE, total);

        if (total == 0)
        {
            var empty = TMP("Empty", rowContainer, "NO SCORES YET — PLAY A GAME FIRST!", 20, DIM, FontStyles.Bold, TextAlignmentOptions.Center);
            SetAP(empty, new Vector2(0, -80), new Vector2(.5f, 1f), new Vector2(ROW_W, 40));
            pageLabel.text = "PAGE 1 / 1";
            prevBtn.interactable = nextBtn.interactable = false;
            return;
        }

        for (int i = start; i < end; i++)
        {
            int rank = i + 1;
            var e = displayList[i];
            float y = -((i - start) * (ROW_H + ROW_GAP)) - ROW_H * 0.5f - 8f;

            var row = Rect("Row" + rank, rowContainer);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin = rowRT.anchorMax = new Vector2(.5f, 1f);
            rowRT.anchoredPosition = new Vector2(0, y);
            rowRT.sizeDelta = new Vector2(ROW_W, ROW_H);

            Color bg = rank == 1 ? new Color(.25f, .20f, .05f, .90f)
                     : rank == 2 ? new Color(.18f, .18f, .20f, .85f)
                     : rank == 3 ? new Color(.20f, .12f, .05f, .85f)
                     : i % 2 == 0 ? new Color(.05f, .07f, .10f, .80f)
                                  : new Color(.03f, .04f, .06f, .75f);
            row.AddComponent<Image>().color = bg;

            var stripe = Rect("Stripe", row);
            var stripeRT = stripe.GetComponent<RectTransform>();
            stripeRT.anchorMin = new Vector2(0, 0); stripeRT.anchorMax = new Vector2(0, 1);
            stripeRT.offsetMin = Vector2.zero; stripeRT.offsetMax = new Vector2(6, 0);
            stripe.AddComponent<Image>().color = ModeColor(e.mode);

            Color tc = rank == 1 ? GOLD : rank == 2 ? SILVER : rank == 3 ? BRONZE : Color.white;
            FontStyles fs = rank <= 3 ? FontStyles.Bold : FontStyles.Normal;

            string[] vals = {
                rank <= 3 ? (rank==1?"1ST":rank==2?"2ND":"3RD") : rank.ToString(),
                e.name, e.score.ToString("N0"), e.accuracy, e.mode.ToUpper(),
                string.IsNullOrEmpty(e.date) ? "-" : e.date,
            };
            PlaceRow(row, vals, tc, 14, fs);
        }

        pageLabel.text = $"PAGE {currentPage + 1} / {pages}";
        prevBtn.interactable = currentPage > 0;
        nextBtn.interactable = currentPage < pages - 1;
    }

    void PlaceRow(GameObject parent, string[] vals, Color col, int sz, FontStyles fs)
    {
        for (int i = 0; i < Cols.Length; i++)
        {
            string txt = vals != null && i < vals.Length ? vals[i] : Cols[i].h;
            var go = TMP("C" + i, parent, txt, sz, col, fs, Cols[i].a);
            SetAP(go, new Vector2(Cols[i].x, 0), new Vector2(.5f, .5f), new Vector2(Cols[i].w, ROW_H));

            if (vals != null)
            {
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.8f);
                shadow.effectDistance = new Vector2(1, -1);
            }
        }
    }

    void TryClear()
    {
        if (!clearPending)
        {
            clearPending = true;
            foreach (var b in canvas.GetComponentsInChildren<Button>())
            {
                var t = b.GetComponentInChildren<TextMeshProUGUI>();
                if (t && (t.text == "CLEAR ALL" || t.text == "CONFIRM?")) t.text = "CONFIRM?";
            }
        }
        else
        {
            clearPending = false;
            LeaderboardManager.Instance?.ClearAll();
            SelectTab(currentTab);
            foreach (var b in canvas.GetComponentsInChildren<Button>())
            {
                var t = b.GetComponentInChildren<TextMeshProUGUI>();
                if (t && t.text == "CONFIRM?") t.text = "CLEAR ALL";
            }
        }
    }

    void GoBack()
    {
        string ret = PlayerPrefs.GetString("LB_ReturnScene", "MainMenu");
        SceneManager.LoadScene(ret);
    }

    static Color ModeColor(string m)
    {
        switch (m)
        {
            case "Easy": return new Color(.1f, 1f, .3f);
            case "Medium": return new Color(1f, .9f, .1f);
            case "Hard": return new Color(1f, .3f, .1f);
            case "Endless": return new Color(1f, .1f, .4f);
            default: return new Color(.6f, .6f, .6f);
        }
    }

    static GameObject Rect(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void Fill(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void AnchorStretchTop(GameObject go, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(0, -h); rt.offsetMax = Vector2.zero;
    }

    static GameObject TMP(string name, GameObject parent, string txt, int sz, Color col, FontStyles style, TextAlignmentOptions align)
    {
        var go = Rect(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = txt; tmp.fontSize = sz; tmp.fontStyle = style;
        tmp.alignment = align; tmp.color = col;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return go;
    }

    static void SetAP(GameObject go, Vector2 pos, Vector2 anchor, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    Button Btn(string label, Color col, GameObject parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction cb)
    {
        var go = Rect("Btn_" + label, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(col.r * .1f, col.g * .1f, col.b * .1f, .95f);

        // Clean glowing text for the button, no ugly blocky outlines
        var lbl = TMP("L", go, label, 15, GetHDR(col, 1.2f), FontStyles.Bold, TextAlignmentOptions.Center);
        Fill(lbl);

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var bc = btn.colors;
        bc.normalColor = new Color(col.r * .15f, col.g * .15f, col.b * .15f, .95f);
        bc.highlightedColor = new Color(col.r * .3f, col.g * .3f, col.b * .3f, 1f);
        bc.pressedColor = new Color(col.r * .5f, col.g * .5f, col.b * .5f, 1f);
        btn.colors = bc;
        btn.onClick.AddListener(cb);
        return btn;
    }
}