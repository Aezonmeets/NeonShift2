using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    static readonly Color CYAN = new Color(0f, 0.92f, 1f);
    static readonly Color MAGENTA = new Color(1f, 0.15f, 0.75f);
    static readonly Color DARK_BG = new Color(0.04f, 0.06f, 0.10f);

    static readonly Color[] ModeBorderColors = {
        new Color(0.1f, 1f, 0.4f),    // Easy - green
        new Color(1f, 0.85f, 0.1f),   // Medium - yellow  
        new Color(1f, 0.45f, 0.1f),   // Hard - orange-red
        new Color(0.9f, 0.1f, 0.3f),  // Endless - red/pink
    };

    static readonly string[] ModeLabels = { "EASY", "MEDIUM", "HARD", "ENDLESS" };

    [Header("Assign Your Icons Here!")]
    public Sprite easyIcon;
    public Sprite mediumIcon;
    public Sprite hardIcon;
    public Sprite endlessIcon;
    public Sprite quitIcon;
    [Space(10)]
    public Sprite settingsIcon;
    public Sprite volumeIcon;
    public Sprite profileIcon;

    AudioSource music;

    void Start()
    {
        Camera.main.backgroundColor = DARK_BG;
        Camera.main.clearFlags = CameraClearFlags.SolidColor;

        music = gameObject.AddComponent<AudioSource>();
        music.loop = true; music.volume = 0.55f;
        var clip = Resources.Load<AudioClip>("Music/Menu");
        if (clip) { music.clip = clip; music.Play(); }

        Build();
    }

    void Build()
    {
        var cgo = new GameObject("Canvas");
        var cv = cgo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;

        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        // ── DARK BACKGROUND ───────────────────────────────────────────────
        var bg = new GameObject("BG"); bg.transform.SetParent(cgo.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = DARK_BG;

        // ── TOP NAV BAR ───────────────────────────────────────────────────
        HBar(cgo, new Color(CYAN.r, CYAN.g, CYAN.b, 0.3f), A(0, 1), A(1, 1), 0, -45, 1f);

        var topLogo = T(cgo, "↺ NEON SHIFT", 16, new Vector2(30, -22), A(0, 1), A(0, 1), TextAlignmentOptions.Left);
        topLogo.color = CYAN; topLogo.fontStyle = FontStyles.Bold;

        // Top Right Utility Buttons with REAL Images
        Sprite[] topSprites = { settingsIcon, volumeIcon, profileIcon };
        float rightStart = -130f;
        for (int i = 0; i < 3; i++)
        {
            var iconBtn = new GameObject("TopIcon_" + i);
            iconBtn.transform.SetParent(cgo.transform, false);
            var irt = iconBtn.AddComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = A(1, 1);
            irt.anchoredPosition = new Vector2(rightStart + (i * 45), -22);
            irt.sizeDelta = new Vector2(35, 35);

            var iImg = iconBtn.AddComponent<Image>();
            iImg.color = new Color(0.1f, 0.15f, 0.2f, 0.8f);
            iconBtn.AddComponent<Outline>().effectColor = new Color(CYAN.r, CYAN.g, CYAN.b, 0.4f);

            // Image inside the box
            var innerImgObj = new GameObject("Sprite");
            innerImgObj.transform.SetParent(iconBtn.transform, false);
            var innerRt = innerImgObj.AddComponent<RectTransform>();
            innerRt.anchorMin = A(0.2f, 0.2f); innerRt.anchorMax = A(0.8f, 0.8f);
            innerRt.offsetMin = innerRt.offsetMax = Vector2.zero;

            var actualImage = innerImgObj.AddComponent<Image>();
            actualImage.sprite = topSprites[i]; // Assigns your dragged sprite
            actualImage.color = CYAN;
            actualImage.preserveAspect = true;
        }

        // ── TITLE ─────────────────────────────────────────────────────────
        float titleY = 160f;
        var t1 = T(cgo, "NEON", 95, new Vector2(-150, titleY), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Right);
        t1.color = MAGENTA; t1.fontStyle = FontStyles.Bold;

        var t2 = T(cgo, "SHIFT", 95, new Vector2(160, titleY), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Left);
        t2.color = CYAN; t2.fontStyle = FontStyles.Bold;

        var sub = T(cgo, "4-LANE RHYTHM GAME", 18, new Vector2(0, titleY - 70), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        sub.color = CYAN; sub.fontStyle = FontStyles.Bold;
        sub.characterSpacing = 5f;

        var sub2 = T(cgo, "Tiles rotate! Stay sharp.", 14, new Vector2(0, titleY - 95), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        sub2.color = new Color(0.6f, 0.7f, 0.8f, 0.75f);

        // ── BUTTONS ───────────────────────────────────────────────────────
        Sprite[] modeSprites = { easyIcon, mediumIcon, hardIcon, endlessIcon };
        float startY = -10f, gap = -65f;

        for (int i = 0; i < 4; i++)
        {
            int mi = i;
            ModeButton(cgo, ModeLabels[i], modeSprites[i], ModeBorderColors[i],
                new Vector2(0, startY + gap * i), () => { PlayerPrefs.SetInt("SelectedMode", mi); SceneManager.LoadScene("GameScene"); });
        }

        // Leaderboard Button
        ModeButton(cgo, "LEADERBOARD", null, new Color(0.4f, 0.7f, 1f),
            new Vector2(0, startY + gap * 4 - 15), () => LeaderboardManager.OpenLeaderboardScene("MainMenu"));

        // Quit Button
        ModeButton(cgo, "QUIT GAME", quitIcon, new Color(0.4f, 0.45f, 0.55f),
            new Vector2(0, startY + gap * 5 - 15), () => Application.Quit());
    }

    // ── BUTTON GENERATOR ──────────────────────────────────────────────
    void ModeButton(GameObject p, string lbl, Sprite iconSprite, Color col, Vector2 pos, UnityEngine.Events.UnityAction cb)
    {
        var go = new GameObject("Btn_" + lbl);
        go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = A(.5f, .5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(380, 50);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.08f, 0.10f, 0.13f, 0.95f);
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(col.r, col.g, col.b, 0.25f);
        outline.effectDistance = new Vector2(1, -1);

        var bar = new GameObject("Bar"); bar.transform.SetParent(go.transform, false);
        var brt = bar.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0, 0f); brt.anchorMax = new Vector2(0, 1f);
        brt.offsetMin = new Vector2(0, 1); brt.offsetMax = new Vector2(5, -1);
        bar.AddComponent<Image>().color = new Color(col.r, col.g, col.b, 1f);

        var tgo = new GameObject("Label"); tgo.transform.SetParent(go.transform, false);
        var trt = tgo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(25, 0); trt.offsetMax = new Vector2(-20, 0);
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = lbl; tmp.fontSize = 18; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.characterSpacing = 3f;
        tmp.color = Color.white;

        // Right-side REAL Icon Image
        if (iconSprite != null)
        {
            var igo = new GameObject("Icon"); igo.transform.SetParent(go.transform, false);
            var irt = igo.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(1, 0.5f); irt.anchorMax = new Vector2(1, 0.5f);
            irt.anchoredPosition = new Vector2(-30, 0); // Position from the right edge
            irt.sizeDelta = new Vector2(24, 24); // Size of the icon
            var actualImage = igo.AddComponent<Image>();
            actualImage.sprite = iconSprite;
            actualImage.color = col; // Tints the icon to match the button's accent color
            actualImage.preserveAspect = true;
        }

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(cb);

        var cb2 = btn.colors;
        cb2.highlightedColor = new Color(col.r * 0.2f, col.g * 0.2f, col.b * 0.2f, 0.95f);
        cb2.pressedColor = new Color(col.r * 0.3f, col.g * 0.3f, col.b * 0.3f, 0.95f);
        btn.colors = cb2;
    }

    void HBar(GameObject p, Color col, Vector2 aMin, Vector2 aMax, float offY, float height, float alpha)
    {
        var go = new GameObject("HBar"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = new Vector2(0, offY); rt.offsetMax = new Vector2(0, offY + height);
        go.AddComponent<Image>().color = new Color(col.r, col.g, col.b, alpha);
    }

    TextMeshProUGUI T(GameObject p, string txt, int sz, Vector2 pos, Vector2 aMin, Vector2 aMax, TextAlignmentOptions al)
    {
        var go = new GameObject("_T"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(900, 130);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = sz; t.alignment = al; t.color = Color.white;
        return t;
    }

    static Vector2 A(float x, float y) => new Vector2(x, y);
}