
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class FlipperController : MonoBehaviour
{
    public enum Side { Left, Right }

    [Header("Entrées Xin-Mo")]
    public Side side = Side.Left;

    [Header("Clavier")]
    public Key key = Key.RightArrow;

    [Header("Moteur")]
    public float activeVelocity = 600f;
    public float restVelocity = -600f;
    public float maxTorque = 1500f;

    [Header("Limites")]
    public bool useLimits = true;
    public float minAngle = 0f;
    public float maxAngle = 60f;

    [Header("Physique")]
    public bool smoothMotion = true;

    private HingeJoint hinge;
    private Rigidbody body;

    private PinballControls board;
    private InputAction boardAction;

    private bool pressed;

    void Awake()
    {
        hinge = GetComponent<HingeJoint>();
        body = GetComponent<Rigidbody>();

        board = new PinballControls();

        boardAction = side == Side.Left
            ? board.GamePlayPF.LeftFlipper
            : board.GamePlayPF.RightFlipper;

        // Physique du flipper
        body.useGravity = false;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        // IMPORTANT : ne pas geler la position
        body.constraints = RigidbodyConstraints.None;

        if (smoothMotion)
        {
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
        }

        // Configuration du moteur
        JointMotor motor = hinge.motor;
        motor.force = maxTorque;
        motor.targetVelocity = restVelocity;

        hinge.motor = motor;
        hinge.useMotor = true;

        // Configuration des limites
        if (useLimits)
        {
            JointLimits limits = hinge.limits;
            limits.min = minAngle;
            limits.max = maxAngle;

            hinge.limits = limits;
            hinge.useLimits = true;
        }

        pressed = false;
    }

    void OnEnable()
    {
        if (board != null)
            board.GamePlayPF.Enable();
    }

    void OnDisable()
    {
        if (board != null)
            board.GamePlayPF.Disable();
    }

    void OnDestroy()
    {
        if (board != null)
            board.Dispose();
    }

    void Update()
    {
        bool boardPressed =
            boardAction != null && boardAction.IsPressed();

        bool keyboardPressed =
            Keyboard.current != null &&
            Keyboard.current[key].isPressed;

        SetPressed(boardPressed || keyboardPressed);
    }

    public void SetPressed(bool value)
    {
        if (pressed == value)
            return;

        pressed = value;

        JointMotor motor = hinge.motor;

        motor.targetVelocity =
            value ? activeVelocity : restVelocity;

        motor.force = maxTorque;

        hinge.motor = motor;
    }

    void OnValidate()
    {
        if (minAngle > maxAngle)
        {
            float temp = minAngle;
            minAngle = maxAngle;
            maxAngle = temp;
        }

        if (hinge == null)
            hinge = GetComponent<HingeJoint>();

        if (hinge == null)
            return;

        JointMotor motor = hinge.motor;
        motor.force = maxTorque;
        hinge.motor = motor;
        hinge.useMotor = true;

        if (useLimits)
        {
            JointLimits limits = hinge.limits;
            limits.min = minAngle;
            limits.max = maxAngle;

            hinge.limits = limits;
            hinge.useLimits = true;
        }
    }
}
