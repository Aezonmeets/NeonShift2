// LiveScoringHUD.cs
// ──────────────────────────────────────────────────────────────────────────────
// Attach this MonoBehaviour to a GameObject in your GameScene.
// It builds the neon scoring card shown in the reference image and keeps
// SCORE + RANK in perfect sync with LiveScoreManager every frame.
//
// SETUP
//   1. Add LiveScoreManager.cs and LiveScoringHUD.cs to your project.
//   2. In GameScene, create an empty GameObject named "LiveScoringHUD"
//      and attach this script to it.
//   3. Wire up your existing game logic to call:
//         LiveScoreManager.Instance.AddScore(points);
//      whenever the player hits a note.
//   4. On scene start, register the player once:
//         LiveScoreManager.Instance.RegisterPlayer(
//             PlayerPrefs.GetString("PlayerName","Player"), 0);
// ──────────────────────────────────────────────────────────────────────────────

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LiveScoringHUD : MonoBehaviour
{
    // ── Colour palette (matches MainMenuManager exactly) ──────────────────────
    static readonly Color CYAN    = new Color(0f,   0.92f, 1f,   1f);
    static readonly Color MAGENTA = new Color(1f,   0.15f, 0.75f,1f);
    static readonly Color DARK_BG = new Color(0.04f,0.06f, 0.10f,1f);

    // ── Tuning ─────────────────────────────────────────────────────────────────
    [Header("Position")]
    [Tooltip("Screen-space anchor. Default: bottom-right corner.")]
    public Vector2 anchorMin = new Vector2(1f, 0f);
    public Vector2 anchorMax = new Vector2(1f, 0f);
    [Tooltip("Offset from the chosen anchor (pixels).")]
    public Vector2 anchoredPosition = new Vector2(-30f, 30f);

    [Header("Card Size")]
    public Vector2 cardSize = new Vector2(370f, 185f);

    [Header("Glow")]
    public float glowIntensity = 2.2f;

    // ── Runtime refs ───────────────────────────────────────────────────────────
    TextMeshProUGUI scoreTxt;
    TextMeshProUGUI rankTxt;
    CanvasGroup     cardCG;

    long   displayedScore = 0;
    int    displayedRank  = 0;
    bool   hudReady       = false;

    // Cached values from last frame – avoids unnecessary string allocations
    long lastScore = -1;
    int  lastRank  = -1;

    // ── Pulse animation state ──────────────────────────────────────────────────
    Coroutine scorePulse;
    Coroutine rankPulse;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        BuildHUD();
        hudReady = true;
        StartCoroutine(FadeIn());
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!hudReady) return;

        // Pull the LIVE values from LiveScoreManager every frame
        long  liveScore = LiveScoreManager.Instance.PlayerScore;
        int   liveRank  = LiveScoreManager.Instance.PlayerRank;

        // Score — smooth counter
        if (liveScore != lastScore)
        {
            lastScore = liveScore;
            if (scorePulse != null) StopCoroutine(scorePulse);
            scorePulse = StartCoroutine(CountScore(displayedScore, liveScore, 0.35f));
        }

        // Rank — instant update with flash
        if (liveRank != lastRank)
        {
            lastRank = liveRank;
            if (rankPulse != null) StopCoroutine(rankPulse);
            rankPulse = StartCoroutine(FlashRank(liveRank));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SMOOTH SCORE COUNTER
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator CountScore(long from, long to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            displayedScore = (long)Mathf.Lerp(from, to, t);
            scoreTxt.text  = FormatScore(displayedScore);
            yield return null;
        }
        displayedScore = to;
        scoreTxt.text  = FormatScore(to);

        // Brief scale-pop on the score text
        yield return ScalePop(scoreTxt.rectTransform, 1.08f, 0.12f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RANK FLASH  (cyan → white → cyan)
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator FlashRank(int newRank)
    {
        displayedRank  = newRank;
        rankTxt.text   = FormatRank(newRank);

        // Flash white
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            rankTxt.color = Color.Lerp(Color.white, GetHDR(CYAN), t / 0.15f);
            yield return null;
        }
        rankTxt.color = GetHDR(CYAN);
        yield return ScalePop(rankTxt.rectTransform, 1.12f, 0.14f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SCALE POP helper
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator ScalePop(RectTransform rt, float peak, float halfDuration)
    {
        Vector3 normal = Vector3.one;
        Vector3 big    = Vector3.one * peak;
        float   t      = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(normal, big, t / halfDuration);
            yield return null;
        }
        t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(big, normal, t / halfDuration);
            yield return null;
        }
        rt.localScale = normal;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FADE-IN on scene load
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator FadeIn()
    {
        cardCG.alpha = 0f;
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            cardCG.alpha = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            yield return null;
        }
        cardCG.alpha = 1f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FORMATTING HELPERS
    // ─────────────────────────────────────────────────────────────────────────
    static string FormatScore(long s)
    {
        // e.g.  1284500  →  "1,284,500"
        return s.ToString("N0");
    }

    static string FormatRank(int rank)
    {
        if (rank <= 0) return "—";
        string suffix = rank switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
        return rank + suffix;
    }

    Color GetHDR(Color c) =>
        new Color(c.r * glowIntensity, c.g * glowIntensity, c.b * glowIntensity, 1f);

    // ─────────────────────────────────────────────────────────────────────────
    // BUILD THE CARD  (matches the reference screenshot exactly)
    // ─────────────────────────────────────────────────────────────────────────
    void BuildHUD()
    {
        // ── Canvas ────────────────────────────────────────────────────────────
        // Reuse the scene's existing Canvas if present, otherwise create one.
        Canvas cv = FindObjectOfType<Canvas>();
        GameObject canvasGo;
        if (cv == null)
        {
            canvasGo = new GameObject("Canvas");
            cv = canvasGo.AddComponent<Canvas>();
            cv.renderMode    = RenderMode.ScreenSpaceCamera;
            cv.worldCamera   = Camera.main;
            cv.planeDistance = 10f;
            cv.sortingOrder  = 20;
            var sc = canvasGo.AddComponent<CanvasScaler>();
            sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1280, 720);
            sc.matchWidthOrHeight  = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
        }
        else { canvasGo = cv.gameObject; }

        // ── Root card container ───────────────────────────────────────────────
        var card   = new GameObject("LiveScoringCard");
        card.transform.SetParent(canvasGo.transform, false);
        var cardRT = card.AddComponent<RectTransform>();
        cardRT.anchorMin        = anchorMin;
        cardRT.anchorMax        = anchorMax;
        cardRT.pivot            = new Vector2(1f, 0f);
        cardRT.anchoredPosition = anchoredPosition;
        cardRT.sizeDelta        = cardSize;

        cardCG = card.AddComponent<CanvasGroup>();

        // Card background — very dark, slight blue tint (matches screenshot)
        var bgImg   = card.AddComponent<Image>();
        bgImg.color = new Color(0.03f, 0.05f, 0.09f, 0.97f);

        // Rounded-feel outer border via Outline
        var outline = card.AddComponent<Outline>();
        outline.effectColor    = new Color(CYAN.r, CYAN.g, CYAN.b, 0.70f);
        outline.effectDistance = new Vector2(2f, -2f);

        // ── Subtle inner ambient glow strip (top edge) ────────────────────────
        var glowStrip   = new GameObject("GlowStrip");
        glowStrip.transform.SetParent(card.transform, false);
        var gsRT = glowStrip.AddComponent<RectTransform>();
        gsRT.anchorMin        = new Vector2(0f, 1f);
        gsRT.anchorMax        = new Vector2(1f, 1f);
        gsRT.pivot            = new Vector2(0.5f, 1f);
        gsRT.anchoredPosition = Vector2.zero;
        gsRT.sizeDelta        = new Vector2(0f, 3f);
        var gsImg = glowStrip.AddComponent<Image>();
        gsImg.color         = new Color(CYAN.r, CYAN.g, CYAN.b, 0.55f);
        gsImg.raycastTarget = false;

        // ── HEADER ROW  [ ▐▐ icon ]  LIVE SCORING ──────────────────────────
        var header   = new GameObject("Header");
        header.transform.SetParent(card.transform, false);
        var hRT = header.AddComponent<RectTransform>();
        hRT.anchorMin        = new Vector2(0f, 1f);
        hRT.anchorMax        = new Vector2(1f, 1f);
        hRT.pivot            = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = new Vector2(0f, -14f);
        hRT.sizeDelta        = new Vector2(-28f, 28f);

        // Bar-chart icon (3 rects, hand-drawn in UI)
        BuildBarIcon(header, new Vector2(16f, 0f), CYAN);

        var headerTxt = new GameObject("HeaderTxt");
        headerTxt.transform.SetParent(header.transform, false);
        var htRT = headerTxt.AddComponent<RectTransform>();
        htRT.anchorMin        = new Vector2(0f, 0f);
        htRT.anchorMax        = new Vector2(1f, 1f);
        htRT.offsetMin        = new Vector2(46f, 0f);
        htRT.offsetMax        = Vector2.zero;
        var htTmp = headerTxt.AddComponent<TextMeshProUGUI>();
        htTmp.text           = "LIVE SCORING";
        htTmp.fontSize       = 14f;
        htTmp.fontStyle      = FontStyles.Bold;
        htTmp.color          = CYAN;
        htTmp.alignment      = TextAlignmentOptions.Left;
        htTmp.characterSpacing = 4f;
        htTmp.raycastTarget  = false;

        // ── Divider ───────────────────────────────────────────────────────────
        var divider   = new GameObject("Divider");
        divider.transform.SetParent(card.transform, false);
        var dRT = divider.AddComponent<RectTransform>();
        dRT.anchorMin        = new Vector2(0f, 1f);
        dRT.anchorMax        = new Vector2(1f, 1f);
        dRT.pivot            = new Vector2(0.5f, 1f);
        dRT.anchoredPosition = new Vector2(0f, -52f);
        dRT.sizeDelta        = new Vector2(-20f, 1f);
        var dImg = divider.AddComponent<Image>();
        dImg.color         = new Color(CYAN.r, CYAN.g, CYAN.b, 0.22f);
        dImg.raycastTarget = false;

        // ── SCORE ROW ─────────────────────────────────────────────────────────
        BuildDataRow(card,
            label:    "SCORE",
            yFromTop: -72f,
            fontSize: 38f,
            color:    GetHDR(CYAN),
            out scoreTxt);

        // ── RANK DIVIDER (thin inner separator) ───────────────────────────────
        var rankDiv   = new GameObject("RankDivider");
        rankDiv.transform.SetParent(card.transform, false);
        var rdRT = rankDiv.AddComponent<RectTransform>();
        rdRT.anchorMin        = new Vector2(0f, 1f);
        rdRT.anchorMax        = new Vector2(1f, 1f);
        rdRT.pivot            = new Vector2(0.5f, 1f);
        rdRT.anchoredPosition = new Vector2(0f, -118f);
        rdRT.sizeDelta        = new Vector2(-20f, 1f);
        var rdImg = rankDiv.AddComponent<Image>();
        rdImg.color         = new Color(1f, 1f, 1f, 0.06f);
        rdImg.raycastTarget = false;

        // ── RANK ROW ──────────────────────────────────────────────────────────
        BuildDataRow(card,
            label:    "RANK",
            yFromTop: -148f,
            fontSize: 36f,
            color:    GetHDR(CYAN),
            out rankTxt);

        // Seed with live values already available (e.g. player just registered)
        scoreTxt.text = FormatScore(LiveScoreManager.Instance.PlayerScore);
        rankTxt.text  = FormatRank (LiveScoreManager.Instance.PlayerRank);
        lastScore     = LiveScoreManager.Instance.PlayerScore;
        lastRank      = LiveScoreManager.Instance.PlayerRank;
    }

    // ── Builds a LABEL (left) + VALUE (right) row ─────────────────────────────
    void BuildDataRow(GameObject parent, string label,
                      float yFromTop, float fontSize, Color color,
                      out TextMeshProUGUI valueTxt)
    {
        // Label
        var lGo   = new GameObject("Lbl_" + label);
        lGo.transform.SetParent(parent.transform, false);
        var lRT = lGo.AddComponent<RectTransform>();
        lRT.anchorMin        = new Vector2(0f, 1f);
        lRT.anchorMax        = new Vector2(0.4f, 1f);
        lRT.pivot            = new Vector2(0f, 1f);
        lRT.anchoredPosition = new Vector2(18f, yFromTop);
        lRT.sizeDelta        = new Vector2(0f, 48f);
        var lTmp = lGo.AddComponent<TextMeshProUGUI>();
        lTmp.text            = label;
        lTmp.fontSize        = 13f;
        lTmp.fontStyle       = FontStyles.Bold;
        lTmp.color           = new Color(CYAN.r * 0.75f, CYAN.g * 0.75f, CYAN.b * 0.75f, 1f);
        lTmp.alignment       = TextAlignmentOptions.BottomLeft;
        lTmp.characterSpacing = 3f;
        lTmp.raycastTarget   = false;

        // Value
        var vGo   = new GameObject("Val_" + label);
        vGo.transform.SetParent(parent.transform, false);
        var vRT = vGo.AddComponent<RectTransform>();
        vRT.anchorMin        = new Vector2(0.35f, 1f);
        vRT.anchorMax        = new Vector2(1f, 1f);
        vRT.pivot            = new Vector2(1f, 1f);
        vRT.anchoredPosition = new Vector2(-18f, yFromTop);
        vRT.sizeDelta        = new Vector2(0f, 52f);
        valueTxt = vGo.AddComponent<TextMeshProUGUI>();
        valueTxt.text          = "—";
        valueTxt.fontSize      = fontSize;
        valueTxt.fontStyle     = FontStyles.Bold;
        valueTxt.color         = color;
        valueTxt.alignment     = TextAlignmentOptions.BottomRight;
        valueTxt.raycastTarget = false;
    }

    // ── Tiny bar-chart icon built from 3 UI Image rects ──────────────────────
    void BuildBarIcon(GameObject parent, Vector2 center, Color col)
    {
        // Three bars of increasing height (left-to-right)
        float[] heights = { 9f, 14f, 19f };
        float[] xOff    = { -9f, -1f,  7f };
        float barW      = 5f;

        for (int i = 0; i < 3; i++)
        {
            var bar   = new GameObject("IconBar_" + i);
            bar.transform.SetParent(parent.transform, false);
            var bRT = bar.AddComponent<RectTransform>();
            bRT.anchorMin        = new Vector2(0f, 0.5f);
            bRT.anchorMax        = new Vector2(0f, 0.5f);
            bRT.pivot            = new Vector2(0.5f, 0f);
            bRT.anchoredPosition = new Vector2(center.x + xOff[i],
                                               center.y - heights[i] * 0.5f + 1f);
            bRT.sizeDelta = new Vector2(barW, heights[i]);
            bar.AddComponent<Image>().color = new Color(col.r, col.g, col.b, 0.90f);
        }
    }
}