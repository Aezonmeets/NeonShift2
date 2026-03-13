using UnityEngine;
using System.Collections;

public class TrackController : MonoBehaviour
{
    public static TrackController Instance { get; private set; }

    public int laneCount = 4;
    
    // Adjusted to 2.5f so the lines aren't too tight!
    public float laneSpacing = 2.5f; 
    
    public float rotationInterval = 8f;
    public float transitionDuration = 0.55f;

    // TARGET NEON COLORS
    public static readonly Color[] LaneColors = {
        new Color(1.0f, 0.05f, 0.6f, 1.0f), // Pink
        new Color(1.0f, 0.95f, 0.0f, 1.0f), // Yellow
        new Color(0.2f, 1.0f, 0.1f, 1.0f),  // Green
        new Color(0.0f, 0.85f, 1.0f, 1.0f)  // Blue
    };

    public float CurrentAngle { get; private set; } = 0f;

    bool transitioning;
    Coroutine rotateCo;
    LineRenderer[] laneLines;

    static readonly float[] Angles = { 0f, 90f, 180f, 270f, 45f, 135f, 225f, 315f };
    float spawnDist;
    float hitDist;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        foreach (Transform child in transform) Destroy(child.gameObject);
    }

    void Start()
    {
        Camera.main.backgroundColor = new Color(0.01f, 0.01f, 0.05f);
        Camera.main.clearFlags = CameraClearFlags.SolidColor;

        float camH = Camera.main.orthographicSize;
        spawnDist = -(camH + 2.0f);
        hitDist   =  (camH - 1.8f);

        BuildLaneLines();
    }

    void BuildLaneLines()
    {
        laneLines = new LineRenderer[laneCount + 1];
        for (int i = 0; i <= laneCount; i++)
        {
            var go = new GameObject("BorderLine_" + i);
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();
            
            lr.material = new Material(Shader.Find("Sprites/Default"));
            
            // THICKER LINES: Increased width to make the neon visible
            lr.startWidth = 0.06f;
            lr.endWidth   = 0.06f;
            lr.positionCount = 2;
            lr.sortingOrder = -1;
            lr.useWorldSpace = true;

            Color c = (i < laneCount) ? LaneColors[i] : LaneColors[laneCount - 1];
            
            // GRADIENT FADE: Makes the top transparent and the bottom solid
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(c, 0.0f), new GradientColorKey(c, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.7f, 1.0f) }
            );
            lr.colorGradient = grad;

            laneLines[i] = lr;
        }
    }

    void Update() => RefreshLines();

    void RefreshLines()
    {
        if (laneLines == null) return;
        Vector2 move = MoveDir();
        Vector2 perp = PerpDir();
        float total = (laneCount - 1) * laneSpacing;
        float startBorderOffset = (total / 2f) + (laneSpacing / 2f);

        for (int i = 0; i <= laneCount; i++)
        {
            if (!laneLines[i]) continue;
            float offset = startBorderOffset - i * laneSpacing;
            Vector2 c = perp * offset;
            
            // SetPosition(0) is the Top end of the line, SetPosition(1) is the Bottom end
            laneLines[i].SetPosition(0, (Vector3)(c - move * 30f)); 
            laneLines[i].SetPosition(1, (Vector3)(c + move * 20f));
        }
    }

    // --- HELPER METHODS FOR PLAYERCONTROLLER / TILE ---
    public int   GetLaneCount()    => laneCount;
    public float GetLaneSpacing()  => laneSpacing;
    public bool  IsTransitioning() => transitioning;

    public Vector2 MoveDir()
    {
        float r = CurrentAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(r), -Mathf.Cos(r));
    }

    public Vector2 PerpDir()
    {
        Vector2 m = MoveDir();
        return new Vector2(m.y, -m.x);
    }

    public Vector3 LaneWorldPos(int lane, float dist)
    {
        float total  = (laneCount - 1) * laneSpacing;
        float offset = total / 2f - lane * laneSpacing;
        Vector2 pos  = PerpDir() * offset + MoveDir() * dist;
        return new Vector3(pos.x, pos.y, 0f);
    }

    public Vector3 SpawnPos(int lane) => LaneWorldPos(lane, spawnDist);
    public Vector3 HitPos(int lane)   => LaneWorldPos(lane, hitDist);

    public void BeginRotating() { rotateCo = StartCoroutine(RotateLoop()); }
    public void StopRotating()  { if (rotateCo != null) StopCoroutine(rotateCo); }

    IEnumerator RotateLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(rotationInterval);
            yield return StartCoroutine(DoRotate());
        }
    }

    IEnumerator DoRotate()
    {
        transitioning = true;
        float from = CurrentAngle;
        float next = Angles[Random.Range(0, Angles.Length)];
        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            CurrentAngle = Mathf.LerpAngle(from, next, t / transitionDuration);
            yield return null;
        }
        CurrentAngle = next;
        transitioning = false;
    }
}