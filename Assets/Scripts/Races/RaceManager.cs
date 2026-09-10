using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class RaceManager : MonoBehaviour
{
    public List<Racer> Racers;
    public List<Checkpoint> Checkpoints;
    [Header("Race Settings")] 
    public bool loops = true;
    public int laps = 3;
    public int secondsCountDown = 3;
    
    [Header("Start Sequence")]
    public float GridWidth;
    public float GridSpacing;


    [System.Serializable]
    public struct RacePartTaker
    {
        public Racer racer;
        public List<float> lapTimes;
        public float timeAtLastLap;
        public int lapsCompleted;
        
        public RacePartTaker(Racer racer, List<float> lapTimes, float timeAtLastLap, int lapsCompleted)
        {
            this.racer = racer;
            this.lapTimes = lapTimes;
            this.timeAtLastLap = timeAtLastLap;
            this.lapsCompleted = lapsCompleted;
        }
    }
    
    [Header("Race evidence")]
    [SerializeField] private Dictionary<Racer,RacePartTaker> racePartTakers = new Dictionary<Racer, RacePartTaker>();
    
    [Header("Baked Path")]
    public BakedRacePath bakedRacePath;

    [Tooltip("When true and a valid baked path is assigned, AI uses RacePathFollower and live Racer pathing is disabled.")]
    public bool useBakedPath = true;

    [Tooltip("Parameters used by Race Path Baker.")]
    public RacePathBakeSettings bakeSettings = RacePathBakeSettings.Default;
    
    public event System.Action<Racer> OnLapCompleted;

    public float RaceStartTime { get; private set; }
    public bool RaceStarted { get; private set; }

    readonly List<Racer> finishOrder = new List<Racer>();

    void Start()
    {
        RaceStart();
    }

    void OnValidate()
    {
        if (bakeSettings.sampleSpacing < 0.1f)
            bakeSettings = RacePathBakeSettings.Default;
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

        for (int i = 0; i < Racers.Count; i++)
        {
            Racer racer = Racers[i];
            if (racer == null)
                continue;

            // Grid behind the start along -forward (path space), not world -Z.
            float lateral = (i % 2 == 0 ? -1f : 1f) * GridWidth * 0.5f;
            float back = i * GridSpacing;
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
    }

    private void RaceStart()
    {
        LoadRace();
        StartCoroutine(RaceCountdown());
    }

    private IEnumerator RaceCountdown()
    {
        yield return new WaitForSeconds(secondsCountDown);
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
    }

    void NotifyLapCompleted(Racer racer)
    {
        OnLapCompletedBy(racer);
        OnLapCompleted?.Invoke(racer);
    }

    private void OnLapCompletedBy(Racer racer)
    {
        if (racer == null || finishOrder.Contains(racer))
            return;

        finishOrder.Add(racer);
        Debug.Log("Lap completed by " + racer.name);

        if (!racePartTakers.TryGetValue(racer, out RacePartTaker racePartTaker))
            return;

        float now = Time.time;
        float lapTime = RaceStarted ? now - racePartTaker.timeAtLastLap : now;
        racePartTaker.lapTimes.Add(lapTime);
        racePartTaker.timeAtLastLap = now;
        racePartTaker.lapsCompleted++;
        racePartTakers[racer] = racePartTaker;
    }

    public int GetFinishPosition(Racer racer)
    {
        int index = finishOrder.IndexOf(racer);
        return index < 0 ? 0 : index + 1;
    }

    public float GetFinishTime(Racer racer)
    {
        if (!racePartTakers.TryGetValue(racer, out RacePartTaker taker) || taker.lapTimes == null || taker.lapTimes.Count == 0)
            return RaceStarted ? Time.time - RaceStartTime : 0f;

        return taker.lapTimes[taker.lapTimes.Count - 1];
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

