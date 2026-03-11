using UnityEngine;
using System.Collections;

public class TrackController : MonoBehaviour
{
    public static TrackController Instance { get; private set; }

    public int   laneCount   = 4;
    public float laneSpacing = 2.2f;
    public float rotationInterval   = 8f;
    public float transitionDuration = 0.55f;

    public static readonly Color[] LaneColors = {
        new Color(0.0f, 0.92f, 1.0f),
        new Color(0.2f, 1.0f,  0.3f),
        new Color(1.0f, 0.92f, 0.1f),
        new Color(1.0f, 0.15f, 0.75f),
    };

    public float CurrentAngle { get; private set; } = 0f;

    bool      transitioning;
    Coroutine rotateCo;
    LineRenderer[] laneLines;

    static readonly float[] Angles = { 0f, 90f, 180f, 270f, 45f, 135f, 225f, 315f };

    // Signed distances along MoveDir from world ORIGIN (0,0)
    // spawnDist < 0 → tiles appear BEHIND the direction of travel (above screen at 0°)
    // hitDist   > 0 → hit zone is AHEAD in direction of travel (below screen at 0°)
    float spawnDist;
    float hitDist;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        Camera.main.backgroundColor = new Color(0.025f, 0.025f, 0.09f);
        Camera.main.clearFlags = CameraClearFlags.SolidColor;

        float camH = Camera.main.orthographicSize;
        spawnDist = -(camH + 1.5f);   // e.g. -6.5 at orthoSize=5 → spawns at y=+6.5
        hitDist   =  (camH - 1.5f);   // e.g. +3.5 → hit zone at y=-3.5

        BuildLaneLines();
    }

    void BuildLaneLines()
    {
        if (laneLines != null) foreach (var l in laneLines) if (l) Destroy(l.gameObject);
        laneLines = new LineRenderer[laneCount];
        for (int i = 0; i < laneCount; i++)
        {
            var go = new GameObject("LL_" + i);
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = lr.endWidth = 0.04f;
            lr.positionCount = 2;
            lr.sortingOrder = -1;
            lr.useWorldSpace = true;
            Color c = LaneColors[i]; c.a = 0.28f;
            lr.startColor = lr.endColor = c;
            laneLines[i] = lr;
        }
        RefreshLines();
    }

    void Update() => RefreshLines();

    void RefreshLines()
    {
        if (laneLines == null) return;
        Vector2 move = MoveDir();
        Vector2 perp = PerpDir();
        float total  = (laneCount - 1) * laneSpacing;

        for (int i = 0; i < laneCount; i++)
        {
            if (!laneLines[i]) continue;
            // Use same offset formula as LaneWorldPos so lines stay on tiles
            float   offset = total / 2f - i * laneSpacing;
            Vector2 c = perp * offset;
            laneLines[i].SetPosition(0, new Vector3(c.x - move.x * 13f, c.y - move.y * 13f, 0));
            laneLines[i].SetPosition(1, new Vector3(c.x + move.x * 13f, c.y + move.y * 13f, 0));
        }
    }

    public void BeginRotating() { StopRotating(); rotateCo = StartCoroutine(RotateLoop()); }
    public void StopRotating()  { if (rotateCo != null) StopCoroutine(rotateCo); }

    IEnumerator RotateLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(rotationInterval);
            if (GameManager.Instance && GameManager.Instance.IsGameActive())
                yield return StartCoroutine(DoRotate());
        }
    }

    IEnumerator DoRotate()
    {
        transitioning = true;
        yield return StartCoroutine(FlashWarning());

        float next = CurrentAngle;
        for (int tries = 0; tries < 20; tries++)
        {
            next = Angles[Random.Range(0, Angles.Length)];
            if (Mathf.Abs(Mathf.DeltaAngle(next, CurrentAngle)) >= 30f) break;
        }

        float from = CurrentAngle, t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            CurrentAngle = Mathf.LerpAngle(from, next, Mathf.SmoothStep(0, 1, t / transitionDuration));
            yield return null;
        }
        CurrentAngle  = next;
        transitioning = false;
    }

    IEnumerator FlashWarning()
    {
        for (int f = 0; f < 5; f++)
        {
            SetAlpha(0.95f); yield return new WaitForSeconds(0.055f);
            SetAlpha(0.12f); yield return new WaitForSeconds(0.055f);
        }
    }

    void SetAlpha(float a)
    {
        foreach (var lr in laneLines)
            if (lr) { Color c = new Color(0.85f, 0.85f, 1f, a); lr.startColor = lr.endColor = c; }
    }

    // Direction tiles travel (at 0° this is straight down = (0,-1))
    public Vector2 MoveDir()
    {
        float r = CurrentAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(r), -Mathf.Cos(r));
    }

    // Perpendicular to travel direction (lane spacing axis)
    public Vector2 PerpDir() { Vector2 m = MoveDir(); return new Vector2(m.y, -m.x); }

    // ── KEY FIX: all positions are RELATIVE TO WORLD ORIGIN (0,0) ────────
    // This ensures that when the angle changes, lanes pivot around the same
    // centre point, so nothing drifts off screen.
    //
    // Lane offset = total/2 - lane*spacing  (lane 0 = leftmost)
    // Lane position = PerpDir * offset + MoveDir * dist
    public Vector3 LaneWorldPos(int lane, float dist)
    {
        float total  = (laneCount - 1) * laneSpacing;
        float offset = total / 2f - lane * laneSpacing;   // lane 0 left, lane 3 right
        Vector2 pos  = PerpDir() * offset + MoveDir() * dist;
        return new Vector3(pos.x, pos.y, 0f);
    }

    public Vector3 SpawnPos(int lane) => LaneWorldPos(lane, spawnDist);
    public Vector3 HitPos(int lane)   => LaneWorldPos(lane, hitDist);

    public bool  IsTransitioning() => transitioning;
    public int   GetLaneCount()    => laneCount;
    public float GetLaneSpacing()  => laneSpacing;
}
