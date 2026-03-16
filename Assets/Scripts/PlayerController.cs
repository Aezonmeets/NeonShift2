using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Asymmetrical Hit Zones")]
    [Tooltip("How far ABOVE the key can you hit for an EARLY rating?")]
    public float earlyDistance = 2.8f;
    [Tooltip("How far ABOVE the key can you hit for a GOOD rating?")]
    public float earlyGoodDistance = 1.2f;
    [Tooltip("How far ABOVE the key does the PERFECT zone start?")]
    public float earlyPerfectDistance = 0.4f;

    [Tooltip("How far BELOW the hit line does the PERFECT zone end?")]
    public float latePerfectDistance = -0.2f;
    [Tooltip("How far BELOW the key can you hit for a GOOD rating?")]
    public float lateGoodDistance = -0.8f;
    [Tooltip("How far BELOW the key can you hit for a LATE rating?")]
    public float lateDistance = -1.4f;

    [Header("Instant Audio Feedback")]
    public AudioClip hitSound;
    private AudioSource fastAudioSource;

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

    GameObject backplateRoot;
    GameObject[] zoneRoots;
    SpriteRenderer[] zoneGlows;
    SpriteRenderer[] zoneRings;
    SpriteRenderer[] zoneBGs;
    TextMeshPro[] zoneLabels;

    // NEW: Reference to make the laser pulse
    SpriteRenderer laserGlowSR;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (keyProfiles == null || keyProfiles.Length != 4) SetupDefaultKeyProfiles();

        fastAudioSource = gameObject.AddComponent<AudioSource>();
        fastAudioSource.playOnAwake = false;
        fastAudioSource.spatialBlend = 0f;
    }

    void Start()
    {
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

        float boxW = spacing * 0.94f;
        float boxH = 0.85f;
        float uiOffset = -0.55f;

        backplateRoot = new GameObject("HitZonesBackplate");
        backplateRoot.transform.SetParent(transform);

        var bpVisualGO = new GameObject("BP_Sprite");
        bpVisualGO.transform.SetParent(backplateRoot.transform, false);
        bpVisualGO.transform.localPosition = new Vector3(0f, uiOffset, 0f);

        var bpSR = bpVisualGO.AddComponent<SpriteRenderer>();
        bpSR.sprite = MakeRoundedSolid();
        bpSR.drawMode = SpriteDrawMode.Sliced;
        bpSR.color = new Color(0.08f, 0.12f, 0.16f, 0.85f);
        bpSR.sortingOrder = 18;
        bpSR.material = new Material(Shader.Find("Sprites/Default"));
        bpSR.size = new Vector2((count * spacing) + 0.2f, boxH * 1.3f);

        Sprite roundedBorderSprite = MakeRoundedBorder();
        Sprite roundedSolidSprite = MakeRoundedSolid();

        for (int i = 0; i < count; i++)
        {
            Color col = TrackController.LaneColors[i];
            var root = new GameObject("Zone_" + i);
            root.transform.SetParent(transform);
            zoneRoots[i] = root;

            var bgGO = new GameObject("BG"); bgGO.transform.SetParent(root.transform, false);
            bgGO.transform.localPosition = new Vector3(0f, uiOffset, 0f);
            var bgSR = bgGO.AddComponent<SpriteRenderer>();
            bgSR.sprite = roundedSolidSprite;
            bgSR.drawMode = SpriteDrawMode.Sliced;
            bgSR.size = new Vector2(boxW, boxH);
            bgSR.sortingOrder = 20;
            bgSR.material = new Material(Shader.Find("Sprites/Default"));
            bgSR.color = new Color(col.r, col.g, col.b, 0.1f);
            zoneBGs[i] = bgSR;

            var ringGO = new GameObject("Border"); ringGO.transform.SetParent(root.transform, false);
            ringGO.transform.localPosition = new Vector3(0f, uiOffset, 0f);
            var ringSR = ringGO.AddComponent<SpriteRenderer>();
            ringSR.sprite = roundedBorderSprite;
            ringSR.drawMode = SpriteDrawMode.Sliced;
            ringSR.size = new Vector2(boxW, boxH);
            ringSR.sortingOrder = 21;
            ringSR.material = new Material(Shader.Find("Sprites/Default"));
            ringSR.color = new Color(col.r * 0.8f, col.g * 0.8f, col.b * 0.8f, 0.95f);
            zoneRings[i] = ringSR;

            var glowGO = new GameObject("Glow"); glowGO.transform.SetParent(root.transform, false);
            glowGO.transform.localPosition = new Vector3(0f, uiOffset - (boxH * 0.4f), 0f);
            var glowSR = glowGO.AddComponent<SpriteRenderer>();
            glowSR.sprite = MakeGlow();
            glowSR.sortingOrder = 19;
            glowSR.material = new Material(Shader.Find("Sprites/Default"));
            glowSR.color = new Color(col.r, col.g, col.b, 0.05f);
            glowGO.transform.localScale = new Vector3(boxW * 0.8f, boxH * 0.6f, 1f);
            zoneGlows[i] = glowSR;

            var lblGO = new GameObject("Lbl"); lblGO.transform.SetParent(root.transform, false);
            lblGO.transform.localPosition = new Vector3(0f, uiOffset, -0.1f);
            var tmp = lblGO.AddComponent<TextMeshPro>();
            tmp.text = activeLabels[i];
            tmp.fontSize = 4.8f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 1f, 1f, 0.95f);
            tmp.sortingOrder = 22;
            zoneLabels[i] = tmp;
        }

        // --- NEW: THE GLOWING PERFECT HIT LINE (LASER) ---
        var laserGO = new GameObject("HitLine_Core");
        laserGO.transform.SetParent(backplateRoot.transform, false);
        // Positioned exactly at mathematical 0 (the true center of the Perfect zone)
        laserGO.transform.localPosition = new Vector3(0f, 0f, -0.2f);

        var laserSR = laserGO.AddComponent<SpriteRenderer>();
        laserSR.sprite = MakeRoundedSolid();
        laserSR.drawMode = SpriteDrawMode.Sliced;
        laserSR.size = new Vector2((count * spacing) + 0.6f, 0.06f);
        laserSR.color = new Color(1f, 1f, 1f, 0.9f); // Solid white core
        laserSR.sortingOrder = 24; // Renders visually above the keys
        laserSR.material = new Material(Shader.Find("Sprites/Default"));

        var laserGlowGO = new GameObject("HitLine_Glow");
        laserGlowGO.transform.SetParent(laserGO.transform, false);
        laserGlowGO.transform.localPosition = Vector3.zero;

        laserGlowSR = laserGlowGO.AddComponent<SpriteRenderer>();
        laserGlowSR.sprite = MakeGlow();
        laserGlowSR.transform.localScale = new Vector3(((count * spacing) + 0.6f) * 0.8f, 0.5f, 1f);
        laserGlowSR.color = new Color(0f, 0.9f, 1f, 0.5f); // Cyan neon glow
        laserGlowSR.sortingOrder = 23;
        laserGlowSR.material = new Material(Shader.Find("Sprites/Default"));
    }

    void Update()
    {
        if (!GameManager.Instance || !GameManager.Instance.IsGameActive()) return;
        var tc = TrackController.Instance;

        // --- NEW: Pulsating animation for the laser line ---
        if (laserGlowSR != null)
        {
            float pulse = 0.4f + Mathf.Sin(Time.time * 8f) * 0.2f;
            laserGlowSR.color = new Color(0f, 0.9f, 1f, pulse);
        }

        Vector3 centerPos = Vector3.zero;
        for (int i = 0; i < zoneRoots.Length; i++) centerPos += tc.HitPos(i);
        centerPos /= zoneRoots.Length;
        backplateRoot.transform.position = centerPos;
        backplateRoot.transform.rotation = Quaternion.Euler(0f, 0f, tc.CurrentAngle);

        for (int i = 0; i < zoneRoots.Length; i++)
        {
            zoneRoots[i].transform.position = tc.HitPos(i);
            zoneRoots[i].transform.rotation = Quaternion.Euler(0f, 0f, tc.CurrentAngle);
        }

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
                    heldTile[i].ReleaseHold();
                    heldTile[i] = null;
                }
            }
        }
    }

    void TryHit(int lane)
    {
        Tile best = null;
        float bestSignedDist = float.MaxValue;
        float minAbsDist = float.MaxValue;
        bool areTilesInLane = false;

        float speedMultiplier = TrackController.Instance != null ? TileSpawner.Instance.tileSpeed / 7f : 1f;

        float currentEarly = earlyDistance * speedMultiplier;
        float currentEarlyGood = earlyGoodDistance * speedMultiplier;
        float currentEarlyPerfect = earlyPerfectDistance * speedMultiplier;

        float currentLatePerfect = latePerfectDistance * speedMultiplier;
        float currentLateGood = lateGoodDistance * speedMultiplier;
        float currentLate = lateDistance * speedMultiplier;

        Vector3 moveDir = TrackController.Instance.MoveDir();
        Vector3 hitPos = TrackController.Instance.HitPos(lane);

        for (int i = 0; i < tiles.Count; i++)
        {
            Tile t = tiles[i];
            if (t == null || t.IsHit || t.IsMissed || t.lane != lane) continue;
            areTilesInLane = true;

            float signedDist = Vector3.Dot(hitPos - t.transform.position, moveDir);

            if (signedDist < currentLate) continue;

            float absDist = Mathf.Abs(signedDist);
            if (absDist < minAbsDist)
            {
                minAbsDist = absDist;
                bestSignedDist = signedDist;
                best = t;
            }
        }

        if (best != null)
        {
            if (bestSignedDist <= currentEarly && bestSignedDist >= currentLate)
            {
                HitResult result = HitResult.Miss;

                if (bestSignedDist <= currentEarlyPerfect && bestSignedDist >= currentLatePerfect)
                {
                    result = HitResult.Perfect;
                }
                else if (bestSignedDist <= currentEarlyGood && bestSignedDist >= currentLateGood)
                {
                    result = HitResult.Good;
                }
                else if (bestSignedDist > currentEarlyGood && bestSignedDist <= currentEarly)
                {
                    result = HitResult.Early;
                }
                else if (bestSignedDist < currentLateGood && bestSignedDist >= currentLate)
                {
                    result = HitResult.Late;
                }

                if (hitSound != null && fastAudioSource != null)
                    fastAudioSource.PlayOneShot(hitSound, 0.8f);

                if (best.isLong)
                {
                    GameManager.Instance?.RegisterHit(result, best.transform.position);
                    best.StartHold();
                    heldTile[lane] = best;
                }
                else
                {
                    GameManager.Instance?.RegisterHit(result, best.transform.position);
                    best.Hit(result);
                }
            }
        }
        else
        {
            if (!areTilesInLane && GameManager.Instance != null)
            {
                GameManager.Instance.ApplySpamPenalty();
            }
        }
    }

    IEnumerator FlashZone(int i)
    {
        if (i >= zoneRoots.Length) yield break;
        Color col = TrackController.LaneColors[i];
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime; float p = t / 0.12f;
            if (zoneRings[i]) zoneRings[i].color = new Color(col.r, col.g, col.b, Mathf.Lerp(1f, 0.85f, p));
            if (zoneBGs[i]) zoneBGs[i].color = new Color(col.r, col.g, col.b, Mathf.Lerp(0.5f, 0.1f, p));
            if (zoneGlows[i]) zoneGlows[i].color = new Color(col.r, col.g, col.b, Mathf.Lerp(0.8f, 0.05f, p));
            yield return null;
        }
    }

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
}