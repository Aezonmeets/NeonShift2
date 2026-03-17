using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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
    GameObject tutorialPanel;
    RectTransform songListContentRT;
    TMP_InputField nameInput;
    int pendingModeIndex = 0;

    // --- TUTORIAL VARIABLES ---
    TextMeshProUGUI tutInstrTxt;
    RectTransform tutTrackContainer;
    RectTransform tutTile;
    Image tutTileImg;
    Image[] tutKeyImgs = new Image[4];
    Coroutine activeTutorial;

    void Start()
    {
        if (Camera.main != null)
        {
            Camera.main.backgroundColor = DARK_BG;
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.allowHDR = true;
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

    Color GetHDR(Color c) => new Color(c.r * glowIntensity, c.g * glowIntensity, c.b * glowIntensity, 1f);

    void Build()
    {
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

        cv.renderMode = RenderMode.ScreenSpaceCamera;
        cv.worldCamera = Camera.main;
        cv.planeDistance = 10f;
        cv.sortingOrder = 10;

        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        var bg = new GameObject("BG"); bg.transform.SetParent(cgo.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = DARK_BG;

        HBar(cgo, new Color(CYAN.r, CYAN.g, CYAN.b, 0.3f), A(0, 1), A(1, 1), 0, -45, 1f);
        var topLogo = T(cgo, "↺ NEON SHIFT", 16, new Vector2(30, -22), A(0, 1), A(0, 1), TextAlignmentOptions.Left);
        topLogo.color = CYAN; topLogo.fontStyle = FontStyles.Bold;

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

        float titleY = 200f;

        var titleParent = new GameObject("TitleGroup");
        titleParent.transform.SetParent(cgo.transform, false);
        var tpRt = titleParent.AddComponent<RectTransform>();
        tpRt.anchorMin = tpRt.anchorMax = A(.5f, .5f);
        tpRt.anchoredPosition = new Vector2(0, titleY);
        titleParent.AddComponent<NeonBreathingAnim>();

        var t1 = T(titleParent, "SHIFT", 95, new Vector2(-160, 0), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Right);
        t1.color = GetHDR(CYAN); t1.fontStyle = FontStyles.Bold;

        var t2 = T(titleParent, "NEON", 95, new Vector2(150, 0), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Left);
        t2.color = GetHDR(MAGENTA); t2.fontStyle = FontStyles.Bold;

        var sub = T(titleParent, "4-LANE RHYTHM GAME", 18, new Vector2(0, -70), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        sub.color = CYAN; sub.fontStyle = FontStyles.Bold;
        sub.characterSpacing = 5f;

        Sprite[] modeSprites = { easyIcon, mediumIcon, hardIcon, endlessIcon };
        float startY = 40f, gap = -55f;

        for (int i = 0; i < 4; i++)
        {
            int mi = i;
            ModeButton(cgo, ModeLabels[i], modeSprites[i], ModeBorderColors[i],
                new Vector2(0, startY + gap * i), i * 0.1f, () => {
                    pendingModeIndex = mi;
                    PopulateSongList(mi);
                    if (songSelectionPanel != null) songSelectionPanel.GetComponent<NeonPanelAnim>().Show();
                    if (settingsPanel != null) settingsPanel.GetComponent<NeonPanelAnim>().Hide();
                    if (nameEntryPanel != null) nameEntryPanel.GetComponent<NeonPanelAnim>().Hide();
                });
        }

        ModeButton(cgo, "LEADERBOARD", null, new Color(0.4f, 0.7f, 1f),
            new Vector2(0, startY + gap * 4), 0.4f, () => LeaderboardManager.OpenLeaderboardScene("MainMenu"));

        ModeButton(cgo, "QUIT GAME", quitIcon, new Color(0.4f, 0.45f, 0.55f),
            new Vector2(0, startY + gap * 5), 0.5f, () => Application.Quit());

        tutorialPanel = CreateTutorialPanel(cgo);
        settingsPanel = CreateSettingsPanel(cgo);
        nameEntryPanel = CreateNameEntryPanel(cgo);
        songSelectionPanel = CreateSongSelectionPanel(cgo);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ── INTERACTIVE TUTORIAL LOGIC 
    // ──────────────────────────────────────────────────────────────────────────
    GameObject CreateTutorialPanel(GameObject canvasGo)
    {
        var panel = new GameObject("TutorialPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        var prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = prt.offsetMax = Vector2.zero;

        var img = panel.AddComponent<Image>();
        img.color = new Color(0.02f, 0.03f, 0.05f, 0.98f);
        panel.AddComponent<GraphicRaycaster>();

        tutInstrTxt = T(panel, "WELCOME", 36, new Vector2(0, 250), A(0.5f, 0.5f), A(0.5f, 0.5f), TextAlignmentOptions.Center);
        tutInstrTxt.color = GetHDR(CYAN);

        var tContainer = new GameObject("TrackContainer");
        tContainer.transform.SetParent(panel.transform, false);
        tutTrackContainer = tContainer.AddComponent<RectTransform>();
        tutTrackContainer.anchorMin = tutTrackContainer.anchorMax = A(0.5f, 0.5f);
        tutTrackContainer.anchoredPosition = Vector2.zero;
        tutTrackContainer.sizeDelta = new Vector2(600, 600);

        float[] laneX = { -120f, -40f, 40f, 120f };
        for (int i = 0; i < 5; i++)
        {
            float xPos = -160f + (i * 80f);
            var line = new GameObject("Line_" + i);
            line.transform.SetParent(tutTrackContainer, false);
            var rt = line.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = A(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(xPos, 0);
            rt.sizeDelta = new Vector2(2, 500);
            line.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
        }

        var hitLine = new GameObject("HitLine");
        hitLine.transform.SetParent(tutTrackContainer, false);
        var hlRT = hitLine.AddComponent<RectTransform>();
        hlRT.anchorMin = hlRT.anchorMax = A(0.5f, 0.5f);
        hlRT.anchoredPosition = new Vector2(0, -100);
        hlRT.sizeDelta = new Vector2(340, 4);
        hitLine.AddComponent<Image>().color = new Color(1, 1, 1, 0.6f);

        string[] keyNames = { "D", "F", "J", "K" };
        Color[] keyColors = {
            new Color(1.0f, 0.05f, 0.6f),
            new Color(1.0f, 0.95f, 0.0f),
            new Color(0.2f, 1.0f, 0.1f),
            new Color(0.0f, 0.85f, 1.0f),
        };

        for (int i = 0; i < 4; i++)
        {
            var keyObj = new GameObject("Key_" + i);
            keyObj.transform.SetParent(tutTrackContainer, false);
            var rt = keyObj.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = A(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(laneX[i], -150);
            rt.sizeDelta = new Vector2(60, 40);

            tutKeyImgs[i] = keyObj.AddComponent<Image>();
            tutKeyImgs[i].color = new Color(0.1f, 0.1f, 0.2f, 0.8f);

            var O = keyObj.AddComponent<Outline>();
            O.effectColor = keyColors[i]; O.effectDistance = new Vector2(2, -2);

            var kt = T(keyObj, keyNames[i], 24, Vector2.zero, A(0, 0), A(1, 1), TextAlignmentOptions.Center);
            kt.rectTransform.offsetMin = kt.rectTransform.offsetMax = Vector2.zero;
        }

        var tObj = new GameObject("TutTile");
        tObj.transform.SetParent(tutTrackContainer, false);
        tutTile = tObj.AddComponent<RectTransform>();
        tutTile.anchorMin = tutTile.anchorMax = A(0.5f, 0.5f);
        tutTile.sizeDelta = new Vector2(70, 20);
        tutTileImg = tObj.AddComponent<Image>();
        tObj.SetActive(false);

        ModeButton(panel, "SKIP TUTORIAL", null, new Color(0.4f, 0.45f, 0.55f), new Vector2(0, -320), 0.1f, () => {
            if (activeTutorial != null) StopCoroutine(activeTutorial);

            foreach (Transform child in tutTrackContainer)
            {
                if (child.name == "TutTile(Clone)") Destroy(child.gameObject);
            }
            panel.SetActive(false);
        });

        panel.SetActive(false);
        return panel;
    }

    IEnumerator RunInteractiveTutorial()
    {
        tutTile.gameObject.SetActive(false);
        tutTrackContainer.localRotation = Quaternion.identity;
        tutTile.sizeDelta = new Vector2(70, 20);

        Color pink = new Color(1.0f, 0.05f, 0.6f);
        Color yellow = new Color(1.0f, 0.95f, 0.0f);
        Color blue = new Color(0.0f, 0.85f, 1.0f);

        // --- PHASE 1: INTRO ---
        tutInstrTxt.color = GetHDR(CYAN);
        tutInstrTxt.text = "WELCOME TO NEON SHIFT.\n<size=24>Let's calibrate your reflexes.</size>";
        yield return new WaitForSeconds(2.5f);

        // --- PHASE 2: STATIONARY HIT ---
        tutInstrTxt.text = "Notes fall down the 4 lanes.\n<color=#ff1aff>Press [D]</color> when it hits the line.";
        tutTile.gameObject.SetActive(true);
        tutTile.anchoredPosition = new Vector2(-120, 200);
        tutTileImg.color = GetHDR(pink);

        while (tutTile.anchoredPosition.y > -100f)
        {
            tutTile.anchoredPosition += Vector2.down * 300f * Time.deltaTime;
            yield return null;
        }
        tutTile.anchoredPosition = new Vector2(-120, -100);

        bool hit = false;
        while (!hit)
        {
            if (Input.GetKeyDown(KeyCode.D))
            {
                hit = true;
                if (sfx != null) sfx.PlayOneShot(clickSound);
                StartCoroutine(FlashTutorialKey(0, pink));
            }
            yield return null;
        }
        tutTile.gameObject.SetActive(false);
        tutInstrTxt.text = "<color=#1aff99>PERFECT!</color>";
        yield return new WaitForSeconds(1.5f);

        // --- PHASE 3: LONG HOLD TILE ---
        tutInstrTxt.text = "Some notes are long.\n<color=#ffff00>Hold [F]</color> to drain it completely.";
        tutTile.gameObject.SetActive(true);
        tutTile.sizeDelta = new Vector2(70, 200);
        tutTileImg.color = GetHDR(yellow);
        tutTile.anchoredPosition = new Vector2(-40, 250);

        while (tutTile.anchoredPosition.y > 0f)
        {
            tutTile.anchoredPosition += Vector2.down * 300f * Time.deltaTime;
            yield return null;
        }

        float holdTimer = 1.5f;
        while (holdTimer > 0)
        {
            if (Input.GetKey(KeyCode.F))
            {
                holdTimer -= Time.deltaTime;
                float p = Mathf.Max(0, holdTimer / 1.5f);
                tutTile.sizeDelta = new Vector2(70, 200 * p);
                tutTile.anchoredPosition = new Vector2(-40, -100 + ((200 * p) / 2));
                tutKeyImgs[1].color = GetHDR(yellow);
            }
            else
            {
                tutKeyImgs[1].color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
                tutInstrTxt.text = "<color=#ffff00>DON'T LET GO!</color>\nHold [F] to drain.";
            }
            yield return null;
        }
        tutKeyImgs[1].color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        tutTile.gameObject.SetActive(false);
        tutTile.sizeDelta = new Vector2(70, 20);
        tutInstrTxt.text = "<color=#1aff99>GREAT HOLD!</color>";
        yield return new WaitForSeconds(1.5f);

        // --- PHASE 4: TRACK ROTATION ---
        tutInstrTxt.text = "<color=#ff3355>WARNING:</color> The track will rotate to the music!";
        yield return new WaitForSeconds(2f);

        float rotT = 0;
        while (rotT < 1f)
        {
            rotT += Time.deltaTime * 2f;
            tutTrackContainer.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, 90f, rotT));
            yield return null;
        }
        tutInstrTxt.text = "Your keys still map exactly to the screen.\n<color=#00d9ff>Press [K]</color> when it drops!";

        bool passedMovingTest = false;
        while (!passedMovingTest)
        {
            tutTile.gameObject.SetActive(true);
            tutTile.anchoredPosition = new Vector2(120, 200);
            tutTileImg.color = GetHDR(blue);
            bool attempted = false;

            while (tutTile.anchoredPosition.y > -250f)
            {
                tutTile.anchoredPosition += Vector2.down * 250f * Time.deltaTime;

                if (!attempted && Input.GetKeyDown(KeyCode.K))
                {
                    attempted = true;
                    StartCoroutine(FlashTutorialKey(3, blue));

                    float diff = Mathf.Abs(tutTile.anchoredPosition.y - (-100f));
                    if (diff < 35f)
                    {
                        passedMovingTest = true;
                        if (sfx != null) sfx.PlayOneShot(clickSound);
                        tutTile.gameObject.SetActive(false);
                        tutInstrTxt.text = "<color=#1aff99>NICE ADJUSTMENT!</color>";
                        break;
                    }
                    else
                    {
                        tutInstrTxt.text = "<color=#ff3355>MISS!</color>\nWait for the line.";
                    }
                }
                yield return null;
            }

            if (!passedMovingTest)
            {
                if (!attempted) tutInstrTxt.text = "<color=#ff3355>MISSED IT!</color>\nTry again.";
                tutTile.gameObject.SetActive(false);
                yield return new WaitForSeconds(1.5f);
            }
        }
        yield return new WaitForSeconds(1.5f);

        rotT = 0;
        while (rotT < 1f)
        {
            rotT += Time.deltaTime * 2f;
            tutTrackContainer.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(90f, 0f, rotT));
            yield return null;
        }

        // --- PHASE 5: THE OVERDRIVE BARRAGE ---
        tutInstrTxt.text = "Maxing out Fever triggers <color=#ff1aff>OVERDRIVE</color>.";
        yield return new WaitForSeconds(2.5f);

        float odTimer = 3f;
        while (odTimer > 0)
        {
            odTimer -= Time.deltaTime;
            tutInstrTxt.color = GetHDR(Color.HSVToRGB((Time.time * 3f) % 1f, 1f, 1f));
            tutInstrTxt.text = "GET READY!";
            yield return null;
        }

        tutInstrTxt.text = "MASH ALL KEYS!";
        float overdriveDuration = 4.0f;
        float tOverdrive = 0;
        float spawnTimer = 0;

        float[] tLaneX = { -120f, -40f, 40f, 120f };
        Color[] tKeyColors = {
            new Color(1.0f, 0.05f, 0.6f),
            new Color(1.0f, 0.95f, 0.0f),
            new Color(0.2f, 1.0f, 0.1f),
            new Color(0.0f, 0.85f, 1.0f)
        };

        List<RectTransform> overdriveTiles = new List<RectTransform>();

        void ProcessMash(int laneIndex, float laneX, Color color)
        {
            if (sfx != null) sfx.PlayOneShot(clickSound);
            StartCoroutine(FlashTutorialKey(laneIndex, color));

            int hitIdx = -1;
            float minY = 9999f;

            for (int i = 0; i < overdriveTiles.Count; i++)
            {
                if (Mathf.Abs(overdriveTiles[i].anchoredPosition.x - laneX) < 5f)
                {
                    if (overdriveTiles[i].anchoredPosition.y < minY)
                    {
                        minY = overdriveTiles[i].anchoredPosition.y;
                        hitIdx = i;
                    }
                }
            }

            if (hitIdx != -1 && minY < 150f)
            {
                Destroy(overdriveTiles[hitIdx].gameObject);
                overdriveTiles.RemoveAt(hitIdx);
            }
        }

        while (tOverdrive < overdriveDuration)
        {
            tOverdrive += Time.deltaTime;
            tutInstrTxt.color = GetHDR(Color.HSVToRGB((Time.time * 5f) % 1f, 1f, 1f));

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                spawnTimer = 0.12f;
                GameObject newTileObj = Instantiate(tutTile.gameObject, tutTrackContainer);
                newTileObj.SetActive(true);
                RectTransform newTileRT = newTileObj.GetComponent<RectTransform>();
                int randLane = Random.Range(0, 4);
                newTileRT.anchoredPosition = new Vector2(tLaneX[randLane], 300);
                newTileObj.GetComponent<Image>().color = GetHDR(Color.HSVToRGB((Time.time * 2f) % 1f, 1f, 1f));
                overdriveTiles.Add(newTileRT);
            }

            for (int i = overdriveTiles.Count - 1; i >= 0; i--)
            {
                RectTransform rt = overdriveTiles[i];
                rt.anchoredPosition += Vector2.down * 900f * Time.deltaTime;
                if (rt.anchoredPosition.y < -250f)
                {
                    Destroy(rt.gameObject);
                    overdriveTiles.RemoveAt(i);
                }
            }

            if (Input.GetKeyDown(KeyCode.D)) ProcessMash(0, tLaneX[0], tKeyColors[0]);
            if (Input.GetKeyDown(KeyCode.F)) ProcessMash(1, tLaneX[1], tKeyColors[1]);
            if (Input.GetKeyDown(KeyCode.J)) ProcessMash(2, tLaneX[2], tKeyColors[2]);
            if (Input.GetKeyDown(KeyCode.K)) ProcessMash(3, tLaneX[3], tKeyColors[3]);

            yield return null;
        }

        foreach (var rt in overdriveTiles) { if (rt != null) Destroy(rt.gameObject); }
        overdriveTiles.Clear();

        tutInstrTxt.color = GetHDR(CYAN);
        tutInstrTxt.text = "You are ready.";
        yield return new WaitForSeconds(2f);

        tutorialPanel.SetActive(false);
    }

    IEnumerator FlashTutorialKey(int index, Color glowColor)
    {
        tutKeyImgs[index].color = GetHDR(glowColor);
        yield return new WaitForSeconds(0.15f);
        tutKeyImgs[index].color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
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
        prt.sizeDelta = new Vector2(450, 480);

        var img = panel.AddComponent<Image>();
        img.color = new Color(0.05f, 0.07f, 0.11f, 0.98f);
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = CYAN;
        outline.effectDistance = new Vector2(2, -2);

        T(panel, "SETTINGS", 35, new Vector2(0, 180), A(0.5f, 0.5f), A(0.5f, 0.5f), TextAlignmentOptions.Center).color = MAGENTA;

        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 0.55f);
        CreateSlider(panel, "MUSIC VOLUME", 80, savedMusic, (val) => {
            PlayerPrefs.SetFloat("MusicVolume", val);
            if (music != null) music.volume = val;
        });

        float savedSfx = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
        CreateSlider(panel, "SFX VOLUME", 0, savedSfx, (val) => {
            PlayerPrefs.SetFloat("SFXVolume", val);
            if (sfx != null) sfx.volume = val;
        });

        ModeButton(panel, "HOW TO PLAY", null, MAGENTA, new Vector2(0, -90), 0.1f, () => {
            panel.GetComponent<NeonPanelAnim>().Hide();
            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(true);
                if (activeTutorial != null) StopCoroutine(activeTutorial);
                activeTutorial = StartCoroutine(RunInteractiveTutorial());
            }
        });

        ModeButton(panel, "CLOSE MENU", null, new Color(0.4f, 0.45f, 0.55f), new Vector2(0, -160), 0.2f, () => {
            panel.GetComponent<NeonPanelAnim>().Hide();
        });

        panel.AddComponent<NeonPanelAnim>();
        return panel;
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

        var scrollView = new GameObject("ScrollView");
        scrollView.transform.SetParent(panel.transform, false);
        var svRT = scrollView.AddComponent<RectTransform>();
        svRT.anchorMin = svRT.anchorMax = A(0.5f, 0.5f);
        svRT.sizeDelta = new Vector2(510, 380);
        svRT.anchoredPosition = new Vector2(0, -20);

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);
        var vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        songListContentRT = content.AddComponent<RectTransform>();
        songListContentRT.anchorMin = new Vector2(0, 1); songListContentRT.anchorMax = new Vector2(1, 1);
        songListContentRT.pivot = new Vector2(0.5f, 1f);
        songListContentRT.anchoredPosition = Vector2.zero;

        var contentImg = content.AddComponent<Image>();
        contentImg.color = Color.clear;

        var sr = scrollView.AddComponent<ScrollRect>();
        sr.viewport = vpRT;
        sr.content = songListContentRT;
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 35f;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.inertia = true;
        sr.decelerationRate = 0.135f;

        ModeButton(panel, "CANCEL", null, new Color(0.4f, 0.45f, 0.55f), new Vector2(0, -250), 0.1f, () => {
            panel.GetComponent<NeonPanelAnim>().Hide();
        });

        panel.AddComponent<NeonPanelAnim>();
        return panel;
    }

    void PopulateSongList(int modeIndex)
    {
        foreach (Transform child in songListContentRT.transform)
        {
            Destroy(child.gameObject);
        }

        string targetFolder = ModeFolders[modeIndex];
        AudioClip[] loadedTracks = Resources.LoadAll<AudioClip>("Music/" + targetFolder);

        if (loadedTracks.Length == 0)
        {
            songListContentRT.sizeDelta = new Vector2(0, 100);
            SongButton(songListContentRT.gameObject, "No Tracks Found!", ModeBorderColors[modeIndex], new Vector2(0, -50), 0f, () => { });
            return;
        }

        float itemHeight = 55f;
        float padding = 20f;

        float calculatedHeight = (loadedTracks.Length * itemHeight) + padding;
        songListContentRT.sizeDelta = new Vector2(0, calculatedHeight);
        songListContentRT.anchoredPosition = Vector2.zero;

        float startY = -itemHeight / 2f - 10f;

        for (int i = 0; i < loadedTracks.Length; i++)
        {
            string songName = loadedTracks[i].name;
            float yPos = startY - (i * itemHeight);

            SongButton(songListContentRT.gameObject, songName, ModeBorderColors[modeIndex], new Vector2(0, yPos), i * 0.05f, () => {
                PlayerPrefs.SetString("SelectedSong", songName);
                PlayerPrefs.SetString("SelectedDifficulty", targetFolder);

                if (sfx != null && clickSound != null) sfx.PlayOneShot(clickSound);

                songSelectionPanel.GetComponent<NeonPanelAnim>().Hide();
                if (nameEntryPanel != null) nameEntryPanel.GetComponent<NeonPanelAnim>().Show();
            });
        }
    }

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

        ModeButton(panel, "INITIALIZE", null, new Color(0.1f, 1f, 0.4f), new Vector2(0, -65), 0.1f, () => {
            string finalName = string.IsNullOrEmpty(nameInput.text) ? "UnknownPlayer" : nameInput.text;
            PlayerPrefs.SetString("PlayerName", finalName);
            PlayerPrefs.SetInt("SelectedMode", pendingModeIndex);

            if (sfx != null && clickSound != null) sfx.PlayOneShot(clickSound);

            SceneManager.LoadScene("GameScene");
        });

        ModeButton(panel, "BACK", null, new Color(0.4f, 0.45f, 0.55f), new Vector2(0, -120), 0.2f, () => {
            panel.GetComponent<NeonPanelAnim>().Hide();
            if (songSelectionPanel != null) songSelectionPanel.GetComponent<NeonPanelAnim>().Show();
        });

        panel.AddComponent<NeonPanelAnim>();
        return panel;
    }

    void SongButton(GameObject p, string lbl, Color col, Vector2 pos, float animDelay, UnityEngine.Events.UnityAction cb)
    {
        var go = new GameObject("SongBtn_" + lbl);
        go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = A(.5f, 1f);
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

        var anim = go.AddComponent<NeonSlideInAnim>();
        anim.delay = animDelay;
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

    void ModeButton(GameObject p, string lbl, Sprite iconSprite, Color col, Vector2 pos, float animDelay, UnityEngine.Events.UnityAction cb)
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

        var anim = go.AddComponent<NeonSlideInAnim>();
        anim.delay = animDelay;
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

    void AddBorder(GameObject go, Color col, float alpha)
    {
        var ov = new GameObject("Border"); ov.transform.SetParent(go.transform, false);
        var rt = ov.AddComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-1, -1); rt.offsetMax = new Vector2(1, 1);
        var img = ov.AddComponent<Image>(); img.color = Color.clear;
        var O = go.AddComponent<Outline>(); O.effectColor = new Color(col.r, col.g, col.b, alpha); O.effectDistance = new Vector2(2, -2);
    }

    static Vector2 A(float x, float y) => new Vector2(x, y);
}

// ──────────────────────────────────────────────────────────────────────────
// ── ANIMATION CLASSES
// ──────────────────────────────────────────────────────────────────────────
public class NeonSlideInAnim : MonoBehaviour
{
    public float delay = 0f;
    Vector2 targetPos;
    Vector2 startOffset = new Vector2(-60f, 0f);
    CanvasGroup cg;
    RectTransform rt;
    float timer = 0f;
    bool started = false;

    void Awake()
    {
        cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        rt = GetComponent<RectTransform>();
    }

    void Start()
    {
        targetPos = rt.anchoredPosition;
        rt.anchoredPosition += startOffset;
    }

    void Update()
    {
        if (timer < delay)
        {
            timer += Time.deltaTime;
            return;
        }

        started = true;
        cg.alpha = Mathf.Lerp(cg.alpha, 1f, Time.deltaTime * 12f);
        rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos, Time.deltaTime * 12f);

        if (cg.alpha > 0.99f && Vector2.Distance(rt.anchoredPosition, targetPos) < 0.5f)
        {
            cg.alpha = 1f;
            rt.anchoredPosition = targetPos;
            Destroy(this);
        }
    }
}

public class NeonButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    Vector3 targetScale = Vector3.one;
    void Update() => transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 15f);
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

        if (!isShowing && cg.alpha < 0.05f) { cg.blocksRaycasts = false; cg.interactable = false; }
        else if (isShowing) { cg.blocksRaycasts = true; cg.interactable = true; }
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