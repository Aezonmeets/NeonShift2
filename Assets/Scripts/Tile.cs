using UnityEngine;
using System.Collections;

public class Tile : MonoBehaviour
{
    [HideInInspector] public int   lane;
    [HideInInspector] public float speed;

    public bool  IsHit         { get; private set; }
    public bool  IsMissed      { get; private set; }
    public float DistToHitLine { get; private set; } = 999f;

    // --- NEW: Add a multiplier to make the color bright enough for Bloom to catch it ---
    public float glowIntensity = 3.5f; 

    SpriteRenderer body;
    Color col;

    public void Init(int laneIndex, float tileSpeed)
    {
        lane  = laneIndex;
        speed = tileSpeed;
        col   = TrackController.LaneColors[laneIndex % TrackController.LaneColors.Length];
        BuildVisuals();
        StartCoroutine(PulseLoop());
    }

    void BuildVisuals()
    {
        body = gameObject.AddComponent<SpriteRenderer>();
        body.sprite = MakeBox();
        
        // --- MODIFIED: Multiply RGB by glowIntensity to create an HDR color ---
        body.color = new Color(col.r * glowIntensity, col.g * glowIntensity, col.b * glowIntensity, 1f);
        
        body.sortingOrder = 10;
        body.material = new Material(Shader.Find("Sprites/Default"));
        transform.localScale = new Vector3(1.8f, 0.35f, 1f);
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

        transform.position += (Vector3)(tc.MoveDir() * speed * Time.deltaTime);

        float target = tc.CurrentAngle;
        float cur    = transform.eulerAngles.z;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(cur, target, Time.deltaTime * 14f));

        var pc = PlayerController.Instance;
        Vector3 receptorPos = pc != null
            ? new Vector3(pc.GetLaneX(lane), pc.GetReceptorY(), 0f)
            : tc.HitPos(lane);

        DistToHitLine = Vector3.Distance(transform.position, receptorPos);

        Vector2 toHit = (Vector2)receptorPos - (Vector2)transform.position;
        float   dot   = Vector2.Dot(tc.MoveDir(), toHit);
        if (dot < -2.5f) Miss();
    }

    public void Miss()
    {
        if (IsMissed || IsHit) return;
        IsMissed = true;
        GameManager.Instance?.RegisterHit(HitResult.Miss, transform.position);
        TileSpawner.Instance?.RemoveTile(this);
        PlayerController.Instance?.UnregisterTile(this);
        StartCoroutine(FadeOut(0.25f));
    }

    public void Hit(HitResult result)
    {
        if (IsHit || IsMissed) return;
        IsHit = true;
        TileSpawner.Instance?.RemoveTile(this);
        PlayerController.Instance?.UnregisterTile(this);
        StopAllCoroutines();
        
        // --- MODIFIED: Apply glow intensity to the hit effects as well ---
        Color fx = result == HitResult.Perfect ? new Color(1f, 0.95f, 0.15f)
                 : result == HitResult.Good    ? new Color(0.25f, 1f, 0.45f)
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
            // --- MODIFIED: Maintain the HDR color while fading out ---
            if (body) body.color = new Color(col.r * glowIntensity, col.g * glowIntensity, col.b * glowIntensity, 1f - t / dur);
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator PulseLoop()
    {
        while (!IsHit && !IsMissed)
        {
            float p = 0.75f + Mathf.Sin(Time.time * 6f + lane) * 0.22f;
            // --- MODIFIED: Multiply RGB by intensity, keep Alpha tied to 'p' ---
            if (body) body.color = new Color(col.r * glowIntensity, col.g * glowIntensity, col.b * glowIntensity, p);
            yield return null;
        }
    }
}