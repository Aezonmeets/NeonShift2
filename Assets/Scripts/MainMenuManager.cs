using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Unity.Burst.Intrinsics.Arm;

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

    [Header("Audio Settings")]
    public AudioClip menuMusic;
    public AudioClip hoverSound;
    public AudioClip clickSound;

    [Header("Assign Your Icons Here!")]
    public Sprite easyIcon;
    public Sprite mediumIcon;
    public Sprite hardIcon;
    public Sprite endlessIcon;
    public Sprite quitIcon;
    [Space(10)]
    public Sprite settingsIcon;

    AudioSource music;
    AudioSource sfx;

    void Start()
    {
        Camera.main.backgroundColor = DARK_BG;
        Camera.main.clearFlags = CameraClearFlags.SolidColor;

        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.volume = PlayerPrefs.GetFloat("MusicVolume", 0.55f);
        if (menuMusic != null) { music.clip = menuMusic; music.Play(); }

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.volume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);

        Build();
    }

    [ContextMenu("Show UI in Editor")]
    void ShowUIInEditor()
    {
        GameObject oldCanvas = GameObject.Find("Canvas");
        if (oldCanvas != null) { DestroyImmediate(oldCanvas); }
        Build();
    }

    void Build()
    {
        var cgo = new GameObject("Canvas");
        var cv = cgo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceCamera;
        cv.worldCamera = Camera.main;
        cv.planeDistance = 5f;

        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        GameObject settingsPanel = null;

        // ── DARK BACKGROUND ──
        var bg = new GameObject("BG"); bg.transform.SetParent(cgo.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = DARK_BG;

        // ── TOP NAV BAR ──
        HBar(cgo, new Color(CYAN.r, CYAN.g, CYAN.b, 0.3f), A(0, 1), A(1, 1), 0, -45, 1f);
        var topLogo = T(cgo, "↺ NEON SHIFT", 16, new Vector2(30, -22), A(0, 1), A(0, 1), TextAlignmentOptions.Left);
        topLogo.color = CYAN; topLogo.fontStyle = FontStyles.Bold;

        // ── SETTINGS BUTTON ──
        var iconBtn = new GameObject("TopIcon_Settings");
        iconBtn.transform.SetParent(cgo.transform, false);
        var irt = iconBtn.AddComponent<RectTransform>();
        irt.anchorMin = irt.anchorMax = A(1, 1);
        irt.anchoredPosition = new Vector2(-40, -22);
        irt.sizeDelta = new Vector2(35, 35);

        var iImg = iconBtn.AddComponent<Image>();
        iImg.color = new Color(0.1f, 0.15f, 0.2f, 0.8f);
        iconBtn.AddComponent<Outline>().effectColor = new Color(CYAN.r, CYAN.g, CYAN.b, 0.4f);

        var innerImgObj = new GameObject("Sprite");
        innerImgObj.transform.SetParent(iconBtn.transform, false);
        var innerRt = innerImgObj.AddComponent<RectTransform>();
        innerRt.anchorMin = A(0.2f, 0.2f); innerRt.anchorMax = A(0.8f, 0.8f);
        innerRt.offsetMin = innerRt.offsetMax = Vector2.zero;
        var actualImage = innerImgObj.AddComponent<Image>();
        actualImage.sprite = settingsIcon;
        actualImage.color = CYAN; actualImage.preserveAspect = true;

        var topBtn = iconBtn.AddComponent<Button>();
        AddButtonSounds(topBtn);
        iconBtn.AddComponent<NeonButtonJuice>(); // Smooth hover!

        topBtn.onClick.AddListener(() => {
            if (settingsPanel != null) settingsPanel.GetComponent<NeonPanelAnim>().Toggle();
        });

        // ── TITLE (Fixed Order: NEONSHIFT) ──
        float titleY = 200f;

        var titleParent = new GameObject("TitleGroup");
        titleParent.transform.SetParent(cgo.transform, false);
        var tpRt = titleParent.AddComponent<RectTransform>();
        tpRt.anchorMin = tpRt.anchorMax = A(.5f, .5f);
        tpRt.anchoredPosition = new Vector2(0, titleY);
        titleParent.AddComponent<NeonBreathingAnim>();

        // NEON on the left (Cyan)
        var t1 = T(titleParent, "SHIFT", 95, new Vector2(-160, 0), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Right);
        t1.color = CYAN; t1.fontStyle = FontStyles.Bold;

        // SHIFT on the right (Magenta)
        var t2 = T(titleParent, "NEON", 95, new Vector2(150, 0), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Left);
        t2.color = MAGENTA; t2.fontStyle = FontStyles.Bold;

        var sub = T(titleParent, "4-LANE RHYTHM GAME", 18, new Vector2(0, -70), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        sub.color = CYAN; sub.fontStyle = FontStyles.Bold;
        sub.characterSpacing = 5f;

        // ── BUTTONS (Adjusted layout gaps so Quit fits perfectly) ──
        Sprite[] modeSprites = { easyIcon, mediumIcon, hardIcon, endlessIcon };
        float startY = 40f, gap = -55f; // Tighter layout

        for (int i = 0; i < 4; i++)
        {
            int mi = i;
            ModeButton(cgo, ModeLabels[i], modeSprites[i], ModeBorderColors[i],
                new Vector2(0, startY + gap * i), () => { PlayerPrefs.SetInt("SelectedMode", mi); SceneManager.LoadScene("GameScene"); });
        }

        // Leaderboard and Quit are spaced slightly lower but still fully on-screen
        ModeButton(cgo, "LEADERBOARD", null, new Color(0.4f, 0.7f, 1f),
            new Vector2(0, startY + gap * 4 - 10), () => LeaderboardManager.OpenLeaderboardScene("MainMenu"));

        ModeButton(cgo, "QUIT GAME", quitIcon, new Color(0.4f, 0.45f, 0.55f),
            new Vector2(0, startY + gap * 5 - 10), () => Application.Quit());

        // ── SETTINGS PANEL ──
        settingsPanel = CreateSettingsPanel(cgo);
    }

    GameObject CreateSettingsPanel(GameObject canvasGo)
    {
        var panel = new GameObject("SettingsPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        var prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = A(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(450, 400);

        var img = panel.AddComponent<Image>();
        img.color = new Color(0.05f, 0.07f, 0.11f, 0.98f);
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = CYAN;
        outline.effectDistance = new Vector2(2, -2);

        T(panel, "SETTINGS", 35, new Vector2(0, 140), A(0.5f, 0.5f), A(0.5f, 0.5f), TextAlignmentOptions.Center).color = MAGENTA;

        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 0.55f);
        CreateSlider(panel, "MUSIC VOLUME", 40, savedMusic, (val) => {
            PlayerPrefs.SetFloat("MusicVolume", val);
            if (music != null) music.volume = val;
        });

        float savedSfx = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
        CreateSlider(panel, "SFX VOLUME", -40, savedSfx, (val) => {
            PlayerPrefs.SetFloat("SFXVolume", val);
            if (sfx != null) sfx.volume = val;
        });

        ModeButton(panel, "CLOSE MENU", null, new Color(0.4f, 0.45f, 0.55f), new Vector2(0, -140), () => {
            panel.GetComponent<NeonPanelAnim>().Hide();
            if (sfx != null && clickSound != null) sfx.PlayOneShot(clickSound);
        });

        // Add the new smooth fader component instead of using SetActive()
        panel.AddComponent<NeonPanelAnim>();

        return panel;
    }

    void CreateSlider(GameObject parent, string label, float yPos, float startVal, UnityEngine.Events.UnityAction<float> onValueChanged)
    {
        T(parent, label, 18, new Vector2(0, yPos + 30), A(0.5f, 0.5f), A(0.5f, 0.5f), TextAlignmentOptions.Center).color = CYAN;

        var sgo = new GameObject("Slider_" + label);
        sgo.transform.SetParent(parent.transform, false);
        var srt = sgo.AddComponent<RectTransform>();
        srt.anchoredPosition = new Vector2(0, yPos);
        srt.sizeDelta = new Vector2(300, 25);

        var bg = new GameObject("Background"); bg.transform.SetParent(sgo.transform, false);
        var bgrt = bg.AddComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
        bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>(); bgImg.color = new Color(0.02f, 0.03f, 0.05f);
        bg.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.2f);

        var fillArea = new GameObject("FillArea"); fillArea.transform.SetParent(sgo.transform, false);
        var fart = fillArea.AddComponent<RectTransform>();
        fart.anchorMin = Vector2.zero; fart.anchorMax = Vector2.one;
        fart.offsetMin = new Vector2(2, 2); fart.offsetMax = new Vector2(-2, -2);

        var fill = new GameObject("Fill"); fill.transform.SetParent(fillArea.transform, false);
        var frt = fill.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = CYAN;

        var slider = sgo.AddComponent<Slider>();
        slider.targetGraphic = bgImg;
        slider.fillRect = frt;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = startVal;
        slider.onValueChanged.AddListener(onValueChanged);
    }

    void ModeButton(GameObject p, string lbl, Sprite iconSprite, Color col, Vector2 pos, UnityEngine.Events.UnityAction cb)
    {
        var go = new GameObject("Btn_" + lbl);
        go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = A(.5f, .5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(350, 45); // Made slightly sleeker

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

        if (iconSprite != null)
        {
            var igo = new GameObject("Icon"); igo.transform.SetParent(go.transform, false);
            var irt = igo.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(1, 0.5f); irt.anchorMax = new Vector2(1, 0.5f);
            irt.anchoredPosition = new Vector2(-30, 0);
            irt.sizeDelta = new Vector2(22, 22);
            var actualImage = igo.AddComponent<Image>();
            actualImage.sprite = iconSprite;
            actualImage.color = col; actualImage.preserveAspect = true;
        }

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(cb);

        var cb2 = btn.colors;
        cb2.fadeDuration = 0.1f; // Smoother color transitions
        cb2.highlightedColor = new Color(col.r * 0.2f, col.g * 0.2f, col.b * 0.2f, 0.95f);
        cb2.pressedColor = new Color(col.r * 0.3f, col.g * 0.3f, col.b * 0.3f, 0.95f);
        btn.colors = cb2;

        AddButtonSounds(btn);
        go.AddComponent<NeonButtonJuice>(); // Attach the smooth scaling animation!
    }

    void AddButtonSounds(Button btn)
    {
        btn.onClick.AddListener(() => {
            if (sfx != null && clickSound != null) sfx.PlayOneShot(clickSound);
        });

        EventTrigger trigger = btn.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener((data) => {
            if (sfx != null && hoverSound != null) sfx.PlayOneShot(hoverSound);
        });
        trigger.triggers.Add(entry);
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

// ──────────────────────────────────────────────────────────────────────────
// ── UI ANIMATION HELPERS (These handle all the smoothness automatically) ──
// ──────────────────────────────────────────────────────────────────────────

public class NeonButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    Vector3 targetScale = Vector3.one;

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 15f);
    }

    public void OnPointerEnter(PointerEventData eventData) => targetScale = new Vector3(1.05f, 1.05f, 1f);
    public void OnPointerExit(PointerEventData eventData) => targetScale = Vector3.one;
    public void OnPointerDown(PointerEventData eventData) => targetScale = new Vector3(0.95f, 0.95f, 1f);
    public void OnPointerUp(PointerEventData eventData) => targetScale = new Vector3(1.05f, 1.05f, 1f);
}

public class NeonPanelAnim : MonoBehaviour
{
    bool isShowing = false;
    CanvasGroup cg;

    void Awake()
    {
        cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        transform.localScale = Vector3.one * 0.8f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void Update()
    {
        float targetAlpha = isShowing ? 1f : 0f;
        float targetScale = isShowing ? 1f : 0.9f;

        cg.alpha = Mathf.Lerp(cg.alpha, targetAlpha, Time.deltaTime * 12f);
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, Time.deltaTime * 12f);

        if (!isShowing && cg.alpha < 0.05f)
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
        else if (isShowing)
        {
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }
    }

    public void Toggle() => isShowing = !isShowing;
    public void Hide() => isShowing = false;
}

public class NeonBreathingAnim : MonoBehaviour
{
    Vector3 startScale;
    void Start() => startScale = transform.localScale;
    void Update() => transform.localScale = startScale * (1f + 0.02f * Mathf.Sin(Time.time * 2.5f));
}