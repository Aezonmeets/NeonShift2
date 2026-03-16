using UnityEngine;
using System.Collections;

public class TrackController : MonoBehaviour
{
    public static TrackController Instance { get; private set; }

    public int laneCount = 4;
    public float laneSpacing = 2.5f;
    public float rotationInterval = 8f;
    public float transitionDuration = 0.55f;

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

    // --- NEW VARIABLES FOR GLOW ---
    float[] lineGlow;
    float warningPulse = 0f;

    static readonly float[] Angles = { 0f, 30f, -30f };

    public float spawnDist;
    public float hitDist;

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
        hitDist = (camH - 1.8f);

        BuildLaneLines();
    }

    void BuildLaneLines()
    {
        laneLines = new LineRenderer[laneCount + 1];
        lineGlow = new float[laneCount + 1]; // Initialize the glow array

        for (int i = 0; i <= laneCount; i++)
        {
            var go = new GameObject("BorderLine_" + i);
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();

            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = 0.06f;
            lr.endWidth = 0.06f;
            lr.positionCount = 2;
            lr.sortingOrder = -1;
            lr.useWorldSpace = true;

            laneLines[i] = lr;
        }
    }

    void Update()
    {
        // Smoothly fade out the keystroke glow over time
        if (lineGlow != null)
        {
            for (int i = 0; i <= laneCount; i++)
            {
                if (lineGlow[i] > 0)
                {
                    lineGlow[i] -= Time.deltaTime * 6f; // Adjust this number to make the fade faster/slower
                    if (lineGlow[i] < 0) lineGlow[i] = 0;
                }
            }
        }

        RefreshLines();
    }

    // --- NEW: CALL THIS FROM PLAYERCONTROLLER WHEN A KEY IS PRESSED ---
    public void PulseLane(int lane)
    {
        if (lane >= 0 && lane < laneCount && lineGlow != null)
        {
            // Light up the left and right border of the target lane
            lineGlow[lane] = 1f;
            lineGlow[lane + 1] = 1f;
        }
    }

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

            laneLines[i].SetPosition(0, (Vector3)(c - move * 30f));
            laneLines[i].SetPosition(1, (Vector3)(c + move * 20f));

            // --- UNIFIED COLOR RENDERING ---
            Color baseColor = (i < laneCount) ? LaneColors[i] : LaneColors[laneCount - 1];

            // 1. Apply Overdrive Warning Flash
            Color currentCol = Color.Lerp(baseColor, Color.white, warningPulse);
            float targetAlpha = Mathf.Lerp(0.7f, 1.0f, warningPulse);

            // 2. Apply Keystroke Glow
            if (lineGlow[i] > 0)
            {
                currentCol = Color.Lerp(currentCol, Color.white, lineGlow[i] * 0.7f); // Shift toward white
                targetAlpha = Mathf.Lerp(targetAlpha, 1.0f, lineGlow[i]); // Maximize alpha

                // Thicken the line when hit for a punchy feel
                laneLines[i].startWidth = 0.06f + (lineGlow[i] * 0.06f);
                laneLines[i].endWidth = 0.06f + (lineGlow[i] * 0.06f);
            }
            else
            {
                laneLines[i].startWidth = 0.06f;
                laneLines[i].endWidth = 0.06f;
            }

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(currentCol, 0.0f), new GradientColorKey(currentCol, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(targetAlpha, 1.0f) }
            );
            laneLines[i].colorGradient = grad;
        }
    }

    public IEnumerator FlashWarningRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // Drives the global warning pulse value instead of directly overwriting colors
            warningPulse = (Mathf.Sin(elapsed * 15f) + 1f) / 2f;
            yield return null;
        }
        warningPulse = 0f; // Reset perfectly
    }

    // --- HELPER METHODS ---
    public int GetLaneCount() => laneCount;
    public float GetLaneSpacing() => laneSpacing;
    public bool IsTransitioning() => transitioning;

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
        float total = (laneCount - 1) * laneSpacing;
        float offset = total / 2f - lane * laneSpacing;
        Vector2 pos = PerpDir() * offset + MoveDir() * dist;
        return new Vector3(pos.x, pos.y, 0f);
    }

    public Vector3 SpawnPos(int lane) => LaneWorldPos(lane, spawnDist);
    public Vector3 HitPos(int lane) => LaneWorldPos(lane, hitDist);

    public void BeginRotating() { rotateCo = StartCoroutine(RotateLoop()); }
    public void StopRotating() { if (rotateCo != null) StopCoroutine(rotateCo); }

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

        int currentIndex = 0;
        float minDiff = float.MaxValue;
        for (int i = 0; i < Angles.Length; i++)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(from, Angles[i]));
            if (diff < minDiff) { minDiff = diff; currentIndex = i; }
        }

        int nextIndex = currentIndex;
        while (nextIndex == currentIndex) { nextIndex = Random.Range(0, Angles.Length); }
        float next = Angles[nextIndex];

        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            CurrentAngle = Mathf.LerpAngle(from, next, t / transitionDuration);
            yield return null;
        }
        CurrentAngle = next;

        if (PlayerController.Instance != null) UpdatePlayerControls(CurrentAngle);
        transitioning = false;
    }

    void UpdatePlayerControls(float targetAngle)
    {
        float normalizedAngle = Mathf.Repeat(targetAngle, 360f);

        if (normalizedAngle >= 315f || normalizedAngle < 45f)
            PlayerController.Instance.SetControlsForRotation(PlayerController.RotationState.Normal);
        else if (normalizedAngle >= 45f && normalizedAngle < 135f)
            PlayerController.Instance.SetControlsForRotation(PlayerController.RotationState.Rotated90);
        else if (normalizedAngle >= 135f && normalizedAngle < 225f)
            PlayerController.Instance.SetControlsForRotation(PlayerController.RotationState.Rotated180);
        else
            PlayerController.Instance.SetControlsForRotation(PlayerController.RotationState.Rotated270);
    }
}