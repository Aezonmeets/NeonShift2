using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

// --- WE MOVED THE SONG DATA DIRECTLY INTO THE GAME MANAGER ---
[System.Serializable]
public struct SongData
{
    public string songName;
    public AudioClip audioClip;
}

public enum GameMode { Easy, Medium, Hard, Endless }
public enum HitResult { Perfect, Good, Miss }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    [HideInInspector] public GameMode currentMode = GameMode.Easy;

    [Header("Glow Settings")]
    [Tooltip("Increase this to make the Game Over screen neon colors glow brighter!")]
    public float glowIntensity = 2.5f;

    [Header("Level Songs")]
    [Tooltip("Add all your songs here! Make sure the names match the Main Menu perfectly.")]
    public SongData[] allSongs;

    [Header("Custom Sound Effects")]
    [Tooltip("Assign your own sound effects here. If left empty, the game will use default procedural beeps.")]
    public AudioClip customPerfectSound;
    public AudioClip customGoodSound;
    public AudioClip customMissSound;

    int score, combo, maxCombo, total, hits, perfectHits;
    float hp = 100f;
    bool alive, paused;
    bool musicStarted = false;

    // --- DSP AUDIO SYNC VARIABLES ---
    double dspStartTime;

    // HUD
    TextMeshProUGUI scoreTxt, comboTxt, accTxt, hpTxt, resultTxt;

    // Game Over / Level Clear
    TextMeshProUGUI goScore, goAcc, goCombo, goTitle;
    GameObject goPanel, pausePanel, lbPanel;
    Coroutine resultCo;

    // Music
    AudioSource music;
    AudioSource sfx;
    AudioClip sPerfect, sGood, sMiss;

    static readonly Color CP = new Color(1f, .95f, .15f);
    static readonly Color CG = new Color(.1f, 1f, .6f);
    static readonly Color CM = new Color(1f, .2f, .35f);
    static readonly Color CYAN = new Color(0f, .9f, 1f);
    static readonly Color MAGENTA = new Color(1f, .15f, .75f);

    static readonly Color[] ModeColors = {
        new Color(.1f,1f,.4f),    // Easy
        new Color(1f,.85f,.1f),   // Medium
        new Color(1f,.35f,.1f),   // Hard
        new Color(.8f,.1f,1f),    // Endless
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        Camera.main.backgroundColor = new Color(.025f, .025f, .09f);
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.allowHDR = true;

        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.gameObject != gameObject) Destroy(c.gameObject);

        sfx = gameObject.AddComponent<AudioSource>(); sfx.volume = .55f;
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true; music.volume = .7f;

        sPerfect = customPerfectSound != null ? customPerfectSound : Beep(880f, .08f);
        sGood = customGoodSound != null ? customGoodSound : Beep(660f, .06f);
        sMiss = customMissSound != null ? customMissSound : Beep(110f, .13f, true);

        BuildUI();
    }

    void Start()
    {
        TileSpawner.Instance.Init(currentMode);
        ApplyMode();
        alive = true;
        TileSpawner.Instance.BeginSpawning();
        TrackController.Instance.BeginRotating();
        TryPlayMusic();
    }

    Color GetHDR(Color c) => new Color(c.r * glowIntensity, c.g * glowIntensity, c.b * glowIntensity, 1f);

    // ── THE NEW, FIXED SONG LOADER ───────────────────────────────────
    void TryPlayMusic()
    {
        string pickedSong = PlayerPrefs.GetString("SelectedSong", "");
        AudioClip clipToPlay = null;

        // Search our new list for the selected song
        foreach (var song in allSongs)
        {
            if (song.songName == pickedSong)
            {
                clipToPlay = song.audioClip;
                break;
            }
        }

        if (clipToPlay != null)
        {
            music.clip = clipToPlay;
            
            // Sync BPM if we are not in Endless mode
            if (currentMode != GameMode.Endless && TileSpawner.Instance != null)
                TileSpawner.Instance.SetDynamicBPM(clipToPlay.name);

            // Loop the song ONLY if we are in endless mode!
            music.loop = (currentMode == GameMode.Endless);
            music.Play();

            // --- RECORD EXACT AUDIO HARDWARE TIME FOR RHYTHM SYNC ---
            dspStartTime = AudioSettings.dspTime;
            musicStarted = true;
            Debug.Log($"<color=green>[Music] Successfully Playing: {pickedSong}</color>");
        }
        else
        {
            Debug.LogError($"<color=red>[Music] Could not find '{pickedSong}' in GameManager's All Songs list! Check your spelling.</color>");
        }
    }

    void Update()
    {
        if (!alive) return;
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();

        if (currentMode == GameMode.Endless)
        {
            float t = Time.timeSinceLevelLoad;
            TileSpawner.Instance.spawnInterval = Mathf.Max(.38f, 1.1f - t * .005f);
            TileSpawner.Instance.tileSpeed = Mathf.Min(18f, 5f + t * .02f);
            TrackController.Instance.rotationInterval = Mathf.Max(3f, 8f - t * .012f);
        }
        else if (musicStarted && !paused)
        {
            if (!music.isPlaying && TileSpawner.Instance.GetActiveTiles().Count == 0)
            {
                musicStarted = false;
                StartCoroutine(TrackClearRoutine());
            }
        }

        RefreshHUD();
    }

    IEnumerator TrackClearRoutine()
    {
        yield return new WaitForSeconds(0.8f);
        if (alive) EndLevel(true);
    }

    void ApplyMode()
    {
        var ts = TileSpawner.Instance; var tc = TrackController.Instance; var pc = PlayerController.Instance;
        switch (currentMode)
        {
            case GameMode.Easy: ts.spawnInterval = 1.3f; ts.tileSpeed = 5f; tc.rotationInterval = 9f; pc.hitZoneDistance = 1.4f; break;
            case GameMode.Medium: ts.spawnInterval = 0.95f; ts.tileSpeed = 7f; tc.rotationInterval = 6f; pc.hitZoneDistance = 1.2f; break;
            case GameMode.Hard: ts.spawnInterval = 0.6f; ts.tileSpeed = 10f; tc.rotationInterval = 4f; pc.hitZoneDistance = 1.0f; break;
            case GameMode.Endless: ts.spawnInterval = 1.3f; ts.tileSpeed = 5f; tc.rotationInterval = 9f; pc.hitZoneDistance = 1.3f; ts.endlessMode = true; break;
        }
    }

    public void RegisterHit(HitResult r, Vector3 pos)
    {
        total++;
        string lbl; Color col;
        switch (r)
        {
            case HitResult.Perfect:
                perfectHits++; combo++; score += 100 + combo * 5; lbl = "PERFECT!"; col = CP; sfx.PlayOneShot(sPerfect); break;
            case HitResult.Good:
                combo = 0; score += 50; lbl = "GOOD"; col = CG; sfx.PlayOneShot(sGood); break;
            default:
                combo = 0; hp = Mathf.Max(0f, hp - 10f); lbl = "MISS"; col = CM; sfx.PlayOneShot(sMiss);
                CameraShake.Instance?.Shake(.2f, .1f);
                if (hp <= 0f) { EndLevel(false); return; }
                break;
        }
        if (combo > maxCombo) maxCombo = combo;
        ShowResult(lbl, col);
    }

    public void EndLevel(bool cleared = false)
    {
        alive = false;
        if (music.isPlaying) music.Stop();

        TileSpawner.Instance.StopSpawning();
        TrackController.Instance.StopRotating();

        float acc = total > 0 ? (float)perfectHits / total * 100f : 0f;

        goScore.text = score.ToString("N0");
        goAcc.text = $"{acc:F1}%";
        goCombo.text = "x" + maxCombo;

        if (cleared)
        {
            goTitle.text = "TRACK CLEARED";
            goTitle.color = GetHDR(CG);
            sfx.PlayOneShot(sPerfect);
        }
        else
        {
            goTitle.text = "GAME OVER";
            goTitle.color = GetHDR(new Color(1f, .15f, .25f));
        }

        goPanel.SetActive(true);
        HighScoreManager.Instance?.TrySubmitScore(currentMode, score);
        LeaderboardManager.Instance?.TrySubmit(currentMode.ToString(), score, acc);
        if (lbPanel != null) LeaderboardManager.Instance?.BuildLeaderboardUI(lbPanel, currentMode.ToString());
    }

    void TogglePause()
    {
        paused = !paused; Time.timeScale = paused ? 0f : 1f;
        if (paused)
        {
            if (music.isPlaying) music.Pause();
        }
        else
        {
            music.UnPause();
            // --- RESYNC DSP TIME ---
            dspStartTime = AudioSettings.dspTime - music.time;
        }
        pausePanel.SetActive(paused);
    }

    public void Restart() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    public void MainMenu() { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
    public bool IsGameActive() => alive;

    public float GetMusicTime()
    {
        if (music != null && music.clip != null && music.isPlaying && !paused)
        {
            return (float)(AudioSettings.dspTime - dspStartTime);
        }
        else if (music != null && paused)
        {
            return music.time;
        }
        return 0f;
    }

    public void ApplySpamPenalty()
    {
        if (!alive) return;
        combo = 0; hp = Mathf.Max(0f, hp - 3f);
        sfx.PlayOneShot(sMiss);
        CameraShake.Instance?.Shake(.1f, .05f);
        ShowResult("SPAM!", CM);
        if (hp <= 0f) EndLevel(false);
        RefreshHUD();
    }

    void RefreshHUD()
    {
        scoreTxt.text = score.ToString("N0");
        float acc = total > 0 ? (float)perfectHits / total * 100f : 100f;
        accTxt.text = $"{acc:F1}%";
        hpTxt.text = "\u2665 " + (int)hp;
        comboTxt.text = combo > 1 ? "x" + combo : "";
        if (combo > 1) comboTxt.color = Color.Lerp(CYAN, MAGENTA, Mathf.Sin(Time.time * 7f) * .5f + .5f);
    }

    void ShowResult(string lbl, Color col) { if (resultCo != null) StopCoroutine(resultCo); resultCo = StartCoroutine(ResultAnim(lbl, col)); }

    IEnumerator ResultAnim(string lbl, Color col)
    {
        resultTxt.text = lbl; resultTxt.transform.localScale = Vector3.one * 1.4f;
        float t = 0f;
        while (t < 0.55f)
        {
            t += Time.deltaTime; resultTxt.color = new Color(col.r, col.g, col.b, 1f - t / 0.55f);
            resultTxt.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Min(t / .18f, 1f)); yield return null;
        }
        resultTxt.text = "";
    }

    // ── UI BUILD ───────────────────────────────────
    void BuildUI()
    {
        var cgo = new GameObject("_Canvas");
        var cv = cgo.AddComponent<Canvas>();

        cv.renderMode = RenderMode.ScreenSpaceCamera;
        cv.worldCamera = Camera.main;
        cv.planeDistance = 5f;
        cv.sortingOrder = 20;

        var sc = cgo.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        scoreTxt = T(cgo, "0", 52, new Vector2(24, -20), A(0, 1), A(0, 1), TextAlignmentOptions.TopLeft);
        scoreTxt.color = Color.white; scoreTxt.fontStyle = FontStyles.Bold;

        hpTxt = T(cgo, "\u2665 100", 28, new Vector2(0, -20), A(.5f, 1), A(.5f, 1), TextAlignmentOptions.Center);
        hpTxt.color = new Color(1f, .35f, .55f);

        accTxt = T(cgo, "100.0%", 26, new Vector2(-20, -20), A(1, 1), A(1, 1), TextAlignmentOptions.TopRight);
        accTxt.color = CYAN;

        comboTxt = T(cgo, "", 72, new Vector2(0, 80), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        comboTxt.color = CYAN; comboTxt.fontStyle = FontStyles.Bold;

        resultTxt = T(cgo, "", 44, new Vector2(0, 10), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        resultTxt.fontStyle = FontStyles.Bold;

        var modeLbl = T(cgo, "NEON SHIFT", 14, new Vector2(16, 50), A(0, 0), A(0, 0), TextAlignmentOptions.BottomLeft);
        modeLbl.color = GetHDR(CYAN); modeLbl.fontStyle = FontStyles.Bold;
        var diffLbl = T(cgo, "DIFFICULTY: " + currentMode.ToString().ToUpper(), 11, new Vector2(16, 34), A(0, 0), A(0, 0), TextAlignmentOptions.BottomLeft);
        diffLbl.color = new Color(.6f, .7f, .8f, .7f);

        T(cgo, "ESC = PAUSE", 18, new Vector2(-16, 16), A(1, 0), A(1, 0), TextAlignmentOptions.BottomRight)
            .color = new Color(.4f, .7f, 1f, .3f);

        goPanel = Panel(cgo, new Color(.02f, .02f, .05f, .95f));

        goTitle = T(goPanel, "GAME OVER", 88, new Vector2(0, 200), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        goTitle.color = GetHDR(new Color(1f, .15f, .25f)); goTitle.fontStyle = FontStyles.Bold | FontStyles.Italic;

        var statsBox = new GameObject("StatsBox"); statsBox.transform.SetParent(goPanel.transform, false);
        var sBoxRt = statsBox.AddComponent<RectTransform>(); sBoxRt.anchorMin = sBoxRt.anchorMax = A(0.5f, 0.5f);
        sBoxRt.anchoredPosition = new Vector2(0, 30); sBoxRt.sizeDelta = new Vector2(750, 140);
        statsBox.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.9f);

        NeonLine(statsBox, new Vector2(-375, 70), new Vector2(375, 70), GetHDR(CYAN), 1f);
        NeonLine(statsBox, new Vector2(-375, -70), new Vector2(375, -70), GetHDR(CYAN), 1f);

        NeonLine(statsBox, new Vector2(-125, 50), new Vector2(-125, -50), GetHDR(CYAN), 0.5f);
        NeonLine(statsBox, new Vector2(125, 50), new Vector2(125, -50), GetHDR(CYAN), 0.5f);

        var sLbl = T(statsBox, "SCORE", 18, new Vector2(-250, 25), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        sLbl.color = Color.white; sLbl.fontStyle = FontStyles.Bold;
        goScore = T(statsBox, "0", 56, new Vector2(-250, -20), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        goScore.color = GetHDR(CP); goScore.fontStyle = FontStyles.Bold;

        var aLbl = T(statsBox, "ACCURACY", 18, new Vector2(0, 25), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        aLbl.color = Color.white; aLbl.fontStyle = FontStyles.Bold;
        goAcc = T(statsBox, "0.0%", 56, new Vector2(0, -20), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        goAcc.color = GetHDR(CG); goAcc.fontStyle = FontStyles.Bold;

        var cLbl = T(statsBox, "MAX COMBO", 18, new Vector2(250, 25), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        cLbl.color = Color.white; cLbl.fontStyle = FontStyles.Bold;
        goCombo = T(statsBox, "x0", 56, new Vector2(250, -20), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        goCombo.color = GetHDR(CYAN); goCombo.fontStyle = FontStyles.Bold;

        NeonBtn(goPanel, "PLAY AGAIN", CYAN, new Vector2(-160, -95), () => Restart());
        NeonBtn(goPanel, "MENU", new Color(1f, 0.2f, 0.4f), new Vector2(160, -95), () => MainMenu());

        var lbDiv = new GameObject("LBDiv"); lbDiv.transform.SetParent(goPanel.transform, false);
        var ldRT = lbDiv.AddComponent<RectTransform>(); ldRT.anchorMin = new Vector2(.5f, .5f); ldRT.anchorMax = new Vector2(.5f, .5f);
        ldRT.anchoredPosition = new Vector2(0, -140); ldRT.sizeDelta = new Vector2(680, 1.5f);
        lbDiv.AddComponent<UnityEngine.UI.Image>().color = new Color(CYAN.r, CYAN.g, CYAN.b, .3f);

        lbPanel = new GameObject("LBPanel"); lbPanel.transform.SetParent(goPanel.transform, false);
        var lbRT = lbPanel.AddComponent<RectTransform>();
        lbRT.anchorMin = new Vector2(.5f, .5f); lbRT.anchorMax = new Vector2(.5f, .5f);
        lbRT.anchoredPosition = new Vector2(0, -255);
        lbRT.sizeDelta = new Vector2(720, 220);
        var lbBG = lbPanel.AddComponent<UnityEngine.UI.Image>(); lbBG.color = new Color(.01f, .02f, .05f, .0f);

        goPanel.SetActive(false);

        pausePanel = Panel(cgo, new Color(.01f, .02f, .08f, .93f));
        NeonLine(pausePanel, new Vector2(-640, 358), new Vector2(640, 358), GetHDR(CYAN), 1f);
        var pauseTitle = T(pausePanel, "PAUSED", 90, new Vector2(0, 210), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        pauseTitle.color = GetHDR(CYAN); pauseTitle.fontStyle = FontStyles.Bold;

        T(pausePanel, "PRESS ESC TO RESUME", 18, new Vector2(0, 140), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center)
            .color = new Color(.5f, .7f, .9f, .7f);

        PauseBtn(pausePanel, "RESUME TRACK", CYAN, new Vector2(0, 60), () => TogglePause());
        PauseBtn(pausePanel, "RESTART TRACK", new Color(.1f, 1f, .4f), new Vector2(0, -20), () => Restart());
        PauseBtn(pausePanel, "QUIT TO MENU", new Color(.4f, .5f, .6f), new Vector2(0, -100), () => MainMenu());

        T(pausePanel, "CURRENT SCORE", 14, new Vector2(-220, -205), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center)
            .color = new Color(1f, .85f, .2f, .7f);
        var pScore = T(pausePanel, "0", 38, new Vector2(-220, -240), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        pScore.color = new Color(1f, .85f, .2f);
        StartCoroutine(KeepPauseScore(pScore));

        NeonLine(pausePanel, new Vector2(0, -185), new Vector2(0, -270), CYAN, .5f);

        T(pausePanel, "COMBO MULTIPLIER", 14, new Vector2(220, -205), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center)
            .color = new Color(.4f, .7f, 1f, .7f);
        var pCombo = T(pausePanel, "x0", 38, new Vector2(220, -240), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        pCombo.color = new Color(.4f, .7f, 1f);
        StartCoroutine(KeepPauseCombo(pCombo));

        pausePanel.SetActive(false);
    }

    IEnumerator KeepPauseScore(TextMeshProUGUI t) { while (true) { if (t) t.text = score.ToString("N0"); yield return new WaitForSecondsRealtime(.1f); } }
    IEnumerator KeepPauseCombo(TextMeshProUGUI t) { while (true) { if (t) t.text = "x" + maxCombo; yield return new WaitForSecondsRealtime(.1f); } }

    static Vector2 A(float x, float y) => new Vector2(x, y);

    TextMeshProUGUI T(GameObject p, string txt, int sz, Vector2 pos, Vector2 aMin, Vector2 aMax, TextAlignmentOptions al)
    {
        var go = new GameObject("_T"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(800, 120);
        var t = go.AddComponent<TextMeshProUGUI>(); t.text = txt; t.fontSize = sz; t.alignment = al; t.color = Color.white;
        return t;
    }

    GameObject Panel(GameObject p, Color bg)
    {
        var go = new GameObject("_P"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero; go.AddComponent<Image>().color = bg; return go;
    }

    void NeonLine(GameObject p, Vector2 from, Vector2 to, Color col, float alpha)
    {
        var go = new GameObject("_L"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = A(.5f, .5f);
        Vector2 mid = (from + to) / 2f; rt.anchoredPosition = mid;
        rt.sizeDelta = new Vector2(Vector2.Distance(from, to), 1.5f);
        float ang = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0, 0, ang);
        var img = go.AddComponent<Image>(); img.color = new Color(col.r, col.g, col.b, alpha);
    }

    void NeonBtn(GameObject p, string lbl, Color col, Vector2 pos, UnityEngine.Events.UnityAction cb)
    {
        var go = new GameObject("_B"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = A(.5f, .5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(250, 54);
        var img = go.AddComponent<Image>(); img.color = new Color(col.r * .08f, col.g * .08f, col.b * .08f, .9f);

        AddBorder(go, GetHDR(col), 1f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(cb);

        var tgo = Label(go, lbl, 26, GetHDR(col));
    }

    void PauseBtn(GameObject p, string lbl, Color col, Vector2 pos, UnityEngine.Events.UnityAction cb)
    {
        var go = new GameObject("_PB"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = A(.5f, .5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(440, 60);
        var img = go.AddComponent<Image>(); img.color = new Color(col.r * .06f, col.g * .06f, col.b * .06f, .9f);
        AddBorder(go, col, .5f);
        var bar = new GameObject("Bar"); bar.transform.SetParent(go.transform, false);
        var brt = bar.AddComponent<RectTransform>(); brt.anchorMin = A(0, .1f); brt.anchorMax = A(0, .9f);
        brt.offsetMin = new Vector2(0, -2); brt.offsetMax = new Vector2(4, 2);
        bar.AddComponent<Image>().color = GetHDR(col);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(cb);
        Label(go, lbl, 22, col);
    }

    void AddBorder(GameObject go, Color col, float alpha)
    {
        var ov = new GameObject("Border"); ov.transform.SetParent(go.transform, false);
        var rt = ov.AddComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-1, -1); rt.offsetMax = new Vector2(1, 1);
        var img = ov.AddComponent<Image>(); img.color = Color.clear;
        var O = go.AddComponent<Outline>(); O.effectColor = new Color(col.r, col.g, col.b, alpha); O.effectDistance = new Vector2(2, -2);
    }

    GameObject Label(GameObject p, string txt, int sz, Color col)
    {
        var go = new GameObject("L"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>(); tmp.text = txt; tmp.fontSize = sz;
        tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center; tmp.color = col;
        return go;
    }

    AudioClip Beep(float freq, float dur, bool noise = false)
    {
        int sr = 44100, n = Mathf.RoundToInt(sr * dur); float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr; float e = Mathf.Exp(-t * 18f);
            d[i] = noise ? (Random.value * 2f - 1f) * e * .5f : Mathf.Sin(2f * Mathf.PI * freq * t) * e * .7f;
        }
        var c = AudioClip.Create("b", n, 1, sr, false); c.SetData(d, 0); return c;
    }
}