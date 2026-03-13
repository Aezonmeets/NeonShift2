using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class LeaderboardScene : MonoBehaviour
{
    // ── Palette ──────────────────────────────────────────────────────────
    static readonly Color DARK_BG = new Color(0.03f, 0.04f, 0.08f, 1f);
    static readonly Color PANEL_BG = new Color(0.05f, 0.07f, 0.12f, 1f);
    static readonly Color CYAN = new Color(0f, 0.92f, 1f, 1f);
    static readonly Color MAGENTA = new Color(1f, 0.15f, 0.75f, 1f);
    static readonly Color DIM = new Color(0.45f, 0.55f, 0.65f, 1f);
    static readonly Color GOLD = new Color(1f, 0.85f, 0.15f, 1f);
    static readonly Color SILVER = new Color(0.80f, 0.80f, 0.85f, 1f);
    static readonly Color BRONZE = new Color(0.85f, 0.55f, 0.25f, 1f);

    static readonly Color[] TabColors = {
        new Color(0.55f, 0.55f, 0.60f),  // ALL
        new Color(0.10f, 1.00f, 0.40f),  // EASY
        new Color(1.00f, 0.85f, 0.10f),  // MEDIUM
        new Color(1.00f, 0.45f, 0.10f),  // HARD
        new Color(0.90f, 0.10f, 0.30f),  // ENDLESS
    };
    static readonly string[] TabLabels = { "ALL", "EASY", "MEDIUM", "HARD", "ENDLESS" };
    static readonly string[] TabFilters = { null, "Easy", "Medium", "Hard", "Endless" };

    // ── Column definitions: (header, xOffset from row centre, width, alignment) ──
    // Total row width = 860. Columns spread across it.
    static readonly (string h, float x, float w, TextAlignmentOptions a)[] Cols = {
        ("#",      -385f,  40f,  TextAlignmentOptions.Center),
        ("NAME",   -290f, 130f,  TextAlignmentOptions.Left),
        ("SCORE",  -140f, 130f,  TextAlignmentOptions.Right),
        ("ACC",    - 30f,  90f,  TextAlignmentOptions.Right),
        ("MODE",    70f,  100f,  TextAlignmentOptions.Center),
        ("DATE",   190f,  130f,  TextAlignmentOptions.Center),
    };

    const float ROW_H = 36f;
    const float ROW_GAP = 3f;
    const int PAGE_SIZE = 10;
    const float ROW_W = 860f;

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

        // Ensure EventSystem exists
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Bootstrap LeaderboardManager if missing (editor direct-play)
        if (LeaderboardManager.Instance == null)
            new GameObject("LeaderboardManager").AddComponent<LeaderboardManager>();

        BuildUI();
        SelectTab(0);
    }

    // ════════════════════════════════════════════════════════════════════
    //  UI BUILD
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

        // Full background
        var bg = Rect("BG", canvas); Fill(bg);
        bg.AddComponent<Image>().color = DARK_BG;

        // ── HEADER BAR (top 72px) ─────────────────────────────────────────
        var header = Rect("Header", canvas);
        AnchorStretchTop(header, 72f);
        header.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.11f, 1f);

        // Bottom border of header
        var hLine = Rect("HLine", header);
        var hlRT = hLine.GetComponent<RectTransform>();
        hlRT.anchorMin = new Vector2(0, 0); hlRT.anchorMax = new Vector2(1, 0);
        hlRT.offsetMin = Vector2.zero; hlRT.offsetMax = new Vector2(0, 2f);
        hLine.AddComponent<Image>().color = new Color(CYAN.r, CYAN.g, CYAN.b, 0.5f);

        // NEON SHIFT title
        var neon = TMP("NEON", header, "NEON", 40, MAGENTA, FontStyles.Bold, TextAlignmentOptions.Right);
        var shift = TMP("SHIFT", header, "SHIFT", 40, CYAN, FontStyles.Bold, TextAlignmentOptions.Left);
        SetAP(neon, new Vector2(-8, -36), new Vector2(.5f, 1f), new Vector2(200, 50));
        SetAP(shift, new Vector2(8, -36), new Vector2(.5f, 1f), new Vector2(200, 50));

        // Sub label
        var sub = TMP("Sub", header, "LEADERBOARD", 12, DIM, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAP(sub, new Vector2(0, -62), new Vector2(.5f, 1f), new Vector2(360, 18));
        sub.GetComponent<TextMeshProUGUI>().characterSpacing = 6f;

        // BACK button — left side of header
        Btn("◀  BACK", new Color(.3f, .38f, .50f), header,
            new Vector2(80, -36), new Vector2(110, 36), GoBack);

        // CLEAR button — right side of header
        var clrBtn = Btn("CLEAR ALL", new Color(.55f, .08f, .08f), header,
            new Vector2(-80, -36), new Vector2(110, 36), TryClear);
        clrBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 12;

        // ── TABS (below header, 44px tall) ────────────────────────────────
        var tabBar = Rect("TabBar", canvas);
        var tbRT = tabBar.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = new Vector2(1, 1);
        tbRT.offsetMin = new Vector2(0, -72 - 44); tbRT.offsetMax = new Vector2(0, -72);
        tabBar.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f, 0.95f);

        tabButtons = new Button[TabLabels.Length];
        float tabW = 120f, gap = 6f;
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
            tabRT.sizeDelta = new Vector2(tabW, 34f);

            var tabImg = tab.AddComponent<Image>();
            tabImg.color = new Color(tc.r * .1f, tc.g * .1f, tc.b * .1f, .95f);

            // Top accent line
            var acc = Rect("Acc", tab);
            var accRT = acc.GetComponent<RectTransform>();
            accRT.anchorMin = new Vector2(0, 1); accRT.anchorMax = new Vector2(1, 1);
            accRT.offsetMin = new Vector2(0, -3); accRT.offsetMax = Vector2.zero;
            acc.AddComponent<Image>().color = tc;

            var lbl = TMP("L", tab, TabLabels[i], 13, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Fill(lbl);

            var btn = tab.AddComponent<Button>(); btn.targetGraphic = tabImg;
            var bc = btn.colors;
            bc.normalColor = new Color(tc.r * .1f, tc.g * .1f, tc.b * .1f, .95f);
            bc.highlightedColor = new Color(tc.r * .3f, tc.g * .3f, tc.b * .3f, 1f);
            bc.pressedColor = new Color(tc.r * .5f, tc.g * .5f, tc.b * .5f, 1f);
            btn.colors = bc;
            btn.onClick.AddListener(() => { clearPending = false; SelectTab(idx); });
            tabButtons[i] = btn;
        }

        // ── COLUMN HEADERS ────────────────────────────────────────────────
        float headerY = 72f + 44f + 28f;  // distance from top
        var colHeader = Rect("ColHeaders", canvas);
        var chRT = colHeader.GetComponent<RectTransform>();
        chRT.anchorMin = new Vector2(.5f, 1f); chRT.anchorMax = new Vector2(.5f, 1f);
        chRT.anchoredPosition = new Vector2(0, -(72f + 44f + 14f));
        chRT.sizeDelta = new Vector2(ROW_W, 28f);
        PlaceRow(colHeader, null, DIM, 11, FontStyles.Bold);

        // Header underline
        var hul = Rect("HUL", canvas);
        var hulRT = hul.GetComponent<RectTransform>();
        hulRT.anchorMin = new Vector2(.5f, 1f); hulRT.anchorMax = new Vector2(.5f, 1f);
        hulRT.anchoredPosition = new Vector2(0, -(72f + 44f + 28f + 2f));
        hulRT.sizeDelta = new Vector2(ROW_W, 1.5f);
        hul.AddComponent<Image>().color = new Color(CYAN.r, CYAN.g, CYAN.b, .3f);

        // ── ROW CONTAINER (stretches between header and pagination) ───────
        rowContainer = Rect("Rows", canvas);
        var rcRT = rowContainer.GetComponent<RectTransform>();
        rcRT.anchorMin = new Vector2(.5f, 0f);
        rcRT.anchorMax = new Vector2(.5f, 1f);
        rcRT.sizeDelta = new Vector2(ROW_W, 0f);
        // top offset = header(72) + tabbar(44) + colheader(30) + underline(4) = 150
        // bottom offset = pagination(48)
        rcRT.offsetMin = new Vector2(-ROW_W / 2f, 48f);
        rcRT.offsetMax = new Vector2(ROW_W / 2f, -154f);

        // ── PAGINATION BAR (bottom 44px) ──────────────────────────────────
        var pageBar = Rect("PageBar", canvas);
        var pbRT = pageBar.GetComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(0, 0); pbRT.anchorMax = new Vector2(1, 0);
        pbRT.offsetMin = Vector2.zero; pbRT.offsetMax = new Vector2(0, 44f);
        pageBar.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f, 0.95f);

        // Top border
        var pLine = Rect("PLine", pageBar);
        var plRT = pLine.GetComponent<RectTransform>();
        plRT.anchorMin = new Vector2(0, 1); plRT.anchorMax = new Vector2(1, 1);
        plRT.offsetMin = Vector2.zero; plRT.offsetMax = new Vector2(0, 2f);
        pLine.AddComponent<Image>().color = new Color(CYAN.r, CYAN.g, CYAN.b, .25f);

        prevBtn = Btn("◀", CYAN, pageBar, new Vector2(-100, 0), new Vector2(36, 30),
            () => { currentPage--; RefreshRows(); });
        nextBtn = Btn("▶", CYAN, pageBar, new Vector2(100, 0), new Vector2(36, 30),
            () => { currentPage++; RefreshRows(); });

        var pgLbl = TMP("PgLbl", pageBar, "Page 1 / 1", 14, DIM, FontStyles.Normal, TextAlignmentOptions.Center);
        SetAP(pgLbl, new Vector2(0, 0), new Vector2(.5f, .5f), new Vector2(200, 30));
        pageLabel = pgLbl.GetComponent<TextMeshProUGUI>();
    }

    // ════════════════════════════════════════════════════════════════════
    //  DATA
    // ════════════════════════════════════════════════════════════════════
    void SelectTab(int idx)
    {
        currentTab = idx;
        currentPage = 0;

        // Update tab visuals
        for (int i = 0; i < tabButtons.Length; i++)
        {
            Color tc = TabColors[i];
            var img = tabButtons[i].GetComponent<Image>();
            img.color = i == currentTab
                ? new Color(tc.r * .35f, tc.g * .35f, tc.b * .35f, 1f)
                : new Color(tc.r * .10f, tc.g * .10f, tc.b * .10f, .95f);
        }

        string filter = TabFilters[currentTab];
        displayList = filter == null
            ? LeaderboardManager.Instance.GetAll()
            : LeaderboardManager.Instance.GetForMode(filter);

        RefreshRows();
    }

    void RefreshRows()
    {
        // Clear existing rows
        foreach (Transform c in rowContainer.transform) Destroy(c.gameObject);

        int total = displayList.Count;
        int pages = Mathf.Max(1, Mathf.CeilToInt((float)total / PAGE_SIZE));
        currentPage = Mathf.Clamp(currentPage, 0, pages - 1);

        int start = currentPage * PAGE_SIZE;
        int end = Mathf.Min(start + PAGE_SIZE, total);

        // Empty state
        if (total == 0)
        {
            var empty = TMP("Empty", rowContainer, "No scores yet — play a game first!", 18,
                DIM, FontStyles.Normal, TextAlignmentOptions.Center);
            SetAP(empty, new Vector2(0, -50), new Vector2(.5f, 1f), new Vector2(ROW_W, 40));
            pageLabel.text = "Page 1 / 1";
            prevBtn.interactable = nextBtn.interactable = false;
            return;
        }

        for (int i = start; i < end; i++)
        {
            int rank = i + 1;
            var e = displayList[i];
            float y = -((i - start) * (ROW_H + ROW_GAP)) - ROW_H * 0.5f - 6f;

            // Row background
            var row = Rect("Row" + rank, rowContainer);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin = rowRT.anchorMax = new Vector2(.5f, 1f);
            rowRT.anchoredPosition = new Vector2(0, y);
            rowRT.sizeDelta = new Vector2(ROW_W, ROW_H);

            Color bg = rank == 1 ? new Color(.20f, .17f, .02f, .90f)
                     : rank == 2 ? new Color(.12f, .12f, .14f, .85f)
                     : rank == 3 ? new Color(.15f, .09f, .02f, .85f)
                     : i % 2 == 0 ? new Color(.05f, .07f, .10f, .80f)
                                : new Color(.04f, .05f, .08f, .75f);
            row.AddComponent<Image>().color = bg;

            // Left mode stripe
            var stripe = Rect("Stripe", row);
            var stripeRT = stripe.GetComponent<RectTransform>();
            stripeRT.anchorMin = new Vector2(0, 0); stripeRT.anchorMax = new Vector2(0, 1);
            stripeRT.offsetMin = Vector2.zero; stripeRT.offsetMax = new Vector2(4, -0);
            stripe.AddComponent<Image>().color = ModeColor(e.mode);

            // Row text colour
            Color tc = rank == 1 ? GOLD : rank == 2 ? SILVER : rank == 3 ? BRONZE : Color.white;
            FontStyles fs = rank <= 3 ? FontStyles.Bold : FontStyles.Normal;

            string[] vals = {
                rank <= 3 ? (rank==1?"🥇":rank==2?"🥈":"🥉") : rank.ToString(),
                e.name,
                e.score.ToString("N0"),
                e.accuracy,
                e.mode,
                string.IsNullOrEmpty(e.date) ? "-" : e.date,
            };
            PlaceRow(row, vals, tc, 13, fs);
        }

        pageLabel.text = $"Page {currentPage + 1} / {pages}";
        prevBtn.interactable = currentPage > 0;
        nextBtn.interactable = currentPage < pages - 1;
    }

    // Places column text into a row (or headers if vals==null)
    void PlaceRow(GameObject parent, string[] vals, Color col, int sz, FontStyles fs)
    {
        for (int i = 0; i < Cols.Length; i++)
        {
            string txt = vals != null && i < vals.Length ? vals[i] : Cols[i].h;
            var go = TMP("C" + i, parent, txt, sz, col, fs, Cols[i].a);
            SetAP(go, new Vector2(Cols[i].x, 0), new Vector2(.5f, .5f), new Vector2(Cols[i].w, ROW_H));
        }
    }

    void TryClear()
    {
        if (!clearPending)
        {
            clearPending = true;
            // Find clear button and warn
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

    // ════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════
    static Color ModeColor(string m)
    {
        switch (m)
        {
            case "Easy": return new Color(.10f, 1f, .40f);
            case "Medium": return new Color(1f, .85f, .10f);
            case "Hard": return new Color(1f, .45f, .10f);
            case "Endless": return new Color(.90f, .10f, .30f);
            default: return new Color(.5f, .5f, .5f);
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

    // Anchors to top of parent, height pixels tall
    static void AnchorStretchTop(GameObject go, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(0, -h); rt.offsetMax = Vector2.zero;
    }

    static GameObject TMP(string name, GameObject parent, string txt, int sz,
        Color col, FontStyles style, TextAlignmentOptions align)
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

    Button Btn(string label, Color col, GameObject parent, Vector2 pos, Vector2 size,
        UnityEngine.Events.UnityAction cb)
    {
        var go = Rect("Btn_" + label, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(col.r * .15f, col.g * .15f, col.b * .15f, .95f);
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(col.r, col.g, col.b, .5f);
        ol.effectDistance = new Vector2(1, -1);

        var lbl = TMP("L", go, label, 14, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Fill(lbl);

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var bc = btn.colors;
        bc.normalColor = new Color(col.r * .15f, col.g * .15f, col.b * .15f, .95f);
        bc.highlightedColor = new Color(col.r * .30f, col.g * .30f, col.b * .30f, 1f);
        bc.pressedColor = new Color(col.r * .50f, col.g * .50f, col.b * .50f, 1f);
        btn.colors = bc;
        btn.onClick.AddListener(cb);
        return btn;
    }
}