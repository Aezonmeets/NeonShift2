using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class LeaderboardScene : MonoBehaviour
{
    // ── Palette (Refined for better UI presence) ─────────────────────────
    static readonly Color DARK_BG = new Color(0.04f, 0.05f, 0.09f, 1f);
    static readonly Color PANEL_BG = new Color(0.08f, 0.10f, 0.16f, 1f);
    static readonly Color CYAN = new Color(0f, 0.92f, 1f, 1f);
    static readonly Color MAGENTA = new Color(1f, 0.15f, 0.75f, 1f);
    static readonly Color DIM = new Color(0.55f, 0.65f, 0.75f, 1f);
    static readonly Color GOLD = new Color(1f, 0.85f, 0.25f, 1f);
    static readonly Color SILVER = new Color(0.85f, 0.85f, 0.90f, 1f);
    static readonly Color BRONZE = new Color(0.90f, 0.55f, 0.35f, 1f);

    static readonly Color[] TabColors = {
        new Color(0.65f, 0.65f, 0.70f),  // ALL
        new Color(0.15f, 1.00f, 0.45f),  // EASY
        new Color(1.00f, 0.85f, 0.15f),  // MEDIUM
        new Color(1.00f, 0.45f, 0.15f),  // HARD
        new Color(1.00f, 0.15f, 0.35f),  // ENDLESS
    };
    static readonly string[] TabLabels = { "ALL", "EASY", "MEDIUM", "HARD", "ENDLESS" };
    static readonly string[] TabFilters = { null, "Easy", "Medium", "Hard", "Endless" };

    // ── Column definitions: (header, xOffset from row centre, width, alignment) ──
    static readonly (string h, float x, float w, TextAlignmentOptions a)[] Cols = {
        ("#",      -385f,  40f,  TextAlignmentOptions.Center),
        ("NAME",   -280f, 150f,  TextAlignmentOptions.Left),
        ("SCORE",  -130f, 140f,  TextAlignmentOptions.Right),
        ("ACC",    - 10f,  90f,  TextAlignmentOptions.Right),
        ("MODE",     90f, 100f,  TextAlignmentOptions.Center),
        ("DATE",    210f, 140f,  TextAlignmentOptions.Center),
    };

    const float ROW_H = 44f;   // Taller rows for a cleaner look
    const float ROW_GAP = 4f;  // More space between rows
    const int PAGE_SIZE = 10;
    const float ROW_W = 880f;

    // ── State ────────────────────────────────────────────────────────────
    int currentTab = 0;
    int currentPage = 0;
    List<LeaderboardEntry> displayList = new List<LeaderboardEntry>();

    GameObject canvas;
    GameObject rowContainer;
    TextMeshProUGUI pageLabel;
    Button prevBtn, nextBtn;
    Button[] tabButtons;
    bool clearPending = false;

    // ════════════════════════════════════════════════════════════════════
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

        // Guarantee Manager exists (handles Editor-only play gracefully)
        var _ = LeaderboardManager.Instance;

        BuildUI();
        SelectTab(0);
    }

    // ════════════════════════════════════════════════════════════════════
    void BuildUI()
    {
        // ── Canvas ───────────────────────────────────────────────────────
        canvas = new GameObject("Canvas");
        var cv = canvas.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 10;
        var sc = canvas.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight = 0.5f;
        canvas.AddComponent<GraphicRaycaster>();

        var bg = Rect("BG", canvas); Fill(bg);
        bg.AddComponent<Image>().color = DARK_BG;

        // ── HEADER BAR (top 80px) ─────────────────────────────────────────
        var header = Rect("Header", canvas);
        AnchorStretchTop(header, 80f);
        header.AddComponent<Image>().color = PANEL_BG;

        var hLine = Rect("HLine", header);
        var hlRT = hLine.GetComponent<RectTransform>();
        hlRT.anchorMin = new Vector2(0, 0); hlRT.anchorMax = new Vector2(1, 0);
        hlRT.offsetMin = Vector2.zero; hlRT.offsetMax = new Vector2(0, 3f);
        hLine.AddComponent<Image>().color = new Color(CYAN.r, CYAN.g, CYAN.b, 0.7f);

        // NEON SHIFT title (Slightly larger, added shadow component effect)
        var neon = TMP("NEON", header, "NEON", 46, MAGENTA, FontStyles.Bold, TextAlignmentOptions.Right);
        var shift = TMP("SHIFT", header, "SHIFT", 46, CYAN, FontStyles.Bold, TextAlignmentOptions.Left);
        SetAP(neon, new Vector2(-10, -40), new Vector2(.5f, 1f), new Vector2(200, 50));
        SetAP(shift, new Vector2(10, -40), new Vector2(.5f, 1f), new Vector2(200, 50));

        var sub = TMP("Sub", header, "LEADERBOARD", 14, DIM, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAP(sub, new Vector2(0, -68), new Vector2(.5f, 1f), new Vector2(360, 20));
        sub.GetComponent<TextMeshProUGUI>().characterSpacing = 8f;

        Btn("◀  BACK", new Color(.3f, .4f, .55f), header, new Vector2(90, -40), new Vector2(120, 42), GoBack);
        var clrBtn = Btn("CLEAR ALL", new Color(.7f, .15f, .15f), header, new Vector2(-90, -40), new Vector2(120, 42), TryClear);
        clrBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 13;

        // ── TABS (below header, 50px tall) ────────────────────────────────
        var tabBar = Rect("TabBar", canvas);
        var tbRT = tabBar.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = new Vector2(1, 1);
        tbRT.offsetMin = new Vector2(0, -80 - 50); tbRT.offsetMax = new Vector2(0, -80);
        tabBar.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.13f, 0.95f);

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
            accRT.offsetMin = Vector2.zero; accRT.offsetMax = new Vector2(0, 4); // Bottom accent instead of top
            acc.AddComponent<Image>().color = tc;

            var lbl = TMP("L", tab, TabLabels[i], 14, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Fill(lbl);

            var btn = tab.AddComponent<Button>(); btn.targetGraphic = tabImg;
            var bc = btn.colors;
            bc.normalColor = new Color(tc.r * .1f, tc.g * .1f, tc.b * .1f, 0f); // transparent default
            bc.highlightedColor = new Color(tc.r * .2f, tc.g * .2f, tc.b * .2f, 1f);
            bc.pressedColor = new Color(tc.r * .4f, tc.g * .4f, tc.b * .4f, 1f);
            btn.colors = bc;
            btn.onClick.AddListener(() => { clearPending = false; SelectTab(idx); });
            tabButtons[i] = btn;
        }

        // ── COLUMN HEADERS ────────────────────────────────────────────────
        var colHeader = Rect("ColHeaders", canvas);
        var chRT = colHeader.GetComponent<RectTransform>();
        chRT.anchorMin = new Vector2(.5f, 1f); chRT.anchorMax = new Vector2(.5f, 1f);
        chRT.anchoredPosition = new Vector2(0, -(80f + 50f + 16f));
        chRT.sizeDelta = new Vector2(ROW_W, 30f);
        PlaceRow(colHeader, null, DIM, 12, FontStyles.Bold);

        var hul = Rect("HUL", canvas);
        var hulRT = hul.GetComponent<RectTransform>();
        hulRT.anchorMin = new Vector2(.5f, 1f); hulRT.anchorMax = new Vector2(.5f, 1f);
        hulRT.anchoredPosition = new Vector2(0, -(80f + 50f + 30f + 4f));
        hulRT.sizeDelta = new Vector2(ROW_W, 2f);
        hul.AddComponent<Image>().color = new Color(CYAN.r, CYAN.g, CYAN.b, .4f);

        // ── ROW CONTAINER ─────────────────────────────────────────────────
        rowContainer = Rect("Rows", canvas);
        var rcRT = rowContainer.GetComponent<RectTransform>();
        rcRT.anchorMin = new Vector2(.5f, 0f);
        rcRT.anchorMax = new Vector2(.5f, 1f);
        rcRT.sizeDelta = new Vector2(ROW_W, 0f);
        rcRT.offsetMin = new Vector2(-ROW_W / 2f, 54f);
        rcRT.offsetMax = new Vector2(ROW_W / 2f, -170f);

        // ── PAGINATION BAR (bottom 54px) ──────────────────────────────────
        var pageBar = Rect("PageBar", canvas);
        var pbRT = pageBar.GetComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(0, 0); pbRT.anchorMax = new Vector2(1, 0);
        pbRT.offsetMin = Vector2.zero; pbRT.offsetMax = new Vector2(0, 54f);
        pageBar.AddComponent<Image>().color = PANEL_BG;

        var pLine = Rect("PLine", pageBar);
        var plRT = pLine.GetComponent<RectTransform>();
        plRT.anchorMin = new Vector2(0, 1); plRT.anchorMax = new Vector2(1, 1);
        plRT.offsetMin = Vector2.zero; plRT.offsetMax = new Vector2(0, 2f);
        pLine.AddComponent<Image>().color = new Color(CYAN.r, CYAN.g, CYAN.b, .35f);

        prevBtn = Btn("◀", CYAN, pageBar, new Vector2(-120, 0), new Vector2(40, 36), () => { currentPage--; RefreshRows(); });
        nextBtn = Btn("▶", CYAN, pageBar, new Vector2(120, 0), new Vector2(40, 36), () => { currentPage++; RefreshRows(); });

        var pgLbl = TMP("PgLbl", pageBar, "Page 1 / 1", 16, DIM, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAP(pgLbl, new Vector2(0, 0), new Vector2(.5f, .5f), new Vector2(200, 30));
        pageLabel = pgLbl.GetComponent<TextMeshProUGUI>();
    }

    // ════════════════════════════════════════════════════════════════════
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
                acc.color = new Color(tc.r, tc.g, tc.b, 1f);
            }
            else
            {
                img.color = new Color(tc.r * .05f, tc.g * .05f, tc.b * .05f, 0f);
                acc.color = new Color(tc.r, tc.g, tc.b, 0.3f);
            }
        }

        string filter = TabFilters[currentTab];
        displayList = filter == null
            ? LeaderboardManager.Instance.GetAll()
            : LeaderboardManager.Instance.GetForMode(filter);

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
                     : i % 2 == 0 ? new Color(.07f, .09f, .13f, .80f)
                                  : new Color(.05f, .06f, .10f, .75f);
            row.AddComponent<Image>().color = bg;

            var stripe = Rect("Stripe", row);
            var stripeRT = stripe.GetComponent<RectTransform>();
            stripeRT.anchorMin = new Vector2(0, 0); stripeRT.anchorMax = new Vector2(0, 1);
            stripeRT.offsetMin = Vector2.zero; stripeRT.offsetMax = new Vector2(6, 0); // Thicker left stripe
            stripe.AddComponent<Image>().color = ModeColor(e.mode);

            Color tc = rank == 1 ? GOLD : rank == 2 ? SILVER : rank == 3 ? BRONZE : Color.white;
            FontStyles fs = rank <= 3 ? FontStyles.Bold : FontStyles.Normal;

            string[] vals = {
                rank <= 3 ? (rank==1?"1ST":rank==2?"2ND":"3RD") : rank.ToString(),
                e.name,
                e.score.ToString("N0"),
                e.accuracy,
                e.mode.ToUpper(),
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

            // Add a slight shadow to row text for readability
            if (vals != null)
            {
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.5f);
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
                if (t && (t.text == "CLEAR ALL" || t.text == "CONFIRM?"))
                    t.text = "CONFIRM?";
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
            case "Easy": return new Color(.15f, 1f, .45f);
            case "Medium": return new Color(1f, .85f, .15f);
            case "Hard": return new Color(1f, .45f, .15f);
            case "Endless": return new Color(1f, .15f, .35f);
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
        img.color = new Color(col.r * .2f, col.g * .2f, col.b * .2f, .95f);

        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(col.r, col.g, col.b, .7f);
        ol.effectDistance = new Vector2(1, -1);

        var lbl = TMP("L", go, label, 15, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Fill(lbl);

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var bc = btn.colors;
        bc.normalColor = new Color(col.r * .2f, col.g * .2f, col.b * .2f, .95f);
        bc.highlightedColor = new Color(col.r * .4f, col.g * .4f, col.b * .4f, 1f);
        bc.pressedColor = new Color(col.r * .6f, col.g * .6f, col.b * .6f, 1f);
        btn.colors = bc;
        btn.onClick.AddListener(cb);
        return btn;
    }
}