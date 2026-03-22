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
    public bool isFeverMode = false;
    public Sprite backgroundImage;
    [Range(0f, 1f)] public float backgroundBrightness = 0.3f;

    // ── Drag your bar-chart icon PNG here in the Inspector ────────────────
    [Header("Live Scoring Card")]
    [Tooltip("Drag your bar-chart icon sprite here. If left empty the icon is drawn in code.")]
    public Sprite liveScoringIcon;
    // ─────────────────────────────────────────────────────────────────────

    [Header("Level Songs")]
    public SongData[] allSongs;

    [Header("Custom Sound Effects")]
    public AudioClip customPerfectSound;
    public AudioClip customGoodSound;
    public AudioClip customMissSound;

    [Header("Audio Analysis (Auto-Beat)")]
    [Tooltip("How loud the mid/high frequencies need to be to trigger 4x speed.")]
    public float activityThreshold = 0.02f;
    private float[] spectrumData = new float[256];
    private float currentDynamicSubdivision = 2f;

    [Header("Overdrive Difficulty")]
    public float overdriveSpeedMultiplier = 1.6f;
    public float overdriveRotationMultiplier = 0.65f;

    [Header("Fever Settings")]
    public int feverEnergyThreshold = 50;
    public float feverDuration = 10f;
    int currentFeverEnergy = 0;

    public bool IsFeverActive   { get; private set; }
    public bool isFeverStarting { get; private set; }

    float baseSpawnInterval, baseTileSpeed, baseTrackRot, origOrthoSize;

    int   score, combo, maxCombo, total, hits, perfectHits;
    float displayScore = 0f;
    float scoreScale   = 1f;
    float hp           = 100f;
    bool  alive, paused, musicStarted;

    double dspStartTime;

    // scoreTxt kept for compat but not added to canvas — card shows score instead
    TextMeshProUGUI scoreTxt;
    TextMeshProUGUI comboTxt, accTxt, hpTxt, resultTxt, feverTxt, feverEnergyTxt;
    RectTransform   feverFillRT;
    Image           feverFillImg;
    TextMeshProUGUI goScore, goAcc, goCombo, goTitle;
    GameObject      goPanel, pausePanel;
    GameObject      feverUIContainer;
    Coroutine       resultCo;

    // ── Right-side HUD (Endless mode only) ───────────────────────────────────
    [Header("Right Panel - Lightning Sprite")]
    [Tooltip("Drag DiamondLightning.png here. Leave empty to use emoji.")]
    public Sprite   lightningSprite;
    TextMeshProUGUI hudScoreTxt;
    TextMeshProUGUI hudHpTxt;
    Outline         hudDiamondOutline;
    Image           hudDiamondGlowImg;
    Coroutine       diamondRainbowCR;
    TextMeshProUGUI origComboTxt; // centre-screen combo for Easy/Medium/Hard

    AudioSource music, sfx;
    AudioClip   sPerfect, sGood, sMiss;

    // ── Live Scoring card refs ─────────────────────────────────────────────
    TextMeshProUGUI liveScoreValueTxt;
    TextMeshProUGUI liveRankValueTxt;
    GameObject      liveScoringCard;
    Coroutine       scoreCountCo;
    Coroutine       rankFlashCo;
    long            liveDisplayedScore = 0;
    int             lastPolledRank     = -1;
    long            lastPolledScore    = -1;

    static readonly Color CP      = new Color(1f,  .95f, .15f);
    static readonly Color CG      = new Color(.1f, 1f,   .6f);
    static readonly Color CO      = new Color(1f,  .6f,  .1f);
    static readonly Color CM      = new Color(1f,  .2f,  .35f);
    static readonly Color CYAN    = new Color(0f,  .9f,  1f);
    static readonly Color MAGENTA = new Color(1f,  .15f, .75f);

    static readonly Color CARD_BG   = new Color(0.03f, 0.05f, 0.09f, 0.97f);
    static readonly Color CARD_CYAN = new Color(0f,    0.92f, 1f,    1f);

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        Camera.main.backgroundColor = new Color(.025f, .025f, .09f);
        Camera.main.clearFlags      = CameraClearFlags.SolidColor;
        Camera.main.allowHDR        = true;

        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.gameObject != gameObject) Destroy(c.gameObject);

        sfx        = gameObject.AddComponent<AudioSource>();
        sfx.volume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);

        music        = gameObject.AddComponent<AudioSource>();
        music.loop   = true;
        music.volume = PlayerPrefs.GetFloat("MusicVolume", 0.45f);

        sPerfect = customPerfectSound != null ? customPerfectSound : Beep(880f, .08f);
        sGood    = customGoodSound    != null ? customGoodSound    : Beep(660f, .06f);
        sMiss    = customMissSound    != null ? customMissSound    : Beep(110f, .13f, true);

        SetupBackground();
        BuildUI();
    }

    void Start()
    {
        TileSpawner.Instance.Init(currentMode);
        ApplyMode();
        alive         = true;
        origOrthoSize = Camera.main.orthographicSize;

        string playerName = PlayerPrefs.GetString("PlayerName", "Player");
        LiveScoreManager.Instance.RegisterPlayer(playerName, 0);

        TileSpawner.Instance.BeginSpawning();
        TrackController.Instance.BeginRotating();
        TryPlayMusic();
        StartCoroutine(FadeInLiveCard());
    }

    void SetupBackground()
    {
        if (backgroundImage == null) return;
        GameObject bgObj = new GameObject("DynamicBackground");
        bgObj.transform.SetParent(Camera.main.transform);
        bgObj.transform.localPosition = new Vector3(0f, 0f, 10f);
        SpriteRenderer bgSR = bgObj.AddComponent<SpriteRenderer>();
        bgSR.sprite       = backgroundImage;
        bgSR.sortingOrder = -100;
        bgSR.color        = new Color(backgroundBrightness, backgroundBrightness, backgroundBrightness, 1f);
        float camHeight  = Camera.main.orthographicSize * 2f;
        float camWidth   = camHeight * Camera.main.aspect;
        float finalScale = Mathf.Max(camWidth / bgSR.sprite.bounds.size.x, camHeight / bgSR.sprite.bounds.size.y);
        bgObj.transform.localScale = new Vector3(finalScale, finalScale, 1f);
    }

    Color GetHDR(Color c) => new Color(c.r * glowIntensity, c.g * glowIntensity, c.b * glowIntensity, 1f);
    public Color GetFeverRGB() => Color.HSVToRGB((Time.time * 2f) % 1f, 1f, 1f);

    void TryPlayMusic()
    {
        string pickedSong = PlayerPrefs.GetString("SelectedSong", "");
        string difficulty = PlayerPrefs.GetString("SelectedDifficulty", "Easy");
        if (System.Enum.TryParse(difficulty, out GameMode parsedMode)) currentMode = parsedMode;
        AudioClip clipToPlay = Resources.Load<AudioClip>($"Music/{difficulty}/{pickedSong}");
        if (clipToPlay != null)
        {
            music.clip = clipToPlay;
            if (currentMode != GameMode.Endless && TileSpawner.Instance != null)
                TileSpawner.Instance.SetDynamicBPM(clipToPlay.name);
            music.loop   = (currentMode == GameMode.Endless);
            music.Play();
            dspStartTime = AudioSettings.dspTime;
            musicStarted = true;
        }
    }

    void Update()
    {
        if (!alive) return;
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();

        if (IsFeverActive && !paused && music.isPlaying)
        {
            music.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);
            float midHighEnergy = 0f;
            for (int i = 10; i < 60; i++) midHighEnergy += spectrumData[i];
            midHighEnergy /= 50f;
            float targetSubdivision = (midHighEnergy > activityThreshold) ? 4f : 2f;
            currentDynamicSubdivision = Mathf.Lerp(currentDynamicSubdivision, targetSubdivision, Time.deltaTime * 12f);
        }

        if (!paused && Camera.main != null)
        {
            Camera.main.orthographicSize = IsFeverActive
                ? Mathf.Lerp(Camera.main.orthographicSize, origOrthoSize * 1.25f, Time.deltaTime * 5f)
                : Mathf.Lerp(Camera.main.orthographicSize, origOrthoSize,         Time.deltaTime * 5f);
            Camera.main.transform.localRotation = Quaternion.identity;
        }

        if (currentMode == GameMode.Endless)
        {
            float t = Time.timeSinceLevelLoad;
            float targetInterval = Mathf.Max(.2f, 0.7f - t * .005f);
            TileSpawner.Instance.spawnInterval = IsFeverActive ? targetInterval / currentDynamicSubdivision : targetInterval;
            float currentEndlessSpeed = Mathf.Min(20f, 6f + t * .02f);
            TileSpawner.Instance.tileSpeed = IsFeverActive ? currentEndlessSpeed * overdriveSpeedMultiplier : currentEndlessSpeed;
        }
        else if (musicStarted && !paused)
        {
            if (IsFeverActive)
                TileSpawner.Instance.spawnInterval = baseSpawnInterval / currentDynamicSubdivision;
            if (!music.isPlaying && TileSpawner.Instance.GetActiveTiles().Count == 0)
            {
                musicStarted = false;
                StartCoroutine(TrackClearRoutine());
            }
        }

        RefreshHUD();
        if (!paused) RefreshLiveCard();
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
                ts.spawnInterval = 0.75f; ts.tileSpeed = 6f;  tc.rotationInterval = 9f;
                pc.earlyDistance = 3.0f;  pc.earlyGoodDistance = 1.5f; pc.earlyPerfectDistance = 0.6f;
                pc.latePerfectDistance = -0.4f; pc.lateGoodDistance = -1.2f; pc.lateDistance = -1.8f;
                break;
            case GameMode.Medium:
                ts.spawnInterval = 0.45f; ts.tileSpeed = 9f;  tc.rotationInterval = 6f;
                pc.earlyDistance = 2.8f;  pc.earlyGoodDistance = 1.2f; pc.earlyPerfectDistance = 0.4f;
                pc.latePerfectDistance = -0.2f; pc.lateGoodDistance = -0.8f; pc.lateDistance = -1.4f;
                break;
            case GameMode.Hard:
                ts.spawnInterval = 0.22f; ts.tileSpeed = 14f; tc.rotationInterval = 3.5f;
                pc.earlyDistance = 2.4f;  pc.earlyGoodDistance = 0.8f; pc.earlyPerfectDistance = 0.25f;
                pc.latePerfectDistance = -0.15f; pc.lateGoodDistance = -0.5f; pc.lateDistance = -1.0f;
                break;
            case GameMode.Endless:
                ts.spawnInterval = 0.7f;  ts.tileSpeed = 6f;  tc.rotationInterval = 8f;
                pc.earlyDistance = 2.8f;  pc.earlyGoodDistance = 1.2f; pc.earlyPerfectDistance = 0.4f;
                pc.latePerfectDistance = -0.2f; pc.lateGoodDistance = -0.8f; pc.lateDistance = -1.4f;
                ts.endlessMode = true;
                break;
        }
        bool isEndless = currentMode == GameMode.Endless;
        // Right-side HUD panel only visible in Endless
        if (feverUIContainer != null) feverUIContainer.SetActive(isEndless);
        // Original heart + centre combo only visible in non-Endless
        if (hpTxt        != null) hpTxt.gameObject.SetActive(!isEndless);
        if (origComboTxt != null) origComboTxt.gameObject.SetActive(!isEndless);
        // Switch comboTxt to the correct element for this mode
        if (!isEndless && origComboTxt != null) comboTxt = origComboTxt;
        baseSpawnInterval = ts.spawnInterval;
        baseTileSpeed     = ts.tileSpeed;
        baseTrackRot      = tc.rotationInterval;
    }

    public void RegisterHit(HitResult r, Vector3 pos)
    {
        total++;
        string lbl; Color col;

        if (IsFeverActive && r != HitResult.Perfect && r != HitResult.Good)
        {
            combo++; score += 50; scoreScale = 1.3f;
            if (combo > maxCombo) maxCombo = combo;
            lbl = "CHAOS!"; col = GetHDR(MAGENTA);
            sfx.PlayOneShot(sGood, 0.8f);
            ShowResult(lbl, col);
            LiveScoreManager.Instance.AddScore(50, "CHAOS");
            return;
        }

        switch (r)
        {
            case HitResult.Perfect:
                perfectHits++; combo++;
                if (currentMode == GameMode.Endless && !IsFeverActive && !isFeverStarting)
                { currentFeverEnergy++; if (currentFeverEnergy >= feverEnergyThreshold) StartFeverSequence(); }
                int pointsEarned = 100 + combo * 5;
                if (IsFeverActive) pointsEarned *= 2;
                score += pointsEarned; scoreScale = 1.4f;
                lbl = "PERFECT!"; col = CP;
                sfx.PlayOneShot(sPerfect, IsFeverActive ? 1.4f : 1.2f);
                LiveScoreManager.Instance.AddScore(pointsEarned, "PERFECT");
                break;
            case HitResult.Good:
                combo = 0;
                if (currentMode == GameMode.Endless && !IsFeverActive && !isFeverStarting)
                { currentFeverEnergy++; if (currentFeverEnergy >= feverEnergyThreshold) StartFeverSequence(); }
                int goodPoints = 50;
                if (IsFeverActive) goodPoints *= 2;
                score += goodPoints; scoreScale = 1.2f;
                lbl = "GOOD"; col = CG;
                sfx.PlayOneShot(sGood);
                LiveScoreManager.Instance.AddScore(goodPoints, "GOOD");
                break;
            case HitResult.Early:
            case HitResult.Late:
                combo = 0; currentFeverEnergy = 0;
                score += 20; scoreScale = 1.1f;
                lbl = r == HitResult.Early ? "EARLY" : "LATE"; col = CO;
                sfx.PlayOneShot(sGood, 0.8f);
                LiveScoreManager.Instance.AddScore(20, lbl);
                break;
            default:
                combo = 0; currentFeverEnergy = 0;
                hp = Mathf.Max(0f, hp - 10f);
                lbl = "MISS"; col = CM;
                sfx.PlayOneShot(sMiss);
                CameraShake.Instance?.Shake(.2f, .1f);
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
        feverTxt.text = "OVERDRIVE"; feverTxt.color = GetHDR(Color.white);
        feverFillImg.color = GetHDR(MAGENTA); comboTxt.fontSize = 85;
        CameraShake.Instance?.Shake(0.3f, 0.15f);
        StartCoroutine(FeverDurationRoutine());
        if (diamondRainbowCR != null) StopCoroutine(diamondRainbowCR);
        diamondRainbowCR = StartCoroutine(DiamondRainbowLoop());
    }

    IEnumerator DiamondRainbowLoop()
    {
        float hue = 0f;
        while (IsFeverActive)
        {
            hue = Mathf.Repeat(hue + Time.deltaTime * 1.5f, 1f);
            if (hudDiamondOutline != null)
            { Color rc = Color.HSVToRGB(hue,1f,1f); rc.a=1f; hudDiamondOutline.effectColor=rc; }
            if (hudDiamondGlowImg != null)
            { Color gc = Color.HSVToRGB(Mathf.Repeat(hue+0.15f,1f),0.8f,1f);
              gc.a = 0.18f + 0.12f * Mathf.Sin(Time.time*4f);
              hudDiamondGlowImg.color = gc; }
            yield return null;
        }
        if (hudDiamondOutline != null) hudDiamondOutline.effectColor = new Color(CYAN.r,CYAN.g,CYAN.b,0.65f);
        if (hudDiamondGlowImg != null) hudDiamondGlowImg.color = new Color(1f,1f,1f,0.10f);
    }

    IEnumerator FeverDurationRoutine()
    {
        float warningDuration = 2f;
        yield return new WaitForSeconds(Mathf.Max(0f, feverDuration - warningDuration));
        if (IsFeverActive && TrackController.Instance != null)
            yield return StartCoroutine(TrackController.Instance.FlashWarningRoutine(warningDuration));
        if (IsFeverActive) DeactivateFever();
    }

    void DeactivateFever()
    {
        IsFeverActive = false; currentFeverEnergy = 0;
        TileSpawner.Instance.spawnInterval = baseSpawnInterval;
        TileSpawner.Instance.tileSpeed = baseTileSpeed;
        TileSpawner.Instance.UpdateActiveTileSpeeds(baseTileSpeed);
        TrackController.Instance.rotationInterval = baseTrackRot;
        feverTxt.text = "FEVER"; feverTxt.color = new Color(1f, 1f, 1f, 0.7f);
        feverFillImg.color = GetHDR(CYAN); comboTxt.fontSize = 72;
        CameraShake.Instance?.Shake(0.2f, 0.1f);
        if (diamondRainbowCR != null) { StopCoroutine(diamondRainbowCR); diamondRainbowCR = null; }
        if (hudDiamondOutline != null) hudDiamondOutline.effectColor = new Color(CYAN.r,CYAN.g,CYAN.b,0.65f);
        if (hudDiamondGlowImg != null) hudDiamondGlowImg.color = new Color(1f,1f,1f,0.10f);
    }

    public void EndLevel(bool cleared = false)
    {
        alive = false;
        if (music.isPlaying) music.Stop();
        TileSpawner.Instance.StopSpawning();
        TrackController.Instance.StopRotating();
        float acc = total > 0 ? (float)perfectHits / total * 100f : 0f;
        goScore.text = score.ToString("N0"); goAcc.text = $"{acc:F1}%"; goCombo.text = "x" + maxCombo;
        if (cleared) { goTitle.text = "TRACK CLEARED"; goTitle.color = GetHDR(CG); sfx.PlayOneShot(sPerfect); }
        else { goTitle.text = "GAME OVER"; goTitle.color = GetHDR(new Color(1f, .15f, .25f)); }
        goPanel.SetActive(true);
        LeaderboardManager.Instance.TrySubmit(currentMode.ToString(), score, maxCombo, acc);
        if (liveScoringCard != null)
        {
            var cg = liveScoringCard.GetComponent<CanvasGroup>();
            if (cg != null) StartCoroutine(FadeOutLiveCard(cg));
        }
    }

    void TogglePause()
    {
        paused = !paused; Time.timeScale = paused ? 0f : 1f;
        if (paused) { if (music.isPlaying) music.Pause(); }
        else { music.UnPause(); dspStartTime = AudioSettings.dspTime - music.time; }
        pausePanel.SetActive(paused);
    }

    public void Restart()  { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    public void MainMenu() { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
    public bool IsGameActive() => alive;

    public float GetMusicTime()
    {
        if (music != null && music.clip != null && music.isPlaying && !paused)
            return (float)(AudioSettings.dspTime - dspStartTime);
        else if (music != null && paused) return music.time;
        return 0f;
    }

    public void ApplySpamPenalty()
    {
        if (!alive || IsFeverActive) return;
        combo = 0; currentFeverEnergy = 0; hp = Mathf.Max(0f, hp - 3f);
        sfx.PlayOneShot(sMiss); CameraShake.Instance?.Shake(.1f, .05f);
        ShowResult("SPAM!", CM); if (hp <= 0f) EndLevel(false); RefreshHUD();
    }

    void RefreshHUD()
    {
        displayScore = Mathf.Lerp(displayScore, score, Time.deltaTime * 15f);
        if (Mathf.Abs(score - displayScore) < 0.5f) displayScore = score;
        scoreScale = Mathf.Lerp(scoreScale, 1f, Time.deltaTime * 10f);

        float acc = total > 0 ? (float)perfectHits / total * 100f : 100f;
        // accTxt permanently hidden
        hpTxt.text    = "\u2665 " + (int)hp;
        comboTxt.text = combo > 1 ? "x" + combo : "";
        if (hudScoreTxt != null) hudScoreTxt.text = ((int)displayScore).ToString("N0");
        if (hudHpTxt    != null) hudHpTxt.text    = ((int)hp).ToString();

        if (combo > 1)
            comboTxt.color = IsFeverActive
                ? GetHDR(GetFeverRGB())
                : Color.Lerp(CYAN, MAGENTA, Mathf.Sin(Time.time * 7f) * .5f + .5f);

        if (feverFillRT != null && feverEnergyTxt != null)
        {
            if (IsFeverActive)
            {
                feverFillRT.localScale = Vector3.one;
                feverFillImg.color     = GetHDR(GetFeverRGB());
                feverEnergyTxt.text    = "MAX";
            }
            else
            {
                float fill = Mathf.Clamp01((float)currentFeverEnergy / feverEnergyThreshold);
                Color tc = fill < 0.5f
                    ? Color.Lerp(CYAN, CP, fill * 2f)
                    : Color.Lerp(CP, CM, (fill - 0.5f) * 2f);
                feverFillImg.color     = GetHDR(tc);
                feverEnergyTxt.text    = $"{currentFeverEnergy} / {feverEnergyThreshold}";
                feverFillRT.localScale = new Vector3(1f, fill, 1f);
            }
        }
    }

    // =========================================================================
    // LIVE SCORING CARD — top-left corner
    // =========================================================================

    void BuildLiveCard(GameObject canvasGo)
    {
        liveScoringCard = new GameObject("LiveScoringCard");
        liveScoringCard.transform.SetParent(canvasGo.transform, false);

        var cardRT = liveScoringCard.AddComponent<RectTransform>();
        cardRT.anchorMin        = new Vector2(0f, 1f);
        cardRT.anchorMax        = new Vector2(0f, 1f);
        cardRT.pivot            = new Vector2(0f, 1f);
        cardRT.anchoredPosition = new Vector2(20f, -20f);
        cardRT.sizeDelta        = new Vector2(220f, 150f);

        var cg   = liveScoringCard.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Dark background
        liveScoringCard.AddComponent<Image>().color = CARD_BG;

        // Cyan border
        var border = liveScoringCard.AddComponent<Outline>();
        border.effectColor    = new Color(CARD_CYAN.r, CARD_CYAN.g, CARD_CYAN.b, 0.70f);
        border.effectDistance = new Vector2(2f, -2f);

        // Top cyan glow strip
        var stripGo = new GameObject("GlowStrip");
        stripGo.transform.SetParent(liveScoringCard.transform, false);
        var sRT = stripGo.AddComponent<RectTransform>();
        sRT.anchorMin        = new Vector2(0f, 1f);
        sRT.anchorMax        = new Vector2(1f, 1f);
        sRT.pivot            = new Vector2(0.5f, 1f);
        sRT.anchoredPosition = Vector2.zero;
        sRT.sizeDelta        = new Vector2(0f, 2f);
        stripGo.AddComponent<Image>().color = new Color(CARD_CYAN.r, CARD_CYAN.g, CARD_CYAN.b, 0.6f);

        // ── HEADER: icon + "LIVE SCORING" ─────────────────────────────────────
        // Uses your assigned sprite if set, otherwise falls back to code-drawn bars
        if (liveScoringIcon != null)
        {
            // Custom sprite icon
            var iconGo = new GameObject("LiveIcon");
            iconGo.transform.SetParent(liveScoringCard.transform, false);
            var iconRT = iconGo.AddComponent<RectTransform>();
            iconRT.anchorMin        = new Vector2(0f, 1f);
            iconRT.anchorMax        = new Vector2(0f, 1f);
            iconRT.pivot            = new Vector2(0f, 1f);
            iconRT.anchoredPosition = new Vector2(8f, -7f);
            iconRT.sizeDelta        = new Vector2(18f, 18f);
            var iconImg             = iconGo.AddComponent<Image>();
            iconImg.sprite          = liveScoringIcon;
            iconImg.color           = CARD_CYAN;
            iconImg.preserveAspect  = true;
            iconImg.raycastTarget   = false;
        }
        else
        {
            // Fallback: three code-drawn bars
            BuildBarChartIcon(liveScoringCard, new Vector2(20f, -16f));
        }

        // "LIVE SCORING" label — sits right of the icon
        var headerGo = new GameObject("HeaderTxt");
        headerGo.transform.SetParent(liveScoringCard.transform, false);
        var hRT = headerGo.AddComponent<RectTransform>();
        hRT.anchorMin        = new Vector2(0f, 1f);
        hRT.anchorMax        = new Vector2(1f, 1f);
        hRT.pivot            = new Vector2(0f, 1f);
        hRT.anchoredPosition = new Vector2(32f, -8f);
        hRT.sizeDelta        = new Vector2(-36f, 20f);
        var hTMP = headerGo.AddComponent<TextMeshProUGUI>();
        hTMP.text             = "LIVE SCORING";
        hTMP.fontSize         = 11f;
        hTMP.fontStyle        = FontStyles.Bold;
        hTMP.color            = CARD_CYAN;
        hTMP.alignment        = TextAlignmentOptions.Left;
        hTMP.characterSpacing = 3f;
        hTMP.raycastTarget    = false;

        // Divider under header
        var divGo = new GameObject("Divider");
        divGo.transform.SetParent(liveScoringCard.transform, false);
        var dRT = divGo.AddComponent<RectTransform>();
        dRT.anchorMin        = new Vector2(0f, 1f);
        dRT.anchorMax        = new Vector2(1f, 1f);
        dRT.pivot            = new Vector2(0.5f, 1f);
        dRT.anchoredPosition = new Vector2(0f, -30f);
        dRT.sizeDelta        = new Vector2(-14f, 1f);
        divGo.AddComponent<Image>().color =
            new Color(CARD_CYAN.r, CARD_CYAN.g, CARD_CYAN.b, 0.22f);

        // ── SCORE ROW ─────────────────────────────────────────────────────────
        BuildCardRow(liveScoringCard, "SCORE", -30f, 28f,
                     GetHDR(CARD_CYAN), out liveScoreValueTxt);

        // Mid divider
        var midDiv = new GameObject("MidDiv");
        midDiv.transform.SetParent(liveScoringCard.transform, false);
        var mdRT = midDiv.AddComponent<RectTransform>();
        mdRT.anchorMin        = new Vector2(0f, 1f);
        mdRT.anchorMax        = new Vector2(1f, 1f);
        mdRT.pivot            = new Vector2(0.5f, 1f);
        mdRT.anchoredPosition = new Vector2(0f, -90f);
        mdRT.sizeDelta        = new Vector2(-14f, 1f);
        midDiv.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

        // ── RANK ROW ──────────────────────────────────────────────────────────
        BuildCardRow(liveScoringCard, "RANK", -94f, 28f,
                     GetHDR(CARD_CYAN), out liveRankValueTxt);

        // Seed initial values
        liveScoreValueTxt.text = "0";
        liveRankValueTxt.text  = "—";
    }

    void BuildCardRow(GameObject parent, string labelText,
                      float yFromTop, float valueFontSize, Color valueColor,
                      out TextMeshProUGUI valueTxt)
    {
        float rowH = valueFontSize + 16f;

        // Label — left side
        var lGo = new GameObject("Lbl_" + labelText);
        lGo.transform.SetParent(parent.transform, false);
        var lRT = lGo.AddComponent<RectTransform>();
        lRT.anchorMin        = new Vector2(0f, 1f);
        lRT.anchorMax        = new Vector2(0.45f, 1f);
        lRT.pivot            = new Vector2(0f, 1f);
        lRT.anchoredPosition = new Vector2(14f, yFromTop);
        lRT.sizeDelta        = new Vector2(0f, rowH);
        var lTMP = lGo.AddComponent<TextMeshProUGUI>();
        lTMP.text             = labelText;
        lTMP.fontSize         = 10f;
        lTMP.fontStyle        = FontStyles.Bold;
        lTMP.color            = new Color(CARD_CYAN.r * 0.75f, CARD_CYAN.g * 0.75f, CARD_CYAN.b * 0.75f, 1f);
        lTMP.alignment        = TextAlignmentOptions.BottomLeft;
        lTMP.characterSpacing = 2f;
        lTMP.raycastTarget    = false;

        // Value — right side, full card width so it never clips
        var vGo = new GameObject("Val_" + labelText);
        vGo.transform.SetParent(parent.transform, false);
        var vRT = vGo.AddComponent<RectTransform>();
        vRT.anchorMin        = new Vector2(0f, 1f);
        vRT.anchorMax        = new Vector2(1f, 1f);
        vRT.pivot            = new Vector2(1f, 1f);
        vRT.anchoredPosition = new Vector2(-14f, yFromTop);
        vRT.sizeDelta        = new Vector2(-14f, rowH);
        valueTxt = vGo.AddComponent<TextMeshProUGUI>();
        valueTxt.text          = "—";
        valueTxt.fontSize      = valueFontSize;
        valueTxt.fontStyle     = FontStyles.Bold;
        valueTxt.color         = valueColor;
        valueTxt.alignment     = TextAlignmentOptions.BottomRight;
        valueTxt.overflowMode  = TextOverflowModes.Overflow;
        valueTxt.raycastTarget = false;
    }

    // Fallback: three ascending code-drawn bars
    void BuildBarChartIcon(GameObject parent, Vector2 anchoredPos)
    {
        float[] heights = { 7f, 11f, 16f };
        float[] xOff    = { -7f, 0f, 7f };
        for (int i = 0; i < 3; i++)
        {
            var bar = new GameObject("IconBar_" + i);
            bar.transform.SetParent(parent.transform, false);
            var bRT = bar.AddComponent<RectTransform>();
            bRT.anchorMin        = new Vector2(0f, 1f);
            bRT.anchorMax        = new Vector2(0f, 1f);
            bRT.pivot            = new Vector2(0.5f, 1f);
            bRT.anchoredPosition = new Vector2(anchoredPos.x + xOff[i], anchoredPos.y);
            bRT.sizeDelta        = new Vector2(4f, heights[i]);
            bar.AddComponent<Image>().color =
                new Color(CARD_CYAN.r, CARD_CYAN.g, CARD_CYAN.b, 0.9f);
        }
    }

    // ── Live card polling ─────────────────────────────────────────────────────
    void RefreshLiveCard()
    {
        if (liveScoreValueTxt == null || liveRankValueTxt == null) return;
        long liveScore = LiveScoreManager.Instance.PlayerScore;
        int  liveRank  = LiveScoreManager.Instance.PlayerRank;

        if (liveScore != lastPolledScore)
        {
            lastPolledScore = liveScore;
            if (scoreCountCo != null) StopCoroutine(scoreCountCo);
            scoreCountCo = StartCoroutine(CountScoreAnim(liveDisplayedScore, liveScore, 0.3f));
        }
        if (liveRank != lastPolledRank)
        {
            lastPolledRank = liveRank;
            if (rankFlashCo != null) StopCoroutine(rankFlashCo);
            rankFlashCo = StartCoroutine(FlashRankAnim(liveRank));
        }
    }

    IEnumerator CountScoreAnim(long from, long to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            liveDisplayedScore     = (long)Mathf.Lerp(from, to, t);
            liveScoreValueTxt.text = liveDisplayedScore.ToString("N0");
            yield return null;
        }
        liveDisplayedScore     = to;
        liveScoreValueTxt.text = to.ToString("N0");
        yield return ScalePopAnim(liveScoreValueTxt.rectTransform, 1.08f, 0.10f);
    }

    IEnumerator FlashRankAnim(int newRank)
    {
        string suffix = newRank switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        liveRankValueTxt.text = newRank > 0 ? newRank + suffix : "—";
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.deltaTime;
            liveRankValueTxt.color = Color.Lerp(Color.white, GetHDR(CARD_CYAN), t / 0.18f);
            yield return null;
        }
        liveRankValueTxt.color = GetHDR(CARD_CYAN);
        yield return ScalePopAnim(liveRankValueTxt.rectTransform, 1.12f, 0.12f);
    }

    IEnumerator ScalePopAnim(RectTransform rt, float peak, float halfDur)
    {
        float t = 0f;
        while (t < halfDur) { t += Time.deltaTime; rt.localScale = Vector3.one * Mathf.Lerp(1f, peak, t / halfDur); yield return null; }
        t = 0f;
        while (t < halfDur) { t += Time.deltaTime; rt.localScale = Vector3.one * Mathf.Lerp(peak, 1f, t / halfDur); yield return null; }
        rt.localScale = Vector3.one;
    }

    IEnumerator FadeInLiveCard()
    {
        yield return new WaitForSeconds(0.4f);
        if (liveScoringCard == null) yield break;
        var cg = liveScoringCard.GetComponent<CanvasGroup>();
        float t = 0f;
        while (t < 0.6f) { t += Time.deltaTime; cg.alpha = Mathf.SmoothStep(0f, 1f, t / 0.6f); yield return null; }
        cg.alpha = 1f;
    }

    IEnumerator FadeOutLiveCard(CanvasGroup cg)
    {
        float t = 0f;
        while (t < 0.4f) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(1f, 0f, t / 0.4f); yield return null; }
        cg.alpha = 0f;
        liveScoringCard.SetActive(false);
    }

    // =========================================================================
    void ShowResult(string lbl, Color col)
    {
        if (resultCo != null) StopCoroutine(resultCo);
        resultCo = StartCoroutine(ResultAnim(lbl, col));
    }

    IEnumerator ResultAnim(string lbl, Color col)
    {
        resultTxt.text = lbl; resultTxt.transform.localScale = Vector3.one * 1.4f;
        float t = 0f;
        while (t < 0.55f)
        {
            t += Time.deltaTime;
            resultTxt.color = new Color(col.r, col.g, col.b, 1f - t / 0.55f);
            resultTxt.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Min(t / .18f, 1f));
            yield return null;
        }
        resultTxt.text = "";
    }

    // =========================================================================
    void BuildUI()
    {
        var cgo = new GameObject("_Canvas");
        var cv  = cgo.AddComponent<Canvas>();
        cv.renderMode    = RenderMode.ScreenSpaceCamera;
        cv.worldCamera   = Camera.main;
        cv.planeDistance = 5f;
        cv.sortingOrder  = 20;

        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight  = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        // HP - top-centre (shown in Easy/Medium/Hard, hidden in Endless by ApplyMode)
        hpTxt = T(cgo, "\u2665 100", 28, new Vector2(0, -40), A(.5f, 1), A(.5f, 1), TextAlignmentOptions.Top);
        hpTxt.rectTransform.pivot = new Vector2(0.5f, 1);
        hpTxt.color = new Color(1f, .35f, .55f);

        // Accuracy - permanently hidden
        accTxt = T(cgo, "100.0%", 26, new Vector2(-40, -40), A(1, 1), A(1, 1), TextAlignmentOptions.TopRight);
        accTxt.rectTransform.pivot = new Vector2(1, 1);
        accTxt.color = CYAN;
        accTxt.gameObject.SetActive(false);

        // Combo - centre screen for Easy/Medium/Hard. Saved as origComboTxt.
        // comboTxt will be reassigned to the Endless right-panel combo below.
        comboTxt = T(cgo, "", 72, new Vector2(0, 100), A(0.5f, 0.5f), A(0.5f, 0.5f), TextAlignmentOptions.Center);
        comboTxt.color = CYAN; comboTxt.fontStyle = FontStyles.Bold;
        origComboTxt = comboTxt; // save BEFORE reassignment below

        // ===== RIGHT-SIDE HUD PANEL (Endless mode only) ======================
        // Visibility is controlled by ApplyMode — hidden for Easy/Medium/Hard
        feverUIContainer = new GameObject("FeverUIContainer");
        feverUIContainer.transform.SetParent(cgo.transform, false);
        var fcRT2 = feverUIContainer.AddComponent<RectTransform>();
        fcRT2.anchorMin = Vector2.zero; fcRT2.anchorMax = Vector2.one;
        fcRT2.offsetMin = fcRT2.offsetMax = Vector2.zero;

        var R = new GameObject("HUD_Root");
        R.transform.SetParent(feverUIContainer.transform, false);
        var rRT = R.AddComponent<RectTransform>();
        rRT.anchorMin = rRT.anchorMax = A(1f,1f);
        rRT.pivot = A(1f,1f);
        rRT.anchoredPosition = new Vector2(-8f,-8f);
        rRT.sizeDelta = new Vector2(140f,700f);

        System.Func<string,float,float,float,GameObject> MkRow = (nm,yTop,w,h) => {
            var g = new GameObject(nm); g.transform.SetParent(R.transform,false);
            var r = g.AddComponent<RectTransform>();
            r.anchorMin = r.anchorMax = A(0.5f,1f); r.pivot = A(0.5f,1f);
            r.anchoredPosition = new Vector2(0f,-yTop); r.sizeDelta = new Vector2(w,h);
            return g;
        };

        { var t = MkRow("HUD_ScoreLbl",5f,140f,20f).AddComponent<TextMeshProUGUI>();
          t.text="SCORE"; t.fontSize=11f; t.color=CYAN;
          t.fontStyle=FontStyles.Bold; t.alignment=TextAlignmentOptions.Center;
          t.characterSpacing=6f; t.raycastTarget=false; }

        { var go = MkRow("HUD_ScoreVal",25f,140f,52f);
          hudScoreTxt = go.AddComponent<TextMeshProUGUI>();
          hudScoreTxt.text="0"; hudScoreTxt.fontSize=36f;
          hudScoreTxt.color=Color.white; hudScoreTxt.fontStyle=FontStyles.Bold;
          hudScoreTxt.alignment=TextAlignmentOptions.Center;
          hudScoreTxt.enableAutoSizing=true;
          hudScoreTxt.fontSizeMin=16f; hudScoreTxt.fontSizeMax=36f;
          hudScoreTxt.raycastTarget=false; }

        { var panel = MkRow("HUD_HPPanel",82f,122f,38f);
          panel.AddComponent<Image>().color=new Color(0.07f,0.08f,0.12f,0.88f);
          var hGo=new GameObject("H"); hGo.transform.SetParent(panel.transform,false);
          var hRT=hGo.AddComponent<RectTransform>();
          hRT.anchorMin=hRT.anchorMax=A(0.5f,0.5f);
          hRT.anchoredPosition=new Vector2(-23f,0f); hRT.sizeDelta=new Vector2(30f,30f);
          var ht=hGo.AddComponent<TextMeshProUGUI>();
          ht.text="\u2665"; ht.fontSize=20f; ht.color=new Color(1f,0.22f,0.62f,1f);
          ht.alignment=TextAlignmentOptions.Center; ht.raycastTarget=false;
          var nGo=new GameObject("N"); nGo.transform.SetParent(panel.transform,false);
          var nRT=nGo.AddComponent<RectTransform>();
          nRT.anchorMin=nRT.anchorMax=A(0.5f,0.5f);
          nRT.anchoredPosition=new Vector2(14f,0f); nRT.sizeDelta=new Vector2(68f,30f);
          hudHpTxt=nGo.AddComponent<TextMeshProUGUI>();
          hudHpTxt.text="100"; hudHpTxt.fontSize=22f; hudHpTxt.color=Color.white;
          hudHpTxt.fontStyle=FontStyles.Bold;
          hudHpTxt.alignment=TextAlignmentOptions.Left; hudHpTxt.raycastTarget=false; }

        MkRow("HUD_Sep",124f,122f,3f).AddComponent<Image>().color=MAGENTA;

        { var dP = MkRow("HUD_DiamondParent",125f,80f,80f);
          var gGo=new GameObject("G"); gGo.transform.SetParent(dP.transform,false);
          gGo.AddComponent<RectTransform>().sizeDelta=new Vector2(90f,90f);
          gGo.GetComponent<RectTransform>().anchorMin=
          gGo.GetComponent<RectTransform>().anchorMax=A(0.5f,0.5f);
          hudDiamondGlowImg=gGo.AddComponent<Image>();
          hudDiamondGlowImg.color=new Color(1f,1f,1f,0.10f);
          hudDiamondGlowImg.raycastTarget=false;
          gGo.transform.localEulerAngles=new Vector3(0f,0f,45f);
          var bGo=new GameObject("B"); bGo.transform.SetParent(dP.transform,false);
          bGo.AddComponent<RectTransform>().sizeDelta=new Vector2(56f,56f);
          bGo.GetComponent<RectTransform>().anchorMin=
          bGo.GetComponent<RectTransform>().anchorMax=A(0.5f,0.5f);
          var bImg=bGo.AddComponent<Image>();
          bImg.color=new Color(0.13f,0.15f,0.19f,1f);
          AddBorder(bGo,CYAN,0.65f);
          hudDiamondOutline=bGo.GetComponent<Outline>();
          bGo.transform.localEulerAngles=new Vector3(0f,0f,45f);
          var lGo=new GameObject("L"); lGo.transform.SetParent(dP.transform,false);
          lGo.AddComponent<RectTransform>().sizeDelta=new Vector2(275f,275f);
          lGo.GetComponent<RectTransform>().anchorMin=
          lGo.GetComponent<RectTransform>().anchorMax=A(0.5f,0.5f);
          if (lightningSprite!=null) {
              var li=lGo.AddComponent<Image>();
              li.sprite=lightningSprite; li.preserveAspect=true; li.raycastTarget=false;
              bImg.color=Color.clear;
              var ol=bGo.GetComponent<Outline>(); if(ol) ol.effectColor=Color.clear;
              hudDiamondGlowImg.color=new Color(1f,1f,1f,0.18f);
          } else {
              var lt=lGo.AddComponent<TextMeshProUGUI>();
              lt.text="\u26A1"; lt.fontSize=28f; lt.color=CYAN;
              lt.alignment=TextAlignmentOptions.Center; lt.raycastTarget=false;
          } }

        { var bc = MkRow("HUD_BarCont",208f,52f,380f);
          var bd=new GameObject("BD"); bd.transform.SetParent(bc.transform,false);
          var bdRT=bd.AddComponent<RectTransform>();
          bdRT.anchorMin=Vector2.zero; bdRT.anchorMax=Vector2.one;
          bdRT.offsetMin=bdRT.offsetMax=Vector2.zero;
          bd.AddComponent<Image>().color=new Color(CYAN.r,CYAN.g,CYAN.b,0.18f);
          var ff=new GameObject("FeverFill"); ff.transform.SetParent(bc.transform,false);
          feverFillRT=ff.AddComponent<RectTransform>();
          feverFillRT.anchorMin=new Vector2(0.08f,0f);
          feverFillRT.anchorMax=new Vector2(0.92f,1f);
          feverFillRT.offsetMin=feverFillRT.offsetMax=Vector2.zero;
          feverFillRT.pivot=new Vector2(0.5f,0f);
          feverFillImg=ff.AddComponent<Image>();
          feverFillImg.color=GetHDR(CYAN);
          feverFillRT.localScale=new Vector3(1f,0f,1f);
          for(int si=0;si<16;si++) {
              float yF=(float)(si+1)/17f;
              var sg=new GameObject("S"+si); sg.transform.SetParent(bc.transform,false);
              var sgRT=sg.AddComponent<RectTransform>();
              sgRT.anchorMin=new Vector2(0.08f,yF); sgRT.anchorMax=new Vector2(0.92f,yF);
              sgRT.sizeDelta=new Vector2(0f,2f); sgRT.anchoredPosition=Vector2.zero;
              sg.AddComponent<Image>().color=new Color(0f,0f,0f,0.40f);
          } }

        { var comboGroup = new GameObject("HUD_ComboGroup");
          comboGroup.transform.SetParent(R.transform,false);
          var cgRT = comboGroup.AddComponent<RectTransform>();
          cgRT.anchorMin = cgRT.anchorMax = A(0.5f,0f);
          cgRT.pivot = A(0.5f,0f);
          cgRT.anchoredPosition = new Vector2(0f,10f);
          cgRT.sizeDelta = new Vector2(130f,80f);
          var numGo = new GameObject("ComboNum");
          numGo.transform.SetParent(comboGroup.transform,false);
          var numRT = numGo.AddComponent<RectTransform>();
          numRT.anchorMin = new Vector2(0f,0.35f);
          numRT.anchorMax = Vector2.one;
          numRT.offsetMin = numRT.offsetMax = Vector2.zero;
          comboTxt = numGo.AddComponent<TextMeshProUGUI>();
          comboTxt.text=""; comboTxt.color=Color.white;
          comboTxt.fontStyle=FontStyles.Bold;
          comboTxt.alignment=TextAlignmentOptions.Center;
          comboTxt.enableAutoSizing=true;
          comboTxt.fontSizeMin=22f; comboTxt.fontSizeMax=52f;
          comboTxt.raycastTarget=false;
          numGo.SetActive(true);
          var lblGo = new GameObject("ComboLbl");
          lblGo.transform.SetParent(comboGroup.transform,false);
          var lblRT = lblGo.AddComponent<RectTransform>();
          lblRT.anchorMin = Vector2.zero;
          lblRT.anchorMax = new Vector2(1f,0.35f);
          lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
          var lblT = lblGo.AddComponent<TextMeshProUGUI>();
          lblT.text="COMBO"; lblT.fontSize=11f;
          lblT.color=new Color(CYAN.r,CYAN.g,CYAN.b,0.80f);
          lblT.fontStyle=FontStyles.Bold;
          lblT.alignment=TextAlignmentOptions.Center;
          lblT.characterSpacing=4f; lblT.raycastTarget=false; }

        { var go = MkRow("HUD_FeverEnergy",598f,140f,22f);
          feverEnergyTxt=go.AddComponent<TextMeshProUGUI>();
          feverEnergyTxt.text="0 / 50"; feverEnergyTxt.fontSize=11f;
          feverEnergyTxt.color=new Color(0.60f,0.78f,0.88f,0.60f);
          feverEnergyTxt.alignment=TextAlignmentOptions.Center;
          feverEnergyTxt.raycastTarget=false; }

        { var go=new GameObject("HUD_FeverHidden");
          go.transform.SetParent(feverUIContainer.transform,false);
          var rt=go.AddComponent<RectTransform>();
          rt.anchorMin=rt.anchorMax=A(0f,0f);
          rt.anchoredPosition=new Vector2(-9999f,-9999f); rt.sizeDelta=new Vector2(1f,1f);
          feverTxt=go.AddComponent<TextMeshProUGUI>();
          feverTxt.text="FEVER"; feverTxt.color=Color.clear; }

        // Result flash
        resultTxt = T(cgo, "", 44, new Vector2(0, 10), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        resultTxt.fontStyle = FontStyles.Bold;

        // Bottom-left labels
        var modeLbl = T(cgo, "NEON SHIFT", 14, new Vector2(16, 50), A(0, 0), A(0, 0), TextAlignmentOptions.BottomLeft);
        modeLbl.color = GetHDR(CYAN); modeLbl.fontStyle = FontStyles.Bold;
        T(cgo, "DIFFICULTY: " + currentMode.ToString().ToUpper(), 11, new Vector2(16, 34), A(0, 0), A(0, 0), TextAlignmentOptions.BottomLeft)
            .color = new Color(.6f, .7f, .8f, .7f);
        T(cgo, "ESC = PAUSE", 18, new Vector2(-16, 16), A(1, 0), A(1, 0), TextAlignmentOptions.BottomRight)
            .color = new Color(.4f, .7f, 1f, .3f);

        // ── LIVE SCORING CARD ─────────────────────────────────────────────────
        BuildLiveCard(cgo);

        // Game Over panel
        goPanel = Panel(cgo, new Color(.02f, .02f, .05f, .95f));
        goTitle = T(goPanel, "GAME OVER", 88, new Vector2(0, 200), A(.5f, .5f), A(.5f, .5f), TextAlignmentOptions.Center);
        goTitle.color = GetHDR(new Color(1f, .15f, .25f)); goTitle.fontStyle = FontStyles.Bold | FontStyles.Italic;

        var statsBox = new GameObject("StatsBox"); statsBox.transform.SetParent(goPanel.transform, false);
        var sBoxRt = statsBox.AddComponent<RectTransform>(); sBoxRt.anchorMin = sBoxRt.anchorMax = A(0.5f, 0.5f);
        sBoxRt.anchoredPosition = new Vector2(0, 30); sBoxRt.sizeDelta = new Vector2(750, 140);
        statsBox.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.9f);
        NeonLine(statsBox, new Vector2(-375, 70),  new Vector2(375,   70), GetHDR(CYAN), 1f);
        NeonLine(statsBox, new Vector2(-375, -70), new Vector2(375,  -70), GetHDR(CYAN), 1f);
        NeonLine(statsBox, new Vector2(-125, 50),  new Vector2(-125, -50), GetHDR(CYAN), 0.5f);
        NeonLine(statsBox, new Vector2(125,  50),  new Vector2(125,  -50), GetHDR(CYAN), 0.5f);

        var sLbl = T(statsBox, "SCORE",     18, new Vector2(-250, 25),  A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center); sLbl.color = Color.white; sLbl.fontStyle = FontStyles.Bold;
        goScore  = T(statsBox, "0",         56, new Vector2(-250, -20), A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center); goScore.color = GetHDR(CP); goScore.fontStyle = FontStyles.Bold;
        var aLbl = T(statsBox, "ACCURACY",  18, new Vector2(0, 25),     A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center); aLbl.color = Color.white; aLbl.fontStyle = FontStyles.Bold;
        goAcc    = T(statsBox, "0.0%",      56, new Vector2(0, -20),    A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center); goAcc.color = GetHDR(CG); goAcc.fontStyle = FontStyles.Bold;
        var cLbl = T(statsBox, "MAX COMBO", 18, new Vector2(250, 25),   A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center); cLbl.color = Color.white; cLbl.fontStyle = FontStyles.Bold;
        goCombo  = T(statsBox, "x0",        56, new Vector2(250, -20),  A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center); goCombo.color = GetHDR(CYAN); goCombo.fontStyle = FontStyles.Bold;

        NeonBtn(goPanel, "PLAY AGAIN", CYAN,                      new Vector2(-160, -95), () => Restart());
        NeonBtn(goPanel, "MENU",       new Color(1f, 0.2f, 0.4f), new Vector2(160,  -95), () => MainMenu());
        goPanel.SetActive(false);

        // Pause panel
        pausePanel = Panel(cgo, new Color(.01f, .02f, .08f, .93f));
        NeonLine(pausePanel, new Vector2(-640, 358), new Vector2(640, 358), GetHDR(CYAN), 1f);
        var pauseTitle = T(pausePanel, "PAUSED", 90, new Vector2(0, 210), A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center);
        pauseTitle.color = GetHDR(CYAN); pauseTitle.fontStyle = FontStyles.Bold;
        T(pausePanel, "PRESS ESC TO RESUME", 18, new Vector2(0, 140), A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center)
            .color = new Color(.5f, .7f, .9f, .7f);
        PauseBtn(pausePanel, "RESUME TRACK",  CYAN,                     new Vector2(0,  60),  () => TogglePause());
        PauseBtn(pausePanel, "RESTART TRACK", new Color(.1f, 1f, .4f),  new Vector2(0, -20),  () => Restart());
        PauseBtn(pausePanel, "QUIT TO MENU",  new Color(.4f, .5f, .6f), new Vector2(0, -100), () => MainMenu());
        pausePanel.SetActive(false);
    }

    // ── Utility helpers ───────────────────────────────────────────────────────
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
        ov.AddComponent<Image>().color = Color.clear;
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
            float t = (float)i / sr, e = Mathf.Exp(-t * 18f);
            d[i] = noise ? (Random.value * 2f - 1f) * e * .5f : Mathf.Sin(2f * Mathf.PI * freq * t) * e * .7f;
        }
        var c = AudioClip.Create("b", n, 1, sr, false); c.SetData(d, 0); return c;
    }
}