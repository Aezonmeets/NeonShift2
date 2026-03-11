using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    static readonly Color CYAN    = new Color(0f,.92f,1f);
    static readonly Color MAGENTA = new Color(1f,.15f,.75f);

    // Mode button colours matching Figma left accent bars
    static readonly Color[] ModeBorderColors = {
        new Color(.1f,1f,.4f),    // Easy - green
        new Color(1f,.85f,.1f),   // Medium - yellow  
        new Color(1f,.45f,.1f),   // Hard - orange-red
        new Color(.8f,.1f,1f),    // Endless - purple
    };
    static readonly string[] ModeLabels = { "EASY", "MEDIUM", "HARD", "ENDLESS" };
    static readonly string[] ModeDescs  = { "Chill intro", "Getting spicy", "Full chaos", "Never-ending" };

    AudioSource music;

    void Start()
    {
        Camera.main.backgroundColor = new Color(.02f,.02f,.07f);
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true; music.volume = .55f;
        // Try to play menu music
        var clip = Resources.Load<AudioClip>("Music/Menu");
        if (clip) { music.clip = clip; music.Play(); }
        Build();
    }

    void Build()
    {
        var cgo = new GameObject("Canvas");
        var cv  = cgo.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc  = cgo.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        // ── DARK BACKGROUND ───────────────────────────────────────────────
        var bg = new GameObject("BG"); bg.transform.SetParent(cgo.transform, false);
        var bgRT = bg.AddComponent<RectTransform>(); bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(.02f,.02f,.07f);

        // ── TOP NAV BAR ───────────────────────────────────────────────────
        HBar(cgo, CYAN, A(0,1), A(1,1), 0, -74, 2); // top cyan line
        var logoTxt = T(cgo, "NEON SHIFT", 22, new Vector2(40, -37), A(0,1), A(0,1), TextAlignmentOptions.Left);
        logoTxt.color = CYAN; logoTxt.fontStyle = FontStyles.Bold;

        // Decorative diagonal lines (approximated as coloured lines)
        StartCoroutine(AnimateTitle(cgo));

        // ── TITLE ─────────────────────────────────────────────────────────
        // "NEON SHIFT" big gradient-like title — two-colour via two overlapping texts
        var t1 = T(cgo,"NEON ",90,new Vector2(-95,210),A(.5f,.5f),A(.5f,.5f),TextAlignmentOptions.Right);
        t1.color = MAGENTA; t1.fontStyle = FontStyles.Bold;
        var t2 = T(cgo,"SHIFT",90,new Vector2(95,210),A(.5f,.5f),A(.5f,.5f),TextAlignmentOptions.Left);
        t2.color = CYAN; t2.fontStyle = FontStyles.Bold;

        var sub = T(cgo,"4-LANE RHYTHM GAME",22,new Vector2(0,152),A(.5f,.5f),A(.5f,.5f),TextAlignmentOptions.Center);
        sub.color = CYAN;
        var sub2 = T(cgo,"Tiles rotate! Stay sharp.",16,new Vector2(0,124),A(.5f,.5f),A(.5f,.5f),TextAlignmentOptions.Center);
        sub2.color = new Color(.6f,.7f,.8f,.75f);

        // ── BUTTONS ───────────────────────────────────────────────────────
        float startY = 40f, gap = -70f;
        for (int i = 0; i < 4; i++)
        {
            int mi = i;
            ModeButton(cgo, ModeLabels[i], ModeDescs[i], ModeBorderColors[i],
                new Vector2(0, startY + gap * i), () => { PlayerPrefs.SetInt("SelectedMode", mi); SceneManager.LoadScene("GameScene"); });
        }
        // Quit
        ModeButton(cgo, "QUIT GAME", "", new Color(.45f,.5f,.55f),
            new Vector2(0, startY + gap * 4 + 8), () => Application.Quit());
    }

    // Full-width mode button matching Figma style: dark background + left accent + text + icon area
    void ModeButton(GameObject p, string lbl, string desc, Color col, Vector2 pos, UnityEngine.Events.UnityAction cb)
    {
        var go = new GameObject("Btn_" + lbl);
        go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = A(.5f,.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(400, 58);

        // Dark BG with slight tint
        var img = go.AddComponent<Image>();
        img.color = new Color(col.r*.06f, col.g*.06f, col.b*.06f, .92f);
        go.AddComponent<Outline>().effectColor = new Color(col.r, col.g, col.b, .35f);

        // Left accent bar (4px — matches Figma)
        var bar = new GameObject("Bar"); bar.transform.SetParent(go.transform, false);
        var brt = bar.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0,.1f); brt.anchorMax = new Vector2(0,.9f);
        brt.offsetMin = new Vector2(0,-2); brt.offsetMax = new Vector2(4,2);
        bar.AddComponent<Image>().color = new Color(col.r, col.g, col.b, .95f);

        // Label
        var tgo = new GameObject("L"); tgo.transform.SetParent(go.transform, false);
        var trt = tgo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(20,0); trt.offsetMax = new Vector2(-20,0);
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = lbl; tmp.fontSize = 22; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left; tmp.color = Color.white;

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(cb);

        // Hover colour effect
        var cb2 = btn.colors; cb2.highlightedColor = new Color(col.r*.2f,col.g*.2f,col.b*.2f,.95f);
        cb2.pressedColor    = new Color(col.r*.3f,col.g*.3f,col.b*.3f,.95f); btn.colors = cb2;
    }

    // Horizontal UI line
    void HBar(GameObject p, Color col, Vector2 aMin, Vector2 aMax, float offY, float height, float alpha)
    {
        var go = new GameObject("HBar"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = new Vector2(0, offY); rt.offsetMax = new Vector2(0, offY + height);
        go.AddComponent<Image>().color = new Color(col.r, col.g, col.b, alpha);
    }

    IEnumerator AnimateTitle(GameObject p)
    {
        // Pulse the title colours between cyan/magenta
        yield break; // placeholder — Unity TMP doesn't support inline gradient easily
    }

    TextMeshProUGUI T(GameObject p, string txt, int sz, Vector2 pos, Vector2 aMin, Vector2 aMax, TextAlignmentOptions al)
    {
        var go = new GameObject("_T"); go.transform.SetParent(p.transform, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchorMin=aMin; rt.anchorMax=aMax;
        rt.anchoredPosition=pos; rt.sizeDelta=new Vector2(900,130);
        var t = go.AddComponent<TextMeshProUGUI>(); t.text=txt; t.fontSize=sz; t.alignment=al; t.color=Color.white;
        return t;
    }

    static Vector2 A(float x, float y) => new Vector2(x, y);
}
