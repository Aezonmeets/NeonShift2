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
    SpriteRenderer[] zoneBGs;
    SpriteRenderer[] zoneRings;
    TextMeshPro[]    zoneLabels;

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }
    void Start()  => BuildHitZones();

    void BuildHitZones()
    {
        int count = TrackController.Instance.GetLaneCount();
        zoneRoots  = new GameObject[count];
        zoneBGs    = new SpriteRenderer[count];
        zoneRings  = new SpriteRenderer[count];
        zoneLabels = new TextMeshPro[count];

        for (int i = 0; i < count; i++)
        {
            Color col = TrackController.LaneColors[i];
            var root = new GameObject("Zone_" + i);
            root.transform.SetParent(transform);
            zoneRoots[i] = root;

            // BG fill
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(root.transform, false);
            var bgSR = bgGO.AddComponent<SpriteRenderer>();
            bgSR.sprite = MakeFill(); bgSR.color = new Color(col.r, col.g, col.b, 0.08f);
            bgSR.sortingOrder = 4; bgSR.material = new Material(Shader.Find("Sprites/Default"));
            bgGO.transform.localScale = new Vector3(1.8f, 0.55f, 1f);
            zoneBGs[i] = bgSR;

            // Ring border
            var ringGO = new GameObject("Ring");
            ringGO.transform.SetParent(root.transform, false);
            var ringSR = ringGO.AddComponent<SpriteRenderer>();
            ringSR.sprite = MakeRing(col); ringSR.sortingOrder = 5;
            ringSR.material = new Material(Shader.Find("Sprites/Default"));
            ringGO.transform.localScale = new Vector3(2.0f, 0.72f, 1f);
            zoneRings[i] = ringSR;

            // Key label
            var lblGO = new GameObject("Lbl");
            lblGO.transform.SetParent(root.transform, false);
            lblGO.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            var tmp = lblGO.AddComponent<TextMeshPro>();
            tmp.text = KeyLabels[i]; tmp.fontSize = 4.2f; tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 1f, 1f, 0.95f); tmp.sortingOrder = 6;
            zoneLabels[i] = tmp;
        }
        Debug.Log($"[PlayerController] Built {count} hit zones");
    }

    void Update()
    {
        if (!GameManager.Instance || !GameManager.Instance.IsGameActive()) return;
        var tc = TrackController.Instance;
        float ang = tc.CurrentAngle;

        for (int i = 0; i < zoneRoots.Length; i++)
        {
            zoneRoots[i].transform.position = tc.HitPos(i);
            zoneRoots[i].transform.rotation = Quaternion.Euler(0f, 0f, ang);
            float pulse = 0.08f + Mathf.Sin(Time.time * 2.5f + i * 1.4f) * 0.04f;
            Color c = TrackController.LaneColors[i]; c.a = pulse;
            if (zoneBGs[i]) zoneBGs[i].color = c;
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
        if (i >= zoneRings.Length) yield break;
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.deltaTime; float p = t / 0.18f;
            if (zoneRings[i]) zoneRings[i].color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.3f, p));
            Color bg = TrackController.LaneColors[i]; bg.a = Mathf.Lerp(0.85f, 0.08f, p);
            if (zoneBGs[i]) zoneBGs[i].color = bg;
            yield return null;
        }
    }

    static Sprite MakeFill()
    {
        var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,1,1), new Vector2(.5f,.5f), 1f);
    }

    static Sprite MakeRing(Color col)
    {
        int w=64,h=24; var tex=new Texture2D(w,h,TextureFormat.RGBA32,false);
        tex.filterMode=FilterMode.Bilinear; var px=new Color[w*h];
        for(int y=0;y<h;y++) for(int x=0;x<w;x++)
        {
            float fx=x/(float)(w-1),fy=y/(float)(h-1);
            float e=Mathf.Min(fx,1f-fx,fy,1f-fy)*8f;
            float ring=Mathf.Clamp01(1f-Mathf.Abs(e-0.35f)*6f);
            px[y*w+x]=new Color(col.r,col.g,col.b,ring*0.9f);
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,w,h),new Vector2(.5f,.5f),32f);
    }

    public void RegisterTile(Tile t)   { if (!tiles.Contains(t)) tiles.Add(t); }
    public void UnregisterTile(Tile t) => tiles.Remove(t);
}
