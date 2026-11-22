using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class IUTBumper : MonoBehaviour
{
    [Header("Propulsion")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private string ballTag = "Ball";

    [Header("Optionnel")]
    [SerializeField] private AudioSource hitSound;
    [SerializeField] private bool debugLogs = true;

    private float lastPushTime = -10f;

    private void OnCollisionEnter(Collision collision) => Push(collision, true);
    private void OnCollisionStay(Collision collision) => Push(collision, false);

    private void Push(Collision collision, bool addScore)
    {
        if (!collision.collider.CompareTag(ballTag)) return;

        Rigidbody ballRb = collision.rigidbody;
        if (ballRb == null) return;

        if (Time.time - lastPushTime < 0.2f) return;

        ContactPoint contact = collision.GetContact(0);

        Vector3 outward = contact.normal;
        if (Vector3.Dot(outward, ballRb.position - contact.point) < 0f)
            outward = -outward;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.0001f)
        {
            outward = ballRb.position - transform.position;
            outward.y = 0f;
        }
        outward.Normalize();

        Vector3 incoming = collision.relativeVelocity;
        incoming.y = 0f;
        if (Vector3.Dot(incoming, outward) > 0f) incoming = -incoming;

        Vector3 reflected = Vector3.Reflect(incoming, outward);
        reflected.y = 0f;
        if (reflected.sqrMagnitude < 0.0001f || Vector3.Dot(reflected.normalized, outward) < 0.1f)
            reflected = outward;
        reflected.Normalize();

        Vector3 newVelocity = reflected * Mathf.Max(speed, incoming.magnitude);

        if (debugLogs)
            Debug.Log($"[IUT] outward={outward} incoming={incoming} -> newVelocity={newVelocity} (vitesse avant={ballRb.linearVelocity})");

        ballRb.linearVelocity = newVelocity;
        lastPushTime = Time.time;
        StartCoroutine(ReapplyNextStep(ballRb, newVelocity));

        if (addScore) PinballScore.Add(100);
        if (hitSound != null) hitSound.Play();
    }

    private IEnumerator ReapplyNextStep(Rigidbody rb, Vector3 velocity)
    {
        yield return new WaitForFixedUpdate();
        if (rb == null) yield break;

        if (debugLogs)
            Debug.Log($"[IUT] vitesse de la bille au pas suivant = {rb.linearVelocity} (attendue {velocity})");

        rb.linearVelocity = velocity;
    }
}