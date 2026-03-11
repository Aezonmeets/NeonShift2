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

    // These are signed distances along MoveDir from world origin:
    //   MoveDir at angle=0 is (0,-1) = downward
    //   NEGATIVE dist * MoveDir = upward = spawn ABOVE screen  ✓
    //   POSITIVE dist * MoveDir = downward = hit zone BELOW center ✓
    float spawnDist;  // negative  (tiles start AGAINST move direction)
    float hitDist;    // positive  (hit zone IS along move direction)

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
        // spawnDist is NEGATIVE so that MoveDir*(spawnDist) points UP (above screen)
        spawnDist = -(camH + 1.5f);
        // hitDist is POSITIVE so that MoveDir*(hitDist) points DOWN (near bottom)
        hitDist   =  (camH - 1.5f);

        Debug.Log($"[TC] camH={camH:F1}  spawnDist={spawnDist:F1}  hitDist={hitDist:F1}");
        Debug.Log($"[TC] Lane0 spawn={SpawnPos(0)}  hit={HitPos(0)}");

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
            Color c = LaneColors[i]; c.a = 0.3f;
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
        float ext    = 12f;

        for (int i = 0; i < laneCount; i++)
        {
            if (!laneLines[i]) continue;
            float   offset = -total / 2f + i * laneSpacing;
            Vector2 c = perp * offset;
            laneLines[i].SetPosition(0, new Vector3(c.x - move.x * ext, c.y - move.y * ext, 0));
            laneLines[i].SetPosition(1, new Vector3(c.x + move.x * ext, c.y + move.y * ext, 0));
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
        CurrentAngle = next;
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

    // Direction tiles travel (downward at angle=0)
    public Vector2 MoveDir()
    {
        float r = CurrentAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(r), -Mathf.Cos(r));
    }

    // Perpendicular to move direction (lane spacing axis)
    public Vector2 PerpDir() { Vector2 m = MoveDir(); return new Vector2(m.y, -m.x); }

    // Lane world position at a signed dist along MoveDir
    public Vector3 LaneWorldPos(int lane, float dist)
    {
        float total  = (laneCount - 1) * laneSpacing;
        // Negate offset so lane 0=D is on the LEFT, lane 3=K on the RIGHT
        float offset = total / 2f - lane * laneSpacing;
        Vector2 pos  = PerpDir() * offset + MoveDir() * dist;
        return new Vector3(pos.x, pos.y, 0f);
    }

    // spawnDist is negative → tiles appear ABOVE (or behind) the hit zone
    public Vector3 SpawnPos(int lane) => LaneWorldPos(lane, spawnDist);
    // hitDist is positive → hit zone is BELOW (or ahead along move direction)
    public Vector3 HitPos(int lane)   => LaneWorldPos(lane, hitDist);

    public bool  IsTransitioning() => transitioning;
    public int   GetLaneCount()    => laneCount;
    public float GetLaneSpacing()  => laneSpacing;
}
