using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Flipper droit : reprend toute la physique de <see cref="FlipperController"/>
/// (Rigidbody + HingeJoint) et force le côté droit / la touche flèche droite.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public sealed class RightFlipperController : FlipperController
{
    protected override void Awake()
    {
        side = Side.Right;
        key = Key.RightArrow;
        base.Awake();
    }

    protected override void OnValidate()
    {
        side = Side.Right;
        key = Key.RightArrow;
        base.OnValidate();
    }
}
