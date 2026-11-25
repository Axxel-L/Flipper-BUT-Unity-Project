using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Pilote la partie : score, combo, billes, meilleurs scores et fin de partie.
/// Créé automatiquement au chargement de la scène, il branche aussi le tableau
/// de score et les événements météo.
/// </summary>
public sealed class PinballGameManager : MonoBehaviour
{
    public static PinballGameManager Instance { get; private set; }

    [Header("Partie")]
    [Min(1)] public int startingBalls = 3;
    public string defaultPlayerName = "AXEL L";

    [Header("Points")]
    [Min(1)] public int treePoints = 25;
    [Min(1)] public int iutPoints = 75;
    [Min(1)] public int bumperPoints = 100;
    [Min(1)] public int maxBumperCombo = 5;
    [Min(0.1f)] public float comboWindow = 2f;
    [Min(1f)] public float stormMultiplier = 2f;

    [Header("Anti-blocage")]
    [Tooltip("Nombre d'impacts sur le même bumper dans la fenêtre avant de propulser la bille.")]
    [Min(2)] public int stuckHits = 3;
    [Min(0.2f)] public float stuckWindow = 1.6f;
    [Min(0f)] public float kickSpeed = 46f;

    public int Score { get; private set; }
    public int Balls { get; private set; }
    public string PlayerName { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool StormActive { get; private set; }
    public int BumperCombo { get; private set; }
    public float InputLockUntil { get; private set; }
    public bool CanLaunch => !IsGameOver && Time.time >= InputLockUntil;
    public bool HasLaunched => hasLaunched;
    public bool ComboActive => Time.time - lastBumperTime <= comboWindow && BumperCombo > 1;

    public HighScore Top1 { get; private set; }
    public HighScore Top2 { get; private set; }

    public event Action ScoreChanged;
    public event Action StateChanged;
    public event Action<string> ToastRequested;

    public struct HighScore
    {
        public string name;
        public int score;
    }

    private float lastBumperTime = -99f;
    private bool hasLaunched;
    private bool initialized;

    private readonly Vector3[] positionSamples = new Vector3[8];
    private int sampleCursor;
    private int sampleCount;
    private float sampleTimer;
    private float nextWatchdogKickTime;
    private Vector3 lastObstacle;
    private float lastImpactTime = -99f;

    private ScoreboardUI scoreboard;
    private SnowStormEvent storm;
    private Coroutine resetRoutine;
    private Rigidbody ball;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        PinballGameManager existing = Instance;
        if (existing == null)
        {
            existing = FindFirstObjectByType<PinballGameManager>();
        }

        if (existing == null)
        {
            GameObject host = new GameObject("Flipper Game");
            existing = host.AddComponent<PinballGameManager>();
        }

        existing.Initialize();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        PlayerName = PlayerPrefs.GetString("flipper.playerName", defaultPlayerName);
        LoadHighScores();

        Score = 0;
        Balls = startingBalls;
        IsGameOver = false;
        BumperCombo = 0;

        scoreboard = GetComponent<ScoreboardUI>();
        if (scoreboard == null)
        {
            scoreboard = gameObject.AddComponent<ScoreboardUI>();
        }

        storm = GetComponent<SnowStormEvent>();
        if (storm == null)
        {
            storm = gameObject.AddComponent<SnowStormEvent>();
        }

        storm.Bind(this);
        scoreboard.Bind(this);

        ScoreChanged?.Invoke();
        StateChanged?.Invoke();
    }

    public void AddScore(int basePoints, Vector3? worldPosition = null, string label = null)
    {
        if (basePoints <= 0 || IsGameOver)
        {
            return;
        }

        int points = Mathf.RoundToInt(basePoints * (StormActive ? stormMultiplier : 1f));
        Score = Score > int.MaxValue - points ? int.MaxValue : Score + points;
        ScoreChanged?.Invoke();

        if (worldPosition.HasValue && scoreboard != null)
        {
            scoreboard.ShowPopup(points, worldPosition.Value, label);
        }
    }

    private void Update()
    {
        if (IsGameOver)
        {
            sampleCount = 0;
            sampleTimer = 0f;
            return;
        }

        Rigidbody rb = Ball;
        if (rb == null || !rb.useGravity)
        {
            sampleCount = 0;
            sampleTimer = 0f;
            return;
        }

        if (rb.linearVelocity.magnitude > 5f)
        {
            hasLaunched = true;
        }

        if (!hasLaunched)
        {
            return;
        }

        // Détecteur de blocage global : si la bille tourne en rond ou tape
        // toujours le même décor, sa position varie très peu sur ~2 s.
        sampleTimer += Time.deltaTime;
        if (sampleTimer < 0.35f)
        {
            return;
        }

        sampleTimer = 0f;
        positionSamples[sampleCursor] = rb.position;
        sampleCursor = (sampleCursor + 1) % positionSamples.Length;
        if (sampleCount < positionSamples.Length)
        {
            sampleCount++;
        }

        if (sampleCount < 6 || Time.time < nextWatchdogKickTime)
        {
            return;
        }

        int oldestIndex = (sampleCursor - sampleCount + positionSamples.Length) % positionSamples.Length;
        Vector3 oldest = positionSamples[oldestIndex];

        if ((rb.position - oldest).sqrMagnitude < 2.25f)
        {
            nextWatchdogKickTime = Time.time + 2.5f;
            TryKickStuckBall(rb, true);
        }
    }

    /// <summary>Mémorise le dernier obstacle touché pour orienter un déblocage.</summary>
    public void NotifyImpact(Vector3 obstacleCenter)
    {
        lastObstacle = obstacleCenter;
        lastImpactTime = Time.time;
    }

    public void RegisterBumperHit(Vector3 worldPosition)
    {
        if (Time.time - lastBumperTime <= comboWindow)
        {
            BumperCombo = Mathf.Min(BumperCombo + 1, maxBumperCombo);
        }
        else
        {
            BumperCombo = 1;
        }

        lastBumperTime = Time.time;

        int points = bumperPoints * BumperCombo;
        AddScore(points, worldPosition, BumperCombo > 1 ? "COMBO x" + BumperCombo : null);
    }

    /// <summary>Propulse la bille dans une direction aléatoire à pleine puissance.</summary>
    public void TryKickStuckBall(Rigidbody target, bool force = false)
    {
        Rigidbody rb = target != null ? target : Ball;
        if (rb == null)
        {
            return;
        }

        float now = Time.time;
        float magnitude = rb.linearVelocity.magnitude;

        if (!force && now < nextKickTime)
        {
            return;
        }

        Vector3 direction;
        Vector3 away = rb.position - lastObstacle;
        away.y = 0f;

        if (Time.time - lastImpactTime < 0.9f && away.sqrMagnitude > 0.04f)
        {
            away.Normalize();
            direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-65f, 65f), 0f) * away;
        }
        else
        {
            direction = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
        }

        direction.y = 0f;
        float speed = Mathf.Max(kickSpeed, magnitude);

        rb.WakeUp();
        rb.linearVelocity = direction * speed;
        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 8f;

        nextKickTime = now + 2.5f;
        Debug.Log($"[Anti-blocage] bille propulsée à {speed:F1} m/s dans {direction}.");
        ToastRequested?.Invoke("DÉBLOCAGE !");
    }

    public void SetStorm(bool active)
    {
        if (StormActive == active)
        {
            return;
        }

        StormActive = active;
        StateChanged?.Invoke();
    }

    public void RegisterDrain()
    {
        if (IsGameOver || resetRoutine != null)
        {
            return;
        }

        Balls = Mathf.Max(0, Balls - 1);

        if (Balls <= 0)
        {
            GameOver();
            return;
        }

        StateChanged?.Invoke();
        ToastRequested?.Invoke($"BILLE PERDUE  -  {Balls} RESTANTE" + (Balls > 1 ? "S" : ""));
        resetRoutine = StartCoroutine(ResetAfterDelay(1.1f));
    }

    private IEnumerator ResetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ResetBallNow();
        resetRoutine = null;
        StateChanged?.Invoke();
    }

    public void ResetBallNow()
    {
        Rigidbody rb = Ball;
        Transform start = FindStartPosition();
        if (rb == null || start == null)
        {
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;

        TrailRenderer ballTrail = rb.GetComponent<TrailRenderer>();
        if (ballTrail != null)
        {
            ballTrail.emitting = false;
            ballTrail.Clear();
        }

        rb.position = start.position;
        rb.rotation = start.rotation;
        rb.transform.SetPositionAndRotation(start.position, start.rotation);
        Physics.SyncTransforms();

        if (ballTrail != null)
        {
            ballTrail.emitting = true;
        }

        BumperCombo = 0;
        lastBumperTime = -99f;
        hasLaunched = false;
        sampleCount = 0;
        sampleCursor = 0;
        sampleTimer = 0f;
        nextWatchdogKickTime = Time.time + 1f;
    }

    private void GameOver()
    {
        IsGameOver = true;
        BumperCombo = 0;

        Rigidbody rb = Ball;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
        }

        UpdateHighScores();
        StateChanged?.Invoke();
        scoreboard?.ShowGameOver();
    }

    public void RestartGame()
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }

        Score = 0;
        Balls = startingBalls;
        IsGameOver = false;
        BumperCombo = 0;
        lastBumperTime = -99f;
        hasLaunched = false;
        InputLockUntil = Time.time + 0.35f;

        ResetBallNow();
        scoreboard?.HideGameOver();

        ScoreChanged?.Invoke();
        StateChanged?.Invoke();
    }

    private void UpdateHighScores()
    {
        int best = Score;

        if (best > Top1.score)
        {
            Top2 = Top1;
            Top1 = new HighScore { name = PlayerName, score = best };
        }
        else if (best > Top2.score)
        {
            Top2 = new HighScore { name = PlayerName, score = best };
        }

        SaveHighScores();
    }

    private void LoadHighScores()
    {
        Top1 = new HighScore
        {
            name = PlayerPrefs.GetString("flipper.top1.name", "AXEL L"),
            score = PlayerPrefs.GetInt("flipper.top1.score", 20000)
        };
        Top2 = new HighScore
        {
            name = PlayerPrefs.GetString("flipper.top2.name", "MARC"),
            score = PlayerPrefs.GetInt("flipper.top2.score", 12000)
        };
    }

    private void SaveHighScores()
    {
        PlayerPrefs.SetString("flipper.top1.name", Top1.name);
        PlayerPrefs.SetInt("flipper.top1.score", Top1.score);
        PlayerPrefs.SetString("flipper.top2.name", Top2.name);
        PlayerPrefs.SetInt("flipper.top2.score", Top2.score);
        PlayerPrefs.Save();
    }

    private Rigidbody Ball
    {
        get
        {
            if (ball == null)
            {
                GameObject found = GameObject.FindWithTag("Ball");
                if (found != null)
                {
                    ball = found.GetComponent<Rigidbody>();
                }
            }

            return ball;
        }
    }

    private static Transform FindStartPosition()
    {
        BallOutOfBounds outOfBounds = FindFirstObjectByType<BallOutOfBounds>();
        if (outOfBounds != null && outOfBounds.startPosition != null)
        {
            return outOfBounds.startPosition;
        }

        GameObject fallback = GameObject.Find("BallStartPosition");
        return fallback != null ? fallback.transform : null;
    }

    private float nextKickTime;
}
