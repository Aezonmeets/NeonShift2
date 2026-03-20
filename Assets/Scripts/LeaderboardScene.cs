using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class LeaderboardScene : MonoBehaviour
{
    // ── PALETTE ────────────────────────────────────────────────────────────
    static readonly Color DARK_BG        = new Color(0.04f, 0.05f, 0.08f, 1f);
    const float GLOW = 1.8f;
    static readonly Color CYAN_ACCENT    = new Color(0f,   0.85f * GLOW, 1f    * GLOW, 1f);
    static readonly Color CYAN_HEADER    = new Color(0f,   0.7f  * GLOW, 0.8f  * GLOW, 1f);
    static readonly Color GOLD_BG        = new Color(1f,   0.85f, 0f,    0.1f);
    static readonly Color CYAN_BG        = new Color(0f,   0.85f, 1f,    0.1f);
    static readonly Color ROW_BG_DEFAULT = new Color(1f,   1f,   1f,    0.02f);
    static readonly Color TAB_ACTIVE_BG  = new Color(0f,   0.85f, 1f,    0.15f);
    static readonly Color GOLD_OUTLINE   = new Color(1f  * GLOW, 0.85f * GLOW, 0f,           0.8f);
    static readonly Color CYAN_OUTLINE   = new Color(0f,          0.85f * GLOW, 1f  * GLOW,  0.8f);
    static readonly Color BORDER_DIM     = new Color(1f,   1f,   1f,    0.25f);
    static readonly Color GOLD_TEXT      = new Color(1f  * GLOW, 0.85f * GLOW, 0.1f * GLOW,  1f);
    static readonly Color DIM_TEXT       = new Color(0.4f, 0.45f, 0.55f, 1f);
    static readonly Color EASY_GREEN     = new Color(0.1f * GLOW, 1f   * GLOW, 0.4f * GLOW,  1f);
    static readonly Color MED_YELLOW     = new Color(1f  * GLOW, 0.85f * GLOW, 0.1f * GLOW,  1f);
    static readonly Color HARD_ORANGE    = new Color(1f  * GLOW, 0.45f * GLOW, 0.1f * GLOW,  1f);
    static readonly Color ENDLESS_PINK   = new Color(0.9f * GLOW, 0.1f * GLOW, 0.3f * GLOW,  1f);

    static readonly string[] TabLabels  = { "ALL", "EASY", "MEDIUM", "HARD", "ENDLESS" };
    static readonly string[] TabFilters = { null,  "Easy", "Medium", "Hard", "Endless" };

    static readonly (string h, float x, float w, TextAlignmentOptions a)[] Cols = {
        ("#",     -410f,  60f,  TextAlignmentOptions.Left),
        ("NAME",  -250f, 210f,  TextAlignmentOptions.Left),
        ("SCORE",    0f, 140f,  TextAlignmentOptions.Center),
        ("ACC",    140f,  90f,  TextAlignmentOptions.Center),
        ("MODE",   260f, 110f,  TextAlignmentOptions.Center),
        ("DATE",   390f, 130f,  TextAlignmentOptions.Right),
    };

    const float ROW_H       = 48f;
    const float ROW_GAP     = 8f;
    const float ROW_W       = 940f;
    const float AVATAR_SIZE = 34f;

    int   currentTab = 0;
    List<LeaderboardEntry> displayList = new List<LeaderboardEntry>();

    GameObject canvas;
    GameObject rowContainer;
    ScrollRect scrollRect;
    Button[]   tabButtons;

    Sprite fillSprite;
    Sprite borderSprite;
    Sprite knobSprite;
    Camera uiCamera;

    // ── LIFECYCLE ──────────────────────────────────────────────────────────
    void Start()
    {
        uiCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (uiCamera != null)
        {
            uiCamera.backgroundColor = DARK_BG;
            uiCamera.clearFlags      = CameraClearFlags.SolidColor;
        }

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        fillSprite   = GenerateSDFSprite(false);
        borderSprite = GenerateSDFSprite(true);
        knobSprite   = MakeCircleSprite();   // generated at runtime — no Knob.psd needed

        // ── KEY FIX: always reload scores from PlayerPrefs before displaying ──
        // This guarantees fresh data whether we arrived from the game scene,
        // main menu, or anywhere else.
        LeaderboardManager.Instance.ReloadFromDisk();

        BuildUI();
        SpawnBackgroundParticles();
        SelectTab(0);
    }

    // ── CIRCLE SPRITE (replaces missing UI/Skin/Knob.psd) ─────────────────
    static Sprite MakeCircleSprite(int size = 64)
    {
        var tex        = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels     = new Color[size * size];
        float r  = size * 0.5f;
        float cx = r - 0.5f;
        float cy = r - 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx    = x - cx;
            float dy    = y - cy;
            float alpha = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ── AVATAR SPRITE ─────────────────────────────────────────────────────
    // Reads from the static cache populated by MainMenuManager.
    // Falls back to initials if the cache is empty (e.g. opened mid-session).
    static Sprite GetAvatarSprite(int index)
        => LeaderboardManager.GetCachedAvatarSprite(index);

    // ── SDF SPRITE (rows & buttons) ───────────────────────────────────────
    Sprite GenerateSDFSprite(bool hollow)
    {
        int size = 32, b = 2;
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float alpha = hollow
                ? (x < b || x >= size - b || y < b || y >= size - b ? 1f : 0f)
                : 1f;
            pixels[y * size + x] = new Color(1, 1, 1, alpha);
        }
        tex.SetPixels(pixels); tex.Apply();
        var border = new Vector4(b, b, b, b);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, border);
    }

    // ── BACKGROUND ─────────────────────────────────────────────────────────
    void SpawnBackgroundParticles()
    {
        var pCont = Rect("Particles", canvas);
        pCont.transform.SetSiblingIndex(1);
        for (int i = 0; i < 30; i++)
        {
            var p = Rect("Particle", pCont);
            p.gameObject.AddComponent<Image>().color = new Color(0f, 0.8f * GLOW, 1f * GLOW, 0.15f);
            var rt = p.GetComponent<RectTransform>();
            rt.sizeDelta        = new Vector2(Random.Range(8, 15), 2);
            rt.anchoredPosition = new Vector2(Random.Range(-640, 640), Random.Range(-360, 360));
            rt.localRotation    = Quaternion.Euler(0, 0, Random.Range(-25, -35));
            p.gameObject.AddComponent<UIFloater>().speed = Random.Range(10f, 25f);
        }
    }

    // ── UI CONSTRUCTION ────────────────────────────────────────────────────
    void BuildUI()
    {
        canvas = new GameObject("Canvas");
        var cv = canvas.AddComponent<Canvas>();
        cv.renderMode    = RenderMode.ScreenSpaceCamera;
        cv.worldCamera   = uiCamera;
        cv.planeDistance = 1f;
        cv.sortingOrder  = 10;

        var sc = canvas.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight  = 0.5f;
        canvas.AddComponent<GraphicRaycaster>();

        var bg = Rect("BG", canvas); Fill(bg);
        bg.AddComponent<Image>().color = DARK_BG;

        var header = Rect("Header", canvas);
        AnchorStretchTop(header, 120f);

        var backBtn  = OutlineBtn("BACK",      CYAN_ACCENT, CYAN_ACCENT, new Color(0f, 0.1f, 0.15f, 0.8f), header, new Vector2(100,  -70), new Vector2(90,  36), GoBack);
        var clearBtn = OutlineBtn("CLEAR ALL", CYAN_ACCENT, CYAN_ACCENT, new Color(0f, 0.1f, 0.15f, 0.8f), header, new Vector2(-100, -70), new Vector2(110, 36), ClearLeaderboard);
        backBtn .GetComponent<RectTransform>().anchorMin =
        backBtn .GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
        clearBtn.GetComponent<RectTransform>().anchorMin =
        clearBtn.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);

        var titleCont = Rect("TitleContainer", header);
        SetAP(titleCont, new Vector2(0, -65), new Vector2(.5f, 1f), new Vector2(600, 100));
        var mainTitle = TMP("Title", titleCont, "LEADERBOARD", 56, Color.white * GLOW, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAP(mainTitle, Vector2.zero, new Vector2(.5f, .5f), new Vector2(600, 100));
        mainTitle.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Overflow;
        var shadow = mainTitle.gameObject.AddComponent<Shadow>();
        shadow.effectColor    = new Color(CYAN_ACCENT.r, CYAN_ACCENT.g, CYAN_ACCENT.b, 0.5f);
        shadow.effectDistance = new Vector2(0, -3);

        // Tabs
        var tabBar = Rect("TabBar", canvas);
        var tbRT   = tabBar.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = new Vector2(1, 1);
        tbRT.offsetMin = new Vector2(0, -180 - 36); tbRT.offsetMax = new Vector2(0, -180);

        tabButtons = new Button[TabLabels.Length];
        float tabW = 110f, gap = 12f;
        float totalW = TabLabels.Length * tabW + (TabLabels.Length - 1) * gap;
        float sx = -totalW / 2f + tabW / 2f;
        for (int i = 0; i < TabLabels.Length; i++)
        {
            int idx = i;
            tabButtons[i] = OutlineBtn(TabLabels[i], BORDER_DIM, DIM_TEXT, ROW_BG_DEFAULT, tabBar,
                new Vector2(sx + i * (tabW + gap), 0), new Vector2(tabW, 36f), () => SelectTab(idx));
        }

        // Column headers
        var colHdr = Rect("ColHeaders", canvas);
        var chRT   = colHdr.GetComponent<RectTransform>();
        chRT.anchorMin        = new Vector2(.5f, 1f); chRT.anchorMax = new Vector2(.5f, 1f);
        chRT.anchoredPosition = new Vector2(0, -(180f + 50f + 10f));
        chRT.sizeDelta        = new Vector2(ROW_W, 30f);
        Color[] hc = new Color[6]; for (int i = 0; i < 6; i++) hc[i] = CYAN_HEADER;
        PlaceRow(colHdr, null, hc, 11, FontStyles.Bold);

        // Scroll view
        var svObj = Rect("ScrollView", canvas);
        var svRT  = svObj.GetComponent<RectTransform>();
        svRT.anchorMin = new Vector2(.5f, 0f); svRT.anchorMax = new Vector2(.5f, 1f);
        svRT.sizeDelta = new Vector2(ROW_W + 20f, 0f);
        svRT.offsetMin = new Vector2(-(ROW_W + 20f) / 2f,  40f);
        svRT.offsetMax = new Vector2( (ROW_W + 20f) / 2f, -250f);

        scrollRect = svObj.AddComponent<ScrollRect>();
        scrollRect.horizontal        = false;
        scrollRect.vertical          = true;
        scrollRect.scrollSensitivity = 45f;
        scrollRect.movementType      = ScrollRect.MovementType.Elastic;
        scrollRect.inertia           = true;
        scrollRect.decelerationRate  = 0.135f;

        var viewport = Rect("Viewport", svObj);
        var vpRT     = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        rowContainer = Rect("Content", viewport);
        var rcRT = rowContainer.GetComponent<RectTransform>();
        rcRT.anchorMin        = new Vector2(0, 1); rcRT.anchorMax = new Vector2(1, 1);
        rcRT.pivot            = new Vector2(0.5f, 1f);
        rcRT.anchoredPosition = Vector2.zero;

        scrollRect.viewport = vpRT;
        scrollRect.content  = rcRT;
    }

    // ── TABS ───────────────────────────────────────────────────────────────
    void SelectTab(int idx)
    {
        currentTab = idx;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            var g = tabButtons[i].gameObject;
            bool active = (i == currentTab);
            g.GetComponent<Image>().color                          = active ? TAB_ACTIVE_BG     : ROW_BG_DEFAULT;
            g.transform.Find("Border").GetComponent<Image>().color = active ? CYAN_ACCENT       : BORDER_DIM;
            g.GetComponentInChildren<TextMeshProUGUI>().color      = active ? Color.white * GLOW : DIM_TEXT;
        }

        string filter = TabFilters[currentTab];
        displayList   = filter == null
            ? LeaderboardManager.Instance.GetAll()
            : LeaderboardManager.Instance.GetForMode(filter);

        Debug.Log($"[LeaderboardScene] Tab '{TabLabels[idx]}' selected — {displayList.Count} entries.");

        scrollRect.verticalNormalizedPosition = 1f;
        RefreshRows();
    }

    Color GetModeColor(string mode)
    {
        if (mode == "EASY")    return EASY_GREEN;
        if (mode == "MEDIUM")  return MED_YELLOW;
        if (mode == "HARD")    return HARD_ORANGE;
        if (mode == "ENDLESS") return ENDLESS_PINK;
        return DIM_TEXT;
    }

    string GetOrdinal(int n)
    {
        if (n <= 0) return n.ToString();
        switch (n % 100) { case 11: case 12: case 13: return n + "TH"; }
        switch (n % 10)
        {
            case 1: return n + "ST";
            case 2: return n + "ND";
            case 3: return n + "RD";
            default: return n + "TH";
        }
    }

    // ── ROWS ───────────────────────────────────────────────────────────────
    void RefreshRows()
    {
        foreach (Transform c in rowContainer.transform) Destroy(c.gameObject);

        int   count    = Mathf.Min(displayList.Count, 50);
        float contentH = count * (ROW_H + ROW_GAP) + ROW_GAP;
        rowContainer.GetComponent<RectTransform>().sizeDelta =
            new Vector2(0, contentH > 0 ? contentH : ROW_H + ROW_GAP);

        if (count == 0) { CreateRow(1, default, false, 0); return; }
        for (int i = 0; i < count; i++) CreateRow(i + 1, displayList[i], true, i);
    }

    void CreateRow(int rank, LeaderboardEntry e, bool hasData, int index)
    {
        float y = -(index * (ROW_H + ROW_GAP)) - (ROW_H * 0.5f) - ROW_GAP;

        var row   = Rect("Row" + rank, rowContainer);
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = rowRT.anchorMax = new Vector2(0.5f, 1f);
        rowRT.pivot            = new Vector2(0.5f, 0.5f);
        rowRT.anchoredPosition = new Vector2(0, y);
        rowRT.sizeDelta        = new Vector2(ROW_W, ROW_H);

        row.AddComponent<RowAnimator>().delay = index * 0.05f;

        var fillImg    = row.AddComponent<Image>(); fillImg.sprite = fillSprite; fillImg.type = Image.Type.Sliced;
        var borderObj  = Rect("Border", row); Fill(borderObj);
        var outlineImg = borderObj.AddComponent<Image>(); outlineImg.sprite = borderSprite; outlineImg.type = Image.Type.Sliced;

        Color[] cc = new Color[6];
        if (!hasData)
        {
            fillImg.color = ROW_BG_DEFAULT; outlineImg.color = BORDER_DIM;
            for (int c = 0; c < 6; c++) cc[c] = DIM_TEXT; cc[0] = Color.white;
        }
        else if (rank == 1)
        {
            fillImg.color = GOLD_BG; outlineImg.color = GOLD_OUTLINE;
            cc[0]=GOLD_TEXT; cc[1]=Color.white*GLOW; cc[2]=GOLD_TEXT; cc[3]=Color.white*GLOW;
            cc[4]=GetModeColor(e.mode.ToUpper()); cc[5]=GOLD_TEXT;
        }
        else if (rank == 2)
        {
            fillImg.color = CYAN_BG; outlineImg.color = CYAN_OUTLINE;
            cc[0]=CYAN_ACCENT; cc[1]=Color.white*GLOW; cc[2]=CYAN_ACCENT; cc[3]=Color.white*GLOW;
            cc[4]=GetModeColor(e.mode.ToUpper()); cc[5]=DIM_TEXT;
        }
        else
        {
            fillImg.color = ROW_BG_DEFAULT; outlineImg.color = BORDER_DIM;
            cc[0]=Color.white; cc[1]=Color.white; cc[2]=CYAN_ACCENT; cc[3]=DIM_TEXT;
            cc[4]=GetModeColor(e.mode.ToUpper()); cc[5]=DIM_TEXT;
        }

        string[] vals = hasData
            ? new[] { GetOrdinal(rank), e.name, e.score.ToString("N0"), e.accuracy, e.mode.ToUpper(), string.IsNullOrEmpty(e.date) ? "-" : e.date }
            : new[] { "1ST", "---", "---", "---", "---", "---" };

        PlaceRow(row, vals, cc, 14, hasData ? FontStyles.Bold : FontStyles.Normal, e, hasData);
    }

    void PlaceRow(GameObject parent, string[] vals, Color[] cols, int sz, FontStyles fs,
                  LeaderboardEntry entry = default, bool hasEntry = false)
    {
        for (int i = 0; i < Cols.Length; i++)
        {
            string txt = vals != null && i < vals.Length ? vals[i] : Cols[i].h;

            if (i == 1 && hasEntry)
            {
                // NAME column: avatar circle + name text
                var cell = Rect("C1", parent);
                SetAP(cell, new Vector2(Cols[i].x, 0), new Vector2(.5f, .5f), new Vector2(Cols[i].w, ROW_H));

                // Circle mask
                var cGo = Rect("AvatarCircle", cell);
                var cRT = cGo.GetComponent<RectTransform>();
                cRT.anchorMin = cRT.anchorMax = new Vector2(0f, 0.5f);
                cRT.anchoredPosition = new Vector2(AVATAR_SIZE * 0.5f, 0f);
                cRT.sizeDelta        = new Vector2(AVATAR_SIZE, AVATAR_SIZE);
                var cImg = cGo.AddComponent<Image>();
                cImg.sprite = knobSprite; cImg.color = new Color(0.12f, 0.14f, 0.20f, 1f);
                cImg.type = Image.Type.Simple; cImg.preserveAspect = false;
                cGo.AddComponent<Mask>().showMaskGraphic = true;

                // Avatar sprite
                Sprite sp  = GetAvatarSprite(entry.avatarIndex);
                var sprGo  = Rect("AvatarImg", cGo);
                var sprRT  = sprGo.GetComponent<RectTransform>();
                sprRT.anchorMin = Vector2.zero; sprRT.anchorMax = Vector2.one;
                sprRT.offsetMin = new Vector2(2f,2f); sprRT.offsetMax = new Vector2(-2f,-2f);
                var sprImg = sprGo.AddComponent<Image>();
                if (sp != null) { sprImg.sprite = sp; sprImg.color = Color.white; sprImg.preserveAspect = true; }
                else
                {
                    sprImg.color = new Color(0.20f, 0.22f, 0.30f, 1f);
                    var init = Rect("Initial", sprGo); Fill(init);
                    var t    = init.AddComponent<TextMeshProUGUI>();
                    t.text = entry.name.Length > 0 ? entry.name[0].ToString().ToUpper() : "?";
                    t.fontSize = 14; t.fontStyle = FontStyles.Bold;
                    t.alignment = TextAlignmentOptions.Center;
                    t.color = new Color(1f,1f,1f,0.6f); t.raycastTarget = false;
                }
                sprImg.raycastTarget = false;

                // Name text
                var nGo = Rect("NameText", cell);
                var nRT = nGo.GetComponent<RectTransform>();
                nRT.anchorMin = Vector2.zero; nRT.anchorMax = Vector2.one;
                nRT.offsetMin = new Vector2(AVATAR_SIZE + 6f, 0); nRT.offsetMax = Vector2.zero;
                var nTmp = nGo.AddComponent<TextMeshProUGUI>();
                nTmp.text = txt; nTmp.fontSize = sz; nTmp.fontStyle = fs;
                nTmp.alignment = Cols[i].a; nTmp.color = cols[i];
                nTmp.textWrappingMode = TextWrappingModes.NoWrap;
                nTmp.overflowMode     = TextOverflowModes.Ellipsis;
            }
            else
            {
                var go = TMP("C" + i, parent, txt, sz, cols[i], fs, Cols[i].a);
                SetAP(go, new Vector2(Cols[i].x, 0), new Vector2(.5f, .5f), new Vector2(Cols[i].w, ROW_H));
            }
        }
    }

    // ── BUTTONS ────────────────────────────────────────────────────────────
    Button OutlineBtn(string label, Color outlineCol, Color textCol, Color bgCol,
                      GameObject parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction cb)
    {
        var go = Rect("Btn_" + label, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;

        var img = go.AddComponent<Image>(); img.sprite = fillSprite; img.type = Image.Type.Sliced; img.color = bgCol;
        var bdr = Rect("Border", go); Fill(bdr);
        bdr.AddComponent<Image>().sprite = borderSprite;
        bdr.GetComponent<Image>().type   = Image.Type.Sliced;
        bdr.GetComponent<Image>().color  = outlineCol;

        if (!string.IsNullOrEmpty(label)) Fill(TMP("L", go, label, 12, textCol, FontStyles.Bold, TextAlignmentOptions.Center));

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(cb);
        return btn;
    }

    void ClearLeaderboard() { LeaderboardManager.Instance.ClearAll(); displayList.Clear(); RefreshRows(); }
    void GoBack()           { SceneManager.LoadScene(PlayerPrefs.GetString("LB_ReturnScene", "MainMenu")); }

    // ── STATIC HELPERS ─────────────────────────────────────────────────────
    static GameObject Rect(string n, GameObject p) { var g = new GameObject(n); g.transform.SetParent(p.transform,false); g.AddComponent<RectTransform>(); return g; }
    static void Fill(GameObject g) { var r=g.GetComponent<RectTransform>(); r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; }
    static void AnchorStretchTop(GameObject g,float h) { var r=g.GetComponent<RectTransform>(); r.anchorMin=new Vector2(0,1); r.anchorMax=new Vector2(1,1); r.offsetMin=new Vector2(0,-h); r.offsetMax=Vector2.zero; }
    static GameObject TMP(string n,GameObject p,string txt,int sz,Color col,FontStyles st,TextAlignmentOptions al) { var g=Rect(n,p); var t=g.AddComponent<TextMeshProUGUI>(); t.text=txt; t.fontSize=sz; t.fontStyle=st; t.alignment=al; t.color=col; t.textWrappingMode=TextWrappingModes.NoWrap; t.overflowMode=TextOverflowModes.Ellipsis; return g; }
    static void SetAP(GameObject g,Vector2 pos,Vector2 anch,Vector2 sz) { var r=g.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=anch; r.anchoredPosition=pos; r.sizeDelta=sz; }
}

// ── ANIMATION HELPERS ──────────────────────────────────────────────────────
public class RowAnimator : MonoBehaviour
{
    public float  delay = 0f;
    CanvasGroup   cg;
    Vector2       targetPos;
    RectTransform rt;
    float t = 0;
    void Start()  { cg=gameObject.AddComponent<CanvasGroup>(); cg.alpha=0f; rt=GetComponent<RectTransform>(); targetPos=rt.anchoredPosition; rt.anchoredPosition=targetPos+new Vector2(150f,0f); }
    void Update() { if(delay>0){delay-=Time.deltaTime;return;} t+=Time.deltaTime*6f; float e=1f-Mathf.Pow(2f,-10f*t); cg.alpha=Mathf.Lerp(0f,1f,t*2f); rt.anchoredPosition=Vector2.Lerp(targetPos+new Vector2(150f,0f),targetPos,e); if(t>=1f){rt.anchoredPosition=targetPos;cg.alpha=1f;Destroy(this);} }
}

public class UIFloater : MonoBehaviour
{
    public float speed;
    RectTransform rt;
    void Start()  { rt = GetComponent<RectTransform>(); }
    void Update() { rt.anchoredPosition+=new Vector2(speed,speed)*Time.deltaTime; if(rt.anchoredPosition.x>800)rt.anchoredPosition=new Vector2(-800,rt.anchoredPosition.y); if(rt.anchoredPosition.y>500)rt.anchoredPosition=new Vector2(rt.anchoredPosition.x,-500); }
}