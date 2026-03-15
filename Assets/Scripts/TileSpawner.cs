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

    [Header("Audio Calibration")]
    public float globalAudioOffset = 0.0f;

    [HideInInspector] public float autoTrackOffset = 0.0f;
    [HideInInspector] public float trackOffset = 0.0f;

    readonly List<Tile> activeTiles = new List<Tile>();
    GameMode mode;
    bool isSpawning = false;
    Coroutine spawnCo;
    Coroutine endlessCo;

    public struct BeatData
    {
        public float hitTime;
        public int tileCount;
        public bool isHold;
        public float holdDuration;
    }

    List<BeatData> proceduralBeatmap = new List<BeatData>();
    float clipLength;

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    public void Init(GameMode m)
    {
        mode = m;
        switch (m)
        {
            case GameMode.Easy: tileSpeed = 7.0f; break;
            case GameMode.Medium: tileSpeed = 12.0f; break;
            // Slightly slower approach rate for Hard to give the eyes more reaction time
            case GameMode.Hard: tileSpeed = 14.5f; break;
            case GameMode.Endless: tileSpeed = 8.5f; endlessMode = true; break;
        }
    }

    public void SetDynamicBPM(string clipName)
    {
        if (mode == GameMode.Endless) return;
        if (spawnCo != null) { StopCoroutine(spawnCo); spawnCo = null; }

        AudioClip clip = LoadClip(clipName);
        if (clip == null) return;

        bpm = DetectBPM(clip.name);
        clipLength = clip.length;

        trackOffset = 0.0f;
        string n = clip.name.ToLower();
        if (n.Contains("numb")) trackOffset = 0.15f;
        else if (n.Contains("overpass")) trackOffset = 0.1f;
        else if (n.Contains("pyramid")) trackOffset = 0.35f;
        else if (n.Contains("blueprint")) trackOffset = 0.25f;
        else if (n.Contains("baby")) trackOffset = 0.1f;
        else if (n.Contains("again")) trackOffset = 0.3f;
        else if (n.Contains("beggin")) trackOffset = 0.05f;

        Debug.Log($"[TileSpawner] Generating Spaced Rhythm for '{clip.name}'...");
        proceduralBeatmap = GenerateCleanBeatmap(clip);
        Debug.Log($"[TileSpawner] Mapped {proceduralBeatmap.Count} Readable Tiles!");

        if (isSpawning) spawnCo = StartCoroutine(ProceduralSpawnLoop());
    }

    List<BeatData> GenerateCleanBeatmap(AudioClip clip)
    {
        List<BeatData> beats = new List<BeatData>();

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        int sampleRate = clip.frequency * clip.channels;
        int samplesPerWindow = 1024;
        int totalWindows = samples.Length / samplesPerWindow;

        float[] bassFlux = new float[totalWindows];
        float[] trebleFlux = new float[totalWindows];
        float[] midFlux = new float[totalWindows];
        float[] midRaw = new float[totalWindows];

        float lastLow = 0f, lastHigh = 0f, lastMid = 0f;
        float currentLowSample = 0f, currentMidSample = 0f;

        float globalMaxFlux = 0.001f;
        float globalMaxMid = 0.001f;
        float firstMajorPeakTime = -1f;

        for (int i = 0; i < totalWindows; i++)
        {
            float lowSum = 0f, midSum = 0f, highSum = 0f;

            for (int j = 0; j < samplesPerWindow; j++)
            {
                float sample = samples[i * samplesPerWindow + j];

                currentLowSample = currentLowSample + 0.02f * (sample - currentLowSample);
                currentMidSample = currentMidSample + 0.15f * (sample - currentMidSample);
                float trueHigh = sample - currentMidSample;
                float trueMid = currentMidSample - currentLowSample;

                lowSum += currentLowSample * currentLowSample;
                midSum += Mathf.Abs(trueMid);
                highSum += trueHigh * trueHigh;
            }

            float cLow = Mathf.Sqrt(lowSum / samplesPerWindow);
            float cMid = Mathf.Sqrt(midSum / samplesPerWindow);
            float cHigh = Mathf.Sqrt(highSum / samplesPerWindow);
            float rMid = midSum / samplesPerWindow;

            bassFlux[i] = Mathf.Max(0, cLow - lastLow);
            trebleFlux[i] = Mathf.Max(0, cHigh - lastHigh);
            midFlux[i] = Mathf.Max(0, cMid - lastMid);
            midRaw[i] = rMid;

            if (bassFlux[i] > globalMaxFlux) globalMaxFlux = Mathf.Lerp(globalMaxFlux, bassFlux[i], 0.1f);
            if (trebleFlux[i] > globalMaxFlux) globalMaxFlux = Mathf.Lerp(globalMaxFlux, trebleFlux[i], 0.1f);
            if (midFlux[i] > globalMaxFlux) globalMaxFlux = Mathf.Lerp(globalMaxFlux, midFlux[i], 0.1f);
            if (rMid > globalMaxMid) globalMaxMid = Mathf.Lerp(globalMaxMid, rMid, 0.1f);

            if (firstMajorPeakTime < 0f && (bassFlux[i] > 0.05f || trebleFlux[i] > 0.05f || midFlux[i] > 0.05f))
            {
                firstMajorPeakTime = (float)(i * samplesPerWindow) / sampleRate;
            }

            lastLow = cLow; lastHigh = cHigh; lastMid = cMid;
        }

        if (firstMajorPeakTime > 1.5f) firstMajorPeakTime = 0f;

        autoTrackOffset = (firstMajorPeakTime >= 0f ? firstMajorPeakTime : 0f) + globalAudioOffset + trackOffset;

        float currentHitTime = autoTrackOffset;
        int beatIndex = 0;
        float stepSize = 15f / bpm;

        float lastChordTime = -99f;
        float lastHoldTime = -99f;

        while (currentHitTime < clip.length - 1.5f)
        {
            int windowIndex = Mathf.Clamp((int)(currentHitTime * sampleRate / samplesPerWindow), 0, totalWindows - 1);

            float peakBass = 0f, peakTreble = 0f, peakMid = 0f;
            float avgBass = 0f, avgTreble = 0f, avgMid = 0f;
            int validW = 0;

            for (int w = -2; w <= 2; w++)
            {
                int idx = Mathf.Clamp(windowIndex + w, 0, totalWindows - 1);
                avgBass += bassFlux[idx]; avgTreble += trebleFlux[idx]; avgMid += midFlux[idx];
                if (bassFlux[idx] > peakBass) peakBass = bassFlux[idx];
                if (trebleFlux[idx] > peakTreble) peakTreble = trebleFlux[idx];
                if (midFlux[idx] > peakMid) peakMid = midFlux[idx];
                validW++;
            }

            float bassProminence = (peakBass - (avgBass / validW)) / globalMaxFlux;
            float trebleProminence = (peakTreble - (avgTreble / validW)) / globalMaxFlux;
            float midProminence = (peakMid - (avgMid / validW)) / globalMaxFlux;
            float midVolume = midRaw[windowIndex] / globalMaxMid;

            int inBar16th = beatIndex % 16;
            bool isQuarter = (inBar16th % 4 == 0);
            bool is8th = (inBar16th % 2 == 0);
            bool is16th = (inBar16th % 2 != 0);

            bool spawnThis = false;
            int tileCount = 1;
            bool spawnHold = false;
            float dynamicHoldLen = 0f;
            bool isWarmup = currentHitTime < (autoTrackOffset + 2.0f);

            if (mode == GameMode.Hard && !isWarmup)
            {
                if (isQuarter && (bassProminence > 0.05f || trebleProminence > 0.05f || midProminence > 0.05f || midVolume > 0.1f)) spawnThis = true;

                // 8ths: Made stricter to space out standard patterns
                if (is8th && !isQuarter && (bassProminence > 0.2f || trebleProminence > 0.2f || midProminence > 0.25f)) spawnThis = true;

                // 16ths: Made significantly stricter. Will only spawn on distinct, sharp drum rolls.
                if (is16th && (trebleProminence > 0.5f || bassProminence > 0.55f || midProminence > 0.6f)) spawnThis = true;

                if (spawnThis)
                {
                    if (isQuarter && midVolume > 0.4f && currentHitTime > lastHoldTime + (60f / bpm) * 2.5f)
                    {
                        float sustainDuration = 0f;
                        for (int futureSteps = 1; futureSteps <= 8; futureSteps++)
                        {
                            int fw = Mathf.Clamp((int)((currentHitTime + (futureSteps * stepSize)) * sampleRate / samplesPerWindow), 0, totalWindows - 1);
                            if (midRaw[fw] / globalMaxMid < 0.15f) break;
                            sustainDuration += stepSize;
                        }

                        if (sustainDuration >= (60f / bpm) * 1.0f)
                        {
                            spawnHold = true; dynamicHoldLen = sustainDuration;
                            lastHoldTime = currentHitTime + dynamicHoldLen;
                        }
                    }

                    // Chords: Increased minimum gap between double-notes from 0.3 to 0.5 seconds
                    if (!spawnHold && isQuarter && bassProminence > 0.35f && (currentHitTime - lastChordTime > 0.5f))
                    {
                        tileCount = 2; lastChordTime = currentHitTime;
                    }
                }
            }
            else if (mode == GameMode.Medium || isWarmup)
            {
                if (isQuarter && (bassProminence > 0.05f || trebleProminence > 0.05f || midProminence > 0.05f || midVolume > 0.1f)) spawnThis = true;
                if (is8th && !isQuarter && (trebleProminence > 0.35f || midProminence > 0.4f)) spawnThis = true;
            }
            else // Easy
            {
                if (isQuarter && (bassProminence > 0.05f || trebleProminence > 0.05f || midVolume > 0.1f)) spawnThis = true;
            }

            if (spawnThis)
            {
                beats.Add(new BeatData
                {
                    hitTime = currentHitTime,
                    tileCount = tileCount,
                    isHold = spawnHold,
                    holdDuration = dynamicHoldLen
                });
            }

            beatIndex++;
            currentHitTime += stepSize;
        }

        return beats;
    }

    IEnumerator ProceduralSpawnLoop()
    {
        float travelTime = CalcTravelTime();

        yield return new WaitUntil(() => GameManager.Instance != null && GameManager.Instance.GetMusicTime() > 0.01f);

        int beatIndex = 0;
        float[] laneNextFree = new float[4];
        float[] laneLastUsed = new float[4] { -99f, -99f, -99f, -99f };

        int lastHand = -1;
        float antiJackBuffer = (15f / bpm) * 1.5f;

        while (isSpawning && GameManager.Instance != null && GameManager.Instance.IsGameActive() && beatIndex < proceduralBeatmap.Count)
        {
            BeatData beat = proceduralBeatmap[beatIndex];
            float spawnTime = beat.hitTime - travelTime;

            if (spawnTime < 0.05f) { beatIndex++; continue; }

            yield return new WaitUntil(() => GameManager.Instance.GetMusicTime() >= spawnTime);

            List<int> availableLanes = new List<int>();
            bool isHoldActiveInAnyLane = false;

            for (int i = 0; i < 4; i++)
            {
                if (beat.hitTime >= laneNextFree[i]) availableLanes.Add(i);
                else if (laneNextFree[i] - beat.hitTime > 0.3f) isHoldActiveInAnyLane = true;
            }

            if (availableLanes.Count > 0)
            {
                int actualCount = Mathf.Min(beat.tileCount, availableLanes.Count);
                if (isHoldActiveInAnyLane) actualCount = 1;

                List<int> chosenLanes = new List<int>();

                if (actualCount == 1)
                {
                    List<int> preferred = new List<int>();

                    if (lastHand == 0) preferred = availableLanes.Where(l => l >= 2).ToList();
                    else if (lastHand == 1) preferred = availableLanes.Where(l => l < 2).ToList();

                    if (preferred.Count == 0) preferred = new List<int>(availableLanes);

                    preferred = preferred.OrderBy(l => laneLastUsed[l]).ToList();

                    int picked = preferred[0];
                    chosenLanes.Add(picked);
                    lastHand = (picked < 2) ? 0 : 1;
                }
                else if (actualCount >= 2)
                {
                    var lefts = availableLanes.Where(l => l < 2).OrderBy(l => laneLastUsed[l]).ToList();
                    var rights = availableLanes.Where(l => l >= 2).OrderBy(l => laneLastUsed[l]).ToList();

                    if (lefts.Count > 0 && rights.Count > 0)
                    {
                        chosenLanes.Add(lefts[0]);
                        chosenLanes.Add(rights[0]);
                    }
                    else
                    {
                        var sorted = availableLanes.OrderBy(l => laneLastUsed[l]).ToList();
                        chosenLanes.Add(sorted[0]);
                        chosenLanes.Add(sorted[1]);
                    }
                }

                if (chosenLanes.Count > 0)
                {
                    if (beat.isHold)
                    {
                        int l = chosenLanes[0];
                        laneNextFree[l] = beat.hitTime + beat.holdDuration + (antiJackBuffer * 2f);
                        laneLastUsed[l] = beat.hitTime;

                        SpawnLongTile(l, beat.holdDuration);

                        if (chosenLanes.Count > 1)
                        {
                            int tapLane = chosenLanes[1];
                            laneNextFree[tapLane] = beat.hitTime + antiJackBuffer;
                            laneLastUsed[tapLane] = beat.hitTime;
                            SpawnNormalTile(tapLane);
                        }
                    }
                    else
                    {
                        foreach (int l in chosenLanes)
                        {
                            laneNextFree[l] = beat.hitTime + antiJackBuffer;
                            laneLastUsed[l] = beat.hitTime;
                            SpawnNormalTile(l);
                        }
                    }
                }
            }

            beatIndex++;
        }
    }

    public void BeginSpawning()
    {
        isSpawning = true;
        if (endlessMode) endlessCo = StartCoroutine(EndlessLoop());
        else if (proceduralBeatmap.Count > 0) spawnCo = StartCoroutine(ProceduralSpawnLoop());
    }

    public void StopSpawning()
    {
        isSpawning = false;
        if (spawnCo != null) { StopCoroutine(spawnCo); spawnCo = null; }
        if (endlessCo != null) { StopCoroutine(endlessCo); endlessCo = null; }
    }

    IEnumerator EndlessLoop()
    {
        yield return new WaitForSeconds(1.5f);
        int beat = 0; int lastEndlessLane = -1;
        while (isSpawning)
        {
            float density = Mathf.Clamp01((tileSpeed - 5f) / 11f);
            int inBar = beat % 4;
            if ((inBar == 0 || inBar == 2) || Random.value < 0.35f + density * 0.3f)
            {
                if (beat > 4 && beat % 8 == 0 && Random.value < 0.4f + density * 0.2f)
                {
                    int l = Random.Range(0, 4); SpawnLongTile(l, Random.Range(1.5f, 3.5f)); lastEndlessLane = l;
                }
                else
                {
                    int l1 = Random.Range(0, 4); if (l1 == lastEndlessLane) l1 = (l1 + 1) % 4;
                    SpawnNormalTile(l1); lastEndlessLane = l1;
                    if (density > 0.5f && Random.value < density * 0.35f)
                    {
                        int l2 = Random.Range(0, 4); if (l2 != l1) SpawnNormalTile(l2);
                    }
                }
            }
            beat++; yield return new WaitForSeconds(60f / Mathf.Max(60f, bpm));
        }
    }

    float CalcTravelTime() => TrackController.Instance != null ? Mathf.Abs(TrackController.Instance.spawnDist - TrackController.Instance.hitDist) / tileSpeed : 15f / tileSpeed;

    float DetectBPM(string name)
    {
        string n = name.ToLower();
        if (n.Contains("runaway") || n.Contains("baby")) return 164f;
        if (n.Contains("blueprint") || n.Contains("supreme")) return 176f;
        if (n.Contains("uptown") || n.Contains("funk")) return 115f;
        if (n.Contains("numb")) return 110f;
        if (n.Contains("overpass")) return 176f;
        if (n.Contains("pyramid")) return 94f;
        if (n.Contains("beggin")) return 129f;
        return mode == GameMode.Hard ? 140f : mode == GameMode.Medium ? 115f : 90f;
    }

    AudioClip LoadClip(string clipName)
    {
        string modeName = mode.ToString();
        var sub = Resources.LoadAll<AudioClip>("Music/" + modeName);
        if (sub != null && sub.Length > 0)
        {
            string s = clipName.ToLower();
            return sub.FirstOrDefault(c => c.name.ToLower().Contains(s)) ?? sub.FirstOrDefault(c => s.Contains(c.name.ToLower())) ?? sub[0];
        }
        return Resources.Load<AudioClip>("Music/" + modeName);
    }

    void SpawnNormalTile(int lane)
    {
        if (!TrackController.Instance) return;
        var go = new GameObject("Tile"); var tile = go.AddComponent<Tile>();
        tile.Init(lane, tileSpeed, false, 0f); go.transform.position = TrackController.Instance.SpawnPos(lane);
        activeTiles.Add(tile); PlayerController.Instance?.RegisterTile(tile);
    }

    void SpawnLongTile(int lane, float holdLen)
    {
        if (!TrackController.Instance) return;
        var go = new GameObject("LongTile"); var tile = go.AddComponent<Tile>();
        tile.Init(lane, tileSpeed, true, holdLen); go.transform.position = TrackController.Instance.SpawnPos(lane);
        activeTiles.Add(tile); PlayerController.Instance?.RegisterTile(tile);
    }

    public void RemoveTile(Tile t) => activeTiles.Remove(t);
    public List<Tile> GetActiveTiles() => activeTiles;
}