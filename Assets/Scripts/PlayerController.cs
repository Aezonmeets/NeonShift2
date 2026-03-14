using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    public float hitZoneDistance = 1.5f;  // slightly wider to accommodate beat-sync precision
    public float perfectZone = 0.35f;  // tighter perfect window — beats are now accurate

    public enum RotationState { Normal, Rotated90, Rotated180, Rotated270 }

    [System.Serializable]
    public struct KeyProfile
    {
        public string profileName;
        public KeyCode[] keys;
        public string[] labels;
    }

    [Header("Dynamic Controls")]
    public KeyProfile[] keyProfiles;

    KeyCode[] activeKeys = new KeyCode[4];
    string[] activeLabels = new string[4];

    Tile[] heldTile = new Tile[4];
    readonly List<Tile> tiles = new List<Tile>();

    // UI Elements
    GameObject backplateRoot;
    GameObject[] zoneRoots;
    SpriteRenderer[] zoneGlows;
    SpriteRenderer[] zoneRings;
    SpriteRenderer[] zoneBGs;
    TextMeshPro[] zoneLabels;

    float receptorY;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (keyProfiles == null || keyProfiles.Length != 4) SetupDefaultKeyProfiles();
    }

    void Start()
    {
        receptorY = -(Camera.main.orthographicSize - 1.5f);
        SetControlsForRotation(RotationState.Normal);
        BuildHitZones();
    }

    public void SetControlsForRotation(RotationState state)
    {
        int index = (int)state;
        if (index >= 0 && index < keyProfiles.Length)
        {
            activeKeys = keyProfiles[index].keys;
            activeLabels = keyProfiles[index].labels;
            if (zoneLabels != null)
                for (int i = 0; i < zoneLabels.Length; i++)
                    if (zoneLabels[i] != null && i < activeLabels.Length)
                        zoneLabels[i].text = activeLabels[i];
        }
    }

    void SetupDefaultKeyProfiles()
    {
        keyProfiles = new KeyProfile[4];
        keyProfiles[0] = new KeyProfile { profileName = "Normal", keys = new[] { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K }, labels = new[] { "D", "F", "J", "K" } };
        keyProfiles[1] = new KeyProfile { profileName = "Rot90", keys = new[] { KeyCode.F, KeyCode.V, KeyCode.J, KeyCode.N }, labels = new[] { "F", "V", "J", "N" } };
        keyProfiles[2] = new KeyProfile { profileName = "Inverted", keys = new[] { KeyCode.K, KeyCode.J, KeyCode.F, KeyCode.D }, labels = new[] { "K", "J", "F", "D" } };
        keyProfiles[3] = new KeyProfile { profileName = "Rot270", keys = new[] { KeyCode.V, KeyCode.F, KeyCode.N, KeyCode.J }, labels = new[] { "V", "F", "N", "J" } };
    }

    void BuildHitZones()
    {
        int count = TrackController.Instance.GetLaneCount();
        float spacing = TrackController.Instance.GetLaneSpacing();

        zoneRoots = new GameObject[count];
        zoneGlows = new SpriteRenderer[count];
        zoneRings = new SpriteRenderer[count];
        zoneBGs = new SpriteRenderer[count];
        zoneLabels = new TextMeshPro[count];

        // ── WIDTH FIX: 94% of spacing guarantees they fit cleanly inside the lane borders ──
        float boxW = spacing * 0.94f;
        float boxH = 0.85f;

        // 1. Build the Global Backplate
        backplateRoot = new GameObject("HitZonesBackplate");
        backplateRoot.transform.SetParent(transform);
        var bpSR = backplateRoot.AddComponent<SpriteRenderer>();
        bpSR.sprite = MakeRoundedSolid();
        bpSR.drawMode = SpriteDrawMode.Sliced; // Enables 9-slicing
        bpSR.color = new Color(0.08f, 0.12f, 0.16f, 0.85f);
        bpSR.sortingOrder = 18;
        bpSR.material = new Material(Shader.Find("Sprites/Default"));
        bpSR.size = new Vector2((count * spacing) + 0.2f, boxH * 1.3f);

        // 2. Build Individual Keys
        Sprite roundedBorderSprite = MakeRoundedBorder();
        Sprite roundedSolidSprite = MakeRoundedSolid();

        for (int i = 0; i < count; i++)
        {
            Color col = TrackController.LaneColors[i];

            var root = new GameObject("Zone_" + i);
            root.transform.SetParent(transform);
            zoneRoots[i] = root;

            // Key Tint Background
            var bgGO = new GameObject("BG"); bgGO.transform.SetParent(root.transform, false);
            var bgSR = bgGO.AddComponent<SpriteRenderer>();
            bgSR.sprite = roundedSolidSprite;
            bgSR.drawMode = SpriteDrawMode.Sliced;
            bgSR.size = new Vector2(boxW, boxH);
            bgSR.sortingOrder = 20;
            bgSR.material = new Material(Shader.Find("Sprites/Default"));
            bgSR.color = new Color(col.r, col.g, col.b, 0.1f);
            zoneBGs[i] = bgSR;

            // Colored Border Box
            var ringGO = new GameObject("Border"); ringGO.transform.SetParent(root.transform, false);
            var ringSR = ringGO.AddComponent<SpriteRenderer>();
            ringSR.sprite = roundedBorderSprite;
            ringSR.drawMode = SpriteDrawMode.Sliced;
            ringSR.size = new Vector2(boxW, boxH);
            ringSR.sortingOrder = 21;
            ringSR.material = new Material(Shader.Find("Sprites/Default"));
            ringSR.color = new Color(col.r * 0.8f, col.g * 0.8f, col.b * 0.8f, 0.95f);
            zoneRings[i] = ringSR;

            // Optional Under-Glow
            var glowGO = new GameObject("Glow"); glowGO.transform.SetParent(root.transform, false);
            glowGO.transform.localPosition = new Vector3(0f, -boxH * 0.4f, 0f);
            var glowSR = glowGO.AddComponent<SpriteRenderer>();
            glowSR.sprite = MakeGlow();
            glowSR.sortingOrder = 19;
            glowSR.material = new Material(Shader.Find("Sprites/Default"));
            glowSR.color = new Color(col.r, col.g, col.b, 0.05f);
            glowGO.transform.localScale = new Vector3(boxW * 0.8f, boxH * 0.6f, 1f);
            zoneGlows[i] = glowSR;

            // Text Label
            var lblGO = new GameObject("Lbl"); lblGO.transform.SetParent(root.transform, false);
            lblGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            var tmp = lblGO.AddComponent<TextMeshPro>();
            tmp.text = activeLabels[i];
            tmp.fontSize = 4.8f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 1f, 1f, 0.95f);
            tmp.sortingOrder = 22;
            zoneLabels[i] = tmp;
        }
    }

    void Update()
    {
        if (!GameManager.Instance || !GameManager.Instance.IsGameActive()) return;
        var tc = TrackController.Instance;

        // Position Global Backplate in the center
        Vector3 centerPos = Vector3.zero;
        for (int i = 0; i < zoneRoots.Length; i++) centerPos += tc.HitPos(i);
        centerPos /= zoneRoots.Length;
        backplateRoot.transform.position = centerPos;
        backplateRoot.transform.rotation = Quaternion.Euler(0f, 0f, tc.CurrentAngle);

        // Update Keys
        for (int i = 0; i < zoneRoots.Length; i++)
        {
            zoneRoots[i].transform.position = tc.HitPos(i);
            zoneRoots[i].transform.rotation = Quaternion.Euler(0f, 0f, tc.CurrentAngle);

            Color col = TrackController.LaneColors[i];
            float t = Time.time * 2.5f + i * 1.57f;

            if (zoneRings[i]) zoneRings[i].color = new Color(col.r * 0.8f, col.g * 0.8f, col.b * 0.8f, 0.85f + Mathf.Sin(t * 1.2f) * 0.1f);
            if (zoneBGs[i]) zoneBGs[i].color = new Color(col.r, col.g, col.b, 0.1f + Mathf.Sin(t) * 0.02f);
            if (zoneGlows[i]) zoneGlows[i].color = new Color(col.r, col.g, col.b, 0.05f + Mathf.Sin(t) * 0.02f);
        }

        // Input
        for (int i = 0; i < activeKeys.Length; i++)
        {
            if (Input.GetKeyDown(activeKeys[i]))
            {
                TryHit(i);
                StartCoroutine(FlashZone(i));
            }
            if (Input.GetKeyUp(activeKeys[i]))
            {
                if (heldTile[i] != null)
                {
                    // Only call ReleaseHold if tile is still alive
                    if (!heldTile[i].IsHit && !heldTile[i].IsMissed)
                        heldTile[i].ReleaseHold();
                    heldTile[i] = null;
                }
            }

            // Auto-clear any held tile references that were destroyed externally
            if (heldTile[i] != null && (heldTile[i].IsHit || heldTile[i].IsMissed))
                heldTile[i] = null;
        }
    }

    void TryHit(int lane)
    {
        Tile best = null; float bestDist = float.MaxValue;
        foreach (var t in tiles)
        {
            if (t == null || t.IsHit || t.IsMissed || t.lane != lane) continue;
            if (t.DistToHitLine < bestDist) { bestDist = t.DistToHitLine; best = t; }
        }

        // ── PENALTY LOGIC: If the player pressed a key but no valid tile was near ──
        if (best == null || bestDist > hitZoneDistance * 1.9f)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ApplySpamPenalty();
            }
            return;
        }

        if (best.isLong)
        {
            // Guard: don't re-register if this tile is already being held
            if (best.IsBeingHeld) return;
            // Don't RegisterHit here — hold tiles score on COMPLETION in Tile.UpdateHold
            // This prevents double-counting total and perfectHits
            best.StartHold();
            heldTile[lane] = best;
        }
        else
        {
            HitResult result = bestDist <= perfectZone ? HitResult.Perfect : HitResult.Good;
            GameManager.Instance?.RegisterHit(result, best.transform.position);
            best.Hit(result);
        }
    }

    IEnumerator FlashZone(int i)
    {
        if (i >= zoneRoots.Length) yield break;
        Color col = TrackController.LaneColors[i];
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime; float p = t / 0.15f;
            if (zoneRings[i]) zoneRings[i].color = new Color(col.r, col.g, col.b, Mathf.Lerp(1f, 0.85f, p));
            if (zoneBGs[i]) zoneBGs[i].color = new Color(col.r, col.g, col.b, Mathf.Lerp(0.4f, 0.1f, p)); // Fills with bright color on hit
            if (zoneGlows[i]) zoneGlows[i].color = new Color(col.r, col.g, col.b, Mathf.Lerp(0.6f, 0.05f, p));
            yield return null;
        }
    }

    // ── PROCEDURAL 9-SLICED TEXTURES ──

    static Sprite MakeRoundedSolid()
    {
        int size = 128; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; var px = new Color[size * size]; float radius = 24f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Abs(x - size / 2f) - (size / 2f - radius);
                float cy = Mathf.Abs(y - size / 2f) - (size / 2f - radius);
                float d = new Vector2(Mathf.Max(cx, 0), Mathf.Max(cy, 0)).magnitude + Mathf.Min(Mathf.Max(cx, cy), 0) - radius;
                px[y * size + x] = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(d));
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(32, 32, 32, 32));
    }

    static Sprite MakeRoundedBorder()
    {
        int size = 128; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; var px = new Color[size * size];
        float radius = 24f; float thickness = 6f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Abs(x - size / 2f) - (size / 2f - radius);
                float cy = Mathf.Abs(y - size / 2f) - (size / 2f - radius);
                float d = new Vector2(Mathf.Max(cx, 0), Mathf.Max(cy, 0)).magnitude + Mathf.Min(Mathf.Max(cx, cy), 0) - radius;
                float alpha = 1f - Mathf.Clamp01(Mathf.Abs(d) - thickness / 2f);
                px[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(32, 32, 32, 32));
    }

    static Sprite MakeGlow()
    {
        int w = 64, h = 32; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.filterMode = FilterMode.Bilinear; var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x / (float)(w - 1) - .5f) * 2f, dy = Mathf.Abs(y / (float)(h - 1) - .5f) * 2f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx * .7f + dy * dy));
                px[y * w + x] = new Color(1f, 1f, 1f, a * a * 0.5f);
            }
        tex.SetPixels(px); tex.Apply(); return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 32f);
    }

    public void RegisterTile(Tile t) { if (!tiles.Contains(t)) tiles.Add(t); }
    public void UnregisterTile(Tile t) => tiles.Remove(t);

    // Force-clear all held tile refs (called on pause/game over)
    public void ClearHeldTiles()
    {
        for (int i = 0; i < heldTile.Length; i++)
        {
            if (heldTile[i] != null && !heldTile[i].IsHit && !heldTile[i].IsMissed)
                heldTile[i].ReleaseHold();
            heldTile[i] = null;
        }
    }
}