using UnityEngine;
[RequireComponent(typeof(Collider))]
public class IUTBumper : MonoBehaviour
{
    [Header("Propulsion")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private string ballTag = "Ball";

    [Header("Passage")]
    [Tooltip("Côté monde où l'IUT reste solide. +Z = rebond au-dessus, passage en dessous.")]
    [SerializeField] private Vector3 solidSideWorldNormal = Vector3.forward;
    [SerializeField, Min(0f)] private float sideTolerance = 0.1f;

    [Header("Optionnel")]
    [SerializeField] private AudioSource hitSound;

    private float lastPushTime;
    private Collider[] ownColliders;
    private Rigidbody ignoredBall;

    private void Awake()
    {
        ownColliders = GetComponents<Collider>();
    }

    private void OnCollisionEnter(Collision collision) => Push(collision);

    private void OnCollisionExit(Collision collision)
    {
        if (collision.rigidbody == ignoredBall)
        {
            ignoredBall = null;
            SetIgnored(collision.collider, false);
        }
    }

    private void FixedUpdate()
    {
        if (ignoredBall == null) return;

        Collider ballCollider = ignoredBall.GetComponent<Collider>();
        if (ballCollider == null)
        {
            ignoredBall = null;
            return;
        }

        Vector3 solidNormal = GetSolidSideNormal();
        Vector3 offset = ignoredBall.worldCenterOfMass - transform.position;
        offset.y = 0f;

        if (Vector3.Dot(offset, solidNormal) >= -sideTolerance)
        {
            ignoredBall = null;
            SetIgnored(ballCollider, false);
        }
    }

    private void Push(Collision collision)
    {
        if (!collision.collider.CompareTag(ballTag)) return;

        Rigidbody ballRb = collision.rigidbody;
        if (ballRb == null) return;

        Vector3 solidNormal = GetSolidSideNormal();
        Vector3 offset = ballRb.worldCenterOfMass - transform.position;
        offset.y = 0f;

        if (Vector3.Dot(offset, solidNormal) < -sideTolerance)
        {
            ignoredBall = ballRb;
            SetIgnored(collision.collider, true);
            return;
        }

        if (Time.time - lastPushTime < 0.2f) return;

        Vector3 normal = collision.contactCount > 0
            ? collision.GetContact(0).normal
            : ballRb.worldCenterOfMass - transform.position;
        normal.y = 0f;

        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = ballRb.worldCenterOfMass - transform.position;
            normal.y = 0f;
        }

        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = -ballRb.linearVelocity;
            normal.y = 0f;
        }

        normal.Normalize();

        Vector3 incoming = ballRb.linearVelocity;
        incoming.y = 0f;
        Vector3 reflected = Vector3.Reflect(incoming, normal);
        reflected.y = 0f;

        if (reflected.sqrMagnitude < 0.0001f)
        {
            reflected = normal;
        }

        reflected.Normalize();
        float finalSpeed = Mathf.Max(speed, incoming.magnitude);
        ballRb.linearVelocity = reflected * finalSpeed;
        lastPushTime = Time.time;

        if (hitSound != null) hitSound.Play();
    }

    private Vector3 GetSolidSideNormal()
    {
        Vector3 normal = solidSideWorldNormal;
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.0001f) normal = transform.forward;
        normal.Normalize();
        return normal;
    }

    private void SetIgnored(Collider ballCollider, bool ignored)
    {
        if (ballCollider == null || ownColliders == null) return;

        foreach (Collider ownCollider in ownColliders)
        {
            if (ownCollider != null)
            {
                Physics.IgnoreCollision(ownCollider, ballCollider, ignored);
            }
        }
    }
}