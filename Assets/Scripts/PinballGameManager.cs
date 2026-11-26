using System;
using System.Collections;
using System.Collections.Generic;
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

    [Header("Voix")]
    [Range(0f, 1f)] public float ballLostVolume = 0.9f;
    [Range(0f, 1f)] public float gameOverVolume = 0.9f;

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

    /// <summary>Meilleurs scores locaux, du plus haut au plus bas.</summary>
    public IReadOnlyList<HighScore> Leaderboard => leaderboard;

    /// <summary>Meilleur score du joueur actuel (persisté par nom).</summary>
    public int PlayerBestScore { get; private set; }

    /// <summary>Meilleur score toutes parties confondues sur cette machine.</summary>
    public int BestScore { get; private set; }

    public int LastScore { get; private set; }
    public int GamesPlayed { get; private set; }

    public event Action ScoreChanged;
    public event Action StateChanged;
    public event Action<string> ToastRequested;

    [Serializable]
    public struct HighScore
    {
        public string name;
        public int score;
    }

    public const int MaxLeaderboardEntries = 8;

    private const string LeaderboardKey = "flipper.leaderboard.v1";
    private const string BestScoreKey = "flipper.bestScore";
    private const string LastScoreKey = "flipper.lastScore";
    private const string GamesPlayedKey = "flipper.gamesPlayed";
    private const string PlayerBestPrefix = "flipper.playerBest.";

    [Serializable]
    private sealed class LeaderboardData
    {
        public List<HighScore> scores = new List<HighScore>();
    }

    private static readonly Comparison<HighScore> HighScoreComparison = CompareHighScores;

    private readonly List<HighScore> leaderboard = new List<HighScore>(MaxLeaderboardEntries);

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
    private AudioSource voiceAudio;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        PinballGameManager existing = Instance;
        if (existing == null)
        {
            existing = FindAnyObjectByType<PinballGameManager>();
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
        BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        LastScore = PlayerPrefs.GetInt(LastScoreKey, 0);
        GamesPlayed = PlayerPrefs.GetInt(GamesPlayedKey, 0);
        PlayerBestScore = LoadPlayerBest(PlayerName);
        LoadLeaderboard();
        EnsureVoiceAudio();

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

        if (!hasLaunched && rb.linearVelocity.sqrMagnitude > 25f)
        {
            hasLaunched = true;
            ScoreChanged?.Invoke();
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

    private void EnsureVoiceAudio()
    {
        if (voiceAudio != null)
        {
            return;
        }

        // AudioSource dédiée sur un enfant : le vent de tempête utilise déjà
        // l'AudioSource du GameObject principal, on évite tout conflit.
        GameObject host = new GameObject("Voice Audio");
        host.transform.SetParent(transform, false);
        voiceAudio = host.AddComponent<AudioSource>();
        voiceAudio.playOnAwake = false;
        voiceAudio.spatialBlend = 0f;
        voiceAudio.dopplerLevel = 0f;
        voiceAudio.volume = 1f;

        ArcadeAudio.GetBallLost();
        ArcadeAudio.GetGameOverVoice();
    }

    private void PlayVoice(AudioClip clip, float volume)
    {
        if (voiceAudio == null || clip == null)
        {
            return;
        }

        voiceAudio.Stop();
        voiceAudio.PlayOneShot(clip, Mathf.Clamp01(volume));
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

        PlayVoice(ArcadeAudio.GetBallLost(), ballLostVolume);
        StateChanged?.Invoke();
        ToastRequested?.Invoke("BILLE PERDUE\n" + Balls + " RESTANTE" + (Balls > 1 ? "S" : ""));
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

        PlayVoice(ArcadeAudio.GetGameOverVoice(), gameOverVolume);
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

        if (voiceAudio != null)
        {
            voiceAudio.Stop();
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

    /// <summary>Termine immédiatement la partie (bouton Quit physique).</summary>
    public void EndGame()
    {
        if (IsGameOver)
        {
            return;
        }

        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }

        Balls = 0;
        scoreboard?.HideNameEntry();
        GameOver();
    }

    /// <summary>Enregistre le nom du joueur (classement) et rafraîchit le tableau.</summary>
    public void SetPlayerName(string playerName)
    {
        string cleaned = string.IsNullOrWhiteSpace(playerName) ? defaultPlayerName : playerName.Trim();
        if (cleaned.Length > 15)
        {
            cleaned = cleaned.Substring(0, 15);
        }

        PlayerName = cleaned;
        PlayerBestScore = LoadPlayerBest(cleaned);
        PlayerPrefs.SetString("flipper.playerName", cleaned);
        PlayerPrefs.Save();
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Bloque ou libère le lancement. Utilisé par le modal de saisie du nom
    /// pour éviter qu'Espace ne déclenche le starter pendant la frappe.
    /// </summary>
    public void SetLaunchLocked(bool locked)
    {
        InputLockUntil = locked ? float.PositiveInfinity : Time.time + 0.35f;
    }

    private void UpdateHighScores()
    {
        LastScore = Score;
        GamesPlayed++;
        if (Score > BestScore)
        {
            BestScore = Score;
        }

        if (Score > 0)
        {
            if (Score > PlayerBestScore)
            {
                PlayerBestScore = Score;
                PlayerPrefs.SetInt(PlayerBestPrefix + PlayerKey(PlayerName), PlayerBestScore);
            }

            leaderboard.Add(new HighScore { name = PlayerName, score = Score });
            SortAndTrimLeaderboard();
            RefreshTopTwo();
        }

        SaveLeaderboard();
    }

    private void LoadLeaderboard()
    {
        leaderboard.Clear();
        bool migrated = false;

        string json = PlayerPrefs.GetString(LeaderboardKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                LeaderboardData data = JsonUtility.FromJson<LeaderboardData>(json);
                if (data != null && data.scores != null)
                {
                    for (int i = 0; i < data.scores.Count; i++)
                    {
                        HighScore entry = data.scores[i];
                        if (string.IsNullOrEmpty(entry.name))
                        {
                            entry.name = defaultPlayerName;
                        }

                        if (entry.score > 0)
                        {
                            leaderboard.Add(entry);
                        }
                    }
                }
            }
            catch (Exception)
            {
                leaderboard.Clear();
            }
        }

        if (leaderboard.Count == 0)
        {
            // Migration des anciennes clés top1/top2 (ou valeurs de démo).
            AddSeedScore(PlayerPrefs.GetString("flipper.top1.name", "AXEL L"),
                PlayerPrefs.GetInt("flipper.top1.score", 20000));
            AddSeedScore(PlayerPrefs.GetString("flipper.top2.name", "MARC"),
                PlayerPrefs.GetInt("flipper.top2.score", 12000));
            migrated = true;
        }

        SortAndTrimLeaderboard();
        RefreshTopTwo();

        if (migrated)
        {
            SaveLeaderboard();
        }
    }

    private void SaveLeaderboard()
    {
        LeaderboardData data = new LeaderboardData();
        data.scores.AddRange(leaderboard);

        PlayerPrefs.SetString(LeaderboardKey, JsonUtility.ToJson(data));
        PlayerPrefs.SetInt(BestScoreKey, BestScore);
        PlayerPrefs.SetInt(LastScoreKey, LastScore);
        PlayerPrefs.SetInt(GamesPlayedKey, GamesPlayed);

        // Rétro-compatibilité : les anciennes clés top1/top2 restent synchronisées
        // et servent de secours si le JSON du classement est perdu.
        PlayerPrefs.SetString("flipper.top1.name", Top1.name);
        PlayerPrefs.SetInt("flipper.top1.score", Top1.score);
        PlayerPrefs.SetString("flipper.top2.name", Top2.name);
        PlayerPrefs.SetInt("flipper.top2.score", Top2.score);
        PlayerPrefs.Save();
    }

    private void AddSeedScore(string entryName, int entryScore)
    {
        if (entryScore <= 0)
        {
            return;
        }

        leaderboard.Add(new HighScore
        {
            name = string.IsNullOrEmpty(entryName) ? defaultPlayerName : entryName,
            score = entryScore
        });
    }

    private void SortAndTrimLeaderboard()
    {
        leaderboard.Sort(HighScoreComparison);

        if (leaderboard.Count > MaxLeaderboardEntries)
        {
            leaderboard.RemoveRange(MaxLeaderboardEntries, leaderboard.Count - MaxLeaderboardEntries);
        }
    }

    private void RefreshTopTwo()
    {
        Top1 = leaderboard.Count > 0 ? leaderboard[0] : new HighScore { name = "---", score = 0 };
        Top2 = leaderboard.Count > 1 ? leaderboard[1] : new HighScore { name = "---", score = 0 };
    }

    private static int CompareHighScores(HighScore a, HighScore b)
    {
        return b.score.CompareTo(a.score);
    }

    private static int LoadPlayerBest(string playerName)
    {
        return PlayerPrefs.GetInt(PlayerBestPrefix + PlayerKey(playerName), 0);
    }

    private static string PlayerKey(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            return "default";
        }

        string key = playerName.Trim().ToUpperInvariant().Replace(' ', '_');
        return key.Length == 0 ? "default" : key;
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
        BallOutOfBounds outOfBounds = FindAnyObjectByType<BallOutOfBounds>();
        if (outOfBounds != null && outOfBounds.startPosition != null)
        {
            return outOfBounds.startPosition;
        }

        GameObject fallback = GameObject.Find("BallStartPosition");
        return fallback != null ? fallback.transform : null;
    }

    private float nextKickTime;
}
