using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public enum GameMode  { Easy, Medium, Hard, Endless }
public enum HitResult { Perfect, Good, Miss }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    [HideInInspector] public GameMode currentMode = GameMode.Easy;

    int   score, combo, maxCombo, total, hits;
    float hp = 100f;
    bool  alive, paused;

    TextMeshProUGUI scoreTxt, comboTxt, accTxt, hpTxt, resultTxt;
    TextMeshProUGUI goScore, goAcc, goCombo;
    GameObject      goPanel, pausePanel;
    Coroutine       resultCo;

    AudioSource sfx;
    AudioClip   sPerfect, sGood, sMiss;

    static readonly Color CP = new Color(1f, .95f, .15f);
    static readonly Color CG = new Color(.25f, 1f, .45f);
    static readonly Color CM = new Color(1f, .2f, .2f);

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        Camera.main.backgroundColor = new Color(.025f, .025f, .09f);
        Camera.main.clearFlags = CameraClearFlags.SolidColor;

        // Kill any stray canvases from old scene setup
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.gameObject != gameObject) Destroy(c.gameObject);

        sfx = gameObject.AddComponent<AudioSource>(); sfx.volume = .55f;
        sPerfect = Beep(880f, .08f);
        sGood    = Beep(660f, .06f);
        sMiss    = Beep(110f, .13f, true);
        BuildUI();
    }

    void Start()
    {
        // Init spawner with mode BEFORE calling BeginSpawning
        TileSpawner.Instance.Init(currentMode);
        ApplyMode();
        alive = true;
        TileSpawner.Instance.BeginSpawning();
        TrackController.Instance.BeginRotating();
    }

    void Update()
    {
        if (!alive) return;
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
        if (currentMode == GameMode.Endless)
        {
            float t = Time.timeSinceLevelLoad;
            TileSpawner.Instance.spawnInterval = Mathf.Max(.38f, 1.1f - t * .005f);
            TileSpawner.Instance.tileSpeed     = Mathf.Min(18f,  5f  + t * .02f);
            TrackController.Instance.rotationInterval = Mathf.Max(3f, 8f - t * .012f);
        }
        RefreshHUD();
    }

    void ApplyMode()
    {
        var ts = TileSpawner.Instance;
        var tc = TrackController.Instance;
        var pc = PlayerController.Instance;
        switch (currentMode)
        {
            case GameMode.Easy:
                ts.spawnInterval=1.3f; ts.tileSpeed=5f;
                tc.rotationInterval=9f; pc.hitZoneDistance=1.4f; break;
            case GameMode.Medium:
                ts.spawnInterval=0.95f; ts.tileSpeed=7f;
                tc.rotationInterval=6f; pc.hitZoneDistance=1.2f; break;
            case GameMode.Hard:
                ts.spawnInterval=0.6f; ts.tileSpeed=10f;
                tc.rotationInterval=4f; pc.hitZoneDistance=1.0f; break;
            case GameMode.Endless:
                ts.spawnInterval=1.3f; ts.tileSpeed=5f;
                tc.rotationInterval=9f; pc.hitZoneDistance=1.3f;
                ts.endlessMode=true; break;
        }
    }

    public void RegisterHit(HitResult r, Vector3 pos)
    {
        total++;
        string lbl; Color col;
        switch (r)
        {
            case HitResult.Perfect:
                hits++; combo++; score += 300 + combo * 12;
                lbl = "PERFECT!"; col = CP; sfx.PlayOneShot(sPerfect); break;
            case HitResult.Good:
                hits++; combo++; score += 100 + combo * 4;
                lbl = "GOOD"; col = CG; sfx.PlayOneShot(sGood); break;
            default:
                combo = 0; hp = Mathf.Max(0f, hp - 10f);
                lbl = "MISS"; col = CM; sfx.PlayOneShot(sMiss);
                CameraShake.Instance?.Shake(.2f, .1f);
                if (hp <= 0f) { GameOver(); return; }
                break;
        }
        if (combo > maxCombo) maxCombo = combo;
        ShowResult(lbl, col);
    }

    void GameOver()
    {
        alive = false;
        TileSpawner.Instance.StopSpawning();
        TrackController.Instance.StopRotating();
        float acc = total > 0 ? (float)hits / total * 100f : 0f;
        goScore.text = "SCORE\n"    + score.ToString("N0");
        goAcc.text   = "ACCURACY\n"+ $"{acc:F1}%";
        goCombo.text = "MAX COMBO\n\u00D7" + maxCombo;
        goPanel.SetActive(true);
        HighScoreManager.Instance?.TrySubmitScore(currentMode, score);
    }

    void TogglePause()
    {
        paused = !paused;
        Time.timeScale = paused ? 0f : 1f;
        pausePanel.SetActive(paused);
    }

    public void Restart()   { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    public void MainMenu()  { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
    public bool IsGameActive() => alive;

    void RefreshHUD()
    {
        scoreTxt.text = score.ToString("N0");
        float acc = total > 0 ? (float)hits / total * 100f : 100f;
        accTxt.text  = $"{acc:F1}%";
        hpTxt.text   = "\u2665 " + hp.ToString("F0");
        comboTxt.text = combo > 1 ? "\u00D7" + combo : "";
        if (combo > 1)
            comboTxt.color = Color.Lerp(Color.white, CP, Mathf.Sin(Time.time * 7f) * .5f + .5f);
    }

    void ShowResult(string lbl, Color col)
    {
        if (resultCo != null) StopCoroutine(resultCo);
        resultCo = StartCoroutine(ResultAnim(lbl, col));
    }

    IEnumerator ResultAnim(string lbl, Color col)
    {
        resultTxt.text = lbl;
        resultTxt.transform.localScale = Vector3.one * 1.5f;
        float t = 0f;
        while (t < 0.65f)
        {
            t += Time.deltaTime;
            resultTxt.color = new Color(col.r, col.g, col.b, 1f - t / 0.65f);
            resultTxt.transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Mathf.Min(t / 0.2f, 1f));
            yield return null;
        }
        resultTxt.text = "";
    }

    // ── BUILD ALL UI IN CODE ──────────────────────────────────────────────
    void BuildUI()
    {
        var cgo = new GameObject("_Canvas");
        var cv  = cgo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 20;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        // Score top-left
        scoreTxt = T(cgo, "0",       60, new Vector2(24,-24),  A(0,1), A(0,1), TextAlignmentOptions.TopLeft);
        scoreTxt.color = new Color(.9f,.95f,1f);

        // Accuracy top-right
        accTxt   = T(cgo, "100.0%",  36, new Vector2(-24,-24), A(1,1), A(1,1), TextAlignmentOptions.TopRight);
        accTxt.color = new Color(.5f,.85f,1f);

        // HP top-centre
        hpTxt    = T(cgo, "\u2665 100", 36, new Vector2(0,-24), A(.5f,1), A(.5f,1), TextAlignmentOptions.Center);
        hpTxt.color = new Color(1f,.35f,.5f);

        // Combo centre
        comboTxt = T(cgo, "", 82, new Vector2(0, 90), A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center);
        comboTxt.color = CP;

        // Hit result centre
        resultTxt = T(cgo, "", 54, new Vector2(0, 0), A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center);

        // Lane colour key labels under the score (D F J K)
        string[] kl = {"D","F","J","K"};
        float[]  kx = {-285f,-95f,95f,285f};
        for (int i = 0; i < 4; i++)
        {
            var kt = T(cgo, kl[i], 28, new Vector2(kx[i], -70), A(.5f,1), A(.5f,1), TextAlignmentOptions.Center);
            Color lc = TrackController.LaneColors[i]; lc.a = 0.55f; kt.color = lc;
        }

        // ESC hint
        T(cgo, "ESC = Pause", 22, new Vector2(-14, 36), A(1,0), A(1,0), TextAlignmentOptions.BottomRight)
            .color = new Color(.4f,.6f,1f,.3f);

        // ── Game Over panel ───────────────────────────────────────────────
        goPanel = Panel(cgo, new Color(0,.02f,.12f,.93f));
        T(goPanel, "GAME OVER", 90, new Vector2(0, 280), A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center)
            .color = new Color(1f,.3f,.3f);
        goScore = T(goPanel, "SCORE\n0",       66, new Vector2(0, 140), A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center);
        goAcc   = T(goPanel, "ACCURACY\n---",  50, new Vector2(0,   0), A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center);
        goCombo = T(goPanel, "MAX COMBO\n---", 50, new Vector2(0,-140), A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center);
        goScore.color = new Color(1f,.9f,.2f);
        goAcc.color   = new Color(.4f,1f,.7f);
        goCombo.color = new Color(.6f,.8f,1f);
        Btn(goPanel, "PLAY AGAIN", new Vector2(-200,-290), () => Restart());
        Btn(goPanel, "MENU",       new Vector2( 200,-290), () => MainMenu());
        goPanel.SetActive(false);

        // ── Pause panel ───────────────────────────────────────────────────
        pausePanel = Panel(cgo, new Color(0,.02f,.08f,.88f));
        T(pausePanel, "PAUSED",        96, Vector2.zero,       A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center)
            .color = new Color(.4f,.85f,1f);
        T(pausePanel, "ESC to resume", 36, new Vector2(0,-110), A(.5f,.5f), A(.5f,.5f), TextAlignmentOptions.Center)
            .color = new Color(.6f,.8f,1f,.8f);
        pausePanel.SetActive(false);
    }

    static Vector2 A(float x, float y) => new Vector2(x, y);

    TextMeshProUGUI T(GameObject p, string txt, int sz, Vector2 pos,
        Vector2 aMin, Vector2 aMax, TextAlignmentOptions al)
    {
        var go = new GameObject("_T");
        go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(700, 110);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = sz; t.alignment = al; t.color = Color.white;
        return t;
    }

    GameObject Panel(GameObject p, Color bg)
    {
        var go = new GameObject("_P");
        go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = bg;
        return go;
    }

    void Btn(GameObject p, string lbl, Vector2 pos, UnityEngine.Events.UnityAction cb)
    {
        var go = new GameObject("_B");
        go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = A(.5f,.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(300, 72);
        var img = go.AddComponent<Image>(); img.color = new Color(.1f,.3f,.6f,.9f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(cb);
        var tgo = new GameObject("L"); tgo.transform.SetParent(go.transform, false);
        var trt = tgo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = lbl; tmp.fontSize = 38; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
    }

    AudioClip Beep(float freq, float dur, bool noise = false)
    {
        int sr = 44100, n = Mathf.RoundToInt(sr * dur);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float e = Mathf.Exp(-t * 18f);
            d[i] = noise ? (Random.value * 2f - 1f) * e * .5f
                         : Mathf.Sin(2f * Mathf.PI * freq * t) * e * .7f;
        }
        var c = AudioClip.Create("b", n, 1, sr, false);
        c.SetData(d, 0); return c;
    }
}
