using UnityEngine;
using System.Collections;

public class Tile : MonoBehaviour
{
    [HideInInspector] public int lane;
    [HideInInspector] public float speed;
    [HideInInspector] public bool isLong;
    [HideInInspector] public float holdLength; // world-unit length of hold tile

    public bool IsHit { get; private set; }
    public bool IsMissed { get; private set; }
    public float DistToHitLine { get; private set; } = 999f;
    public bool IsPastHitLine { get; private set; } = false;

    // Hold state
    public bool IsBeingHeld { get; private set; }
    bool holdComplete;

    public float glowIntensity = 3.5f;

    SpriteRenderer body;
    SpriteRenderer tail;   // long tile tail
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
        // Main head tile
        body = gameObject.AddComponent<SpriteRenderer>();
        body.sprite = MakeBox();
        body.color = new Color(col.r * glowIntensity, col.g * glowIntensity, col.b * glowIntensity, 1f);
        body.sortingOrder = 10;
        body.material = new Material(Shader.Find("Sprites/Default"));
        transform.localScale = new Vector3(1.8f, 0.35f, 1f);

        if (isLong)
        {
            // Tail: a stretched box behind the head
            var tailGO = new GameObject("Tail");
            tailGO.transform.SetParent(transform, false);
            tail = tailGO.AddComponent<SpriteRenderer>();
            tail.sprite = MakeBox();
            tail.sortingOrder = 9;
            tail.material = new Material(Shader.Find("Sprites/Default"));

            // Tail is scaled in local space — width matches head, height = holdLength
            float tailH = holdLength / 0.35f; // compensate for parent's y scale
            tailGO.transform.localScale = new Vector3(1f, -tailH, 1f); // negative = extends behind
            tailGO.transform.localPosition = new Vector3(0f, 0.5f, 0.01f); // pivot at top of head
            tail.color = new Color(col.r * glowIntensity * 0.55f, col.g * glowIntensity * 0.55f,
                                   col.b * glowIntensity * 0.55f, 0.65f);
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

    void Update()
    {
        if (IsHit || IsMissed) return;
        var tc = TrackController.Instance;
        if (!tc) return;

        // Handle hold input
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

        // distPast > 0 means tile has passed the key line (gone below)
        float distPast = currentDist - tc.hitDist;

        // Once past the keys: DistToHitLine = 999 so PlayerController CANNOT register a hit
        // Only tiles still approaching get a real distance value
        DistToHitLine = distPast <= 0f ? Mathf.Abs(distPast) : 999f;

        // Auto-miss with tiny grace (one frame) — anything below keys = miss
        if (distPast > 0.0f) Miss();
    }

    void UpdateHold(TrackController tc)
    {
        // Head moves with the track but is CLAMPED at the key line
        currentDist += speed * Time.deltaTime;

        // Head position: clamp so it never goes past the hit line (stays on the keys)
        float headDist = Mathf.Min(currentDist, tc.hitDist);
        transform.position = tc.LaneWorldPos(lane, headDist);

        float target = tc.CurrentAngle;
        float cur = transform.eulerAngles.z;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(cur, target, Time.deltaTime * 14f));

        if (tail)
        {
            // Tail extends ABOVE the key line only — never below
            // tailStart = how far above hitDist the tail begins (the unplayed portion)
            float tailStart = tc.hitDist - holdLength;                    // world dist where tail begins
            float consumed = Mathf.Max(0f, currentDist - tailStart);     // how much has been eaten
            float remaining = Mathf.Max(0f, holdLength - consumed);        // how much tail is left

            // Tail is always anchored at the hit line, growing upward (negative MoveDir)
            // We reposition the tail GO to sit exactly at hitDist and scale upward
            tail.transform.position = tc.LaneWorldPos(lane, tc.hitDist);
            tail.transform.rotation = transform.rotation;
            // localScale: width=1 (inherits head's x=1.8), height = remaining in world units
            float tailH = remaining / 0.35f;  // 0.35 = head's localScale.y
            tail.transform.localScale = new Vector3(1f, Mathf.Max(0.01f, tailH), 1f);
            // Reset local position so it extends behind (above) the key line
            tail.transform.localPosition = new Vector3(0f, 0.5f, 0.01f);

            if (remaining <= 0f)
            {
                // Hold complete!
                holdComplete = true;
                Hit(HitResult.Perfect);
            }
        }
    }

    // Called by PlayerController when key pressed on a long tile
    public void StartHold()
    {
        if (!isLong || IsHit || IsMissed) return;
        IsBeingHeld = true;
        StopAllCoroutines(); // stop the dim pulse
        // Instant bright white flash to show hold registered
        if (body) body.color = new Color(
            Mathf.Min(1f, col.r * glowIntensity * 1.6f),
            Mathf.Min(1f, col.g * glowIntensity * 1.6f),
            Mathf.Min(1f, col.b * glowIntensity * 1.6f), 1f);
        if (tail) tail.color = new Color(col.r * glowIntensity, col.g * glowIntensity, col.b * glowIntensity, 0.85f);
        StartCoroutine(HoldPulse()); // bright pulsing while held
    }

    IEnumerator HoldPulse()
    {
        while (IsBeingHeld && !IsHit && !IsMissed)
        {
            float p = 0.85f + Mathf.Sin(Time.time * 12f) * 0.15f; // fast bright pulse
            if (body) body.color = new Color(col.r * glowIntensity * p, col.g * glowIntensity * p, col.b * glowIntensity * p, 1f);
            yield return null;
        }
    }

    // Called by PlayerController when key released during hold
    public void ReleaseHold()
    {
        if (!IsBeingHeld) return;
        IsBeingHeld = false;
        if (!holdComplete)
        {
            StopAllCoroutines();
            StartCoroutine(FadeOut(0.08f)); // fast red flash then miss
            IsMissed = true;
            GameManager.Instance?.RegisterHit(HitResult.Miss, transform.position);
            TileSpawner.Instance?.RemoveTile(this);
            PlayerController.Instance?.UnregisterTile(this);
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
            if (body) body.color = new Color(col.r * glowIntensity, col.g * glowIntensity, col.b * glowIntensity, 1f - t / dur);
            if (tail) tail.color = new Color(col.r * glowIntensity * 0.55f, col.g * glowIntensity * 0.55f, col.b * glowIntensity * 0.55f, (1f - t / dur) * 0.65f);
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator PulseLoop()
    {
        while (!IsHit && !IsMissed)
        {
            float p = 0.75f + Mathf.Sin(Time.time * 6f + lane) * 0.22f;
            if (body) body.color = new Color(col.r * glowIntensity, col.g * glowIntensity, col.b * glowIntensity, p);
            yield return null;
        }
    }
}