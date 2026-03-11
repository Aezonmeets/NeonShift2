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

    // Fixed Y — receptor bar never moves vertically
    float receptorY;

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    void Start()
    {
        receptorY = -(Camera.main.orthographicSize - 1.5f);
        BuildHitZones();
    }

    // Public accessors for Tile hit detection
    public float GetLaneX(int lane) => zoneRoots != null && lane < zoneRoots.Length
        ? zoneRoots[lane].transform.position.x : 0f;
    public float GetReceptorY() => receptorY;

    void BuildHitZones()
    {
        int count  = TrackController.Instance.GetLaneCount();
        zoneRoots  = new GameObject[count];
        zoneGlows  = new SpriteRenderer[count];
        zoneRings  = new SpriteRenderer[count];
        zoneBGs    = new SpriteRenderer[count];
        zoneLabels = new TextMeshPro[count];

        for (int i = 0; i < count; i++)
        {
            Color col = TrackController.LaneColors[i];

            var root = new GameObject("Zone_" + i);
            root.transform.SetParent(transform);
            root.transform.rotation = Quaternion.identity;
            zoneRoots[i] = root;

            // Outer glow halo
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(root.transform, false);
            var glowSR = glowGO.AddComponent<SpriteRenderer>();
            glowSR.sprite = MakeGlow(col); glowSR.sortingOrder = 2;
            glowSR.material = new Material(Shader.Find("Sprites/Default"));
            glowGO.transform.localScale = new Vector3(2.6f, 1.4f, 1f);
            zoneGlows[i] = glowSR;

            // Dark BG
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(root.transform, false);
            var bgSR = bgGO.AddComponent<SpriteRenderer>();
            bgSR.sprite = MakeSolid(new Color(.04f,.04f,.11f,.92f)); bgSR.sortingOrder = 3;
            bgSR.material = new Material(Shader.Find("Sprites/Default"));
            bgGO.transform.localScale = new Vector3(2.0f, 0.62f, 1f);
            zoneBGs[i] = bgSR;

            // Ring border
            var ringGO = new GameObject("Ring");
            ringGO.transform.SetParent(root.transform, false);
            var ringSR = ringGO.AddComponent<SpriteRenderer>();
            ringSR.sprite = MakeRing(col); ringSR.sortingOrder = 4;
            ringSR.material = new Material(Shader.Find("Sprites/Default"));
            ringGO.transform.localScale = new Vector3(2.15f, 0.76f, 1f);
            zoneRings[i] = ringSR;

            // Key label — always upright
            var lblGO = new GameObject("Lbl");
            lblGO.transform.SetParent(root.transform, false);
            lblGO.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            var tmp = lblGO.AddComponent<TextMeshPro>();
            tmp.text = KeyLabels[i]; tmp.fontSize = 4.8f; tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(col.r * 1.2f + .2f, col.g * 1.2f + .2f, col.b * 1.2f + .2f, 1f);
            tmp.sortingOrder = 6;
            zoneLabels[i] = tmp;
        }
    }

    void Update()
    {
        if (!GameManager.Instance || !GameManager.Instance.IsGameActive()) return;

        var tc = TrackController.Instance;

        for (int i = 0; i < zoneRoots.Length; i++)
        {
            // X follows the lane's world position (tracks rotation) — Y stays fixed
            float laneX = tc.HitPos(i).x;
            zoneRoots[i].transform.position = new Vector3(laneX, receptorY, 0f);
            zoneRoots[i].transform.rotation = Quaternion.identity; // never tilt

            Color col = TrackController.LaneColors[i];
            float t   = Time.time * 3f + i * 1.57f;

            if (zoneGlows[i])  zoneGlows[i].color  = new Color(col.r, col.g, col.b, 0.18f + Mathf.Sin(t) * 0.10f);
            if (zoneRings[i])  zoneRings[i].color   = new Color(col.r, col.g, col.b, 0.5f + Mathf.Sin(t * 1.4f) * 0.2f);

            if (zoneLabels[i])
            {
                float la = 0.75f + Mathf.Sin(t * 1.1f) * 0.25f;
                zoneLabels[i].color = new Color(
                    Mathf.Min(1f, col.r * la + .3f),
                    Mathf.Min(1f, col.g * la + .3f),
                    Mathf.Min(1f, col.b * la + .3f), 1f);
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
            if (zoneRings[i]) zoneRings[i].color = new Color(col.r, col.g, col.b, Mathf.Lerp(1f, 0.5f, p));
            if (zoneGlows[i]) zoneGlows[i].color = new Color(col.r, col.g, col.b, Mathf.Lerp(0.9f, 0.18f, p));
            if (zoneLabels[i]) zoneLabels[i].color = Color.Lerp(Color.white, new Color(col.r,col.g,col.b,1f), p);
            yield return null;
        }
    }

    static Sprite MakeSolid(Color col)
    {
        var tex = new Texture2D(4,4); var px = new Color[16];
        for(int i=0;i<16;i++) px[i]=col; tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,4,4),new Vector2(.5f,.5f),4f);
    }

    static Sprite MakeGlow(Color col)
    {
        int w=64,h=32; var tex=new Texture2D(w,h,TextureFormat.RGBA32,false);
        tex.filterMode=FilterMode.Bilinear; var px=new Color[w*h];
        for(int y=0;y<h;y++) for(int x=0;x<w;x++)
        {
            float fx=x/(float)(w-1), fy=y/(float)(h-1);
            float dx=Mathf.Abs(fx-.5f)*2f, dy=Mathf.Abs(fy-.5f)*2f;
            float a=Mathf.Clamp01(1f-Mathf.Sqrt(dx*dx*.7f+dy*dy));
            px[y*w+x]=new Color(col.r,col.g,col.b,a*a*0.65f);
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,w,h),new Vector2(.5f,.5f),32f);
    }

    static Sprite MakeRing(Color col)
    {
        int w=64,h=24; var tex=new Texture2D(w,h,TextureFormat.RGBA32,false);
        tex.filterMode=FilterMode.Bilinear; var px=new Color[w*h];
        for(int y=0;y<h;y++) for(int x=0;x<w;x++)
        {
            float fx=x/(float)(w-1), fy=y/(float)(h-1);
            float e=Mathf.Min(fx,1f-fx,fy,1f-fy)*7f;
            float ring=Mathf.Clamp01(1f-Mathf.Abs(e-.4f)*5.5f);
            px[y*w+x]=new Color(col.r,col.g,col.b,ring*0.95f);
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,w,h),new Vector2(.5f,.5f),32f);
    }

    public void RegisterTile(Tile t)   { if(!tiles.Contains(t)) tiles.Add(t); }
    public void UnregisterTile(Tile t) => tiles.Remove(t);
}
