using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TileSpawner : MonoBehaviour
{
    public static TileSpawner Instance { get; private set; }

    [HideInInspector] public float spawnInterval = 1.0f;
    [HideInInspector] public float tileSpeed = 7f;
    [HideInInspector] public float bpm = 120f;
    [HideInInspector] public bool endlessMode = false;

    readonly List<Tile> activeTiles = new List<Tile>();
    GameMode mode;
    bool isSpawning = false;
    Coroutine spawnCo;
    Coroutine endlessCo;

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    public void Init(GameMode m)
    {
        mode = m;
        switch (m)
        {
            case GameMode.Easy: tileSpeed = 5f; break;
            case GameMode.Medium: tileSpeed = 7.5f; break;
            case GameMode.Hard: tileSpeed = 11.5f; break;
            case GameMode.Endless: tileSpeed = 6f; endlessMode = true; break;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  CALLED BY GAMEMANAGER BEFORE EACH SONG
    // ══════════════════════════════════════════════════════════════════
    public void SetDynamicBPM(string clipName)
    {
        if (mode == GameMode.Endless) return;

        if (spawnCo != null) { StopCoroutine(spawnCo); spawnCo = null; }

        AudioClip clip = LoadClip(clipName);
        if (clip == null)
        {
            Debug.LogWarning($"[TileSpawner] Clip '{clipName}' not found — using default BPM.");
            bpm = DetectBPM(clipName);
            return;
        }

        bpm = DetectBPM(clip.name);
        Debug.Log($"[TileSpawner] '{clip.name}' | BPM={bpm:F0}");

        sectionEnergy = BuildSectionEnergy(clip, out sectionBlockSec);
        clipLength = clip.length;

        if (isSpawning)
            spawnCo = StartCoroutine(BeatSpawnLoop());
    }

    // ── Section energy (one float per ~0.5s of audio) ─────────────────
    float[] sectionEnergy;
    float sectionBlockSec;
    float clipLength;

    float[] BuildSectionEnergy(AudioClip clip, out float blockSec)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        int sr = clip.frequency * clip.channels;
        blockSec = 0.5f;
        int blockSize = (int)(sr * blockSec);
        int blocks = samples.Length / blockSize;
        float[] energy = new float[blocks];

        for (int b = 0; b < blocks; b++)
        {
            float sum = 0f;
            for (int i = 0; i < blockSize; i++)
                sum += samples[b * blockSize + i] * samples[b * blockSize + i];
            energy[b] = Mathf.Sqrt(sum / blockSize);
        }

        for (int b = 1; b < blocks - 1; b++)
            energy[b] = (energy[b - 1] + energy[b] + energy[b + 1]) / 3f;

        return energy;
    }

    float GetSectionIntensity(float musicTime)
    {
        if (sectionEnergy == null || sectionEnergy.Length == 0) return 0.5f;
        int idx = Mathf.Clamp((int)(musicTime / sectionBlockSec), 0, sectionEnergy.Length - 1);

        float val = sectionEnergy[idx];
        float min = sectionEnergy.Min();
        float max = sectionEnergy.Max();
        return max > min ? Mathf.Clamp01((val - min) / (max - min)) : 0.5f;
    }

    // ══════════════════════════════════════════════════════════════════
    //  BEAT SPAWN LOOP
    // ══════════════════════════════════════════════════════════════════
    IEnumerator BeatSpawnLoop()
    {
        float beatSec = 60f / bpm;
        float offset = DetectOffset(bpm);
        float travelTime = CalcTravelTime();

        yield return new WaitUntil(() =>
            GameManager.Instance != null && GameManager.Instance.GetMusicTime() > 0.01f);

        yield return new WaitUntil(() =>
            GameManager.Instance.GetMusicTime() >= offset);

        int beat = 0;
        float[] laneNextFree = new float[4];
        int lastLane = -1;
        int lastLaneRun = 0;

        while (isSpawning && GameManager.Instance != null && GameManager.Instance.IsGameActive())
        {
            float musicNow = GameManager.Instance.GetMusicTime();
            float intensity = GetSectionIntensity(musicNow);

            int inBar = beat % 4;

            // ── SPAWN DECISION ─────────────────────────────────────────
            bool doSpawn;
            switch (mode)
            {
                case GameMode.Easy:
                    doSpawn = (inBar == 0 || inBar == 2) && intensity > 0.20f;
                    break;
                case GameMode.Medium:
                    doSpawn = (inBar == 0 || inBar == 2)
                           || (inBar == 1 && intensity > 0.60f)
                           || (inBar == 3 && intensity > 0.70f);
                    break;
                default: // Hard
                    // Raised threshold to 0.40f for off-beats to give verses more breathing room
                    doSpawn = (inBar == 0 || inBar == 2) || (intensity > 0.40f);
                    break;
            }

            if (doSpawn)
            {
                int tileCount = 1;
                bool spawnOffBeat = false;

                if (mode == GameMode.Medium && intensity > 0.75f && inBar == 0) tileCount = 2;

                if (mode == GameMode.Hard)
                {
                    // 1. Doubles ONLY on Beat 1, raised threshold to 0.75f (only heavy drops)
                    if (intensity > 0.75f && inBar == 0)
                        tileCount = 2;

                    // 2. Off-beats on Beats 2 & 4. Raised threshold to 0.85f so it only 
                    // triggers at the absolute peak of the song, preventing stamina drain.
                    if (intensity > 0.85f && (inBar == 1 || inBar == 3) && tileCount == 1)
                        spawnOffBeat = true;
                }

                bool spawnLong = false;
                float holdLen = 0f;
                float holdBase = Mathf.Lerp(2.5f, 1.2f, Mathf.Clamp01((bpm - 60f) / 140f));

                if (inBar == 0 && beat > 0 && beat % 8 == 0 && intensity > 0.65f && Random.value > 0.60f)
                {
                    spawnLong = true;
                    holdLen = holdBase;
                    tileCount = Mathf.Min(tileCount, 2);
                }

                // ── Lane selection ─────────────────────────────────────
                List<int> spawnedLanes = new List<int>();

                if (spawnLong)
                {
                    int lane = PickFreeLane(laneNextFree, musicNow);
                    if (lane >= 0)
                    {
                        laneNextFree[lane] = musicNow + holdLen;
                        SpawnLongTile(lane, holdLen);
                        spawnedLanes.Add(lane);
                    }
                }
                else
                {
                    var lanes = GetFreeLanes(laneNextFree, musicNow, tileCount);
                    if (tileCount == 1 && lastLaneRun >= 2 && lanes.Count > 1) lanes.Remove(lastLane);

                    float gap = beatSec * 0.4f;
                    foreach (int l in lanes)
                    {
                        laneNextFree[l] = musicNow + gap;
                        SpawnNormalTile(l);
                        spawnedLanes.Add(l);
                    }

                    if (lanes.Count == 1)
                    {
                        if (lanes[0] == lastLane) lastLaneRun++;
                        else { lastLane = lanes[0]; lastLaneRun = 1; }
                    }
                    else { lastLane = -1; lastLaneRun = 0; }
                }

                // ── SPAWN THE EXTRA "FASTER" TILE ──
                if (spawnOffBeat && spawnedLanes.Count > 0)
                {
                    StartCoroutine(SpawnDelayedTile(beatSec * 0.5f, spawnedLanes[0]));
                }
            }

            beat++;

            float nextBeatTime = offset + beat * beatSec;
            yield return new WaitUntil(() =>
                GameManager.Instance == null
                || !GameManager.Instance.IsGameActive()
                || GameManager.Instance.GetMusicTime() >= nextBeatTime);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  SPAWNING CONTROL
    // ══════════════════════════════════════════════════════════════════
    public void BeginSpawning()
    {
        isSpawning = true;
        if (endlessMode)
        {
            endlessCo = StartCoroutine(EndlessLoop());
        }
        else if (sectionEnergy != null)
        {
            spawnCo = StartCoroutine(BeatSpawnLoop());
        }
    }

    public void StopSpawning()
    {
        isSpawning = false;
        if (spawnCo != null) { StopCoroutine(spawnCo); spawnCo = null; }
        if (endlessCo != null) { StopCoroutine(endlessCo); endlessCo = null; }
    }

    // ── Endless: coroutine with live BPM ─────────────────────────────
    IEnumerator EndlessLoop()
    {
        yield return new WaitForSeconds(1.5f);
        int beat = 0;
        while (isSpawning)
        {
            float density = Mathf.Clamp01((tileSpeed - 5f) / 11f);
            int inBar = beat % 4;
            bool spawn = (inBar == 0 || inBar == 2) || Random.value < 0.35f + density * 0.3f;
            if (spawn)
            {
                if (beat > 4 && beat % 8 == 0 && Random.value < 0.4f + density * 0.2f)
                    SpawnLongTile(Random.Range(0, 4), Random.Range(1.5f, 3.5f));
                else
                {
                    SpawnNormalTile(Random.Range(0, 4));
                    if (density > 0.5f && Random.value < density * 0.35f)
                        SpawnNormalTile(Random.Range(0, 4));
                }
            }
            beat++;
            yield return new WaitForSeconds(60f / Mathf.Max(60f, bpm));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════

    // Spawns a single tile halfway between beats, FORCED into a different lane
    IEnumerator SpawnDelayedTile(float delaySec, int avoidLane)
    {
        yield return new WaitForSeconds(delaySec);

        if (isSpawning && GameManager.Instance != null && GameManager.Instance.IsGameActive())
        {
            int nextLane;
            do
            {
                nextLane = Random.Range(0, 4);
            } while (nextLane == avoidLane);

            SpawnNormalTile(nextLane);
        }
    }

    float CalcTravelTime()
        => TrackController.Instance != null
            ? Mathf.Abs(TrackController.Instance.spawnDist - TrackController.Instance.hitDist) / tileSpeed
            : 15f / tileSpeed;

    float DetectOffset(float songBPM) => Mathf.Max(0.05f, 60f / songBPM * 0.25f);

    float DetectBPM(string name)
    {
        string n = name.ToLower();
        if (n.Contains("runaway") || n.Contains("baby")) return 176f;
        if (n.Contains("blueprint") || n.Contains("supreme")) return 140f;
        if (n.Contains("uptown") || n.Contains("funk")) return 115f;
        if (n.Contains("feeling") || n.Contains("timberlake")) return 113f;
        if (n.Contains("blinding") || n.Contains("lights")) return 171f;
        if (n.Contains("levitat")) return 103f;
        if (n.Contains("titanium")) return 126f;
        if (n.Contains("numb")) return 110f;
        if (n.Contains("believer")) return 124f;
        if (n.Contains("thunder")) return 168f;
        if (n.Contains("enemy")) return 148f;
        if (n.Contains("bad guy") || n.Contains("badguy")) return 135f;
        if (n.Contains("stay")) return 170f;
        if (n.Contains("heat wave")) return 81f;
        if (n.Contains("shape") || n.Contains("of you")) return 96f;
        if (n.Contains("perfect")) return 95f;
        if (n.Contains("dance monkey")) return 98f;
        if (n.Contains("watermelon")) return 95f;
        if (n.Contains("industry")) return 103f;
        return mode == GameMode.Hard ? 140f : mode == GameMode.Medium ? 115f : 90f;
    }

    AudioClip LoadClip(string clipName)
    {
        string modeName = mode.ToString();
        var sub = Resources.LoadAll<AudioClip>("Music/" + modeName);
        if (sub != null && sub.Length > 0)
        {
            string s = clipName.ToLower();
            return sub.FirstOrDefault(c => c.name.ToLower().Contains(s))
                ?? sub.FirstOrDefault(c => s.Contains(c.name.ToLower()))
                ?? sub[0];
        }
        return Resources.Load<AudioClip>("Music/" + modeName);
    }

    int PickFreeLane(float[] next, float t)
    {
        var free = Enumerable.Range(0, 4).Where(i => t >= next[i]).ToList();
        return free.Count > 0 ? free[Random.Range(0, free.Count)] : -1;
    }

    List<int> GetFreeLanes(float[] next, float t, int count)
        => Enumerable.Range(0, 4).Where(i => t >= next[i])
            .OrderBy(_ => Random.value).Take(count).ToList();

    void SpawnNormalTile(int lane)
    {
        if (!TrackController.Instance) return;
        var go = new GameObject("Tile"); var tile = go.AddComponent<Tile>();
        tile.Init(lane, tileSpeed, false, 0f);
        go.transform.position = TrackController.Instance.SpawnPos(lane);
        activeTiles.Add(tile); PlayerController.Instance?.RegisterTile(tile);
    }

    void SpawnLongTile(int lane, float holdLen)
    {
        if (!TrackController.Instance) return;
        var go = new GameObject("LongTile"); var tile = go.AddComponent<Tile>();
        tile.Init(lane, tileSpeed, true, holdLen);
        go.transform.position = TrackController.Instance.SpawnPos(lane);
        activeTiles.Add(tile); PlayerController.Instance?.RegisterTile(tile);
    }

    public void RemoveTile(Tile t) => activeTiles.Remove(t);
    public List<Tile> GetActiveTiles() => activeTiles;
}