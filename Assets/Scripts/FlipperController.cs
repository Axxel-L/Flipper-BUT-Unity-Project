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
/// - un Collider, sinon le flipper ne touchera jamais la bille
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

    [Tooltip("Angle de la butée basse (position de repos), en degrés")]
    public float minAngle = 0f;

    [Tooltip("Angle de la butée haute (flipper levé), en degrés")]
    public float maxAngle = 60f;

    private HingeJoint hinge;

    // Dernier état connu : on n'écrit au moteur que quand il change,
    // pour ne pas rappeler le moteur du joint à chaque frame.
    private bool pressed;

    void Awake()
    {
        hinge = GetComponent<HingeJoint>();

        ApplyJointSettings();

        // On démarre au repos
        pressed = false;
        ApplyMotorVelocity(restVelocity);
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
