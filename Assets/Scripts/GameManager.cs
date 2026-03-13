using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public enum GameMode { Easy, Medium, Hard, Endless }
public enum HitResult { Perfect, Good, Miss }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    [HideInInspector] public GameMode currentMode = GameMode.Easy;

    [Header("Glow Settings")]
    [Tooltip("Intensity of the neon glow. Requires Bloom in Global Volume!")]
    public float glowIntensity = 4.5f;

    [Header("Combo Animation")]
    public float comboFadeDelay = 1.0f;
    public float comboFadeSpeed = 2.5f;
    private float comboFadeTimer;
    private Vector3 comboBaseScale;
    private Coroutine comboBumpCo;

    int score, combo, maxCombo, total, hits;
    float hp = 100f;
    bool alive, paused;

    // HUD
    TextMeshProUGUI scoreTxt, comboTxt, accTxt, hpTxt, resultTxt;

    // Game Over
    TextMeshProUGUI goScore, goAcc, goCombo;
    GameObject goPanel, pausePanel, lbPanel;
    Coroutine resultCo;

    // Music/Audio
    AudioSource music, sfx;
    AudioClip sPerfect, sGood, sMiss;

    AudioClip[] playlist;
    int playlistIndex;

    // Neon Colors
    static readonly Color CP = new Color(1f, .95f, .15f);      // Perfect (Yellow)
    static readonly Color CG = new Color(.1f, 1f, .6f);        // Good (Green)
    static readonly Color CM = new Color(1f, .2f, .35f);       // Miss (Red)
    static readonly Color CYAN = new Color(0f, .9f, 1f);       // Cyan
    static readonly Color MAGENTA = new Color(1f, .15f, .75f); // Magenta

    static readonly Color[] ModeColors = {
        new Color(.1f,1f,.4f),
        new Color(1f,.85f,.1f),
        new Color(1f,.35f,.1f),
        new Color(.8f,.1f,1f),
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // --- CAMERA & HDR SETUP ---
        if (Camera.main != null)
        {
            Camera.main.backgroundColor = new Color(.01f, .01f, .04f);
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.allowHDR = true;

            var data = Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data != null) data.renderPostProcessing = true;
        }

        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.gameObject != gameObject) Destroy(c.gameObject);

        sfx = gameObject.AddComponent<AudioSource>(); sfx.volume = .55f;
        music = gameObject.AddComponent<AudioSource>();
        music.loop = false; music.volume = .7f;

        sPerfect = Beep(880f, .08f); sGood = Beep(660f, .06f); sMiss = Beep(110f, .13f, true);

        BuildUI();
    }

    void Start()
    {
        if (TileSpawner.Instance != null) TileSpawner.Instance.Init(currentMode);
        ApplyMode();
        alive = true;
        if (TileSpawner.Instance != null) TileSpawner.Instance.BeginSpawning();
        if (TrackController.Instance != null) TrackController.Instance.BeginRotating();
        TryPlayMusic();
    }

    Color GetHDR(Color c) => new Color(c.r * glowIntensity, c.g * glowIntensity, c.b * glowIntensity, 1f);
    public bool IsGameActive() => alive;

    void TryPlayMusic()
    {
        playlist = LoadPlaylist(currentMode.ToString());
        playlistIndex = 0;

        if (playlist == null || playlist.Length == 0)
        {
            Debug.Log($"[Music] No clips found in Resources/Music/{currentMode} — add audio files there!");
            return;
        }

        ShufflePlaylist();
        music.loop = false;
        PlayTrack(playlistIndex);
    }

    AudioClip[] LoadPlaylist(string modeName)
    {
        var clips = Resources.LoadAll<AudioClip>("Music/" + modeName);
        if (clips != null && clips.Length > 0) return clips;

        var single = Resources.Load<AudioClip>("Music/" + modeName);
        if (single != null) return new[] { single };

        return null;
    }

    void PlayTrack(int index)
    {
        if (playlist == null || index >= playlist.Length) return;
        music.clip = playlist[index];
        music.Play();
        Debug.Log($"[Music] Playing track {index + 1}/{playlist.Length}: {playlist[index].name}");
    }

    void ShufflePlaylist()
    {
        for (int i = playlist.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = playlist[i]; playlist[i] = playlist[j]; playlist[j] = tmp;
        }
    }

    void Update()
    {
        if (!alive) return;
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();

        if (currentMode == GameMode.Endless)
        {
            UpdateEndless();
        }
        else
        {
            if (music.clip != null && !music.isPlaying && Time.timeSinceLevelLoad > 2f)
            {
                playlistIndex++;
                if (playlist != null && playlistIndex < playlist.Length)
                    PlayTrack(playlistIndex);
                else
                    GameOver();
            }
        }

        HandleComboFading();
        RefreshHUD();
    }

    void UpdateEndless()
    {
        float t = Time.timeSinceLevelLoad;
        float cycleLen = 55f;
        float cyclePos = t % cycleLen;
        float intensity;

        if (cyclePos < 30f) intensity = cyclePos / 30f;
        else if (cyclePos < 40f) intensity = 1f;
        else intensity = 1f - (cyclePos - 40f) / 15f;

        intensity = Mathf.Clamp01(intensity);
        float longTermBoost = Mathf.Min(4f, t * 0.008f);

        if (TileSpawner.Instance != null)
        {
            TileSpawner.Instance.tileSpeed = 5f + intensity * 11f + longTermBoost;
            TileSpawner.Instance.spawnInterval = Mathf.Max(0.35f, 1.2f - intensity * 0.85f);
            TileSpawner.Instance.bpm = 90f + intensity * 60f + longTermBoost * 5f;
        }
        if (TrackController.Instance != null)
        {
            TrackController.Instance.rotationInterval = Mathf.Max(2.5f, 9f - intensity * 6f);
        }
    }

    void HandleComboFading()
    {
        if (comboFadeTimer > 0) comboFadeTimer -= Time.deltaTime;
        else if (comboTxt != null)
        {
            Color c = comboTxt.color;
            c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * comboFadeSpeed);
            comboTxt.color = c;
        }
    }

    void ApplyMode()
    {
        var ts = TileSpawner.Instance; var tc = TrackController.Instance; var pc = PlayerController.Instance;
        if (ts == null || tc == null || pc == null) return;

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
                hits++; combo++; score += 300 + combo * 12; lbl = "PERFECT!"; col = CP; sfx.PlayOneShot(sPerfect);
                BumpCombo();
                break;
            case HitResult.Good:
                hits++; combo = 0; score += 100; lbl = "GOOD"; col = CG; sfx.PlayOneShot(sGood);
                break;
            default:
                combo = 0; hp = Mathf.Max(0f, hp - 10f); lbl = "MISS"; col = CM; sfx.PlayOneShot(sMiss);
                CameraShake.Instance?.Shake(.2f, .1f);
                comboFadeTimer = 0;
                if (hp <= 0f) { GameOver(); return; }
                break;
        }
        if (combo > maxCombo) maxCombo = combo;
        ShowResult(lbl, col);
    }

    public void ApplySpamPenalty()
    {
        if (!alive) return;
        combo = 0; hp = Mathf.Max(0f, hp - 5f);
        sfx.PlayOneShot(sMiss);
        CameraShake.Instance?.Shake(.1f, .05f);
        ShowResult("MISS", CM);
        if (hp <= 0f) GameOver();
    }

    void BumpCombo()
    {
        if (combo < 2) return;
        comboTxt.text = "x" + combo;
        comboFadeTimer = comboFadeDelay;
        comboTxt.color = GetHDR(CYAN);
        if (comboBumpCo != null) StopCoroutine(comboBumpCo);
        comboBumpCo = StartCoroutine(ComboHeartbeat());
    }

    IEnumerator ComboHeartbeat()
    {
        float t = 0, dur = 0.08f;
        while (t < dur)
        {
            t += Time.deltaTime;
            comboTxt.transform.localScale = Vector3.Lerp(comboBaseScale, comboBaseScale * 1.3f, t / dur);
            yield return null;
        }
        t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            comboTxt.transform.localScale = Vector3.Lerp(comboBaseScale * 1.3f, comboBaseScale, t / dur);
            yield return null;
        }
        comboTxt.transform.localScale = comboBaseScale;
    }

    void GameOver()
    {
        alive = false; music.Stop();
        if (TileSpawner.Instance != null) TileSpawner.Instance.StopSpawning();
        if (TrackController.Instance != null) TrackController.Instance.StopRotating();

        float acc = total > 0 ? (float)hits / total * 100f : 0f;
        goScore.text = score.ToString("N0");
        goAcc.text = $"{acc:F1}%";
        goCombo.text = "x" + maxCombo;

        goPanel.SetActive(true);
        HighScoreManager.Instance?.TrySubmitScore(currentMode, score);
        LeaderboardManager.Instance?.TrySubmit(currentMode.ToString(), score, maxCombo, acc);
        if (lbPanel != null) LeaderboardManager.Instance?.BuildLeaderboardUI(lbPanel, currentMode.ToString());
    }

    void TogglePause()
    {
        paused = !paused; Time.timeScale = paused ? 0f : 1f;
        if (music.isPlaying && paused) music.Pause(); else if (!paused) music.UnPause();
        pausePanel.SetActive(paused);
    }

    public void Restart() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    public void MainMenu() { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }

    void RefreshHUD()
    {
        if (scoreTxt) scoreTxt.text = score.ToString("N0");
        float acc = total > 0 ? (float)hits / total * 100f : 100f;
        if (accTxt) accTxt.text = $"{acc:F1}%";
        if (hpTxt) hpTxt.text = "\u2665 " + (int)hp;
    }

    void ShowResult(string lbl, Color col) { if (resultCo != null) StopCoroutine(resultCo); resultCo = StartCoroutine(ResultAnim(lbl, col)); }

    IEnumerator ResultAnim(string lbl, Color col)
    {
        resultTxt.text = lbl; resultTxt.transform.localScale = Vector3.one * 1.4f;
        float t = 0f;
        while (t < 0.55f)
        {
            t += Time.deltaTime;
            resultTxt.color = new Color(col.r * glowIntensity, col.g * glowIntensity, col.b * glowIntensity, 1f - t / 0.55f);
            resultTxt.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Min(t / .18f, 1f)); yield return null;
        }
        resultTxt.text = "";
    }

    [ContextMenu("Show UI in Editor")]
    void ShowUIInEditor()
    {
        GameObject oldCanvas = GameObject.Find("_Canvas");
        if (oldCanvas != null) DestroyImmediate(oldCanvas);
        BuildUI();
    }

    void BuildUI()
    {
        var cgo = new GameObject("_Canvas");
        var cv = cgo.AddComponent<Canvas>();

        cv.renderMode = RenderMode.ScreenSpaceCamera;
        cv.worldCamera = Camera.main;
        cv.planeDistance = 2f;
        cv.sortingOrder = 20;

        var sc = cgo.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        scoreTxt = T(cgo, "0", 52, new Vector2(24, -20), A(0, 1), A(0, 1), TextAlignmentOptions.TopLeft);
        hpTxt = T(cgo, "\u2665 100", 28, new Vector2(0, -20), A(.5f, 1), A(.5f, 1), TextAlignmentOptions.Center);
        hpTxt.color = GetHDR(new Color(1f, .2f, .4f));

        accTxt = T(cgo, "100.0%", 26, new Vector2(-20, -20), A(1, 1), A(1, 1), TextAlignmentOptions.TopRight);
        accTxt.color = GetHDR(CYAN);

        comboTxt = T(cgo, "", 75, new Vector2(0, 120), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        comboTxt.color = new Color(CYAN.r, CYAN.g, CYAN.b, 0f);
        comboTxt.fontStyle = FontStyles.Bold | FontStyles.Italic;
        comboBaseScale = comboTxt.transform.localScale;

        resultTxt = T(cgo, "", 44, new Vector2(0, 10), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);

        var modeLbl = T(cgo, "NEON SHIFT", 14, new Vector2(16, 50), A(0, 0), A(0, 0), TextAlignmentOptions.BottomLeft);
        modeLbl.color = GetHDR(CYAN); modeLbl.fontStyle = FontStyles.Bold;
        var diffLbl = T(cgo, "DIFFICULTY: " + currentMode.ToString().ToUpper(), 11, new Vector2(16, 34), A(0, 0), A(0, 0), TextAlignmentOptions.BottomLeft);
        diffLbl.color = new Color(.6f, .7f, .8f, .7f);

        T(cgo, "ESC = PAUSE", 18, new Vector2(-16, 16), A(1, 0), A(1, 0), TextAlignmentOptions.BottomRight)
            .color = new Color(.4f, .7f, 1f, .3f);

        // Game Over Panel
        goPanel = Panel(cgo, new Color(.02f, .02f, .05f, .95f));

        var goTitle = T(goPanel, "GAME OVER", 88, new Vector2(0, 200), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        goTitle.color = GetHDR(new Color(1f, .15f, .25f));

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

        NeonBtn(goPanel, "REPLAY", CYAN, new Vector2(-160, -95), () => Restart());
        NeonBtn(goPanel, "MENU", MAGENTA, new Vector2(160, -95), () => MainMenu());

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

        // Pause Panel
        pausePanel = Panel(cgo, new Color(.01f, .02f, .08f, .93f));
        NeonLine(pausePanel, new Vector2(-640, 358), new Vector2(640, 358), GetHDR(CYAN), 1f);
        var pauseTitle = T(pausePanel, "PAUSED", 90, new Vector2(0, 210), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        pauseTitle.color = GetHDR(CYAN); pauseTitle.fontStyle = FontStyles.Bold;

        T(pausePanel, "PRESS ESC TO RESUME", 18, new Vector2(0, 140), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center)
            .color = new Color(.5f, .7f, .9f, .7f);

        PauseBtn(pausePanel, "RESUME", CYAN, new Vector2(0, 60), () => TogglePause());
        PauseBtn(pausePanel, "REPLAY", new Color(.1f, 1f, .4f), new Vector2(0, -20), () => Restart());
        PauseBtn(pausePanel, "QUIT TO MENU", MAGENTA, new Vector2(0, -100), () => MainMenu());

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

        goPanel.SetActive(false);
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
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = sz; t.alignment = al;
        t.color = Color.white;
        t.extraPadding = true;
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
        rt.anchoredPosition = (from + to) / 2f;
        rt.sizeDelta = new Vector2(Vector2.Distance(from, to), 2f);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        go.AddComponent<Image>().color = new Color(col.r, col.g, col.b, alpha);
    }

    void NeonBtn(GameObject p, string lbl, Color col, Vector2 pos, UnityEngine.Events.UnityAction cb)
    {
        var go = new GameObject("_B"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = A(.5f, .5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(250, 54);
        var img = go.AddComponent<Image>(); img.color = new Color(col.r * .08f, col.g * .08f, col.b * .08f, .9f);

        AddBorder(go, GetHDR(col), 1f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(cb);

        Label(go, lbl, 26, GetHDR(col));
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