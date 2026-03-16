using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    static readonly string[] ModeFolders = { "Easy", "Medium", "Hard", "Endless" };

    [Header("Neon Settings")]
    public float glowIntensity = 2.5f;

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

    GameObject settingsPanel;
    GameObject nameEntryPanel;
    GameObject songSelectionPanel;
    GameObject songListContainer;
    TMP_InputField nameInput;
    int pendingModeIndex = 0;

    void Start()
    {
        // Wrapped in a null check just in case the camera is missing on scene reload
        if (Camera.main != null)
        {
            Camera.main.backgroundColor = DARK_BG;
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.allowHDR = true; // Added this to ensure the camera allows high-intensity colors
        }

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

    // Helper method to push colors into HDR range for Bloom
    Color GetHDR(Color c) => new Color(c.r * glowIntensity, c.g * glowIntensity, c.b * glowIntensity, 1f);

    void Build()
    {
        // --- FIX: Cleanup old UI on scene load to prevent invisible conflicts ---
        if (Application.isPlaying)
        {
            GameObject oldCanvas = GameObject.Find("Canvas");
            if (oldCanvas != null) Destroy(oldCanvas);
        }

        if (FindObjectOfType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        var cgo = new GameObject("Canvas");
        var cv = cgo.AddComponent<Canvas>();

        // --- THE NEON GLOW FIX: Render through the Camera to catch Post-Processing! ---
        cv.renderMode = RenderMode.ScreenSpaceCamera;
        cv.worldCamera = Camera.main;
        cv.planeDistance = 10f;
        cv.sortingOrder = 10;
        // -----------------------------------------------------------------------------

        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

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
        iconBtn.AddComponent<NeonButtonJuice>();

        topBtn.onClick.AddListener(() => {
            if (settingsPanel != null) settingsPanel.GetComponent<NeonPanelAnim>().Toggle();
            if (nameEntryPanel != null) nameEntryPanel.GetComponent<NeonPanelAnim>().Hide();
            if (songSelectionPanel != null) songSelectionPanel.GetComponent<NeonPanelAnim>().Hide();
        });

        // ── TITLE ──
        float titleY = 200f;

        var titleParent = new GameObject("TitleGroup");
        titleParent.transform.SetParent(cgo.transform, false);
        var tpRt = titleParent.AddComponent<RectTransform>();
        tpRt.anchorMin = tpRt.anchorMax = A(.5f, .5f);
        tpRt.anchoredPosition = new Vector2(0, titleY);
        titleParent.AddComponent<NeonBreathingAnim>();

        // Applying GetHDR here to make the title pop!
        var t1 = T(titleParent, "SHIFT", 95, new Vector2(-160, 0), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Right);
        t1.color = GetHDR(CYAN); t1.fontStyle = FontStyles.Bold;

        var t2 = T(titleParent, "NEON", 95, new Vector2(150, 0), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Left);
        t2.color = GetHDR(MAGENTA); t2.fontStyle = FontStyles.Bold;

        var sub = T(titleParent, "4-LANE RHYTHM GAME", 18, new Vector2(0, -70), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        sub.color = CYAN; sub.fontStyle = FontStyles.Bold;
        sub.characterSpacing = 5f;

        // ── BUTTONS ──
        Sprite[] modeSprites = { easyIcon, mediumIcon, hardIcon, endlessIcon };
        float startY = 40f, gap = -55f;

        for (int i = 0; i < 4; i++)
        {
            int mi = i;
            ModeButton(cgo, ModeLabels[i], modeSprites[i], ModeBorderColors[i],
                new Vector2(0, startY + gap * i), () => {
                    pendingModeIndex = mi;

                    PopulateSongList(mi);
                    if (songSelectionPanel != null) songSelectionPanel.GetComponent<NeonPanelAnim>().Show();
                    if (settingsPanel != null) settingsPanel.GetComponent<NeonPanelAnim>().Hide();
                    if (nameEntryPanel != null) nameEntryPanel.GetComponent<NeonPanelAnim>().Hide();
                });
        }

        ModeButton(cgo, "LEADERBOARD", null, new Color(0.4f, 0.7f, 1f),
            new Vector2(0, startY + gap * 4 - 10), () => LeaderboardManager.OpenLeaderboardScene("MainMenu"));

        ModeButton(cgo, "QUIT GAME", quitIcon, new Color(0.4f, 0.45f, 0.55f),
            new Vector2(0, startY + gap * 5 - 10), () => Application.Quit());

        // ── PANELS ──
        settingsPanel = CreateSettingsPanel(cgo);
        nameEntryPanel = CreateNameEntryPanel(cgo);
        songSelectionPanel = CreateSongSelectionPanel(cgo);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ── SONG SELECTION PANEL
    // ──────────────────────────────────────────────────────────────────────────
    GameObject CreateSongSelectionPanel(GameObject canvasGo)
    {
        var panel = new GameObject("SongSelectionPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        var prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = A(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(550, 600);

        var img = panel.AddComponent<Image>();
        img.color = new Color(0.05f, 0.07f, 0.11f, 0.98f);
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = CYAN;
        outline.effectDistance = new Vector2(2, -2);

        T(panel, "SELECT TRACK", 35, new Vector2(0, 240), A(0.5f, 0.5f), A(0.5f, 0.5f), TextAlignmentOptions.Center).color = MAGENTA;

        songListContainer = new GameObject("Container");
        songListContainer.transform.SetParent(panel.transform, false);
        var crt = songListContainer.AddComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = A(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(0, 0);

        ModeButton(panel, "CANCEL", null, new Color(0.4f, 0.45f, 0.55f), new Vector2(0, -250), () => {
            panel.GetComponent<NeonPanelAnim>().Hide();
        });

        panel.AddComponent<NeonPanelAnim>();
        return panel;
    }

    void PopulateSongList(int modeIndex)
    {
        foreach (Transform child in songListContainer.transform)
        {
            Destroy(child.gameObject);
        }

        string targetFolder = ModeFolders[modeIndex];
        AudioClip[] loadedTracks = Resources.LoadAll<AudioClip>("Music/" + targetFolder);

        if (loadedTracks.Length == 0)
        {
            SongButton(songListContainer, "No Tracks Found!", ModeBorderColors[modeIndex], Vector2.zero, () => { });
            return;
        }

        float startY = (loadedTracks.Length - 1) * 55f / 2f;

        for (int i = 0; i < loadedTracks.Length; i++)
        {
            string songName = loadedTracks[i].name;
            float yPos = startY - (i * 55f);

            SongButton(songListContainer, songName, ModeBorderColors[modeIndex], new Vector2(0, yPos), () => {

                PlayerPrefs.SetString("SelectedSong", songName);
                PlayerPrefs.SetString("SelectedDifficulty", targetFolder);

                if (sfx != null && clickSound != null) sfx.PlayOneShot(clickSound);

                songSelectionPanel.GetComponent<NeonPanelAnim>().Hide();
                if (nameEntryPanel != null) nameEntryPanel.GetComponent<NeonPanelAnim>().Show();
            });
        }
    }

    void SongButton(GameObject p, string lbl, Color col, Vector2 pos, UnityEngine.Events.UnityAction cb)
    {
        var go = new GameObject("SongBtn_" + lbl);
        go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = A(.5f, .5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(480, 45);

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
        trt.offsetMin = new Vector2(20, 0); trt.offsetMax = new Vector2(-10, 0);
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = lbl; tmp.fontSize = 16; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(cb);

        var cb2 = btn.colors;
        cb2.fadeDuration = 0.1f;
        cb2.highlightedColor = new Color(col.r * 0.2f, col.g * 0.2f, col.b * 0.2f, 0.95f);
        cb2.pressedColor = new Color(col.r * 0.3f, col.g * 0.3f, col.b * 0.3f, 0.95f);
        btn.colors = cb2;

        AddButtonSounds(btn);
        go.AddComponent<NeonButtonJuice>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ── NAME ENTRY PANEL
    // ──────────────────────────────────────────────────────────────────────────
    GameObject CreateNameEntryPanel(GameObject canvasGo)
    {
        var panel = new GameObject("NameEntryPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        var prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = A(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(450, 300);

        var img = panel.AddComponent<Image>();
        img.color = new Color(0.05f, 0.07f, 0.11f, 0.98f);
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = MAGENTA;
        outline.effectDistance = new Vector2(2, -2);

        T(panel, "ENTER CALLSIGN", 35, new Vector2(0, 90), A(0.5f, 0.5f), A(0.5f, 0.5f), TextAlignmentOptions.Center).color = CYAN;

        var inputGo = new GameObject("InputField");
        inputGo.transform.SetParent(panel.transform, false);
        var irt = inputGo.AddComponent<RectTransform>();
        irt.anchorMin = irt.anchorMax = A(0.5f, 0.5f);
        irt.anchoredPosition = new Vector2(0, 10);
        irt.sizeDelta = new Vector2(350, 50);

        var inputImg = inputGo.AddComponent<Image>();
        inputImg.color = new Color(0.02f, 0.03f, 0.05f, 1f);
        var inputOutline = inputGo.AddComponent<Outline>();
        inputOutline.effectColor = new Color(1f, 1f, 1f, 0.3f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(inputGo.transform, false);
        var trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(15, 0); trt.offsetMax = new Vector2(-15, 0);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = CYAN;

        nameInput = inputGo.AddComponent<TMP_InputField>();
        nameInput.targetGraphic = inputImg;
        nameInput.textComponent = tmp;
        nameInput.characterLimit = 12;

        nameInput.customCaretColor = true;
        nameInput.caretColor = CYAN;
        nameInput.selectionColor = new Color(CYAN.r, CYAN.g, CYAN.b, 0.4f);

        string[] prefixes = { "Neon", "Cyber", "Synth", "Byte", "Pixel", "Zero" };
        string[] suffixes = { "Rider", "Ninja", "Runner", "Ghost", "Punk", "Shift" };
        string generatedName = prefixes[Random.Range(0, prefixes.Length)] + suffixes[Random.Range(0, suffixes.Length)] + Random.Range(10, 99);

        nameInput.text = PlayerPrefs.GetString("PlayerName", generatedName);

        ModeButton(panel, "INITIALIZE", null, new Color(0.1f, 1f, 0.4f), new Vector2(0, -65), () => {
            string finalName = string.IsNullOrEmpty(nameInput.text) ? "UnknownPlayer" : nameInput.text;
            PlayerPrefs.SetString("PlayerName", finalName);
            PlayerPrefs.SetInt("SelectedMode", pendingModeIndex);

            if (sfx != null && clickSound != null) sfx.PlayOneShot(clickSound);

            SceneManager.LoadScene("GameScene");
        });

        ModeButton(panel, "BACK", null, new Color(0.4f, 0.45f, 0.55f), new Vector2(0, -120), () => {
            panel.GetComponent<NeonPanelAnim>().Hide();
            if (songSelectionPanel != null) songSelectionPanel.GetComponent<NeonPanelAnim>().Show();
        });

        panel.AddComponent<NeonPanelAnim>();
        return panel;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ── SETTINGS PANEL 
    // ──────────────────────────────────────────────────────────────────────────
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
        });

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
        rt.sizeDelta = new Vector2(350, 45);

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
        tmp.raycastTarget = false;

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
            actualImage.raycastTarget = false;
        }

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(cb);

        var cb2 = btn.colors;
        cb2.fadeDuration = 0.1f;
        cb2.highlightedColor = new Color(col.r * 0.2f, col.g * 0.2f, col.b * 0.2f, 0.95f);
        cb2.pressedColor = new Color(col.r * 0.3f, col.g * 0.3f, col.b * 0.3f, 0.95f);
        btn.colors = cb2;

        AddButtonSounds(btn);
        go.AddComponent<NeonButtonJuice>();
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
        var img = go.AddComponent<Image>();
        img.color = new Color(col.r, col.g, col.b, alpha);
        img.raycastTarget = false;
    }

    TextMeshProUGUI T(GameObject p, string txt, int sz, Vector2 pos, Vector2 aMin, Vector2 aMax, TextAlignmentOptions al)
    {
        var go = new GameObject("_T"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(900, 130);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = sz; t.alignment = al; t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    static Vector2 A(float x, float y) => new Vector2(x, y);
}

// ──────────────────────────────────────────────────────────────────────────
// ── UI ANIMATION HELPERS 
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
    public void Show() => isShowing = true;
    public void Hide() => isShowing = false;
}

public class NeonBreathingAnim : MonoBehaviour
{
    Vector3 startScale;
    void Start() => startScale = transform.localScale;
    void Update() => transform.localScale = startScale * (1f + 0.02f * Mathf.Sin(Time.time * 2.5f));
}