using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Flipper gauche : reprend toute la physique de <see cref="FlipperController"/>
/// (Rigidbody + HingeJoint) et force le côté gauche / la touche flèche gauche.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public sealed class LeftFlipperController : FlipperController
{
    protected override void Awake()
    {
        side = Side.Left;
        key = Key.LeftArrow;
        base.Awake();
    }

    protected override void OnValidate()
    {
        side = Side.Left;
        key = Key.LeftArrow;
        base.OnValidate();
    }
}
