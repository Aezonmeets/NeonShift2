using UnityEngine;
using System.Collections;

public class Tile : MonoBehaviour
{
    [HideInInspector] public int   lane;
    [HideInInspector] public float speed;

    public bool  IsHit         { get; private set; }
    public bool  IsMissed      { get; private set; }
    public float DistToHitLine { get; private set; } = 999f;

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
        // Make a simple 1x1 white sprite, colour it, and scale it in world units
        body = gameObject.AddComponent<SpriteRenderer>();
        body.sprite       = MakePixel();
        body.color        = col;
        body.sortingOrder = 5;   // on top of everything
        body.material     = new Material(Shader.Find("Sprites/Default"));

        // World-space size: 1.6 wide, 0.3 tall (no child scale confusion)
        transform.localScale = new Vector3(1.6f, 0.3f, 1f);
    }

    // Single white pixel sprite — simplest possible, guaranteed visible
    static Sprite MakePixel()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    void Update()
    {
        if (IsHit || IsMissed) return;
        var tc = TrackController.Instance;
        if (!tc) return;

        // Move along lane direction
        transform.position += (Vector3)(tc.MoveDir() * speed * Time.deltaTime);

        // Rotate to face lane direction
        float target = tc.CurrentAngle;
        float cur    = transform.eulerAngles.z;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(cur, target, Time.deltaTime * 14f));

        // Distance to hit zone
        Vector3 hp = tc.HitPos(lane);
        DistToHitLine = Vector3.Distance(transform.position, hp);

        // Auto-miss once tile passes hit zone
        Vector2 toHit = (Vector2)hp - (Vector2)transform.position;
        if (Vector2.Dot(tc.MoveDir(), toHit) < -2.5f) Miss();
    }

    public void Miss()
    {
        if (IsMissed || IsHit) return;
        IsMissed = true;
        GameManager.Instance?.RegisterHit(HitResult.Miss, transform.position);
        TileSpawner.Instance?.RemoveTile(this);
        PlayerController.Instance?.UnregisterTile(this);
        StartCoroutine(FadeOut(0.22f));
    }

    public void Hit(HitResult result)
    {
        if (IsHit || IsMissed) return;
        IsHit = true;
        TileSpawner.Instance?.RemoveTile(this);
        PlayerController.Instance?.UnregisterTile(this);
        StopAllCoroutines();
        Color fx = result == HitResult.Perfect ? new Color(1f, 0.95f, 0.15f)
                 : result == HitResult.Good    ? new Color(0.25f, 1f, 0.45f)
                                               : new Color(1f, 0.25f, 0.25f);
        ParticlePoolManager.Instance?.SpawnAt(transform.position, fx);
        StartCoroutine(HitAnim(fx));
    }

    IEnumerator HitAnim(Color fx)
    {
        float t = 0f;
        Vector3 bs = transform.localScale;
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
            if (body) body.color = new Color(col.r, col.g, col.b, 1f - t / dur);
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator PulseLoop()
    {
        while (!IsHit && !IsMissed)
        {
            float p = 0.75f + Mathf.Sin(Time.time * 6f + lane) * 0.22f;
            if (body) body.color = new Color(col.r, col.g, col.b, p);
            yield return null;
        }
    }
}
