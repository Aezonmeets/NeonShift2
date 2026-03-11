using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TileSpawner : MonoBehaviour
{
    public static TileSpawner Instance { get; private set; }

    public float spawnInterval = 1.1f;
    public float tileSpeed     = 5.5f;
    public bool  endlessMode   = false;

    static readonly int[][] PatEasy = {
        new[]{0}, new[]{2}, new[]{1}, new[]{3},
        new[]{0}, new[]{2}, new[]{0,3}, new[]{1},
        new[]{2}, new[]{1,2}
    };
    static readonly int[][] PatMedium = {
        new[]{0,2}, new[]{1}, new[]{3}, new[]{0},
        new[]{2,3}, new[]{1,2}, new[]{0}, new[]{1,3},
        new[]{2}, new[]{0,1,3}
    };
    static readonly int[][] PatHard = {
        new[]{0,1,2}, new[]{3}, new[]{1,3}, new[]{0,2},
        new[]{1,2,3}, new[]{0}, new[]{0,1,2,3}, new[]{2,3},
        new[]{0,1}, new[]{1,2}
    };

    int[][] pattern;
    int     patIdx;
    bool    spawning;
    Coroutine spawnCo;
    readonly List<Tile> active = new List<Tile>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Called by GameManager after mode is set
    public void Init(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Medium:  pattern = PatMedium; break;
            case GameMode.Hard:    pattern = PatHard;   break;
            default:               pattern = PatEasy;   break;
        }
    }

    public void BeginSpawning()
    {
        if (pattern == null) Init(GameMode.Easy); // fallback
        spawning = true;
        if (spawnCo != null) StopCoroutine(spawnCo);
        spawnCo = StartCoroutine(Loop());
    }

    public void StopSpawning()
    {
        spawning = false;
        if (spawnCo != null) { StopCoroutine(spawnCo); spawnCo = null; }
        foreach (var t in active) if (t) Destroy(t.gameObject);
        active.Clear();
    }

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(0.9f); // brief intro pause
        while (spawning)
        {
            Spawn();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void Spawn()
    {
        int[] lanes;
        if (endlessMode)
        {
            int n = Random.value < 0.28f ? 2 : 1;
            lanes = new int[n];
            for (int i = 0; i < n; i++) lanes[i] = Random.Range(0, 4);
        }
        else
        {
            lanes = pattern[patIdx % pattern.Length];
            patIdx++;
        }
        foreach (int l in lanes) SpawnOne(l);
    }

    void SpawnOne(int lane)
    {
        var tc = TrackController.Instance;
        if (tc == null) { Debug.LogWarning("TileSpawner: TrackController missing!"); return; }

        var go = new GameObject("Tile_" + lane);
        go.transform.position = tc.SpawnPos(lane);
        go.transform.rotation = Quaternion.Euler(0f, 0f, tc.CurrentAngle);

        var tile = go.AddComponent<Tile>();
        tile.Init(lane, tileSpeed);
        active.Add(tile);
        PlayerController.Instance?.RegisterTile(tile);
    }

    public void RemoveTile(Tile t) => active.Remove(t);
}
