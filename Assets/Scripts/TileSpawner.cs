using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TileSpawner : MonoBehaviour
{
    public static TileSpawner Instance { get; private set; }

    [HideInInspector] public float spawnInterval = 1.0f;
    [HideInInspector] public float tileSpeed = 7f;
    [HideInInspector] public bool endlessMode = false;

    // Beat detection
    [Header("Beat Sync")]
    public float bpm = 120f;           // Set this to match your music
    public bool useBeatSpawn = true;  // If false, falls back to interval-based

    readonly List<Tile> activeTiles = new List<Tile>();
    Coroutine spawnCo;
    GameMode mode;

    // Weighted lane patterns — varies spawning so it feels rhythmic
    static readonly int[][] Patterns = {
        new[]{0},new[]{1},new[]{2},new[]{3},          // singles
        new[]{0,2},new[]{1,3},new[]{0,3},              // doubles
        new[]{1,2},new[]{0,1},new[]{2,3},
        new[]{0,1,2},new[]{1,2,3},                     // triples (rare)
    };

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    public void Init(GameMode m)
    {
        mode = m;
        switch (m)
        {
            // Easy  → "Can't Stop the Feeling" Justin Timberlake (113 BPM)
            case GameMode.Easy:
                spawnInterval = 60f / 113f; tileSpeed = 5f; bpm = 113f;
                break;
            // Medium → "Uptown Funk" Bruno Mars (115 BPM)
            case GameMode.Medium:
                spawnInterval = 60f / 115f; tileSpeed = 7f; bpm = 115f;
                break;
            // Hard   → "Runaway Baby" Bruno Mars (176 BPM)
            // Spawn every 2 beats (half-note) so it's tough but readable
            case GameMode.Hard:
                spawnInterval = 60f / 176f * 2f; tileSpeed = 11f; bpm = 176f;
                break;
            // Endless → dynamic BPM set by GameManager.UpdateEndless()
            case GameMode.Endless:
                spawnInterval = 1.2f; tileSpeed = 5f; bpm = 90f;
                endlessMode = true;
                break;
        }
    }

    public void BeginSpawning() { StopSpawning(); spawnCo = StartCoroutine(SpawnLoop()); }
    public void StopSpawning() { if (spawnCo != null) StopCoroutine(spawnCo); }

    IEnumerator SpawnLoop()
    {
        var tc = TrackController.Instance;

        // Pre-travel offset: tiles need time to travel from spawn to hit zone
        // so we spawn early by travelTime = distance / speed
        float travelTime = tc != null
            ? Mathf.Abs(tc.spawnDist - tc.hitDist) / tileSpeed
            : 1.5f;

        // Wait until the first beat lands on the receptor
        yield return new WaitForSeconds(Mathf.Max(0.1f, travelTime));

        int beatCount = 0;

        while (true)
        {
            SpawnBeat(beatCount);
            beatCount++;

            // Recalculate every beat — allows Endless mode to update BPM live
            float beatInterval = 60f / Mathf.Max(60f, bpm);
            yield return new WaitForSeconds(beatInterval);
        }
    }

    void SpawnBeat(int beat)
    {
        if (!TrackController.Instance) return;

        int inBar = beat % 4;
        int bar = beat / 4;

        // ── RHYTHMIC RULES per mode ──────────────────────────────────────────
        // Easy (113 BPM, Can't Stop the Feeling): every beat is a party beat
        //   spawn on 1 & 3 always, 2 & 4 at 40%
        // Medium (115 BPM, Uptown Funk): syncopated — strong on 1, ghost on 2,
        //   backbeat 3, anticipation before 4
        // Hard (176 BPM, Runaway Baby): rockabilly — driving on 1&3 (kick),
        //   snare hits on 2&4, 16th-note fills every 2 bars
        // Endless: purely density-driven

        bool spawnThis;
        switch (mode)
        {
            case GameMode.Easy:
                spawnThis = (inBar == 0 || inBar == 2) || Random.value < 0.40f;
                break;
            case GameMode.Medium:
                // Uptown Funk groove: 1=yes, 2=ghost(25%), 3=yes, 4=anticipation(60%)
                spawnThis = inBar == 0 || inBar == 2
                    || (inBar == 1 && Random.value < 0.25f)
                    || (inBar == 3 && Random.value < 0.60f);
                break;
            case GameMode.Hard:
                // Runaway Baby: every beat hits (176 BPM, spawning on half-notes)
                // inBar 0=beat1(kick), 1=beat3(kick), 2=beat5..., cycling 8 half-notes
                // All half-note beats spawn, occasional 16th fill on bar boundaries
                spawnThis = true; // every half-note spawn slot is active
                // Extra 16th-note burst on every 4th bar: spawn double lane
                if (bar > 0 && bar % 4 == 0 && inBar == 3 && Random.value < 0.70f)
                {
                    SpawnNormalTile(Random.Range(0, 4));
                    SpawnNormalTile(Random.Range(0, 4));
                    return;
                }
                break;
            default: // Endless
                spawnThis = (inBar == 0 || inBar == 2) || Random.value < 0.45f;
                break;
        }

        if (!spawnThis) return;

        // Long hold tile: every 2 bars on the downbeat (feels intentional)
        if (inBar == 0 && bar > 0 && bar % 2 == 0 && Random.value < 0.55f)
        {
            SpawnLongTile(Random.Range(0, 4));
            return;
        }

        int[] lanes = PickPattern(inBar);
        foreach (int l in lanes)
            SpawnNormalTile(l);
    }

    int[] PickPattern(int inBar)
    {
        float multiChance = mode == GameMode.Easy ? 0.12f
                          : mode == GameMode.Medium ? 0.28f
                          : mode == GameMode.Hard ? 0.48f : 0.32f;

        // Downbeats (0,2) slightly more likely to be chords/doubles
        if (inBar == 0 || inBar == 2) multiChance += 0.10f;

        if (Random.value > multiChance)
            return new[] { Random.Range(0, 4) };
        else
            return Patterns[Random.Range(4, Patterns.Length)];
    }

    void SpawnNormalTile(int lane)
    {
        var go = new GameObject("Tile");
        var tile = go.AddComponent<Tile>();
        tile.Init(lane, tileSpeed, false, 0f);
        go.transform.position = TrackController.Instance.SpawnPos(lane);
        activeTiles.Add(tile);
        PlayerController.Instance?.RegisterTile(tile);
    }

    void SpawnLongTile(int lane)
    {
        var go = new GameObject("LongTile");
        var tile = go.AddComponent<Tile>();
        float holdLen = Random.Range(2.5f, 5.0f); // longer hold tiles
        tile.Init(lane, tileSpeed, true, holdLen);
        go.transform.position = TrackController.Instance.SpawnPos(lane);
        activeTiles.Add(tile);
        PlayerController.Instance?.RegisterTile(tile);
    }

    public void RemoveTile(Tile t) => activeTiles.Remove(t);
    public List<Tile> GetActiveTiles() => activeTiles;
}