using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class LeaderboardScene : MonoBehaviour
{
    // ── Palette ──────
    static readonly Color DARK_BG = new Color(0.04f, 0.04f, 0.08f, 1f);

    static readonly Color MAGENTA = new Color(1f, 0.15f, 0.75f, 1f);
    static readonly Color CYAN = new Color(0f, 0.92f, 1f, 1f);
    
    // Gold Theme (1st Place)
    static readonly Color GOLD_TEXT = new Color(1f, 0.85f, 0f, 1f);
    static readonly Color GOLD_BORDER = new Color(0.8f, 0.65f, 0f, 1f);
    static readonly Color GOLD_BG_TINT = new Color(0.8f, 0.6f, 0f, 0.15f);

    static readonly Color ROW_BG = new Color(0.08f, 0.08f, 0.12f, 0.8f);
    static readonly Color BORDER_DIM = new Color(1f, 1f, 1f, 0.25f);
    static readonly Color DIM_TEXT = new Color(0.4f, 0.45f, 0.55f, 1f);

    static readonly string[] TabLabels = { "ALL", "EASY", "MEDIUM", "HARD", "ENDLESS" };
    static readonly string[] TabFilters = { null, "Easy", "Medium", "Hard", "Endless" };

   static readonly (string h, float x, float w, TextAlignmentOptions a)[] Cols = {
        ("#",      -420f,  60f,  TextAlignmentOptions.Left),
        ("NAME",   -300f, 200f,  TextAlignmentOptions.Left),
        ("SCORE",     0f, 140f,  TextAlignmentOptions.Center), // Shifted left
        ("ACC",     140f,  90f,  TextAlignmentOptions.Center), // Shifted left
        ("MODE",    250f, 110f,  TextAlignmentOptions.Center), // Shifted left
        ("DATE",    380f, 150f,  TextAlignmentOptions.Right),  // Safely tucked inside the right edge
    };

    const float ROW_H = 50f;
    const float ROW_GAP = 8f;
    const int PAGE_SIZE = 10;
    const float ROW_W = 960f;

    int currentTab = 0;
    int currentPage = 0;
    List<LeaderboardEntry> displayList = new List<LeaderboardEntry>();

    GameObject canvas;
    GameObject rowContainer;
    TextMeshProUGUI pageLabel;
    Button prevBtn, nextBtn;
    Button[] tabButtons;
    Image[] tabGlows; // Tracks the glow behind the tabs
    
    Sprite roundedSprite;
    Sprite glowSprite; // NEW: The procedural soft glow texture
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

        // --- Generate solid rounded sprite ---
        Texture2D tex = new Texture2D(8, 8);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++) tex.SetPixel(x, y, Color.white);
        tex.Apply();
        roundedSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(2, 2, 2, 2));

        // --- NEW: Generate soft radial glow sprite ---
        glowSprite = CreateGlowSprite();

        var _ = LeaderboardManager.Instance;
        BuildUI();
        SelectTab(0);
    }

    // ── PROCEDURAL GLOW GENERATOR ──
    Sprite CreateGlowSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / (size / 2f);
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = Mathf.Pow(alpha, 1.5f); // Soften the edge falloff
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(size/2.1f, size/2.1f, size/2.1f, size/2.1f));
    }

    // ── HELPER TO ADD GLOW TO ANY UI ELEMENT ──
    Image AddGlow(GameObject parent, Color color, Vector2 expansion)
    {
        var glowGo = Rect("Glow", parent);
        glowGo.transform.SetAsFirstSibling(); // Push it behind the main graphic
        var rt = glowGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = -expansion; rt.offsetMax = expansion; // Expand outward
        
        var img = glowGo.AddComponent<Image>();
        img.sprite = glowSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
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

        // Massive ambient background glow
        AddGlow(bg.gameObject, new Color(CYAN.r, CYAN.g, CYAN.b, 0.1f), new Vector2(-200, -100));

        // ── TOP CORNER BUTTONS ──
        var header = Rect("Header", canvas);
        AnchorStretchTop(header, 120f);

        var backBtn = OutlineBtn("BACK", BORDER_DIM, CYAN, header, new Vector2(120, -60), new Vector2(80, 36), GoBack);
        var backRT = backBtn.GetComponent<RectTransform>();
        backRT.anchorMin = backRT.anchorMax = new Vector2(0, 1);
        AddGlow(backBtn.gameObject, new Color(CYAN.r, CYAN.g, CYAN.b, 0.2f), new Vector2(10, 10));

        var clrBtn = OutlineBtn("", BORDER_DIM, DIM_TEXT, header, new Vector2(-120, -60), new Vector2(80, 36), TryClear);
        var clrRT = clrBtn.GetComponent<RectTransform>();
        clrRT.anchorMin = clrRT.anchorMax = new Vector2(1, 1);

        // ── TITLE (With Massive Magenta Glow) ──
        var titleContainer = Rect("TitleContainer", header);
        SetAP(titleContainer, new Vector2(0, -60), new Vector2(.5f, 1f), new Vector2(600, 60));

        var mainTitle = TMP("Title", titleContainer, "LEADERBOARD", 54, MAGENTA, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAP(mainTitle, Vector2.zero, new Vector2(.5f, .5f), new Vector2(600, 60));
        
        var shadow = mainTitle.AddComponent<Shadow>();
        shadow.effectColor = new Color(CYAN.r, CYAN.g, CYAN.b, 0.9f);
        shadow.effectDistance = new Vector2(3, -3);

        // Add the intense backlight glow
        AddGlow(mainTitle.gameObject, new Color(MAGENTA.r, MAGENTA.g, MAGENTA.b, 0.45f), new Vector2(80, 40));

        // ── TABS ──
        var tabBar = Rect("TabBar", canvas);
        var tbRT = tabBar.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = new Vector2(1, 1);
        tbRT.offsetMin = new Vector2(0, -120 - 40); tbRT.offsetMax = new Vector2(0, -120);

        tabButtons = new Button[TabLabels.Length];
        tabGlows = new Image[TabLabels.Length];
        float tabW = 120f, gap = 10f;
        float totalW = TabLabels.Length * tabW + (TabLabels.Length - 1) * gap;
        float sx = -totalW / 2f + tabW / 2f;

        for (int i = 0; i < TabLabels.Length; i++)
        {
            int idx = i;
            var tab = Rect("Tab" + i, tabBar);
            var tabRT = tab.GetComponent<RectTransform>();
            tabRT.anchorMin = tabRT.anchorMax = new Vector2(.5f, .5f);
            tabRT.anchoredPosition = new Vector2(sx + i * (tabW + gap), 0);
            tabRT.sizeDelta = new Vector2(tabW, 36f);

            var tabImg = tab.AddComponent<Image>();
            tabImg.sprite = roundedSprite;
            tabImg.type = Image.Type.Sliced;

            var outline = tab.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1, -1);

            // Pre-create the glow behind each tab
            tabGlows[i] = AddGlow(tab, Color.clear, new Vector2(15, 15));

            var lbl = TMP("L", tab, TabLabels[i], 12, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Fill(lbl);

            var btn = tab.AddComponent<Button>(); btn.targetGraphic = tabImg;
            btn.onClick.AddListener(() => { clearPending = false; SelectTab(idx); });
            tabButtons[i] = btn;
        }

        // ── COLUMNS HEADERS ──
        var colHeader = Rect("ColHeaders", canvas);
        var chRT = colHeader.GetComponent<RectTransform>();
        chRT.anchorMin = new Vector2(.5f, 1f); chRT.anchorMax = new Vector2(.5f, 1f);
        chRT.anchoredPosition = new Vector2(0, -(120f + 60f + 20f));
        chRT.sizeDelta = new Vector2(ROW_W, 30f);
        PlaceRow(colHeader, null, MAGENTA, 14, FontStyles.Bold);

        // ── ROW CONTAINER ──
        rowContainer = Rect("Rows", canvas);
        var rcRT = rowContainer.GetComponent<RectTransform>();
        rcRT.anchorMin = new Vector2(.5f, 0f); rcRT.anchorMax = new Vector2(.5f, 1f);
        rcRT.sizeDelta = new Vector2(ROW_W, 0f);
        rcRT.offsetMin = new Vector2(-ROW_W / 2f, 100f); rcRT.offsetMax = new Vector2(ROW_W / 2f, -220f);

        // Add a soft backdrop glow behind the entire list
        AddGlow(rowContainer, new Color(CYAN.r, CYAN.g, CYAN.b, 0.05f), new Vector2(40, 40));

        // ── PAGINATION ──
        var pageBar = Rect("PageBar", canvas);
        var pbRT = pageBar.GetComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(.5f, 0); pbRT.anchorMax = new Vector2(.5f, 0);
        pbRT.anchoredPosition = new Vector2(0, 50f);
        pbRT.sizeDelta = new Vector2(300f, 40f);

        prevBtn = OutlineBtn("<", BORDER_DIM, CYAN, pageBar, new Vector2(-110, 0), new Vector2(36, 36), () => { currentPage--; RefreshRows(); });
        nextBtn = OutlineBtn(">", BORDER_DIM, CYAN, pageBar, new Vector2(110, 0), new Vector2(36, 36), () => { currentPage++; RefreshRows(); });

        var pgLbl = TMP("PgLbl", pageBar, "PAGE 01 / 01", 12, CYAN, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAP(pgLbl, new Vector2(0, 0), new Vector2(.5f, .5f), new Vector2(150, 30));
        pageLabel = pgLbl.GetComponent<TextMeshProUGUI>();
    }

    void SelectTab(int idx)
    {
        currentTab = idx;
        currentPage = 0;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            var img = tabButtons[i].GetComponent<Image>();
            var outline = tabButtons[i].GetComponent<Outline>();
            var txt = tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();

            if (i == currentTab)
            {
                img.color = new Color(CYAN.r, CYAN.g, CYAN.b, 0.15f);
                outline.effectColor = CYAN;
                txt.color = Color.white;
                
                // Light up the glow behind the active tab!
                tabGlows[i].color = new Color(CYAN.r, CYAN.g, CYAN.b, 0.4f);
            }
            else
            {
                img.color = ROW_BG;
                outline.effectColor = BORDER_DIM;
                txt.color = DIM_TEXT;
                
                // Turn off glow for inactive tabs
                tabGlows[i].color = Color.clear;
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

        int rowsToDisplay = Mathf.Max(4, Mathf.Min(PAGE_SIZE, total - start));

        for (int i = 0; i < rowsToDisplay; i++)
        {
            int rank = start + i + 1;
            bool hasData = (start + i) < total;
            var e = hasData ? displayList[start + i] : default;

            float y = -(i * (ROW_H + ROW_GAP)) - ROW_H * 0.5f;

            var row = Rect("Row" + rank, rowContainer);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin = rowRT.anchorMax = new Vector2(.5f, 1f);
            rowRT.anchoredPosition = new Vector2(0, y);
            rowRT.sizeDelta = new Vector2(ROW_W, ROW_H);

            var img = row.AddComponent<Image>();
            img.sprite = roundedSprite;
            img.type = Image.Type.Sliced;
            img.color = ROW_BG; 
            
            var outline = row.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1, -1);
            outline.effectColor = BORDER_DIM;

            Color textColor = hasData ? CYAN : DIM_TEXT;
            FontStyles fs = hasData ? FontStyles.Bold : FontStyles.Normal;

            // --- RANK GLOWS ---
            if (hasData)
            {
                if (rank == 1) 
                {
                    outline.effectColor = GOLD_BORDER;
                    AddGlow(row, new Color(GOLD_BORDER.r, GOLD_BORDER.g, GOLD_BORDER.b, 0.4f), new Vector2(15, 15));
                }
                else if (rank == 2) 
                {
                    outline.effectColor = CYAN;
                    AddGlow(row, new Color(CYAN.r, CYAN.g, CYAN.b, 0.25f), new Vector2(15, 15));
                }
            }

            string[] vals;
            if (hasData)
            {
                vals = new string[] {
                    rank <= 4 ? (rank==1?"1ST":rank==2?"2ND":rank==3?"3RD":"4TH") : rank.ToString(),
                    e.name,
                    e.score.ToString("N0"),
                    e.accuracy,
                    e.mode.ToUpper(),
                    string.IsNullOrEmpty(e.date) ? "-" : e.date,
                };
            }
            else
            {
                vals = new string[] {
                    rank <= 4 ? (rank==1?"1ST":rank==2?"2ND":rank==3?"3RD":"4TH") : rank.ToString(),
                    "---", "---", "---", "---", "---"
                };
            }

            PlaceRow(row, vals, textColor, 13, fs, rank);
        }

        pageLabel.text = $"PAGE {currentPage + 1:D2} / {pages:D2}";
        prevBtn.interactable = currentPage > 0;
        nextBtn.interactable = currentPage < pages - 1;
    }

    void PlaceRow(GameObject parent, string[] vals, Color defaultCol, int sz, FontStyles fs, int rank = -1)
    {
        for (int i = 0; i < Cols.Length; i++)
        {
            string txt = vals != null && i < vals.Length ? vals[i] : Cols[i].h;
            
            Color col = (vals == null) ? MAGENTA : defaultCol;
            
            // Highlight the #1 Player text in Gold!
            if (rank == 1 && vals != null) col = GOLD_TEXT; 

            var go = TMP("C" + i, parent, txt, sz, col, fs, Cols[i].a);
            SetAP(go, new Vector2(Cols[i].x, 0), new Vector2(.5f, .5f), new Vector2(Cols[i].w, ROW_H));
        }
    }

    Button OutlineBtn(string label, Color outlineCol, Color textCol, GameObject parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction cb)
    {
        var go = Rect("Btn_" + label, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = roundedSprite;
        img.type = Image.Type.Sliced;
        img.color = ROW_BG;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = outlineCol;
        outline.effectDistance = new Vector2(1, -1);

        if (!string.IsNullOrEmpty(label))
        {
            var lbl = TMP("L", go, label, 11, textCol, FontStyles.Bold, TextAlignmentOptions.Center);
            Fill(lbl);
        }

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(cb);
        return btn;
    }

    void TryClear()
    {
        if (!clearPending)
        {
            clearPending = true;
            foreach (var b in canvas.GetComponentsInChildren<Button>())
            {
                var t = b.GetComponentInChildren<TextMeshProUGUI>();
                if (t && t.text == "") t.text = "SURE?";
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
                if (t && t.text == "SURE?") t.text = "";
            }
        }
    }

    void GoBack()
    {
        string ret = PlayerPrefs.GetString("LB_ReturnScene", "MainMenu");
        SceneManager.LoadScene(ret);
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
}