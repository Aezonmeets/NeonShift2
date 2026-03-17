using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class LeaderboardScene : MonoBehaviour
{
    // ── FRONT-END PALETTE (HDR ENABLED FOR BLOOM) ──────
    static readonly Color DARK_BG = new Color(0.04f, 0.05f, 0.08f, 1f);

    // This multiplier pushes the colors past 1.0 (HDR) so the Camera's Bloom catches them
    const float GLOW = 1.8f;

    static readonly Color CYAN_ACCENT = new Color(0f, 0.85f * GLOW, 1f * GLOW, 1f);
    static readonly Color CYAN_HEADER = new Color(0f, 0.7f * GLOW, 0.8f * GLOW, 1f);

    // Background Tints 
    static readonly Color GOLD_BG = new Color(1f, 0.85f, 0f, 0.1f);
    static readonly Color CYAN_BG = new Color(0f, 0.85f, 1f, 0.1f);
    static readonly Color ROW_BG_DEFAULT = new Color(1f, 1f, 1f, 0.02f);
    static readonly Color TAB_ACTIVE_BG = new Color(0f, 0.85f, 1f, 0.15f);

    // Outline Borders
    static readonly Color GOLD_OUTLINE = new Color(1f * GLOW, 0.85f * GLOW, 0f, 0.8f);
    static readonly Color CYAN_OUTLINE = new Color(0f, 0.85f * GLOW, 1f * GLOW, 0.8f);
    static readonly Color BORDER_DIM = new Color(1f, 1f, 1f, 0.25f);

    // Text Colors
    static readonly Color GOLD_TEXT = new Color(1f * GLOW, 0.85f * GLOW, 0.1f * GLOW, 1f);
    static readonly Color DIM_TEXT = new Color(0.4f, 0.45f, 0.55f, 1f);
    static readonly Color EASY_GREEN = new Color(0.1f * GLOW, 1f * GLOW, 0.4f * GLOW, 1f);
    static readonly Color MED_YELLOW = new Color(1f * GLOW, 0.85f * GLOW, 0.1f * GLOW, 1f);
    static readonly Color HARD_ORANGE = new Color(1f * GLOW, 0.45f * GLOW, 0.1f * GLOW, 1f);
    static readonly Color ENDLESS_PINK = new Color(0.9f * GLOW, 0.1f * GLOW, 0.3f * GLOW, 1f);

    static readonly string[] TabLabels = { "ALL", "EASY", "MEDIUM", "HARD", "ENDLESS" };
    static readonly string[] TabFilters = { null, "Easy", "Medium", "Hard", "Endless" };

    static readonly (string h, float x, float w, TextAlignmentOptions a)[] Cols = {
        ("#",      -410f,  60f,  TextAlignmentOptions.Left),
        ("NAME",   -260f, 200f,  TextAlignmentOptions.Left),
        ("SCORE",     0f, 140f,  TextAlignmentOptions.Center),
        ("ACC",     140f,  90f,  TextAlignmentOptions.Center),
        ("MODE",    260f, 110f,  TextAlignmentOptions.Center),
        ("DATE",    390f, 130f,  TextAlignmentOptions.Right),
    };

    const float ROW_H = 48f;
    const float ROW_GAP = 8f;
    const float ROW_W = 940f;

    int currentTab = 0;
    List<LeaderboardEntry> displayList = new List<LeaderboardEntry>();

    GameObject canvas;
    GameObject rowContainer;
    ScrollRect scrollRect;
    Button[] tabButtons;

    Sprite fillSprite;
    Sprite borderSprite;
    Camera uiCamera;

    void Start()
    {
        // Find the camera aggressively (in case MainCamera tag is missing)
        uiCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        if (uiCamera != null)
        {
            uiCamera.backgroundColor = DARK_BG;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        fillSprite = GenerateSDFSprite(false);
        borderSprite = GenerateSDFSprite(true);

        var _ = LeaderboardManager.Instance;
        BuildUI();
        SpawnBackgroundParticles();
        SelectTab(0);
    }

    Sprite GenerateSDFSprite(bool hollow)
    {
        int size = 32;
        int borderThickness = 2;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 0f;
                if (hollow)
                {
                    if (x < borderThickness || x >= size - borderThickness ||
                        y < borderThickness || y >= size - borderThickness)
                    {
                        alpha = 1f;
                    }
                }
                else
                {
                    alpha = 1f;
                }
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        Vector4 sliceBorder = new Vector4(borderThickness, borderThickness, borderThickness, borderThickness);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, sliceBorder);
    }

    void SpawnBackgroundParticles()
    {
        var pCont = Rect("Particles", canvas);
        pCont.transform.SetSiblingIndex(1);

        for (int i = 0; i < 30; i++)
        {
            var p = Rect("Particle", pCont);
            var img = p.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0.8f * GLOW, 1f * GLOW, 0.15f);

            var rt = p.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(Random.Range(8, 15), 2);
            rt.anchoredPosition = new Vector2(Random.Range(-640, 640), Random.Range(-360, 360));
            rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-25, -35));

            var floater = p.gameObject.AddComponent<UIFloater>();
            floater.speed = Random.Range(10f, 25f);
        }
    }

    void BuildUI()
    {
        canvas = new GameObject("Canvas");
        var cv = canvas.AddComponent<Canvas>();

        // --- SCREEN SPACE CAMERA LOGIC ---
        // This is exactly what Unity needs to apply your camera's post-processing (Bloom) 
        // to the Canvas while keeping it pinned to the screen like an overlay.
        cv.renderMode = RenderMode.ScreenSpaceCamera;
        cv.worldCamera = uiCamera;
        cv.planeDistance = 1f; // Placed right in front of the camera to act as an overlay
        cv.sortingOrder = 10;

        var sc = canvas.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight = 0.5f;
        canvas.AddComponent<GraphicRaycaster>();

        var bg = Rect("BG", canvas); Fill(bg);
        bg.AddComponent<Image>().color = DARK_BG;

        var header = Rect("Header", canvas);
        AnchorStretchTop(header, 120f);

        // --- BACK & CLEAR BUTTONS ---
        var backBtn = OutlineBtn("BACK", CYAN_ACCENT, CYAN_ACCENT, new Color(0f, 0.1f, 0.15f, 0.8f), header, new Vector2(100, -70), new Vector2(90, 36), GoBack);
        var backRT = backBtn.GetComponent<RectTransform>();
        backRT.anchorMin = backRT.anchorMax = new Vector2(0, 1);

        var clearBtn = OutlineBtn("CLEAR ALL", CYAN_ACCENT, CYAN_ACCENT, new Color(0f, 0.1f, 0.15f, 0.8f), header, new Vector2(-100, -70), new Vector2(110, 36), ClearLeaderboard);
        var clearRT = clearBtn.GetComponent<RectTransform>();
        clearRT.anchorMin = clearRT.anchorMax = new Vector2(1, 1);

        // --- TITLE ---
        var titleContainer = Rect("TitleContainer", header);
        SetAP(titleContainer, new Vector2(0, -65), new Vector2(.5f, 1f), new Vector2(600, 100));

        var mainTitle = TMP("Title", titleContainer, "LEADERBOARD", 56, Color.white * GLOW, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAP(mainTitle, Vector2.zero, new Vector2(.5f, .5f), new Vector2(600, 100));
        mainTitle.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Overflow;

        var shadow = mainTitle.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(CYAN_ACCENT.r, CYAN_ACCENT.g, CYAN_ACCENT.b, 0.5f);
        shadow.effectDistance = new Vector2(0, -3);

        // --- TABS ---
        var tabBar = Rect("TabBar", canvas);
        var tbRT = tabBar.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = new Vector2(1, 1);
        tbRT.offsetMin = new Vector2(0, -180 - 36); tbRT.offsetMax = new Vector2(0, -180);

        tabButtons = new Button[TabLabels.Length];
        float tabW = 110f, gap = 12f;
        float totalW = TabLabels.Length * tabW + (TabLabels.Length - 1) * gap;
        float sx = -totalW / 2f + tabW / 2f;

        for (int i = 0; i < TabLabels.Length; i++)
        {
            int idx = i;
            var tabBtn = OutlineBtn(TabLabels[i], BORDER_DIM, DIM_TEXT, ROW_BG_DEFAULT, tabBar, new Vector2(sx + i * (tabW + gap), 0), new Vector2(tabW, 36f), () => { SelectTab(idx); });
            tabButtons[i] = tabBtn;
        }

        // --- COLUMNS HEADERS ---
        var colHeader = Rect("ColHeaders", canvas);
        var chRT = colHeader.GetComponent<RectTransform>();
        chRT.anchorMin = new Vector2(.5f, 1f); chRT.anchorMax = new Vector2(.5f, 1f);
        chRT.anchoredPosition = new Vector2(0, -(180f + 50f + 10f));
        chRT.sizeDelta = new Vector2(ROW_W, 30f);

        Color[] headerColors = new Color[6];
        for (int i = 0; i < 6; i++) headerColors[i] = CYAN_HEADER;
        PlaceRow(colHeader, null, headerColors, 11, FontStyles.Bold);

        // --- SCROLL VIEW ---
        var svObj = Rect("ScrollView", canvas);
        var svRT = svObj.GetComponent<RectTransform>();
        svRT.anchorMin = new Vector2(.5f, 0f); svRT.anchorMax = new Vector2(.5f, 1f);
        svRT.sizeDelta = new Vector2(ROW_W + 20f, 0f);
        svRT.offsetMin = new Vector2(-(ROW_W + 20f) / 2f, 40f);
        svRT.offsetMax = new Vector2((ROW_W + 20f) / 2f, -250f);

        scrollRect = svObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 45f;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;

        var viewport = Rect("Viewport", svObj);
        var vpRT = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;

        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(0, 0, 0, 0.01f);
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content Container
        rowContainer = Rect("Content", viewport);
        var rcRT = rowContainer.GetComponent<RectTransform>();
        rcRT.anchorMin = new Vector2(0, 1); rcRT.anchorMax = new Vector2(1, 1);
        rcRT.pivot = new Vector2(0.5f, 1f);
        rcRT.anchoredPosition = Vector2.zero;

        scrollRect.viewport = vpRT;
        scrollRect.content = rcRT;
    }

    void SelectTab(int idx)
    {
        currentTab = idx;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            var btnGo = tabButtons[i].gameObject;
            var fillImg = btnGo.GetComponent<Image>();
            var outlineImg = btnGo.transform.Find("Border").GetComponent<Image>();
            var txt = btnGo.GetComponentInChildren<TextMeshProUGUI>();

            if (i == currentTab)
            {
                fillImg.color = TAB_ACTIVE_BG;
                outlineImg.color = CYAN_ACCENT;
                txt.color = Color.white * GLOW;
            }
            else
            {
                fillImg.color = ROW_BG_DEFAULT;
                outlineImg.color = BORDER_DIM;
                txt.color = DIM_TEXT;
            }
        }

        string filter = TabFilters[currentTab];
        displayList = filter == null ? LeaderboardManager.Instance.GetAll() : LeaderboardManager.Instance.GetForMode(filter);

        scrollRect.verticalNormalizedPosition = 1f;
        RefreshRows();
    }

    Color GetModeColor(string mode)
    {
        if (mode == "EASY") return EASY_GREEN;
        if (mode == "MEDIUM") return MED_YELLOW;
        if (mode == "HARD") return HARD_ORANGE;
        if (mode == "ENDLESS") return ENDLESS_PINK;
        return DIM_TEXT;
    }

    string GetOrdinal(int num)
    {
        if (num <= 0) return num.ToString();
        switch (num % 100) { case 11: case 12: case 13: return num + "TH"; }
        switch (num % 10)
        {
            case 1: return num + "ST";
            case 2: return num + "ND";
            case 3: return num + "RD";
            default: return num + "TH";
        }
    }

    void RefreshRows()
    {
        foreach (Transform c in rowContainer.transform) Destroy(c.gameObject);

        int total = displayList.Count;
        int rowsToDisplay = Mathf.Min(total, 50);

        float contentHeight = rowsToDisplay * (ROW_H + ROW_GAP) + ROW_GAP;
        rowContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(0, contentHeight);

        if (rowsToDisplay == 0)
        {
            rowContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(0, ROW_H + ROW_GAP);
            CreateRow(1, default, false, 0);
            return;
        }

        for (int i = 0; i < rowsToDisplay; i++)
        {
            CreateRow(i + 1, displayList[i], true, i);
        }
    }

    void CreateRow(int rank, LeaderboardEntry e, bool hasData, int index)
    {
        float y = -(index * (ROW_H + ROW_GAP)) - (ROW_H * 0.5f) - ROW_GAP;

        var row = Rect("Row" + rank, rowContainer);
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0.5f, 1f);
        rowRT.anchorMax = new Vector2(0.5f, 1f);
        rowRT.pivot = new Vector2(0.5f, 0.5f);
        rowRT.anchoredPosition = new Vector2(0, y);
        rowRT.sizeDelta = new Vector2(ROW_W, ROW_H);

        var anim = row.AddComponent<RowAnimator>();
        anim.delay = index * 0.05f;

        var fillImg = row.AddComponent<Image>();
        fillImg.sprite = fillSprite;
        fillImg.type = Image.Type.Sliced;

        var borderObj = Rect("Border", row);
        Fill(borderObj);
        var outlineImg = borderObj.AddComponent<Image>();
        outlineImg.sprite = borderSprite;
        outlineImg.type = Image.Type.Sliced;

        Color[] colColors = new Color[6];

        if (!hasData)
        {
            fillImg.color = ROW_BG_DEFAULT;
            outlineImg.color = BORDER_DIM;
            for (int c = 0; c < 6; c++) colColors[c] = DIM_TEXT;
            colColors[0] = Color.white;
        }
        else if (rank == 1)
        {
            fillImg.color = GOLD_BG;
            outlineImg.color = GOLD_OUTLINE;
            colColors[0] = GOLD_TEXT;
            colColors[1] = Color.white * GLOW;
            colColors[2] = GOLD_TEXT;
            colColors[3] = Color.white * GLOW;
            colColors[4] = GetModeColor(e.mode.ToUpper());
            colColors[5] = GOLD_TEXT;
        }
        else if (rank == 2)
        {
            fillImg.color = CYAN_BG;
            outlineImg.color = CYAN_OUTLINE;
            colColors[0] = CYAN_ACCENT;
            colColors[1] = Color.white * GLOW;
            colColors[2] = CYAN_ACCENT;
            colColors[3] = Color.white * GLOW;
            colColors[4] = GetModeColor(e.mode.ToUpper());
            colColors[5] = DIM_TEXT;
        }
        else
        {
            fillImg.color = ROW_BG_DEFAULT;
            outlineImg.color = BORDER_DIM;
            colColors[0] = Color.white;
            colColors[1] = Color.white;
            colColors[2] = CYAN_ACCENT;
            colColors[3] = DIM_TEXT;
            colColors[4] = GetModeColor(e.mode.ToUpper());
            colColors[5] = DIM_TEXT;
        }

        string[] vals;
        if (hasData)
        {
            vals = new string[] {
                GetOrdinal(rank),
                e.name,
                e.score.ToString("N0"),
                e.accuracy,
                e.mode.ToUpper(),
                string.IsNullOrEmpty(e.date) ? "-" : e.date,
            };
        }
        else
        {
            vals = new string[] { "1ST", "---", "---", "---", "---", "---" };
        }

        PlaceRow(row, vals, colColors, 14, hasData ? FontStyles.Bold : FontStyles.Normal);
    }

    void PlaceRow(GameObject parent, string[] vals, Color[] cols, int sz, FontStyles fs)
    {
        for (int i = 0; i < Cols.Length; i++)
        {
            string txt = vals != null && i < vals.Length ? vals[i] : Cols[i].h;
            var go = TMP("C" + i, parent, txt, sz, cols[i], fs, Cols[i].a);
            SetAP(go, new Vector2(Cols[i].x, 0), new Vector2(.5f, .5f), new Vector2(Cols[i].w, ROW_H));
        }
    }

    Button OutlineBtn(string label, Color outlineCol, Color textCol, Color bgCol, GameObject parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction cb)
    {
        var go = Rect("Btn_" + label, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = fillSprite;
        img.type = Image.Type.Sliced;
        img.color = bgCol;

        var borderObj = Rect("Border", go);
        Fill(borderObj);
        var outlineImg = borderObj.AddComponent<Image>();
        outlineImg.sprite = borderSprite;
        outlineImg.type = Image.Type.Sliced;
        outlineImg.color = outlineCol;

        if (!string.IsNullOrEmpty(label))
        {
            var lbl = TMP("L", go, label, 12, textCol, FontStyles.Bold, TextAlignmentOptions.Center);
            Fill(lbl);
        }

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(cb);
        return btn;
    }

    void ClearLeaderboard()
    {
        displayList.Clear();
        Debug.Log("CLEAR ALL clicked!");
        RefreshRows();
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

public class RowAnimator : MonoBehaviour
{
    public float delay = 0f;
    CanvasGroup cg;
    Vector2 targetPos;
    RectTransform rt;
    float t = 0;

    void Start()
    {
        cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        rt = GetComponent<RectTransform>();
        targetPos = rt.anchoredPosition;
        rt.anchoredPosition = targetPos + new Vector2(150f, 0f);
    }

    void Update()
    {
        if (delay > 0) { delay -= Time.deltaTime; return; }

        t += Time.deltaTime * 6f;

        float eased = 1f - Mathf.Pow(2f, -10f * t);

        cg.alpha = Mathf.Lerp(0f, 1f, t * 2f);
        rt.anchoredPosition = Vector2.Lerp(targetPos + new Vector2(150f, 0f), targetPos, eased);

        if (t >= 1f)
        {
            rt.anchoredPosition = targetPos;
            cg.alpha = 1f;
            Destroy(this);
        }
    }
}

public class UIFloater : MonoBehaviour
{
    public float speed;
    RectTransform rt;
    void Start() { rt = GetComponent<RectTransform>(); }
    void Update()
    {
        rt.anchoredPosition += new Vector2(speed, speed) * Time.deltaTime;
        if (rt.anchoredPosition.x > 800) rt.anchoredPosition = new Vector2(-800, rt.anchoredPosition.y);
        if (rt.anchoredPosition.y > 500) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -500);
    }
}