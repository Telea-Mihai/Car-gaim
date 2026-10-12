using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaceManager : MonoBehaviour
{
    public List<Racer> Racers;
    public List<Checkpoint> Checkpoints = new List<Checkpoint>();
    [Header("Race Settings")]
    public bool loops = true;
    public int laps = 3;
    public int secondsCountDown = 3;

    [Header("Start Sequence")]
    public float GridWidth;
    public float GridSpacing;
    public float offsetFromStart = 5f;

    [System.Serializable]
    public struct RacePartTaker
    {
        public Racer racer;
        public List<float> lapTimes;
        public float timeAtLastLap;
        public int lapsCompleted;
        public float bestLapTime;
        public bool finished;

        public RacePartTaker(Racer racer, List<float> lapTimes, float timeAtLastLap, int lapsCompleted)
        {
            this.racer = racer;
            this.lapTimes = lapTimes;
            this.timeAtLastLap = timeAtLastLap;
            this.lapsCompleted = lapsCompleted;
            bestLapTime = 0f;
            finished = false;
        }
    }

    [Header("Race evidence")]
    [SerializeField] private Dictionary<Racer, RacePartTaker> racePartTakers = new Dictionary<Racer, RacePartTaker>();

    [Header("Baked Path")]
    public BakedRacePath bakedRacePath;

    [Tooltip("When true and a valid baked path is assigned, AI uses RacePathFollower and live Racer pathing is disabled.")]
    public bool useBakedPath = true;

    [Tooltip("Parameters used by Race Path Baker.")]
    public RacePathBakeSettings bakeSettings = RacePathBakeSettings.Default;

    public event System.Action<Racer> OnLapCompleted;
    public event System.Action<Racer> OnRaceFinished;

    public float RaceStartTime { get; private set; }
    public bool RaceStarted { get; private set; }

    readonly List<Racer> finishOrder = new List<Racer>();
    readonly List<Racer> standingsBuffer = new List<Racer>();
    private UIManager uiManager;
    private Racer playerRacer;

    void Start()
    {
        uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager == null)
            Debug.LogWarning("No UIManager found. RaceManager will not show UI.");

        ResolvePlayerRacer();
    }

    /// <summary>
    /// Place racers on the grid and hold them. Does not start the countdown.
    /// </summary>
    public void PrepareGrid()
    {
        ResolvePlayerRacer();
        LoadRace();
    }

    /// <summary>
    /// Begin countdown and release cars. Call after the player presses Start Race.
    /// </summary>
    public void BeginRace()
    {
        if (RaceStarted)
            return;

        if (racePartTakers.Count == 0)
            PrepareGrid();

        StartCoroutine(RaceCountdown());
    }

    void Update()
    {
        if (!RaceStarted || uiManager == null)
            return;

        if (playerRacer == null)
            ResolvePlayerRacer();

        if (playerRacer == null)
            return;

        if (!racePartTakers.TryGetValue(playerRacer, out RacePartTaker taker) || taker.finished)
            return;

        uiManager.SetCurrentLapTime(GetCurrentLapTime(playerRacer));
    }

    void ResolvePlayerRacer()
    {
        if (playerRacer != null || uiManager == null || uiManager.activeVehicle == null)
            return;

        Vehicle vehicle = uiManager.activeVehicle;
        playerRacer = vehicle.GetComponent<Racer>();
        if (playerRacer == null)
            playerRacer = vehicle.GetComponentInParent<Racer>();
        if (playerRacer == null)
            playerRacer = vehicle.GetComponentInChildren<Racer>();
    }

    void OnValidate()
    {
        if (bakeSettings.sampleSpacing < 0.1f)
            bakeSettings = RacePathBakeSettings.Default;
    }

    public void LoadCheckpoints()
    {
        Checkpoints.Clear();
        Checkpoint[] children = GetComponentsInChildren<Checkpoint>();

        foreach (Checkpoint child in children)
        {
            Checkpoints.Add(child);
        }
        Debug.Log("Loaded " + Checkpoints.Count + " checkpoints.");
    }

    void LoadRace()
    {
        if (Checkpoints == null || Checkpoints.Count == 0)
            return;

        for (int i = 1; i < Checkpoints.Count - 1; i++)
        {
            Checkpoints[i].nextCheckpoint = Checkpoints[i + 1];
            Checkpoints[i].previousCheckpoint = Checkpoints[i - 1];
        }

        Checkpoints[0].previousCheckpoint = loops ? Checkpoints[Checkpoints.Count - 1] : null;
        Checkpoints[0].nextCheckpoint = Checkpoints[1];
        Checkpoints[Checkpoints.Count - 1].nextCheckpoint = loops ? Checkpoints[0] : null;
        Checkpoints[Checkpoints.Count - 1].previousCheckpoint = Checkpoints[Checkpoints.Count - 2];

        if (loops)
        {
            Checkpoints[0].isFinish = true;
            Checkpoints[0].OnCheckpointReached = NotifyLapCompleted;
        }
        else
        {
            Checkpoints[Checkpoints.Count - 1].isFinish = true;
            Checkpoints[Checkpoints.Count - 1].OnCheckpointReached = NotifyLapCompleted;
        }

        bool baked = useBakedPath && bakedRacePath != null && bakedRacePath.IsValid;

        if (Racers == null)
            return;

        // Race start is the beginning of the bake (checkpoint 0 → 1), never the closed-loop seam at TotalLength.
        float startS = 0f;
        Vector3 startPos = Checkpoints[0] != null ? Checkpoints[0].transform.position : Vector3.zero;
        
        Vector3 startTangent = Checkpoints[0] != null ? Checkpoints[0].transform.forward : Vector3.forward;
        if (baked && bakedRacePath.PointCount > 0)
        {
            startS = 0f;
            if (bakedRacePath.tangents != null && bakedRacePath.tangents.Length > 0
                && bakedRacePath.tangents[0].sqrMagnitude > 0.01f)
            {
                startTangent = Vector3.ProjectOnPlane(bakedRacePath.tangents[0], Vector3.up).normalized;
            }

            if (bakedRacePath.points != null && bakedRacePath.points.Length > 0)
            {
                // Use path point for lateral grid origin so cars sit on the racing line.
                startPos = bakedRacePath.points[0];
            }
        }

        Vector3 forward = startTangent.sqrMagnitude > 0.01f ? startTangent.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        finishOrder.Clear();
        racePartTakers.Clear();

        for (int i = 0; i < Racers.Count; i++)
        {
            Racer racer = Racers[i];
            if (racer == null)
                continue;

            // Grid behind the start along -forward (path space), not world -Z.
            float lateral = (i % 2 == 0 ? -1f : 1f) * GridWidth * 0.5f;
            float back = i * GridSpacing + offsetFromStart;
            Vector3 gridPos = startPos - forward * back + right * lateral;
            gridPos.y = startPos.y + 2f;
            racer.transform.position = gridPos;
            racer.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            racer.target = Checkpoints[0].transform;

            racer.GetComponent<Vehicle>().forceHoldStop();

            racePartTakers[racer] = new RacePartTaker(
                racer,
                new List<float>(),
                0f,
                0
            );
            
            PlayerLinePath playerLinePath = racer.GetComponent<PlayerLinePath>();
            Debug.Log(racer.name);
            if (playerLinePath != null)
            {
                playerLinePath.SetPath(bakedRacePath);
            }

            var follower = racer.GetComponent<RacePathFollower>();
            if (baked)
            {
                if (follower == null)
                    continue;

                follower.SetPath(bakedRacePath);
                // Snap to start progress (cars sit slightly behind startS along -forward).
                float carS = BakedPathMath.AdvanceS(bakedRacePath, startS, -back);
                // follower.SnapToS(carS, alignFacing: true);
                follower.enabled = true;
                racer.SetLivePathingEnabled(false);
            }
            else
            {
                if (follower != null)
                    follower.enabled = false;
                racer.SetLivePathingEnabled(false);
                Debug.LogWarning(
                    "[RaceManager] No valid BakedRacePath — race AI will not drive. " +
                    "Assign an asset and click Bake Race Path in the inspector.",
                    this);
            }
            
           
        }

        PushPlayerHud(false);
    }

    private IEnumerator RaceCountdown()
    {
        for (int i = 0; i < secondsCountDown; i++)
        {
            if (uiManager)
                uiManager.setCountDownTimer((secondsCountDown - i).ToString());
            yield return new WaitForSeconds(1);
        }

        RaceStartTime = Time.time;
        RaceStarted = true;
        List<Racer> keys = new List<Racer>(racePartTakers.Keys);
        foreach (Racer key in keys)
        {
            RacePartTaker taker = racePartTakers[key];
            taker.timeAtLastLap = RaceStartTime;
            racePartTakers[key] = taker;

            Vehicle vehicle = taker.racer != null ? taker.racer.GetComponent<Vehicle>() : null;
            if (vehicle != null)
                vehicle.startCar();
        }
        if (uiManager)
        {
            uiManager.setCountDownTimer("GO!");
            PushPlayerHud(true);
            yield return new WaitForSeconds(1);
            uiManager.setCountDownTimer("");
        }
    }

    void NotifyLapCompleted(Racer racer)
    {
        OnLapCompletedBy(racer);
        OnLapCompleted?.Invoke(racer);
    }

    private void OnLapCompletedBy(Racer racer)
    {
        if (racer == null || !RaceStarted)
            return;

        if (!racePartTakers.TryGetValue(racer, out RacePartTaker racePartTaker))
            return;

        if (racePartTaker.finished)
            return;

        float now = Time.time;
        float lapTime = now - racePartTaker.timeAtLastLap;
        racePartTaker.lapTimes.Add(lapTime);
        racePartTaker.timeAtLastLap = now;
        racePartTaker.lapsCompleted++;

        if (racePartTaker.bestLapTime <= 0f || lapTime < racePartTaker.bestLapTime && racePartTaker.lapsCompleted > 1) 
            racePartTaker.bestLapTime = lapTime;

        bool justFinished = racePartTaker.lapsCompleted >= Mathf.Max(1, laps);
        if (justFinished)
        {
            racePartTaker.finished = true;
            if (!finishOrder.Contains(racer))
                finishOrder.Add(racer);
        }

        racePartTakers[racer] = racePartTaker;
        Debug.Log($"Lap {racePartTaker.lapsCompleted}/{laps} completed by {racer.name} in {lapTime:F3}s");

        if (racer == playerRacer)
            PushPlayerHud(true);

        if (justFinished)
            OnRaceFinished?.Invoke(racer);
    }

    void PushPlayerHud(bool includeCompletedLapStats)
    {
        if (uiManager == null || playerRacer == null)
            return;

        if (!racePartTakers.TryGetValue(playerRacer, out RacePartTaker taker))
            return;

        int position = GetRacePosition(playerRacer);
        int total = Mathf.Max(1, GetRacerCount());
        int currentLap = taker.lapsCompleted;
        float lastLap = taker.lapTimes != null && taker.lapTimes.Count > 0
            ? taker.lapTimes[taker.lapTimes.Count - 1]
            : 0f;
        float bestLap = taker.bestLapTime;
        float delta = 0f;
        bool hasDelta = false;

        if (includeCompletedLapStats && taker.lapTimes != null && taker.lapTimes.Count > 0 && bestLap > 0f)
        {
            // Delta vs previous best (before this lap), approximated as last - best.
            // If this lap is the new best, show improvement against the prior best.
            if (taker.lapTimes.Count == 1)
            {
                delta = 0f;
                hasDelta = false;
            }
            else if (Mathf.Approximately(lastLap, bestLap))
            {
                float priorBest = float.MaxValue;
                for (int i = 0; i < taker.lapTimes.Count - 1; i++)
                    priorBest = Mathf.Min(priorBest, taker.lapTimes[i]);
                delta = lastLap - priorBest;
                hasDelta = priorBest < float.MaxValue;
            }
            else
            {
                delta = lastLap - bestLap;
                hasDelta = true;
            }
        }

        uiManager.UpdateRaceHud(
            position,
            total,
            currentLap,
            laps,
            GetCurrentLapTime(playerRacer),
            lastLap,
            bestLap,
            delta,
            hasDelta,
            includeCompletedLapStats);
    }


    public int GetRacePosition(Racer racer)
    {
        if (racer == null || !racePartTakers.ContainsKey(racer))
            return 0;

        standingsBuffer.Clear();
        foreach (var pair in racePartTakers)
        {
            if (pair.Key != null)
                standingsBuffer.Add(pair.Key);
        }

        standingsBuffer.Sort(CompareStandings);

        int index = standingsBuffer.IndexOf(racer);
        return index < 0 ? 0 : index + 1;
    }

    int CompareStandings(Racer a, Racer b)
    {
        RacePartTaker ta = racePartTakers[a];
        RacePartTaker tb = racePartTakers[b];

        // Finished racers keep finish-order ranking among themselves.
        if (ta.finished && tb.finished)
            return finishOrder.IndexOf(a).CompareTo(finishOrder.IndexOf(b));
        if (ta.finished != tb.finished)
            return ta.finished ? -1 : 1;

        if (ta.lapsCompleted != tb.lapsCompleted)
            return tb.lapsCompleted.CompareTo(ta.lapsCompleted);

        // Same laps: earlier last-lap timestamp = ahead.
        return ta.timeAtLastLap.CompareTo(tb.timeAtLastLap);
    }

    public int GetFinishPosition(Racer racer)
    {
        int index = finishOrder.IndexOf(racer);
        if (index >= 0)
            return index + 1;
        return GetRacePosition(racer);
    }

    public float GetFinishTime(Racer racer)
    {
        if (!racePartTakers.TryGetValue(racer, out RacePartTaker taker))
            return RaceStarted ? Time.time - RaceStartTime : 0f;

        if (taker.finished && taker.lapTimes != null && taker.lapTimes.Count > 0)
        {
            float total = 0f;
            for (int i = 0; i < taker.lapTimes.Count; i++)
                total += taker.lapTimes[i];
            return total;
        }

        return RaceStarted ? Time.time - RaceStartTime : 0f;
    }

    public float GetCurrentLapTime(Racer racer)
    {
        if (!RaceStarted || !racePartTakers.TryGetValue(racer, out RacePartTaker taker) || taker.finished)
            return 0f;
        return Mathf.Max(0f, Time.time - taker.timeAtLastLap);
    }

    public float GetLastLapTime(Racer racer)
    {
        if (!racePartTakers.TryGetValue(racer, out RacePartTaker taker) || taker.lapTimes == null || taker.lapTimes.Count == 0)
            return 0f;
        return taker.lapTimes[taker.lapTimes.Count - 1];
    }

    public float GetBestLapTime(Racer racer)
    {
        if (!racePartTakers.TryGetValue(racer, out RacePartTaker taker))
            return 0f;
        return taker.bestLapTime;
    }

    public int GetLapsCompleted(Racer racer)
    {
        if (!racePartTakers.TryGetValue(racer, out RacePartTaker taker))
            return 0;
        return taker.lapsCompleted;
    }

    public bool HasFinished(Racer racer)
    {
        return racePartTakers.TryGetValue(racer, out RacePartTaker taker) && taker.finished;
    }

    public int GetRacerCount()
    {
        return Racers != null ? Racers.Count : 0;
    }

    void OnDrawGizmos()
    {
        if (bakedRacePath != null)
            bakedRacePath.DrawGizmos(new Color(0.1f, 1f, 0.4f, 0.85f), 0.4f);
    }
}
