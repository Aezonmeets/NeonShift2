using UnityEngine;
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

    [Header("Audio Calibration")]
    [Tooltip("If the tiles hit the line slightly AFTER the music beat, increase this number (e.g. 0.2). If they hit BEFORE, decrease it (-0.2).")]
    public float trackOffset = 0.0f;

    readonly List<Tile> activeTiles = new List<Tile>();
    GameMode mode;

    // Dynamic Beatmap Variables
    string currentSong = "";
    float spawnIntensity = 1.0f;

    // Rhythm Engine Variables
    bool isSpawning = false;
    float nextHitTime = 0f;
    int beatCount = 0;

    // Weighted lane patterns
    static readonly int[][] Patterns = {
        new[]{0},new[]{1},new[]{2},new[]{3},           // singles
        new[]{0,2},new[]{1,3},new[]{0,3},              // doubles
        new[]{1,2},new[]{0,1},new[]{2,3},
        new[]{0,1,2},new[]{1,2,3},                     // triples (rare)
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Init(GameMode m)
    {
        mode = m;
        switch (m)
        {
            case GameMode.Easy: tileSpeed = 5f; bpm = 100f; break;
            case GameMode.Medium: tileSpeed = 7f; bpm = 115f; break;
            case GameMode.Hard: tileSpeed = 10f; bpm = 176f; break;
            case GameMode.Endless: tileSpeed = 5f; bpm = 90f; endlessMode = true; break;
        }
    }

    public void SetDynamicBPM(string clipName)
    {
        if (mode == GameMode.Endless) return;

        currentSong = clipName.ToLower();
        spawnIntensity = 1.0f;
        beatCount = 0;

        // Custom MP3 Offsets
        trackOffset = 0.0f;
        if (currentSong.Contains("numb")) trackOffset = 0.15f;
        if (currentSong.Contains("overpass")) trackOffset = 0.1f;
        if (currentSong.Contains("pyramid")) trackOffset = 0.2f;
        if (currentSong.Contains("baby shark")) trackOffset = 0.1f;
        if (currentSong.Contains("again")) trackOffset = 0.3f;

        nextHitTime = trackOffset;
        UpdateSongTimestamps(0f);

        // --- SMART FAST-FORWARD FIX ---
        // Calculate exactly how many seconds a tile takes to fall
        float travelTime = TrackController.Instance != null
            ? Mathf.Abs(TrackController.Instance.spawnDist - TrackController.Instance.hitDist) / tileSpeed
            : 1.5f;

        // Skip any beats that are physically impossible to spawn because the song just started!
        while (nextHitTime < travelTime)
        {
            nextHitTime += (60f / bpm);
            beatCount++;
        }

        Debug.Log($"[TileSpawner] Loaded '{currentSong}'. Fast-forwarded to beat {beatCount} to prevent note stacking!");
    }

    public void BeginSpawning()
    {
        if (mode == GameMode.Endless)
        {
            beatCount = 0;
            nextHitTime = 0f;
        }
        isSpawning = true;
    }

    public void StopSpawning()
    {
        isSpawning = false;
    }

    void Update()
    {
        if (!isSpawning || GameManager.Instance == null || !GameManager.Instance.IsGameActive()) return;

        float musicTime = GameManager.Instance.GetMusicTime();
        if (musicTime == 0f && mode != GameMode.Endless) return;

        if (mode != GameMode.Endless)
        {
            UpdateSongTimestamps(musicTime);
        }

        float travelTime = TrackController.Instance != null
            ? Mathf.Abs(TrackController.Instance.spawnDist - TrackController.Instance.hitDist) / tileSpeed
            : 1.5f;

        // Fire a note exactly when it needs to leave the spawn point to hit the line on time
        while (musicTime >= (nextHitTime - travelTime))
        {
            SpawnBeat(beatCount);
            beatCount++;
            nextHitTime += (60f / bpm);
        }
    }

    void UpdateSongTimestamps(float currentTime)
    {
        if (mode == GameMode.Easy) UpdateEasyBeatmap(currentTime);
        else if (mode == GameMode.Medium) UpdateMediumBeatmap(currentTime);
        else if (mode == GameMode.Hard) UpdateHardBeatmap(currentTime);
    }

    void UpdateEasyBeatmap(float currentTime)
    {
        if (currentSong.Contains("tyler") || currentSong.Contains("again"))
        {
            if (currentTime < 133f) { bpm = 79f; tileSpeed = 4.5f; spawnIntensity = 0.4f; }
            else { bpm = 135f; tileSpeed = 7.5f; spawnIntensity = 1.0f; }
        }
        else if (currentSong.Contains("lau") || currentSong.Contains("start"))
        {
            if (currentTime < 42f) { bpm = 82f; tileSpeed = 4.5f; spawnIntensity = 0.5f; }
            else if (currentTime < 112f) { bpm = 82f; tileSpeed = 5.5f; spawnIntensity = 0.8f; }
            else { bpm = 164f; tileSpeed = 7.0f; spawnIntensity = 1.0f; }
        }
        else if (currentSong.Contains("upto") || currentSong.Contains("funk"))
        {
            if (currentTime < 64f) { bpm = 115f; tileSpeed = 5.5f; spawnIntensity = 0.6f; }
            else if (currentTime < 145f) { bpm = 115f; tileSpeed = 7.5f; spawnIntensity = 1.0f; }
            else if (currentTime < 204f) { bpm = 115f; tileSpeed = 4.0f; spawnIntensity = 0.2f; }
            else { bpm = 115f; tileSpeed = 8.5f; spawnIntensity = 1.0f; }
        }
    }

    void UpdateMediumBeatmap(float currentTime)
    {
        if (currentSong.Contains("arian"))
        {
            if (currentTime < 15f) { bpm = 108f; tileSpeed = 6.0f; spawnIntensity = 0.3f; }
            else if (currentTime < 50f) { bpm = 108f; tileSpeed = 6.5f; spawnIntensity = 0.7f; }
            else { bpm = 108f; tileSpeed = 8.0f; spawnIntensity = 1.0f; }
        }
        else if (currentSong.Contains("baby"))
        {
            if (currentTime < 45f) { bpm = 115f; tileSpeed = 6.5f; spawnIntensity = 0.5f; }
            else if (currentTime < 85f) { bpm = 125f; tileSpeed = 7.5f; spawnIntensity = 0.8f; }
            else { bpm = 135f; tileSpeed = 8.5f; spawnIntensity = 1.0f; }
        }
        else if (currentSong.Contains("numb"))
        {
            if (currentTime < 21f) { bpm = 110f; tileSpeed = 6.0f; spawnIntensity = 0.3f; }
            else if (currentTime < 46f) { bpm = 110f; tileSpeed = 6.5f; spawnIntensity = 0.6f; }
            else if (currentTime < 76f) { bpm = 110f; tileSpeed = 8.0f; spawnIntensity = 1.0f; }
            else if (currentTime < 101f) { bpm = 110f; tileSpeed = 6.5f; spawnIntensity = 0.6f; }
            else if (currentTime < 125f) { bpm = 110f; tileSpeed = 8.0f; spawnIntensity = 1.0f; }
            else if (currentTime < 145f) { bpm = 110f; tileSpeed = 7.0f; spawnIntensity = 0.6f; }
            else { bpm = 110f; tileSpeed = 8.5f; spawnIntensity = 1.0f; }
        }
    }

    void UpdateHardBeatmap(float currentTime)
    {
        if (currentSong.Contains("chari") || currentSong.Contains("pyramid"))
        {
            bpm = 188f;
            // 0:00 - Intro 
            if (currentTime < 13f) { tileSpeed = 6.0f; spawnIntensity = 0.2f; }
            // 0:13 - Verse 1 
            else if (currentTime < 38f) { tileSpeed = 7.5f; spawnIntensity = 0.4f; }
            // 0:38 - Pre-Chorus (Building)
            else if (currentTime < 63f) { tileSpeed = 8.5f; spawnIntensity = 0.6f; }
            // 1:03 - Chorus 1 (Big, but not max)
            else if (currentTime < 88f) { tileSpeed = 11.0f; spawnIntensity = 0.85f; }
            // 1:28 - Verse 2 / Iyaz Rap (Pull back)
            else if (currentTime < 114f) { tileSpeed = 9.5f; spawnIntensity = 0.65f; }
            // 1:54 - Pre-Chorus 2
            else if (currentTime < 127f) { tileSpeed = 10.0f; spawnIntensity = 0.75f; }
            // 2:07 - Chorus 2 (Pushing harder)
            else if (currentTime < 152f) { tileSpeed = 11.5f; spawnIntensity = 0.9f; }
            // 2:32 - Bridge Part 1 (The Void - Drop the intensity to build tension)
            else if (currentTime < 164f) { tileSpeed = 7.0f; spawnIntensity = 0.3f; }
            // 2:44 - Bridge Part 2 (The Build - Scaling slowly to medium)
            else if (currentTime < 177f) { tileSpeed = 9.0f; spawnIntensity = 0.6f; }
            // 2:57 - FINAL CHORUS - BOOM! (Max Speed & Flood Drop)
            else { tileSpeed = 13.0f; spawnIntensity = 1.0f; }
        }
        else if (currentSong.Contains("runa") || currentSong.Contains("baby"))
        {
            bpm = 164f;
            if (currentTime < 34f) { tileSpeed = 10.5f; spawnIntensity = 0.85f; }
            else if (currentTime < 57f) { tileSpeed = 11.5f; spawnIntensity = 1.0f; }
            else if (currentTime < 75f) { tileSpeed = 12.5f; spawnIntensity = 1.0f; }
            else if (currentTime < 130f) { tileSpeed = 6.0f; spawnIntensity = 0.25f; }
            else { tileSpeed = 13.0f; spawnIntensity = 1.0f; }
        }
        else if (currentSong.Contains("ed s") || currentSong.Contains("overpass"))
        {
            bpm = 176f;
            if (currentTime < 48f) { tileSpeed = 11.0f; spawnIntensity = 0.85f; }
            else if (currentTime < 70f) { tileSpeed = 12.0f; spawnIntensity = 1.0f; }
            else if (currentTime < 91f) { tileSpeed = 13.0f; spawnIntensity = 1.0f; }
            else if (currentTime < 120f) { tileSpeed = 10.0f; spawnIntensity = 0.7f; }
            else { tileSpeed = 13.5f; spawnIntensity = 1.0f; }
        }
        else if (currentSong.Contains("blueprint") || currentSong.Contains("skai"))
        {
            bpm = 176f;
            // 0:00 - Intro (Calm traditional sample)
            if (currentTime < 13f) { tileSpeed = 6.0f; spawnIntensity = 0.2f; }
            // 0:13 - Verse 1 (Beat drops, solid rhythm)
            else if (currentTime < 51f) { tileSpeed = 10.0f; spawnIntensity = 0.7f; }
            // 0:51 - Chorus 1 (First big hook)
            else if (currentTime < 77f) { tileSpeed = 12.0f; spawnIntensity = 1.0f; }
            // 1:17 - Verse 2 (Slight breather)
            else if (currentTime < 102f) { tileSpeed = 10.5f; spawnIntensity = 0.75f; }
            // 1:42 - Chorus 2 (Final massive hook)
            else if (currentTime < 127f) { tileSpeed = 12.5f; spawnIntensity = 1.0f; }
            // 2:07 - Outro (Beat stops, coast to the end)
            else { tileSpeed = 5.0f; spawnIntensity = 0.1f; }
        }
    }

    // --- DEAD AIR FIX ---
    void SpawnBeat(int beat)
    {
        if (!TrackController.Instance) return;

        int inBar = beat % 4; // Tracks the 4 beats in a measure (0, 1, 2, 3)
        int bar = beat / 4;

        bool spawnThis = false;

        switch (mode)
        {
            case GameMode.Easy:
                spawnThis = (inBar == 0 || inBar == 2);
                break;
            case GameMode.Medium:
                spawnThis = (inBar == 0 || inBar == 2) || (inBar == 1 && Random.value < 0.4f);
                break;
            case GameMode.Hard:
                // INTRO / QUIET: Always spawns a steady 2 notes per bar (No Dead Air!)
                if (spawnIntensity <= 0.3f)
                {
                    spawnThis = (inBar == 0 || inBar == 2);
                }
                // VERSE / BUILD UP: Spawns downbeats + 75% of the off-beats
                else if (spawnIntensity <= 0.7f)
                {
                    spawnThis = (inBar == 0 || inBar == 2) || Random.value < 0.75f;
                }
                // DROP / CLIMAX: Relentless notes
                else
                {
                    spawnThis = Random.value < 0.95f;
                }

                // Double Chords during Heavy Drops
                if (spawnThis && (inBar == 0 || inBar == 2) && spawnIntensity > 0.7f && Random.value < 0.60f)
                {
                    SpawnNormalTile(Random.Range(0, 4));
                    SpawnNormalTile(Random.Range(0, 4));
                    return;
                }
                break;
            case GameMode.Endless:
                spawnThis = (inBar == 0 || inBar == 2) || Random.value < 0.45f;
                break;
        }

        if (!spawnThis) return;

        // Long Hold Tiles
        if (inBar == 0 && bar > 0 && bar % 2 == 0 && Random.value < 0.55f && spawnIntensity > 0.5f)
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

        if (inBar == 0 || inBar == 2) multiChance += 0.10f;

        if (spawnIntensity <= 0.6f) multiChance = 0f;

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
        float holdLen = Random.Range(2.5f, 5.0f);
        tile.Init(lane, tileSpeed, true, holdLen);
        go.transform.position = TrackController.Instance.SpawnPos(lane);
        activeTiles.Add(tile);
        PlayerController.Instance?.RegisterTile(tile);
    }

    public void RemoveTile(Tile t) => activeTiles.Remove(t);
    public List<Tile> GetActiveTiles() => activeTiles;
}