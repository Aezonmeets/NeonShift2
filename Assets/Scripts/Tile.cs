using UnityEngine;
using System.Collections;

public class Tile : MonoBehaviour
{
    [HideInInspector] public int lane;
    [HideInInspector] public float speed;
    [HideInInspector] public bool isLong;
    [HideInInspector] public float holdLength;

    public bool IsHit { get; private set; }
    public bool IsMissed { get; private set; }
    public float DistToHitLine { get; private set; } = 999f;
    public bool IsPastHitLine { get; private set; } = false;

    public bool IsBeingHeld { get; private set; }
    bool holdComplete;

    public float glowIntensity = 3.5f;

    SpriteRenderer body;
    SpriteRenderer tail;
    Color col;

    float currentDist;

    public void Init(int laneIndex, float tileSpeed, bool longTile, float holdLen)
    {
        lane = laneIndex;
        speed = tileSpeed;
        isLong = longTile;
        holdLength = holdLen;
        col = TrackController.LaneColors[laneIndex % TrackController.LaneColors.Length];

        BuildVisuals();

        if (TrackController.Instance != null)
            currentDist = TrackController.Instance.spawnDist;

        StartCoroutine(PulseLoop());
    }

    void BuildVisuals()
    {
        body = gameObject.AddComponent<SpriteRenderer>();
        body.sprite = MakeBox();
        body.color = new Color(col.r * glowIntensity, col.g * glowIntensity, col.b * glowIntensity, 1f);
        body.sortingOrder = 10;
        body.material = new Material(Shader.Find("Sprites/Default"));
        transform.localScale = new Vector3(1.8f, 0.35f, 1f);

        if (isLong)
        {
            var tailGO = new GameObject("Tail");
            tailGO.transform.SetParent(transform, false);
            tail = tailGO.AddComponent<SpriteRenderer>();

            tail.sprite = MakeGradientTail();
            tail.sortingOrder = 9;
            tail.material = new Material(Shader.Find("Sprites/Default"));

            float tailH = holdLength / 0.35f;
            tailGO.transform.localScale = new Vector3(0.95f, tailH, 1f);
            tailGO.transform.localPosition = new Vector3(0f, 0f, 0.01f);

            tail.color = new Color(col.r * glowIntensity * 0.85f, col.g * glowIntensity * 0.85f, col.b * glowIntensity * 0.85f, 1f);
        }
    }

    static Sprite MakeBox()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    static Sprite MakeGradientTail()
    {
        int w = 64, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float alpha = 1f - (y / (float)(h - 1));
            for (int x = 0; x < w; x++)
            {
                px[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(px); tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 64f);
    }

    // =========================================================================
    // --- RAINBOW FEVER OVERRIDE (FIXED TO MATCH GAMEMANAGER) ---
    // =========================================================================
    Color GetActiveColor()
    {
        // Checks the exact name used in your GameManager: IsFeverActive
        if (GameManager.Instance != null && GameManager.Instance.IsFeverActive)
        {
            float hue = Mathf.Repeat(Time.unscaledTime * 2f + (currentDist * 0.05f) + (lane * 0.1f), 1f);
            return Color.HSVToRGB(hue, 1f, 1f);
        }
        return col; 
    }
    // =========================================================================

    void Update()
    {
        if (IsHit || IsMissed) return;

        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive()) return;

        var tc = TrackController.Instance;
        if (!tc) return;

        if (IsBeingHeld)
        {
            UpdateHold(tc);
            return;
        }

        currentDist += speed * Time.deltaTime;
        transform.position = tc.LaneWorldPos(lane, currentDist);

        float target = tc.CurrentAngle;
        float cur = transform.eulerAngles.z;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(cur, target, Time.deltaTime * 14f));

        float distPast = currentDist - tc.hitDist;

        float missDistance = 0.08f;
        if (PlayerController.Instance != null)
        {
            missDistance = Mathf.Abs(PlayerController.Instance.lateGoodDistance) + 0.1f;
        }

        if (isLong)
        {
            float tailEndDist = currentDist - holdLength;

            if (distPast <= 0f)
            {
                DistToHitLine = Mathf.Abs(distPast);
            }
            else if (tailEndDist <= tc.hitDist)
            {
                DistToHitLine = 0f;
            }
            else
            {
                DistToHitLine = 999f;
            }

            if (tailEndDist > tc.hitDist + missDistance) Miss();
        }
        else
        {
            DistToHitLine = distPast <= 0f ? Mathf.Abs(distPast) : 999f;
            if (distPast > missDistance) Miss();
        }
    }

    void UpdateHold(TrackController tc)
    {
        currentDist += speed * Time.deltaTime;

        float headDist = Mathf.Min(currentDist, tc.hitDist);
        transform.position = tc.LaneWorldPos(lane, headDist);

        float target = tc.CurrentAngle;
        float cur = transform.eulerAngles.z;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(cur, target, Time.deltaTime * 14f));

        if (tail)
        {
            float remainingLength;

            if (currentDist < tc.hitDist)
            {
                remainingLength = holdLength;
            }
            else
            {
                float overshot = currentDist - tc.hitDist;
                remainingLength = Mathf.Max(0f, holdLength - overshot);
            }

            float tailH = remainingLength / 0.35f;
            tail.transform.localScale = new Vector3(0.95f, Mathf.Max(0.01f, tailH), 1f);

            if (remainingLength <= 0f)
            {
                holdComplete = true;
                GameManager.Instance?.RegisterHit(HitResult.Perfect, transform.position);
                Hit(HitResult.Perfect);
            }
        }
    }

    public void StartHold()
    {
        if (!isLong || IsHit || IsMissed) return;
        IsBeingHeld = true;
        StopAllCoroutines();

        Color c = GetActiveColor();
        if (body) body.color = new Color(
            Mathf.Min(1f, c.r * glowIntensity * 1.6f),
            Mathf.Min(1f, c.g * glowIntensity * 1.6f),
            Mathf.Min(1f, c.b * glowIntensity * 1.6f), 1f);

        if (tail) tail.color = new Color(c.r * glowIntensity * 1.4f, c.g * glowIntensity * 1.4f, c.b * glowIntensity * 1.4f, 1f);

        StartCoroutine(HoldPulse());
    }

    IEnumerator HoldPulse()
    {
        while (IsBeingHeld && !IsHit && !IsMissed)
        {
            Color c = GetActiveColor(); // <--- Fetch rainbow color!
            float p = 0.85f + Mathf.Sin(Time.time * 12f) * 0.15f;
            
            if (body) body.color = new Color(c.r * glowIntensity * p, c.g * glowIntensity * p, c.b * glowIntensity * p, 1f);
            if (tail) tail.color = new Color(c.r * glowIntensity * 1.4f, c.g * glowIntensity * 1.4f, c.b * glowIntensity * 1.4f, 1f);
            
            yield return null;
        }
    }

    public void ReleaseHold()
    {
        if (!IsBeingHeld) return;
        IsBeingHeld = false;

        if (!holdComplete)
        {
            GameManager.Instance?.RegisterHit(HitResult.Good, transform.position);
            Hit(HitResult.Good); 
        }
    }

    public void Miss()
    {
        if (IsMissed || IsHit) return;
        IsMissed = true;
        GameManager.Instance?.RegisterHit(HitResult.Miss, transform.position);
        TileSpawner.Instance?.RemoveTile(this);
        PlayerController.Instance?.UnregisterTile(this);
        StopAllCoroutines();
        StartCoroutine(FadeOut(0.12f));
    }

    public void Hit(HitResult result)
    {
        if (IsHit || IsMissed) return;
        IsHit = true;
        TileSpawner.Instance?.RemoveTile(this);
        PlayerController.Instance?.UnregisterTile(this);
        StopAllCoroutines();

        Color fx = result == HitResult.Perfect ? new Color(1f, 0.95f, 0.15f)
                 : result == HitResult.Good ? new Color(0.25f, 1f, 0.45f)
                                               : new Color(1f, 0.25f, 0.25f);
        Color hdrFx = new Color(fx.r * glowIntensity, fx.g * glowIntensity, fx.b * glowIntensity, 1f);
        ParticlePoolManager.Instance?.SpawnAt(transform.position, hdrFx);
        StartCoroutine(HitAnim(hdrFx));
    }

    IEnumerator HitAnim(Color fx)
    {
        float t = 0f; Vector3 bs = transform.localScale;
        while (t < 0.2f)
        {
            t += Time.deltaTime; float p = t / 0.2f;
            transform.localScale = bs * (1f + p);
            if (body) body.color = new Color(fx.r, fx.g, fx.b, 1f - p);
            if (tail) tail.color = new Color(fx.r, fx.g, fx.b, (1f - p) * 0.7f);
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator FadeOut(float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            Color c = GetActiveColor();
            if (body) body.color = new Color(c.r * glowIntensity, c.g * glowIntensity, c.b * glowIntensity, 1f - t / dur);
            if (tail) tail.color = new Color(c.r * glowIntensity * 0.7f, c.g * glowIntensity * 0.7f, c.b * glowIntensity * 0.7f, (1f - t / dur) * 0.75f);
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator PulseLoop()
    {
        while (!IsHit && !IsMissed)
        {
            Color c = GetActiveColor(); // <--- Fetch rainbow color!
            float p = 0.75f + Mathf.Sin(Time.time * 6f + lane) * 0.22f;
            
            if (body) body.color = new Color(c.r * glowIntensity, c.g * glowIntensity, c.b * glowIntensity, p);
            
            if (tail && GameManager.Instance != null && GameManager.Instance.IsFeverActive) 
            {
                tail.color = new Color(c.r * glowIntensity * 0.85f, c.g * glowIntensity * 0.85f, c.b * glowIntensity * 0.85f, 1f);
            }
            
            yield return null;
        }
    }
}