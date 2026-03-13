using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    public float hitZoneDistance = 1.3f;
    public float perfectZone     = 0.48f;

    static readonly KeyCode[] Keys      = { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
    static readonly string[]  KeyLabels = { "D", "F", "J", "K" };

    readonly List<Tile> tiles = new List<Tile>();

    GameObject[]     zoneRoots;
    SpriteRenderer[] zoneGlows;
    SpriteRenderer[] zoneRings;
    SpriteRenderer[] zoneBGs;
    TextMeshPro[]    zoneLabels;

    float receptorY;

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    void Start()
    {
        receptorY = -(Camera.main.orthographicSize - 1.5f);
        BuildHitZones();
    }

    public float GetLaneX(int lane) => zoneRoots != null && lane < zoneRoots.Length
        ? zoneRoots[lane].transform.position.x : 0f;
    public float GetReceptorY() => receptorY;

    void BuildHitZones()
    {
        int   count   = TrackController.Instance.GetLaneCount();
        float spacing = TrackController.Instance.GetLaneSpacing();

        zoneRoots  = new GameObject[count];
        zoneGlows  = new SpriteRenderer[count];
        zoneRings  = new SpriteRenderer[count];
        zoneBGs    = new SpriteRenderer[count];
        zoneLabels = new TextMeshPro[count];

        float boxW = spacing * 0.86f;
        float boxH = 0.62f;

        for (int i = 0; i < count; i++)
        {
            Color col = TrackController.LaneColors[i];

            // Root sits at the lane X, receptorY
            var root = new GameObject("Zone_" + i);
            root.transform.SetParent(transform);
            root.transform.rotation = Quaternion.identity;
            zoneRoots[i] = root;

            // ── 1. SOLID dark BG — fully opaque (alpha 1) so lane line is hidden behind it
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(root.transform, false);
            bgGO.transform.localPosition = Vector3.zero;
            var bgSR = bgGO.AddComponent<SpriteRenderer>();
            bgSR.sprite   = MakeSolid(new Color(0.06f, 0.06f, 0.11f, 1f)); // FULLY opaque
            bgSR.sortingOrder = 20;                                           // above lane lines
            bgSR.material = new Material(Shader.Find("Sprites/Default"));
            bgGO.transform.localScale = new Vector3(boxW, boxH, 1f);
            zoneBGs[i] = bgSR;

            // ── 2. Colored border on top of BG
            var ringGO = new GameObject("Ring");
            ringGO.transform.SetParent(root.transform, false);
            ringGO.transform.localPosition = Vector3.zero;
            var ringSR = ringGO.AddComponent<SpriteRenderer>();
            ringSR.sprite = MakeRing(col);
            ringSR.sortingOrder = 21;
            ringSR.material = new Material(Shader.Find("Sprites/Default"));
            ringSR.color  = new Color(col.r * 0.65f, col.g * 0.65f, col.b * 0.65f, 0.95f);
            ringGO.transform.localScale = new Vector3(boxW, boxH, 1f);
            zoneRings[i] = ringSR;

            // ── 3. Subtle glow BELOW the box only (no side bleed)
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(root.transform, false);
            glowGO.transform.localPosition = new Vector3(0f, -boxH * 0.55f, 0f);
            var glowSR = glowGO.AddComponent<SpriteRenderer>();
            glowSR.sprite = MakeGlow(col);
            glowSR.sortingOrder = 19;
            glowSR.material = new Material(Shader.Find("Sprites/Default"));
            glowSR.color  = new Color(col.r, col.g, col.b, 0.10f);
            glowGO.transform.localScale = new Vector3(boxW * 0.85f, boxH * 0.7f, 1f);
            zoneGlows[i] = glowSR;

            // ── 4. Key label — INSIDE the box, centered, white
            var lblGO = new GameObject("Lbl");
            lblGO.transform.SetParent(root.transform, false);
            lblGO.transform.localPosition = new Vector3(0f, 0f, -0.1f); // slightly in front
            var tmp = lblGO.AddComponent<TextMeshPro>();
            tmp.text      = KeyLabels[i];
            tmp.fontSize  = 4.2f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(1f, 1f, 1f, 0.92f); // plain white — no color tint
            tmp.sortingOrder = 22;
            zoneLabels[i] = tmp;
        }
    }

    void Update()
    {
        if (!GameManager.Instance || !GameManager.Instance.IsGameActive()) return;

        var tc = TrackController.Instance;

        for (int i = 0; i < zoneRoots.Length; i++)
        {
            // Move box to lane center X, fixed Y
            float laneX = tc.HitPos(i).x;
            zoneRoots[i].transform.position = new Vector3(laneX, receptorY, 0f);
            zoneRoots[i].transform.rotation = Quaternion.identity;

            Color col = TrackController.LaneColors[i];
            float t   = Time.time * 2.5f + i * 1.57f;

            // Very subtle border pulse — stays dim
            if (zoneRings[i])
                zoneRings[i].color = new Color(
                    col.r * 0.65f, col.g * 0.65f, col.b * 0.65f,
                    0.75f + Mathf.Sin(t * 1.2f) * 0.12f);

            // Glow barely visible
            if (zoneGlows[i])
                zoneGlows[i].color = new Color(col.r, col.g, col.b,
                    0.07f + Mathf.Sin(t) * 0.03f);

            // Label stays white and upright
            if (zoneLabels[i])
            {
                zoneLabels[i].color = new Color(1f, 1f, 1f, 0.90f);
                zoneLabels[i].transform.rotation = Quaternion.identity;
            }
        }

        for (int i = 0; i < Keys.Length; i++)
            if (Input.GetKeyDown(Keys[i])) { TryHit(i); StartCoroutine(FlashZone(i)); }
    }

    void TryHit(int lane)
    {
        Tile best = null; float bestDist = float.MaxValue;
        foreach (var t in tiles)
        {
            if (t == null || t.IsHit || t.IsMissed || t.lane != lane) continue;
            if (t.DistToHitLine < bestDist) { bestDist = t.DistToHitLine; best = t; }
        }
        if (best == null || bestDist > hitZoneDistance * 1.9f) return;
        HitResult result = bestDist <= perfectZone ? HitResult.Perfect : HitResult.Good;
        GameManager.Instance?.RegisterHit(result, best.transform.position);
        best.Hit(result);
    }

    IEnumerator FlashZone(int i)
    {
        if (i >= zoneRoots.Length) yield break;
        Color col = TrackController.LaneColors[i];
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.deltaTime; float p = t / 0.18f;
            if (zoneRings[i]) zoneRings[i].color = new Color(col.r, col.g, col.b,
                Mathf.Lerp(1f, 0.75f, p));
            if (zoneGlows[i]) zoneGlows[i].color = new Color(col.r, col.g, col.b,
                Mathf.Lerp(0.6f, 0.07f, p));
            if (zoneLabels[i]) zoneLabels[i].color = Color.Lerp(Color.white,
                new Color(1f, 1f, 1f, 0.90f), p);
            yield return null;
        }
    }

    static Sprite MakeSolid(Color col)
    {
        var tex = new Texture2D(4, 4);
        var px  = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = col;
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,4,4), new Vector2(.5f,.5f), 4f);
    }

    static Sprite MakeGlow(Color col)
    {
        int w=64, h=32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float fx = x/(float)(w-1), fy = y/(float)(h-1);
            float dx = Mathf.Abs(fx-.5f)*2f, dy = Mathf.Abs(fy-.5f)*2f;
            float a  = Mathf.Clamp01(1f - Mathf.Sqrt(dx*dx*.7f + dy*dy));
            px[y*w+x] = new Color(col.r, col.g, col.b, a*a*0.5f);
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,w,h), new Vector2(.5f,.5f), 32f);
    }

    static Sprite MakeRing(Color col)
    {
        int w=64, h=24;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float fx = x/(float)(w-1), fy = y/(float)(h-1);
            float e    = Mathf.Min(fx, 1f-fx, fy, 1f-fy) * 7f;
            float ring = Mathf.Clamp01(1f - Mathf.Abs(e-.4f)*5.5f);
            px[y*w+x] = new Color(col.r, col.g, col.b, ring * 0.95f);
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,w,h), new Vector2(.5f,.5f), 32f);
    }

    public void RegisterTile(Tile t)   { if (!tiles.Contains(t)) tiles.Add(t); }
    public void UnregisterTile(Tile t) => tiles.Remove(t);
}