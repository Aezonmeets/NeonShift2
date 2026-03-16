using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

[System.Serializable]
public struct SongData
{
    public string songName;
    public AudioClip audioClip;
}

public enum GameMode { Easy, Medium, Hard, Endless }
public enum HitResult { Perfect, Good, Early, Late, Miss }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    [HideInInspector] public GameMode currentMode = GameMode.Easy;

    [Header("Visuals & Background")]
    public float glowIntensity = 2.5f;
    public Sprite backgroundImage;
    [Range(0f, 1f)] public float backgroundBrightness = 0.3f;

    [Header("Level Songs")]
    public SongData[] allSongs;

    [Header("Custom Sound Effects")]
    public AudioClip customPerfectSound;
    public AudioClip customGoodSound;
    public AudioClip customMissSound;

    [Header("Audio Analysis (Auto-Beat)")]
    [Tooltip("How loud the mid/high frequencies need to be to trigger 4x speed (rap/fast beats).")]
    public float activityThreshold = 0.02f;
    private float[] spectrumData = new float[256];
    private float currentDynamicSubdivision = 2f;

    // --- OVERDRIVE MULTIPLIERS ---
    [Header("Overdrive Difficulty")]
    [Tooltip("How much faster the tiles move during Overdrive.")]
    public float overdriveSpeedMultiplier = 1.6f;
    [Tooltip("How much faster the track rotates during Overdrive (lower is faster).")]
    public float overdriveRotationMultiplier = 0.65f;

    // --- FEVER ENERGY SYSTEM ---
    [Header("Fever Settings")]
    public int feverEnergyThreshold = 50; // BUMPED TO 50
    public float feverDuration = 10f;     // How long the chaos lasts
    int currentFeverEnergy = 0;

    public bool IsFeverActive { get; private set; }
    public bool isFeverStarting { get; private set; } // Prevents multiple warnings

    // Original state variables to revert back to after Fever
    float baseSpawnInterval;
    float baseTileSpeed;
    float baseTrackRot;
    float origOrthoSize;

    int score, combo, maxCombo, total, hits, perfectHits;
    float hp = 100f;
    bool alive, paused;
    bool musicStarted = false;

    double dspStartTime;

    TextMeshProUGUI scoreTxt, comboTxt, accTxt, hpTxt, resultTxt, feverTxt, feverEnergyTxt;
    RectTransform feverFillRT;
    Image feverFillImg;
    TextMeshProUGUI goScore, goAcc, goCombo, goTitle;
    GameObject goPanel, pausePanel, lbPanel;
    GameObject feverUIContainer;
    Coroutine resultCo;

    AudioSource music;
    AudioSource sfx;
    AudioClip sPerfect, sGood, sMiss;

    static readonly Color CP = new Color(1f, .95f, .15f); // Yellow
    static readonly Color CG = new Color(.1f, 1f, .6f);   // Green
    static readonly Color CO = new Color(1f, .6f, .1f);   // Orange
    static readonly Color CM = new Color(1f, .2f, .35f);  // Red
    static readonly Color CYAN = new Color(0f, .9f, 1f);  // Cyan
    static readonly Color MAGENTA = new Color(1f, .15f, .75f); // Magenta

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        Camera.main.backgroundColor = new Color(.025f, .025f, .09f);
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.allowHDR = true;

        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.gameObject != gameObject) Destroy(c.gameObject);

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.volume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);

        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.volume = PlayerPrefs.GetFloat("MusicVolume", 0.45f);

        sPerfect = customPerfectSound != null ? customPerfectSound : Beep(880f, .08f);
        sGood = customGoodSound != null ? customGoodSound : Beep(660f, .06f);
        sMiss = customMissSound != null ? customMissSound : Beep(110f, .13f, true);

        SetupBackground();
        BuildUI();
    }

    void Start()
    {
        TileSpawner.Instance.Init(currentMode);
        ApplyMode();
        alive = true;

        origOrthoSize = Camera.main.orthographicSize;

        TileSpawner.Instance.BeginSpawning();
        TrackController.Instance.BeginRotating();
        TryPlayMusic();
    }

    void SetupBackground()
    {
        if (backgroundImage == null) return;
        GameObject bgObj = new GameObject("DynamicBackground");
        bgObj.transform.SetParent(Camera.main.transform);
        bgObj.transform.localPosition = new Vector3(0f, 0f, 10f);
        SpriteRenderer bgSR = bgObj.AddComponent<SpriteRenderer>();
        bgSR.sprite = backgroundImage;
        bgSR.sortingOrder = -100;
        bgSR.color = new Color(backgroundBrightness, backgroundBrightness, backgroundBrightness, 1f);
        float camHeight = Camera.main.orthographicSize * 2f;
        float camWidth = camHeight * Camera.main.aspect;
        float finalScale = Mathf.Max(camWidth / bgSR.sprite.bounds.size.x, camHeight / bgSR.sprite.bounds.size.y);
        bgObj.transform.localScale = new Vector3(finalScale, finalScale, 1f);
    }

    Color GetHDR(Color c) => new Color(c.r * glowIntensity, c.g * glowIntensity, c.b * glowIntensity, 1f);

    public Color GetFeverRGB()
    {
        return Color.HSVToRGB((Time.time * 2f) % 1f, 1f, 1f);
    }

    void TryPlayMusic()
    {
        string pickedSong = PlayerPrefs.GetString("SelectedSong", "");
        string difficulty = PlayerPrefs.GetString("SelectedDifficulty", "Easy");

        if (System.Enum.TryParse(difficulty, out GameMode parsedMode))
        {
            currentMode = parsedMode;
        }

        AudioClip clipToPlay = Resources.Load<AudioClip>($"Music/{difficulty}/{pickedSong}");

        if (clipToPlay != null)
        {
            music.clip = clipToPlay;
            if (currentMode != GameMode.Endless && TileSpawner.Instance != null)
                TileSpawner.Instance.SetDynamicBPM(clipToPlay.name);

            music.loop = (currentMode == GameMode.Endless);
            music.Play();
            dspStartTime = AudioSettings.dspTime;
            musicStarted = true;
        }
    }

    void Update()
    {
        if (!alive) return;
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();

        // --- DYNAMIC AUDIO ANALYSIS FOR OVERDRIVE ---
        if (IsFeverActive && !paused && music.isPlaying)
        {
            music.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);

            float midHighEnergy = 0f;
            for (int i = 10; i < 60; i++)
            {
                midHighEnergy += spectrumData[i];
            }
            midHighEnergy /= 50f;

            // Rap/Fast section detection: kicks it into 4x spawn mode
            float targetSubdivision = (midHighEnergy > activityThreshold) ? 4f : 2f;
            currentDynamicSubdivision = Mathf.Lerp(currentDynamicSubdivision, targetSubdivision, Time.deltaTime * 12f);
        }

        if (!paused && Camera.main != null)
        {
            if (IsFeverActive)
            {
                Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, origOrthoSize * 1.25f, Time.deltaTime * 5f);
            }
            else
            {
                Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, origOrthoSize, Time.deltaTime * 5f);
            }
            Camera.main.transform.localRotation = Quaternion.identity;
        }

        if (currentMode == GameMode.Endless)
        {
            float t = Time.timeSinceLevelLoad;
            float targetInterval = Mathf.Max(.2f, 0.7f - t * .005f);

            TileSpawner.Instance.spawnInterval = IsFeverActive ? targetInterval / currentDynamicSubdivision : targetInterval;

            // Harder speed scaling in endless Overdrive
            float currentEndlessSpeed = Mathf.Min(20f, 6f + t * .02f);
            TileSpawner.Instance.tileSpeed = IsFeverActive ? currentEndlessSpeed * overdriveSpeedMultiplier : currentEndlessSpeed;
        }
        else if (musicStarted && !paused)
        {
            if (IsFeverActive)
            {
                // Dynamic fast spawns + static fast tile speed
                TileSpawner.Instance.spawnInterval = baseSpawnInterval / currentDynamicSubdivision;
            }

            if (!music.isPlaying && TileSpawner.Instance.GetActiveTiles().Count == 0)
            {
                musicStarted = false;
                StartCoroutine(TrackClearRoutine());
            }
        }
        RefreshHUD();
    }

    IEnumerator TrackClearRoutine() { yield return new WaitForSeconds(0.8f); if (alive) EndLevel(true); }

    void ApplyMode()
    {
        var ts = TileSpawner.Instance;
        var tc = TrackController.Instance;
        var pc = PlayerController.Instance;

        switch (currentMode)
        {
            case GameMode.Easy:
                ts.spawnInterval = 0.75f; ts.tileSpeed = 6f; tc.rotationInterval = 9f;
                pc.earlyDistance = 3.0f; pc.earlyGoodDistance = 1.5f; pc.earlyPerfectDistance = 0.6f;
                pc.latePerfectDistance = -0.4f; pc.lateGoodDistance = -1.2f; pc.lateDistance = -1.8f;
                break;
            case GameMode.Medium:
                ts.spawnInterval = 0.45f; ts.tileSpeed = 9f; tc.rotationInterval = 6f;
                pc.earlyDistance = 2.8f; pc.earlyGoodDistance = 1.2f; pc.earlyPerfectDistance = 0.4f;
                pc.latePerfectDistance = -0.2f; pc.lateGoodDistance = -0.8f; pc.lateDistance = -1.4f;
                break;
            case GameMode.Hard:
                ts.spawnInterval = 0.22f; ts.tileSpeed = 14f; tc.rotationInterval = 3.5f;
                pc.earlyDistance = 2.4f; pc.earlyGoodDistance = 0.8f; pc.earlyPerfectDistance = 0.25f;
                pc.latePerfectDistance = -0.15f; pc.lateGoodDistance = -0.5f; pc.lateDistance = -1.0f;
                break;
            case GameMode.Endless:
                ts.spawnInterval = 0.7f; ts.tileSpeed = 6f; tc.rotationInterval = 8f;
                pc.earlyDistance = 2.8f; pc.earlyGoodDistance = 1.2f; pc.earlyPerfectDistance = 0.4f;
                pc.latePerfectDistance = -0.2f; pc.lateGoodDistance = -0.8f; pc.lateDistance = -1.4f;
                ts.endlessMode = true;
                break;
        }

        if (feverUIContainer != null)
        {
            feverUIContainer.SetActive(currentMode == GameMode.Endless);
        }

        baseSpawnInterval = ts.spawnInterval;
        baseTileSpeed = ts.tileSpeed;
        baseTrackRot = tc.rotationInterval;
    }

    public void RegisterHit(HitResult r, Vector3 pos)
    {
        total++; string lbl; Color col;

        // "GOD MODE" CHAOS FORGIVENESS
        if (IsFeverActive && r != HitResult.Perfect && r != HitResult.Good)
        {
            combo++;
            score += 50;
            if (combo > maxCombo) maxCombo = combo;

            lbl = "CHAOS!"; col = GetHDR(MAGENTA);
            sfx.PlayOneShot(sGood, 0.8f);
            ShowResult(lbl, col);
            return;
        }

        switch (r)
        {
            case HitResult.Perfect:
                perfectHits++;
                combo++;
                if (currentMode == GameMode.Endless && !IsFeverActive && !isFeverStarting)
                {
                    currentFeverEnergy++;
                    if (currentFeverEnergy >= feverEnergyThreshold) StartFeverSequence();
                }

                int pointsEarned = 100 + combo * 5;
                if (IsFeverActive) pointsEarned *= 2;
                score += pointsEarned;

                lbl = "PERFECT!"; col = CP;
                sfx.PlayOneShot(sPerfect, IsFeverActive ? 1.4f : 1.2f);
                break;

            case HitResult.Good:
                combo = 0;
                if (currentMode == GameMode.Endless && !IsFeverActive && !isFeverStarting)
                {
                    currentFeverEnergy++;
                    if (currentFeverEnergy >= feverEnergyThreshold) StartFeverSequence();
                }

                int goodPoints = 50;
                if (IsFeverActive) goodPoints *= 2;
                score += goodPoints;

                lbl = "GOOD"; col = CG;
                sfx.PlayOneShot(sGood);
                break;

            case HitResult.Early:
            case HitResult.Late:
                combo = 0; currentFeverEnergy = 0;
                score += 20; lbl = r == HitResult.Early ? "EARLY" : "LATE"; col = CO; sfx.PlayOneShot(sGood, 0.8f);
                break;

            default:
                combo = 0; currentFeverEnergy = 0; hp = Mathf.Max(0f, hp - 10f);
                lbl = "MISS"; col = CM;
                sfx.PlayOneShot(sMiss); CameraShake.Instance?.Shake(.2f, .1f);
                if (hp <= 0f) { EndLevel(false); return; }
                break;
        }
        if (combo > maxCombo) maxCombo = combo;
        ShowResult(lbl, col);
    }

    void StartFeverSequence()
    {
        if (isFeverStarting || IsFeverActive) return;
        isFeverStarting = true;
        StartCoroutine(FeverWarningRoutine());
    }

    IEnumerator FeverWarningRoutine()
    {
        float prepTime = 1.5f;
        CameraShake.Instance?.Shake(0.15f, prepTime);
        yield return StartCoroutine(TrackController.Instance.FlashWarningRoutine(prepTime));
        isFeverStarting = false;
        ActivateFever();
    }

    void ActivateFever()
    {
        IsFeverActive = true;
        currentFeverEnergy = feverEnergyThreshold;

        currentDynamicSubdivision = 2f;

        TileSpawner.Instance.spawnInterval = baseSpawnInterval / currentDynamicSubdivision;

        float newSpeed = baseTileSpeed * overdriveSpeedMultiplier;
        TileSpawner.Instance.tileSpeed = newSpeed;
        TileSpawner.Instance.UpdateActiveTileSpeeds(newSpeed);

        TrackController.Instance.rotationInterval = baseTrackRot * overdriveRotationMultiplier;

        feverTxt.text = "OVERDRIVE";
        feverTxt.color = GetHDR(Color.white);
        feverFillImg.color = GetHDR(MAGENTA);
        comboTxt.fontSize = 85;

        CameraShake.Instance?.Shake(0.3f, 0.15f);

        StartCoroutine(FeverDurationRoutine());
    }

    // Coroutine to automatically end fever with a flashing warning
    IEnumerator FeverDurationRoutine()
    {
        float warningDuration = 2f; // Flash for 2 seconds before ending
        float initialDuration = Mathf.Max(0f, feverDuration - warningDuration);

        yield return new WaitForSeconds(initialDuration);

        // Warn the player that the chaos is about to stop
        if (IsFeverActive && TrackController.Instance != null)
        {
            yield return StartCoroutine(TrackController.Instance.FlashWarningRoutine(warningDuration));
        }

        if (IsFeverActive) DeactivateFever();
    }

    void DeactivateFever()
    {
        IsFeverActive = false;
        currentFeverEnergy = 0;

        TileSpawner.Instance.spawnInterval = baseSpawnInterval;

        TileSpawner.Instance.tileSpeed = baseTileSpeed;
        TileSpawner.Instance.UpdateActiveTileSpeeds(baseTileSpeed);

        TrackController.Instance.rotationInterval = baseTrackRot;

        feverTxt.text = "FEVER";
        feverTxt.color = new Color(1f, 1f, 1f, 0.7f);
        feverFillImg.color = GetHDR(CYAN);
        comboTxt.fontSize = 72;

        CameraShake.Instance?.Shake(0.2f, 0.1f);
    }

    public void EndLevel(bool cleared = false)
    {
        alive = false; if (music.isPlaying) music.Stop();
        TileSpawner.Instance.StopSpawning(); TrackController.Instance.StopRotating();
        float acc = total > 0 ? (float)perfectHits / total * 100f : 0f;
        goScore.text = score.ToString("N0"); goAcc.text = $"{acc:F1}%"; goCombo.text = "x" + maxCombo;
        if (cleared) { goTitle.text = "TRACK CLEARED"; goTitle.color = GetHDR(CG); sfx.PlayOneShot(sPerfect); }
        else { goTitle.text = "GAME OVER"; goTitle.color = GetHDR(new Color(1f, .15f, .25f)); }
        goPanel.SetActive(true);
    }

    void TogglePause()
    {
        paused = !paused; Time.timeScale = paused ? 0f : 1f;
        if (paused) { if (music.isPlaying) music.Pause(); }
        else { music.UnPause(); dspStartTime = AudioSettings.dspTime - music.time; }
        pausePanel.SetActive(paused);
    }

    public void Restart() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    public void MainMenu() { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
    public bool IsGameActive() => alive;

    public float GetMusicTime()
    {
        if (music != null && music.clip != null && music.isPlaying && !paused) return (float)(AudioSettings.dspTime - dspStartTime);
        else if (music != null && paused) return music.time;
        return 0f;
    }

    public void ApplySpamPenalty()
    {
        if (!alive) return;

        // Ignore spam penalties entirely during Overdrive (God Mode!)
        if (IsFeverActive) return;

        combo = 0; currentFeverEnergy = 0; hp = Mathf.Max(0f, hp - 3f);
        sfx.PlayOneShot(sMiss); CameraShake.Instance?.Shake(.1f, .05f);
        ShowResult("SPAM!", CM); if (hp <= 0f) EndLevel(false);
        RefreshHUD();
    }

    void RefreshHUD()
    {
        scoreTxt.text = score.ToString("N0");
        float acc = total > 0 ? (float)perfectHits / total * 100f : 100f;
        accTxt.text = $"{acc:F1}%"; hpTxt.text = "\u2665 " + (int)hp;

        comboTxt.text = combo > 1 ? "x" + combo : "";

        if (combo > 1)
        {
            if (IsFeverActive) comboTxt.color = GetHDR(GetFeverRGB());
            else comboTxt.color = Color.Lerp(CYAN, MAGENTA, Mathf.Sin(Time.time * 7f) * .5f + .5f);
        }

        if (feverFillRT != null && feverEnergyTxt != null)
        {
            if (IsFeverActive)
            {
                feverFillRT.localScale = new Vector3(1f, 1f, 1f);
                feverFillImg.color = GetHDR(GetFeverRGB());
                feverEnergyTxt.text = "MAX";
            }
            else
            {
                // Temperature Style Logic! 
                // Cyan (Cold) -> Yellow (Warm) -> Red (Hot)
                float fill = Mathf.Clamp01((float)currentFeverEnergy / feverEnergyThreshold);

                Color tempColor;
                if (fill < 0.5f)
                    tempColor = Color.Lerp(CYAN, CP, fill * 2f); // 0% to 50%
                else
                    tempColor = Color.Lerp(CP, CM, (fill - 0.5f) * 2f); // 50% to 100%

                feverFillImg.color = GetHDR(tempColor);

                feverEnergyTxt.text = $"{currentFeverEnergy} / {feverEnergyThreshold}";
                feverFillRT.localScale = new Vector3(1f, fill, 1f);
            }
        }
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

    void BuildUI()
    {
        var cgo = new GameObject("_Canvas");
        var cv = cgo.AddComponent<Canvas>();

        // --- THE NEON GLOW FIX ---
        cv.renderMode = RenderMode.ScreenSpaceCamera;
        cv.worldCamera = Camera.main;
        cv.planeDistance = 5f;
        // -------------------------

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

        comboTxt = T(cgo, "", 72, new Vector2(0, 100), A(0.5f, 0.5f), A(0.5f, 0.5f), TextAlignmentOptions.Center);
        comboTxt.color = CYAN; comboTxt.fontStyle = FontStyles.Bold;

        feverUIContainer = new GameObject("FeverUIContainer");
        feverUIContainer.transform.SetParent(cgo.transform, false);
        var fContainerRT = feverUIContainer.AddComponent<RectTransform>();
        fContainerRT.anchorMin = Vector2.zero; fContainerRT.anchorMax = Vector2.one;
        fContainerRT.offsetMin = fContainerRT.offsetMax = Vector2.zero;

        var fBG = new GameObject("FeverBG"); fBG.transform.SetParent(feverUIContainer.transform, false);
        var fBGRt = fBG.AddComponent<RectTransform>();
        fBGRt.anchorMin = A(1, 0.5f); fBGRt.anchorMax = A(1, 0.5f);
        fBGRt.anchoredPosition = new Vector2(-60, 0);
        fBGRt.sizeDelta = new Vector2(30, 350); // Made it slightly wider for that thermometer look
        fBG.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        AddBorder(fBG, new Color(0.5f, 0.5f, 0.6f, 0.5f), 0.3f); // Calmer border to let the internal color pop

        var fFill = new GameObject("FeverFill"); fFill.transform.SetParent(fBG.transform, false);
        feverFillRT = fFill.AddComponent<RectTransform>();
        feverFillRT.anchorMin = A(0, 0); feverFillRT.anchorMax = A(1, 1);
        feverFillRT.offsetMin = feverFillRT.offsetMax = Vector2.zero;
        feverFillRT.pivot = new Vector2(0.5f, 0f);
        feverFillImg = fFill.AddComponent<Image>(); feverFillImg.color = GetHDR(CYAN);
        feverFillRT.localScale = new Vector3(1, 0, 1);

        feverTxt = T(feverUIContainer, "FEVER", 18, new Vector2(-60, 205), A(1, 0.5f), A(1, 0.5f), TextAlignmentOptions.Center);
        feverTxt.color = new Color(1f, 1f, 1f, 0.7f);

        feverEnergyTxt = T(feverUIContainer, "0 / 50", 14, new Vector2(-60, -195), A(1, 0.5f), A(1, 0.5f), TextAlignmentOptions.Center);
        feverEnergyTxt.color = Color.white;

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

        pausePanel.SetActive(false);
    }

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