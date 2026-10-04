using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(TrailRenderer))]
public class BallTrailController : MonoBehaviour
{
    [Header("Traînée")]
    [Tooltip("Longueur visible de la traînée en unités du monde.")]
    [Min(0f)]
    public float trailLength = 8f;

    [Tooltip("Durée minimale d'émission pour garder la traînée visible à faible vitesse.")]
    [Min(0f)]
    public float minimumTime = 0.02f;

    [Tooltip("Durée maximale pour éviter que la traînée se décroche à vitesse élevée.")]
    [Min(0f)]
    public float maximumTime = 0.15f;

    [Tooltip("Distance minimale entre deux points de la traînée.")]
    [Min(0.001f)]
    public float minimumVertexDistance = 0.05f;

    private Rigidbody ball;
    private TrailRenderer trail;

    private void Awake()
    {
        ball = GetComponent<Rigidbody>();
        trail = GetComponent<TrailRenderer>();
        trail.minVertexDistance = minimumVertexDistance;
    }

    private void LateUpdate()
    {
        float speed = ball.linearVelocity.magnitude;
        float time = speed > 0.01f ? trailLength / speed : maximumTime;

        trail.time = Mathf.Clamp(time, minimumTime, maximumTime);
    }
}
