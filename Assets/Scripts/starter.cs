using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Starter de flipper.
///
/// Le starter joue une animation en 3 temps (recul -> coup -> retour) et, s'il touche la bille
/// pendant le coup, il lui applique une véritable impulsion sur l'axe X (Rigidbody.AddForce)
/// au lieu de compter uniquement sur le choc physique.
/// </summary>
public class SlingshotMover : MonoBehaviour
{
    [Header("Axe (X = rouge dans la scène)")]
    [Tooltip("Direction locale du déplacement ET de l'impulsion. Pour l'axe rouge (X) : (1,0,0)")]
    public Vector3 direction = Vector3.right;

    [Tooltip("Vrai = 'direction' est exprimée dans l'espace local du starter (suit sa rotation).\n" +
             "Faux = 'direction' est une direction monde (pratique pour viser un axe précis).")]
    public bool useLocalSpace = true;

    [Header("Bille")]
    [Tooltip("Rigidbody de la bille à propulser : glisse l'objet 'Bille' dans ce champ.\n" +
             "Laisser vide = le script prend le Rigidbody le plus proche au démarrage.")]
    public Rigidbody ball;

    [Tooltip("Optimise le Rigidbody de la bille pour une vitesse élevée : interpolation " +
             "(mouvement fluide à l'écran) et détection continue des collisions " +
             "(sans quoi la bille traverse les murs à 70 m/s).")]
    public bool optimizeBallMotion = true;

    [Header("Impulsion donnée à la bille")]
    [Tooltip("Vitesse de sortie de la bille en m/s, donnée en une seule impulsion (indépendant de la masse).\n" +
             "À garder au-dessus de la vitesse du starter (punchDistance / punchDuration).")]
    public float launchSpeed = 750f;

    [Tooltip("Vrai = la bille sort exactement à 'launchSpeed'.\n" +
             "Faux = 'launchSpeed' s'ajoute à la vitesse déjà acquise pendant la poussée.")]
    public bool overrideBallVelocity = true;

    [Tooltip("Écrit dans la Console la vitesse donnée à la bille (utile pour régler launchSpeed).")]
    public bool logLaunch = false;

    [Header("Boost à l'impact")]
    [Tooltip("Multiplie la vitesse donnée à la bille au moment de l'impact (1 = inchangé).\n" +
             "S'applique que 'randomizeLaunch' soit actif ou non.")]
    [Min(1f)]
    public float impactBoostMultiplier = 10f;

    [Header("Variation contrôlée des tirs")]
    [Tooltip("Ajoute une variation légère à chaque tir pour éviter une trajectoire identique.")]
    public bool randomizeLaunch = true;

    [Tooltip("Vitesse minimale réellement appliquée, en m/s (avant le boost à l'impact).")]
    [Min(0f)]
    public float minimumLaunchSpeed = 25f;

    [Tooltip("Vitesse maximale réellement appliquée, en m/s (avant le boost à l'impact).")]
    [Min(0f)]
    public float maximumLaunchSpeed = 31f;

    [Tooltip("Écart angulaire maximal du tir, dans le plan du flipper.")]
    [Range(0f, 20f)]
    public float maximumLaunchAngle = 8f;

    [Header("Phase 1 : recul (avant le tir)")]
    [Tooltip("Distance du léger recul, en sens inverse de 'direction'")]
    public float pullbackDistance = 0.5f;

    [Tooltip("Temps pour reculer")]
    public float pullbackDuration = 0.06f;

    [Header("Phase 2 : coup (projection de la bille)")]
    [Tooltip("Distance parcourue vers l'avant, dans le sens de 'direction'")]
    public float punchDistance = 1f;

    [Tooltip("Temps pour aller jusqu'au bout du coup (très court = coup sec et rapide)")]
    public float punchDuration = 0.1f;

    [Header("Phase 3 : retour à la position initiale")]
    [Tooltip("Temps pour revenir à la position de repos")]
    public float returnDuration = 0.15f;

    [Header("Input")]
    [Tooltip("Touche de secours, pour lancer sans la plaque branchée (nouveau Input System)")]
    public Key triggerKey = Key.Space;

    [Tooltip("Lance la bille avec le bouton de la plaque xin-mo (action LaunchBall de PinballControls)")]
    public bool useBoardButton = true;

    private Vector3 startPosition;
    private Coroutine currentRoutine;
    private bool launchedThisShot;

    private PinballControls board;
    private InputAction launchAction;

    private Collider plungerCollider;
    private Collider ballCollider;

    /// <summary>Direction du réglage, normalisée et jamais nulle.</summary>
    private Vector3 DirNormalized
    {
        get { return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.right; }
    }

    /// <summary>Direction du tir exprimée en espace monde (celle de l'impulsion).</summary>
    private Vector3 WorldDir
    {
        get { return useLocalSpace ? transform.TransformDirection(DirNormalized) : DirNormalized; }
    }

    void Awake()
    {
        // Le starter est déplacé par script : en "dynamic", Unity téléporte un corps géré par le
        // solveur physique (contacts approximatifs, à-coups). En kinematic, le transform est la
        // seule autorité sur la position du starter.
        Rigidbody self = GetComponent<Rigidbody>();
        if (self != null) self.isKinematic = true;

        plungerCollider = GetComponent<Collider>();
        if (plungerCollider == null)
        {
            Debug.LogWarning("[Starter] Aucun Collider sur le starter : la bille ne pourra pas être détectée.", this);
        }

        ResolveBall();

        // Bouton de la plaque : action LaunchBall (<HID::xin-mo.com Xinmotek Controller>/button4)
        board = new PinballControls();
        launchAction = board.GamePlayPF.LaunchBall;
    }

    void OnEnable()
    {
        if (board != null) board.GamePlayPF.Enable();
    }

    void OnDisable()
    {
        if (board != null) board.GamePlayPF.Disable();
    }

    void OnDestroy()
    {
        if (board == null) return;

        board.GamePlayPF.Disable();
        board.Dispose();
    }

    void Start()
    {
        // Mémorise la position de repos au démarrage
        startPosition = transform.localPosition;
    }

    void Update()
    {
        bool launchPressed = useBoardButton && launchAction != null && launchAction.WasPressedThisFrame();

        // Keyboard.current peut être null si aucun clavier n'est détecté
        if (!launchPressed && Keyboard.current != null)
        {
            launchPressed = Keyboard.current[triggerKey].wasPressedThisFrame;
        }

        if (launchPressed) TriggerMove();
    }

    public void TriggerMove()
    {
        // Un seul tir à la fois : on ignore l'appui tant que l'animation n'est pas terminée.
        if (currentRoutine != null) return;

        // Si la bille a été détruite/remplacée, on la retrouve avant de tirer.
        if (ball == null) ResolveBall();

        currentRoutine = StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        Vector3 localDir = DirNormalized;   // déplacement : espace local du starter
        Vector3 worldDir = WorldDir;        // impulsion   : espace monde

        Vector3 pulledBackPosition = startPosition - localDir * pullbackDistance;
        Vector3 punchedPosition = startPosition + localDir * punchDistance;

        launchedThisShot = false;

        // Phase 1 : léger recul
        yield return MoveOverTime(transform.localPosition, pulledBackPosition, pullbackDuration);

        // Phase 2 : coup vers l'avant (la bille est poussée, puis reçoit son impulsion)
        yield return PunchRoutine(punchedPosition, worldDir);

        // Phase 3 : retour à la position de repos
        yield return MoveOverTime(transform.localPosition, startPosition, returnDuration);

        currentRoutine = null;
    }

    private IEnumerator PunchRoutine(Vector3 punchedPosition, Vector3 worldDir)
    {
        Vector3 from = transform.localPosition;

        // Sécurité : si duration = 0, on saute directement à la position finale
        if (punchDuration <= 0f)
        {
            transform.localPosition = punchedPosition;
            if (PlungerTouchesBall()) LaunchBall(worldDir);
            yield break;
        }

        bool touchedBall = false;
        float elapsed = 0f;

        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / punchDuration);
            transform.localPosition = Vector3.Lerp(from, punchedPosition, t);

            // On mémorise le contact : en fin de coup la bille est déjà devant le starter.
            if (!touchedBall && PlungerTouchesBall()) touchedBall = true;

            yield return null;
        }
        transform.localPosition = punchedPosition;

        // Impulsion donnée quand le starter a fini de pousser : la vitesse de sortie de la bille
        // est ainsi celle réglée, et non "poussée physique + impulsion".
        if (touchedBall) LaunchBall(worldDir);
    }

    /// <summary>Vrai si les colliders du starter et de la bille se recouvrent (détection géométrique).</summary>
    private bool PlungerTouchesBall()
    {
        if (launchedThisShot) return false;
        if (plungerCollider == null || ballCollider == null) return false;

        return plungerCollider.bounds.Intersects(ballCollider.bounds);
    }

    /// <summary>Donne son impulsion à la bille, le long de l'axe de tir.</summary>
    private void LaunchBall(Vector3 worldDir)
    {
        if (ball == null || launchedThisShot) return;

        launchedThisShot = true;
        ball.useGravity = true;

        Vector3 launchDirection = GetLaunchDirection(worldDir);
        float launchVelocity = GetLaunchSpeed();

        if (overrideBallVelocity)
        {
            // Le starter a poussé la bille physiquement pendant le coup : on annule la composante
            // de vitesse déjà acquise le long de l'axe, sinon on obtiendrait "poussée + launchSpeed".
            Vector3 velocity = ball.linearVelocity;
            velocity -= launchDirection * Vector3.Dot(velocity, launchDirection);
            ball.linearVelocity = velocity;
        }

        // ForceMode.VelocityChange : impulsion exprimée en vitesse (m/s), indépendante de la masse.
        ball.AddForce(launchDirection * launchVelocity, ForceMode.VelocityChange);

        Debug.Log(
            $"[Starter][Diagnostic] Tir de '{ball.name}' à t={Time.time:F3} : " +
            $"position={ball.position}, direction={launchDirection}, " +
            $"impulsion={launchVelocity:F2} m/s (boost x{impactBoostMultiplier:F2}), vitesse={ball.linearVelocity}", this);

        if (logLaunch)
        {
            float speed = Vector3.Dot(ball.linearVelocity, launchDirection);
            Debug.Log($"[Starter] Impulsion donnée à '{ball.name}' : {speed:F2} m/s le long de {launchDirection}", this);
        }
    }

    private Vector3 GetLaunchDirection(Vector3 baseDirection)
    {
        // Le Starter a la même inclinaison que le plateau : son "haut" est la normale de la table.
        Vector3 boardNormal = transform.up;

        Vector3 directionInPlane = Vector3.ProjectOnPlane(baseDirection, boardNormal);
        if (directionInPlane.sqrMagnitude < 0.0001f)
        {
            directionInPlane = baseDirection;
        }
        directionInPlane.Normalize();

        if (!randomizeLaunch || maximumLaunchAngle <= 0f)
        {
            return directionInPlane;
        }

        // Rotation autour de la normale du plateau = déviation en travers de la table
        float angle = Random.Range(-maximumLaunchAngle, maximumLaunchAngle);
        return Quaternion.AngleAxis(angle, boardNormal) * directionInPlane;
    }

    private float GetLaunchSpeed()
    {
        float baseSpeed;

        if (!randomizeLaunch)
        {
            baseSpeed = launchSpeed;
        }
        else
        {
            float minimum = Mathf.Min(minimumLaunchSpeed, maximumLaunchSpeed);
            float maximum = Mathf.Max(minimumLaunchSpeed, maximumLaunchSpeed);
            baseSpeed = Random.Range(minimum, maximum);
        }

        // Boost à l'impact : plus grande accélération donnée à la bille.
        return baseSpeed * Mathf.Max(1f, impactBoostMultiplier);
    }

    private IEnumerator MoveOverTime(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            transform.localPosition = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localPosition = Vector3.Lerp(from, to, t);
            yield return null;
        }
        transform.localPosition = to;
    }

    /// <summary>Récupère la bille : référence de l'Inspector, sinon le Rigidbody le plus proche.</summary>
    private void ResolveBall()
    {
        if (ball == null) ball = FindNearestRigidbody();

        if (ball != null)
        {
            ballCollider = ball.GetComponent<Collider>();
            ball.useGravity = false;

            if (optimizeBallMotion)
            {
                // À 70 m/s (1,4 unité par pas physique), un Rigidbody sans interpolation avance
                // par saccades à l'écran, et une détection discrète le fait traverser les murs.
                ball.interpolation = RigidbodyInterpolation.Interpolate;
                ball.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }
        else
        {
            Debug.LogWarning("[Starter] Aucune bille trouvée. Assigne le Rigidbody de la bille dans " +
                "le champ 'Ball' du composant SlingshotMover.", this);
        }
    }

    /// <summary>Trouve le Rigidbody le plus proche (hors starter) quand la référence n'est pas assignée.</summary>
    private Rigidbody FindNearestRigidbody()
    {
        Rigidbody[] bodies = FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude);
        Rigidbody nearest = null;
        float bestDistance = float.MaxValue;

        foreach (Rigidbody candidate in bodies)
        {
            if (candidate == null) continue;
            if (candidate.transform == transform || candidate.transform.IsChildOf(transform)) continue;
            if (!candidate.CompareTag("Ball")) continue;

            float distance = (candidate.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }
}