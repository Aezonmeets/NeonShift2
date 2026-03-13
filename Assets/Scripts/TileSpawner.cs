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
    public float bpm = 120f;           
    public bool useBeatSpawn = true;  

    [Header("Endless Difficulty")]
    public float bpmIncreaseRate = 0.5f; 
    public float speedIncreaseRate = 0.05f;

    readonly List<Tile> activeTiles = new List<Tile>();
    Coroutine spawnCo;
    GameMode mode;
    private bool isSpawning = false;

    // --- NEW: This prevents tiles from being "sikit" (overlapping) ---
    private float[] laneBlockedTimer = new float[4];

    static readonly int[][] Patterns = {
        new[]{0},new[]{1},new[]{2},new[]{3},           // singles
        new[]{0,2},new[]{1,3},new[]{0,3},              // doubles
        new[]{1,2},new[]{0,1},new[]{2,3},
        new[]{0,1,2},new[]{1,2,3},                     // triples
    };

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    public void Init(GameMode m)
    {
        mode = m;
        switch (m)
        {
            case GameMode.Easy: spawnInterval = 1.3f; tileSpeed = 5f; bpm = 80f; endlessMode = false; break;
            case GameMode.Medium: spawnInterval = 0.9f; tileSpeed = 7f; bpm = 110f; endlessMode = false; break;
            case GameMode.Hard: spawnInterval = 0.55f; tileSpeed = 10f; bpm = 140f; endlessMode = false; break;
            case GameMode.Endless: spawnInterval = 0.50f; tileSpeed = 11f; bpm = 145f; endlessMode = true; break;
        }
    }

    public void BeginSpawning() 
    { 
        StopSpawning(); 
        isSpawning = true;
        
        // Reset lane blockers when we start
        for (int i = 0; i < 4; i++) laneBlockedTimer[i] = 0f;
        
        spawnCo = StartCoroutine(SpawnLoop()); 
    }
    
    public void StopSpawning() 
    { 
        isSpawning = false;
        if (spawnCo != null) StopCoroutine(spawnCo); 
    }

    void Update()
    {
        if (isSpawning && endlessMode)
        {
            bpm += bpmIncreaseRate * Time.deltaTime;       
            tileSpeed += speedIncreaseRate * Time.deltaTime; 
        }

        // --- NEW: Count down the blocked lanes ---
        for (int i = 0; i < 4; i++)
        {
            if (laneBlockedTimer[i] > 0)
                laneBlockedTimer[i] -= Time.deltaTime;
        }
    }

    IEnumerator SpawnLoop()
    {
        var tc = TrackController.Instance;

        float travelTime = tc != null
            ? Mathf.Abs(tc.spawnDist - tc.hitDist) / tileSpeed
            : 1.5f;

        yield return new WaitForSeconds(Mathf.Max(0.1f, travelTime));

        int beatCount = 0;

        while (true)
        {
            float beatInterval = 60f / bpm; 

            SpawnBeat(beatCount);
            beatCount++;

            yield return new WaitForSeconds(beatInterval);
        }
    }

    void SpawnBeat(int beat)
    {
        if (!TrackController.Instance) return;

        int inBar = beat % 4;   

        bool spawnThis = inBar == 0 || inBar == 2
            || (inBar == 1 && (mode != GameMode.Easy || Random.value < 0.30f))
            || (inBar == 3 && Random.value < 0.50f);

        if (!spawnThis) return;

        int bar = beat / 4;
        if (inBar == 0 && bar > 0 && bar % 2 == 0 && Random.value < 0.60f)
        {
            // --- FIX: Only spawn a long tile in a lane that isn't currently blocked! ---
            List<int> freeLanes = new List<int>();
            for (int i = 0; i < 4; i++) 
            {
                if (laneBlockedTimer[i] <= 0f) freeLanes.Add(i);
            }

            if (freeLanes.Count > 0)
            {
                int chosenLane = freeLanes[Random.Range(0, freeLanes.Count)];
                SpawnLongTile(chosenLane);
            }
            return;
        }

        // Normal tiles
        int[] lanes = PickPattern(inBar);
        foreach (int l in lanes)
        {
            // --- FIX: Only spawn normal tiles if the lane isn't busy with a long tile! ---
            if (laneBlockedTimer[l] <= 0f)
            {
                SpawnNormalTile(l);
            }
        }
    }

    int[] PickPattern(int inBar)
    {
        float multiChance = mode == GameMode.Easy ? 0.12f
                          : mode == GameMode.Medium ? 0.28f
                          : mode == GameMode.Hard ? 0.48f 
                          : 0.35f; 

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
        
        // --- FIX: Make the length scale with speed so they don't squish together ---
        // This calculates how long the tile will take in seconds (0.5 to 1.2 seconds)
        float holdTime = Random.Range(0.5f, 1.2f); 
        float holdLen = holdTime * tileSpeed; 
        
        tile.Init(lane, tileSpeed, true, holdLen);
        go.transform.position = TrackController.Instance.SpawnPos(lane);
        activeTiles.Add(tile);
        PlayerController.Instance?.RegisterTile(tile);

        // --- FIX: Block this lane! No normal tiles can spawn here until the long tile is gone + a tiny gap ---
        laneBlockedTimer[lane] = holdTime + 0.15f; 
    }

    public void RemoveTile(Tile t) => activeTiles.Remove(t);
    public List<Tile> GetActiveTiles() => activeTiles;
}