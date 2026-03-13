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
            case GameMode.Easy: spawnInterval = 1.3f; tileSpeed = 5f; bpm = 80f; break;
            case GameMode.Medium: spawnInterval = 0.9f; tileSpeed = 7f; bpm = 110f; break;
            case GameMode.Hard: spawnInterval = 0.55f; tileSpeed = 10f; bpm = 140f; break;
            case GameMode.Endless: spawnInterval = 1.2f; tileSpeed = 5f; bpm = 90f; endlessMode = true; break;
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

        float beatInterval = 60f / bpm;
        int beatCount = 0;

        while (true)
        {
            SpawnBeat(beatCount);
            beatCount++;

            // Wait exactly one beat before next spawn decision
            yield return new WaitForSeconds(beatInterval);
        }
    }

    void SpawnBeat(int beat)
    {
        if (!TrackController.Instance) return;

        int inBar = beat % 4;   // 0=beat1, 1=beat2, 2=beat3, 3=beat4

        // Musical pattern per bar:
        // Beat 0 (1st): always spawn — downbeat
        // Beat 1 (2nd): spawn if medium+ or random 30%
        // Beat 2 (3rd): always spawn — backbeat
        // Beat 3 (4th): spawn 50% — anticipation / off-beat

        bool spawnThis = inBar == 0 || inBar == 2
            || (inBar == 1 && (mode != GameMode.Easy || Random.value < 0.30f))
            || (inBar == 3 && Random.value < 0.50f);

        if (!spawnThis) return;

        // Long tile: spawn at the START of every 2 bars (beat 0 of bar 0, 2, 4...)
        // Only on the downbeat so it feels musical
        int bar = beat / 4;
        if (inBar == 0 && bar > 0 && bar % 2 == 0 && Random.value < 0.60f)
        {
            SpawnLongTile(Random.Range(0, 4));
            return;
        }

        // Normal tiles
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