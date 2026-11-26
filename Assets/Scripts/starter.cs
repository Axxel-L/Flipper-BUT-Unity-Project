using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Starter de flipper (bouteille utilisée comme lanceur).
///
/// Maintenir la touche / le bouton de la plaque pour reculer la bouteille et charger le tir,
/// relâcher pour propulser la bille. Plus la charge est longue, plus la bille part vite.
/// La position reculée de la bouteille sert directement d'indicateur de puissance.
///
/// Le déplacement est piloté par une petite machine à états dans Update :
/// aucune allocation par image, aucune coroutine, aucun accès physique inutile.
/// </summary>
public class SlingshotMover : MonoBehaviour
{
    private const string StarterModelResourcePath =
        "StarterModel/Meshy_AI_Jagermeister_Bottle_20261126_texture_obj/Meshy_AI_Jagermeister_Bottle_20261126_texture";

    private enum ShotState
    {
        Idle,
        Pulling,
        Charging,
        Punching,
        Returning
    }

    [Header("Modèle 3D")]
    [Tooltip("Affiche le starter dès le chargement de la scène, avant le premier tir.")]
    public bool showStarterModel = true;

    [Tooltip("Échelle locale du modèle. Plus grand = bouteille plus visible.")]
    public Vector3 starterModelScale = new Vector3(1.25f, 0.78f, 0.78f);

    [Tooltip("Décalage du modèle par rapport au starter (local).")]
    public Vector3 starterModelOffset = Vector3.zero;

    [Header("Axe (X = rouge dans la scène)")]
    [Tooltip("Direction locale du déplacement ET de l'impulsion. Pour l'axe rouge (X) : (1,0,0)")]
    public Vector3 direction = Vector3.right;

    [Tooltip("Vrai = 'direction' est exprimée dans l'espace local du starter (suit sa rotation).\n" +
             "Faux = 'direction' est une direction monde.")]
    public bool useLocalSpace = true;

    [Header("Bille")]
    [Tooltip("Rigidbody de la bille à propulser. Vide = le Rigidbody le plus proche au démarrage.")]
    public Rigidbody ball;

    [Tooltip("Optimise le Rigidbody de la bille pour les hautes vitesses (interpolation + détection continue).")]
    public bool optimizeBallMotion = true;

    [Header("Charge (maintenir pour armer)")]
    [Tooltip("Vrai = tir chargé (maintenir puis relâcher). Faux = ancien tir automatique à puissance fixe.")]
    public bool useCharge = true;

    [Tooltip("Temps, en secondes, pour atteindre la charge maximale.")]
    [Min(0.05f)]
    public float fullChargeTime = 0.9f;

    [Tooltip("Courbe de conversion charge -> vitesse. 1 = linéaire, > 1 = plus de précision à faible charge.")]
    [Range(0.2f, 3f)]
    public float chargePowerCurve = 1f;

    [Header("Puissance du tir")]
    [Tooltip("Vitesse (m/s) pour un appui très court (charge minimale).")]
    [Min(0f)]
    public float minimumLaunchSpeed = 20f;

    [Tooltip("Vitesse (m/s) à charge maximale.")]
    [Min(0f)]
    public float maximumLaunchSpeed = 62f;

    [Tooltip("Vitesse fixe utilisée quand le tir chargé est désactivé.")]
    [Min(0f)]
    public float launchSpeed = 54f;

    [Tooltip("Vrai = la bille sort exactement à la vitesse calculée.\n" +
             "Faux = la vitesse calculée s'ajoute à celle déjà acquise pendant la poussée.")]
    public bool overrideBallVelocity = true;

    [Tooltip("Écrit dans la Console la vitesse donnée à la bille.")]
    public bool logLaunch = false;

    [Header("Variation contrôlée des tirs")]
    [Tooltip("Ajoute une variation légère à chaque tir pour éviter une trajectoire identique.")]
    public bool randomizeLaunch = true;

    [Tooltip("Variation aléatoire de vitesse, en pourcentage (±).")]
    [Range(0f, 0.25f)]
    public float launchSpeedJitter = 0.03f;

    [Tooltip("Écart angulaire maximal du tir, dans le plan du flipper.")]
    [Range(0f, 20f)]
    public float maximumLaunchAngle = 3f;

    [Header("Phase 1 : recul")]
    [Tooltip("Distance de recul maximale, en sens inverse de 'direction' (sert aussi de visuel de charge).")]
    [Min(0f)]
    public float pullbackDistance = 0.8f;

    [Tooltip("Durée du recul quand le tir chargé est désactivé.")]
    [Min(0f)]
    public float pullbackDuration = 0.08f;

    [Header("Phase 2 : coup (projection de la bille)")]
    [Tooltip("Distance parcourue vers l'avant, dans le sens de 'direction'")]
    [Min(0f)]
    public float punchDistance = 1f;

    [Tooltip("Temps pour aller jusqu'au bout du coup (très court = coup sec et rapide)")]
    [Min(0f)]
    public float punchDuration = 0.1f;

    [Header("Phase 3 : retour à la position initiale")]
    [Tooltip("Temps pour revenir à la position de repos")]
    [Min(0f)]
    public float returnDuration = 0.15f;

    [Header("Input")]
    [Tooltip("Touche de secours, pour lancer sans la plaque branchée (nouveau Input System)")]
    public Key triggerKey = Key.Space;

    [Tooltip("Utilise le bouton de la plaque xin-mo (action LaunchBall de PinballControls)")]
    public bool useBoardButton = true;

    private Transform cachedTransform;
    private Vector3 startPosition;
    private Vector3 localDir;
    private Vector3 worldDir;

    private ShotState state = ShotState.Idle;
    private float poseTimer;
    private float charge;
    private float shotPower;
    private bool touchedBall;
    private bool launchedThisShot;
    private bool previousHeld;

    private Vector3 poseFrom;
    private Vector3 poseTo;

    private PinballControls board;
    private InputAction launchAction;
    private Keyboard cachedKeyboard;
    private KeyControl triggerControl;

    private Collider plungerCollider;
    private Collider ballCollider;

    /// <summary>Direction du réglage, normalisée et jamais nulle.</summary>
    private Vector3 DirNormalized
    {
        get { return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.right; }
    }

    private void OnValidate()
    {
        Transform model = transform.Find("StarterBottle");
        if (model == null)
            return;

        model.localRotation = Quaternion.FromToRotation(Vector3.up, DirNormalized)
            * Quaternion.Euler(0f, -90f, 0f);
        model.localPosition = starterModelOffset;
        model.localScale = starterModelScale;
        model.gameObject.SetActive(showStarterModel);
    }

    private void Awake()
    {
        cachedTransform = transform;

        // Le starter est déplacé par script : en kinematic, le transform est la seule
        // autorité sur sa position (pas de téléportation par le solveur physique).
        Rigidbody self = GetComponent<Rigidbody>();
        if (self != null) self.isKinematic = true;

        foreach (MeshRenderer blockRenderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (blockRenderer.transform == cachedTransform)
            {
                blockRenderer.enabled = false;
            }
        }

        plungerCollider = GetComponent<Collider>();
        if (plungerCollider == null)
        {
            Debug.LogWarning("[Starter] Aucun Collider sur le starter : la bille ne pourra pas être détectée.", this);
        }

        AddStarterModel();
        ResolveBall();

        board = new PinballControls();
        launchAction = board.GamePlayPF.LaunchBall;
    }

    private void OnEnable()
    {
        if (board != null) board.GamePlayPF.Enable();
    }

    private void OnDisable()
    {
        if (board != null) board.GamePlayPF.Disable();
    }

    private void OnDestroy()
    {
        if (board == null) return;

        board.GamePlayPF.Disable();
        board.Dispose();
    }

    private void Start()
    {
        startPosition = cachedTransform.localPosition;
        ApplyModelPose();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        bool canLaunch = ComputeCanLaunch();
        bool held = ReadHeld();
        bool pressedEdge = held && !previousHeld;
        bool releasedEdge = !held && previousHeld;
        previousHeld = held;

        switch (state)
        {
            case ShotState.Idle:
                if (pressedEdge && canLaunch)
                {
                    if (useCharge)
                    {
                        charge = 0f;
                        state = ShotState.Charging;
                        ApplyChargePose();
                    }
                    else
                    {
                        BeginAutoPull();
                    }
                }
                break;

            case ShotState.Pulling:
                AdvancePose(dt, ref poseTimer, poseFrom, poseTo, pullbackDuration,
                    ShotState.Punching, 1f);
                break;

            case ShotState.Charging:
                if (!canLaunch)
                {
                    BeginReturn();
                    break;
                }

                charge = Mathf.Min(1f, charge + dt / Mathf.Max(0.05f, fullChargeTime));
                ApplyChargePose();

                if (releasedEdge)
                {
                    BeginPunch(charge);
                }
                break;

            case ShotState.Punching:
                AdvancePunch(dt);
                break;

            case ShotState.Returning:
                AdvancePose(dt, ref poseTimer, poseFrom, poseTo, returnDuration,
                    ShotState.Idle, 0f);
                break;
        }
    }

    public void TriggerMove()
    {
        if (state != ShotState.Idle) return;

        if (ball == null) ResolveBall();

        if (useCharge)
        {
            charge = 1f;
            BeginPunch(charge);
        }
        else
        {
            BeginAutoPull();
        }
    }

    private bool ComputeCanLaunch()
    {
        PinballGameManager manager = PinballGameManager.Instance;
        return manager == null || manager.CanLaunch;
    }

    private bool ReadHeld()
    {
        if (useBoardButton && launchAction != null && launchAction.IsPressed())
        {
            return true;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != cachedKeyboard)
        {
            cachedKeyboard = keyboard;
            triggerControl = keyboard != null ? keyboard[triggerKey] : null;
        }

        return triggerControl != null && triggerControl.isPressed;
    }

    private void ApplyChargePose()
    {
        cachedTransform.localPosition = startPosition - localDir * (pullbackDistance * charge);
    }

    private void BeginAutoPull()
    {
        poseFrom = cachedTransform.localPosition;
        poseTo = startPosition - localDir * pullbackDistance;
        poseTimer = 0f;
        state = ShotState.Pulling;
    }

    private void BeginPunch(float power)
    {
        shotPower = Mathf.Clamp01(power);
        touchedBall = false;
        launchedThisShot = false;

        poseFrom = cachedTransform.localPosition;
        poseTo = startPosition + localDir * punchDistance;
        poseTimer = 0f;
        state = ShotState.Punching;
    }

    private void AdvancePunch(float dt)
    {
        if (punchDuration <= 0f)
        {
            cachedTransform.localPosition = poseTo;
            FinishPunch();
            return;
        }

        poseTimer += dt;
        float t = Mathf.Clamp01(poseTimer / punchDuration);
        cachedTransform.localPosition = Vector3.Lerp(poseFrom, poseTo, t);

        if (!touchedBall && (PlungerTouchesBall() || PlungerCanReachBall()))
        {
            touchedBall = true;
        }

        if (t >= 1f)
        {
            FinishPunch();
        }
    }

    private void FinishPunch()
    {
        cachedTransform.localPosition = poseTo;

        if (touchedBall)
        {
            worldDir = useLocalSpace ? cachedTransform.TransformDirection(localDir) : localDir;
            LaunchBall(worldDir);
        }

        BeginReturn();
    }

    private void BeginReturn()
    {
        poseFrom = cachedTransform.localPosition;
        poseTo = startPosition;
        poseTimer = 0f;
        charge = 0f;
        state = ShotState.Returning;
    }

    /// <summary>Avance une phase de déplacement et enchaîne sur l'état suivant.</summary>
    private void AdvancePose(float dt, ref float timer, Vector3 from, Vector3 to,
        float duration, ShotState nextState, float nextPower)
    {
        if (duration <= 0f)
        {
            cachedTransform.localPosition = to;
            if (nextState == ShotState.Punching)
            {
                BeginPunch(nextPower);
            }
            else
            {
                state = nextState;
            }
            return;
        }

        timer += dt;
        float t = Mathf.Clamp01(timer / duration);
        cachedTransform.localPosition = Vector3.Lerp(from, to, t);

        if (t >= 1f)
        {
            cachedTransform.localPosition = to;

            if (nextState == ShotState.Punching)
            {
                BeginPunch(nextPower);
            }
            else
            {
                state = nextState;
            }
        }
    }

    /// <summary>Vrai si les colliders du starter et de la bille se recouvrent.</summary>
    private bool PlungerTouchesBall()
    {
        if (launchedThisShot) return false;
        if (plungerCollider == null || ballCollider == null) return false;

        return plungerCollider.bounds.Intersects(ballCollider.bounds);
    }

    /// <summary>Détection tolérante pour ne pas rater l'impulsion sur un petit décalage.</summary>
    private bool PlungerCanReachBall()
    {
        if (launchedThisShot) return false;
        if (plungerCollider == null || ball == null) return false;

        float reach = Mathf.Max(2f, punchDistance + 1f);
        float distanceSquared = plungerCollider.bounds.SqrDistance(ball.worldCenterOfMass);
        return distanceSquared <= reach * reach;
    }

    /// <summary>Donne son impulsion à la bille, le long de l'axe de tir.</summary>
    private void LaunchBall(Vector3 baseDirection)
    {
        if (ball == null || launchedThisShot) return;

        launchedThisShot = true;
        ball.WakeUp();
        ball.useGravity = true;

        Vector3 launchDirection = GetLaunchDirection(baseDirection);
        float launchVelocity = GetLaunchSpeed(shotPower);

        if (overrideBallVelocity)
        {
            // Le starter a poussé la bille physiquement : on annule la composante de vitesse
            // déjà acquise le long de l'axe pour ne pas obtenir "poussée + vitesse réglée".
            Vector3 velocity = ball.linearVelocity;
            velocity -= launchDirection * Vector3.Dot(velocity, launchDirection);
            ball.linearVelocity = velocity;
        }

        // ForceMode.VelocityChange : vitesse (m/s) indépendante de la masse.
        ball.AddForce(launchDirection * launchVelocity, ForceMode.VelocityChange);

        if (logLaunch)
        {
            Debug.Log(
                $"[Starter] Tir de '{ball.name}' : charge={shotPower:P0}, vitesse={launchVelocity:F2} m/s, " +
                $"direction={launchDirection}", this);
        }
    }

    private Vector3 GetLaunchDirection(Vector3 baseDirection)
    {
        // Le plateau est incliné : sa normale est le "haut" du starter.
        Vector3 boardNormal = cachedTransform.up;

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

        float angle = Random.Range(-maximumLaunchAngle, maximumLaunchAngle);
        return Quaternion.AngleAxis(angle, boardNormal) * directionInPlane;
    }

    private float GetLaunchSpeed(float power)
    {
        float speed;

        if (!useCharge)
        {
            speed = launchSpeed;
        }
        else
        {
            float minimum = Mathf.Min(minimumLaunchSpeed, maximumLaunchSpeed);
            float maximum = Mathf.Max(minimumLaunchSpeed, maximumLaunchSpeed);
            float t = Mathf.Pow(Mathf.Clamp01(power), Mathf.Max(0.01f, chargePowerCurve));
            speed = Mathf.Lerp(minimum, maximum, t);
        }

        if (randomizeLaunch && launchSpeedJitter > 0f)
        {
            speed *= 1f + Random.Range(-launchSpeedJitter, launchSpeedJitter);
        }

        return Mathf.Max(0f, speed);
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
                // À haute vitesse, sans interpolation la bille avance par saccades,
                // et sans détection continue elle traverse les murs.
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

    private void AddStarterModel()
    {
        Transform existingModel = cachedTransform.Find("StarterBottle");
        GameObject model;
        if (existingModel != null)
        {
            model = existingModel.gameObject;
        }
        else
        {
            GameObject modelPrefab = Resources.Load<GameObject>(StarterModelResourcePath);
            if (modelPrefab == null)
            {
                Debug.LogError(
                    $"[Starter] Modèle introuvable dans Resources : {StarterModelResourcePath}.", this);
                return;
            }

            model = Instantiate(modelPrefab, cachedTransform, false);
        }

        model.name = "StarterBottle";
        model.SetActive(showStarterModel);

        foreach (Renderer modelRenderer in model.GetComponentsInChildren<Renderer>(true))
        {
            modelRenderer.enabled = true;
        }

        foreach (Collider modelCollider in model.GetComponentsInChildren<Collider>(true))
        {
            modelCollider.enabled = false;
            Destroy(modelCollider);
        }

        MeshRenderer originalRenderer = GetComponent<MeshRenderer>();
        if (originalRenderer != null)
        {
            originalRenderer.enabled = false;
        }

        ApplyModelPose();
    }

    private void ApplyModelPose()
    {
        Transform model = cachedTransform.Find("StarterBottle");
        if (model == null) return;

        localDir = DirNormalized;
        worldDir = useLocalSpace ? cachedTransform.TransformDirection(localDir) : localDir;

        model.localRotation = Quaternion.FromToRotation(Vector3.up, localDir)
            * Quaternion.Euler(0f, -90f, 0f);
        model.localPosition = starterModelOffset;
        model.localScale = starterModelScale;
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
            if (candidate.transform == cachedTransform || candidate.transform.IsChildOf(cachedTransform)) continue;
            if (!candidate.CompareTag("Ball")) continue;

            float distance = (candidate.position - cachedTransform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }
}
