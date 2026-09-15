using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Contrôle un flipper 3D piloté par un HingeJoint (moteur + butées angulaires).
///
/// À placer sur CHAQUE flipper (gauche et droit), avec une touche différente.
/// Le flipper tourne autour de l'axe du HingeJoint : monter le flipper = faire
/// tourner le moteur dans un sens, le relâcher = le faire tourner dans l'autre.
///
/// Prérequis sur le GameObject :
/// - un Rigidbody
/// - un HingeJoint (Axis = (0, 1, 0) pour un flipper à plat dans le plan XZ)
/// - un Collider (BoxCollider de préférence ; un MeshCollider doit être coché "Convex"
///   sur un Rigidbody dynamique, sinon Unity l'ignore)
///
/// CONTRAINTE IMPORTANTE : à l'ouverture de la scène, le HingeJoint considère que
/// l'angle vaut 0, c'est-à-dire la pose dans laquelle tu as placé le flipper. La
/// butée contre laquelle pousse le moteur au repos doit donc être l'angle 0, sinon
/// le flipper se déplace tout seul dès qu'on appuie sur Play. Le script vérifie ce
/// point au démarrage et prévient dans la Console si c'est incohérent.
///
/// Ce script utilise le NOUVEAU Input System (celui configuré dans le projet).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class FlipperController : MonoBehaviour
{
    [Header("Touche (nouveau Input System)")]
    [Tooltip("Touche qui active ce flipper. Ex : LeftArrow / RightArrow")]
    public Key key = Key.RightArrow;

    [Header("Moteur du HingeJoint")]
    [Tooltip("Vitesse de rotation quand le flipper est activé, en degrés/seconde")]
    public float activeVelocity = 900f;

    [Tooltip("Vitesse de rotation quand la touche est relâchée, en degrés/seconde (signe opposé)")]
    public float restVelocity = -900f;

    [Tooltip("Couple maximum du moteur. Trop faible = flipper mou ; très élevé = il catapulte la bille")]
    public float maxTorque = 10000f;

    [Header("Butées (indispensable, sinon le flipper tourne en rond)")]
    [Tooltip("Active les butées angulaires du HingeJoint")]
    public bool useLimits = true;

    [Tooltip("Angle de la butée basse, en degrés")]
    public float minAngle = 0f;

    [Tooltip("Angle de la butée haute, en degrés")]
    public float maxAngle = 60f;

    [Header("Physique")]
    [Tooltip("Interpolation du Rigidbody (mouvement fluide à l'écran) et détection continue " +
             "des collisions (le flipper ne rate plus la bille pendant le coup)")]
    public bool smoothMotion = true;

    private HingeJoint hinge;
    private Rigidbody body;

    // Dernier état connu : on n'écrit au moteur que quand il change,
    // pour ne pas rappeler le moteur du joint à chaque frame.
    private bool pressed;

    void Awake()
    {
        hinge = GetComponent<HingeJoint>();
        body = GetComponent<Rigidbody>();

        ApplyPhysicsSettings();
        ApplyJointSettings();

        // On démarre au repos
        pressed = false;
        ApplyMotorVelocity(restVelocity);

        WarnIfRestDirectionLooksWrong();
    }

    void Update()
    {
        // Keyboard.current peut être null si aucun clavier n'est détecté
        if (Keyboard.current == null) return;

        SetPressed(Keyboard.current[key].isPressed);
    }

    /// <summary>Active ou relâche le flipper. Public pour pouvoir le piloter autrement (bouton, autre script).</summary>
    public void SetPressed(bool value)
    {
        if (value == pressed) return;

        pressed = value;
        ApplyMotorVelocity(value ? activeVelocity : restVelocity);
    }

    private void ApplyMotorVelocity(float velocity)
    {
        if (hinge == null) hinge = GetComponent<HingeJoint>();
        if (hinge == null) return;

        JointMotor motor = hinge.motor;
        motor.targetVelocity = velocity;
        hinge.motor = motor;
    }

    private void ApplyPhysicsSettings()
    {
        if (!smoothMotion) return;

        if (body == null) body = GetComponent<Rigidbody>();
        if (body == null) return;

        // Le flipper tourne à 900°/s : sans interpolation, il avance par saccades à l'écran.
        body.interpolation = RigidbodyInterpolation.Interpolate;

        // Détection continue : à cette vitesse, un contact discret peut être manqué.
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    /// <summary>Recopie les réglages du script sur le HingeJoint (moteur + butées).</summary>
    private void ApplyJointSettings()
    {
        if (hinge == null) hinge = GetComponent<HingeJoint>();
        if (hinge == null) return;

        float torque = Mathf.Max(0f, maxTorque);

        // On n'écrit que si la valeur change : sinon l'objet est marqué "modifié"
        // en permanence et la scène apparaît sale dès qu'on la charge.
        JointMotor motor = hinge.motor;
        if (!Mathf.Approximately(motor.force, torque))
        {
            motor.force = torque;
            hinge.motor = motor;
        }
        if (!hinge.useMotor) hinge.useMotor = true;

        if (useLimits)
        {
            JointLimits limits = hinge.limits;
            if (!Mathf.Approximately(limits.min, minAngle) || !Mathf.Approximately(limits.max, maxAngle))
            {
                limits.min = minAngle;
                limits.max = maxAngle;
                hinge.limits = limits;
            }
            if (!hinge.useLimits) hinge.useLimits = true;
        }
    }

    /// <summary>
    /// Prévient si le réglage va faire bouger le flipper tout seul au lancement de la scène :
    /// le moteur de repos pousse vers une butée située au-delà de la pose actuelle (angle 0).
    /// </summary>
    private void WarnIfRestDirectionLooksWrong()
    {
        if (!useLimits) return;

        bool pushesPastMax = restVelocity > 0f && maxAngle > 0f;
        bool pushesPastMin = restVelocity < 0f && minAngle < 0f;
        if (!pushesPastMax && !pushesPastMin) return;

        float range = Mathf.Abs(maxAngle - minAngle);

        string fix = pushesPastMax
            ? $"min = {-range:F0}, max = 0"
            : $"min = 0, max = {range:F0}";

        Debug.LogWarning(
            $"[Flipper '{name}'] Au démarrage, ce flipper va se déplacer tout seul : restVelocity " +
            $"({restVelocity:F0}°/s) le pousse vers la butée {range:F0}° alors que sa pose dans la scène " +
            $"correspond à l'angle 0. Pour qu'il reste en place, change les bornes en {fix} " +
            $"(actuellement {minAngle:F0} / {maxAngle:F0}), ou inverse le signe de restVelocity et " +
            "activeVelocity.", this);
    }

    void OnValidate()
    {
        // La butée basse doit rester sous la butée haute
        if (minAngle > maxAngle)
        {
            float temp = minAngle;
            minAngle = maxAngle;
            maxAngle = temp;
        }

        // Réglages visibles immédiatement dans l'Inspector (et en Play mode)
        ApplyJointSettings();
    }
}
