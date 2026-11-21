using UnityEngine;

[RequireComponent(typeof(Collider))]
public class IUTBumper : MonoBehaviour
{
    [Header("Propulsion")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private string ballTag = "Ball";

    [Header("Aléatoire")]
    [SerializeField] private bool fullRandom = false;        // true = n'importe quelle direction (360°)
    [Range(0f, 180f)]
    [SerializeField] private float maxAngle = 75f;           // écart max autour de la direction de rebond
    [SerializeField] private float minSpeedVariation = 0.8f; // variation aléatoire de la vitesse (x0.8 à x1.2)
    [SerializeField] private float maxSpeedVariation = 1.2f;

    [Header("Optionnel")]
    [SerializeField] private AudioSource hitSound;

    private float lastPushTime;

    private void OnCollisionEnter(Collision collision) => Push(collision);
    private void OnCollisionStay(Collision collision) => Push(collision);

    private void Push(Collision collision)
    {
        if (!collision.collider.CompareTag(ballTag)) return;
        if (Time.time - lastPushTime < 0.2f) return;

        Rigidbody ballRb = collision.rigidbody;
        if (ballRb == null) return;

        // Direction naturelle : du point de contact vers la bille, à plat
        Vector3 contactPoint = collision.GetContact(0).point;
        Vector3 baseDir = ballRb.position - contactPoint;
        baseDir.y = 0f;
        if (baseDir.sqrMagnitude < 0.0001f)
            baseDir = Vector3.ProjectOnPlane(-collision.GetContact(0).normal, Vector3.up);
        baseDir.Normalize();

        // Angle aléatoire autour de l'axe vertical (Y)
        float angle = fullRandom
            ? Random.Range(0f, 360f)
            : Random.Range(-maxAngle, maxAngle);

        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * baseDir;

        float finalSpeed = speed * Random.Range(minSpeedVariation, maxSpeedVariation);
        ballRb.linearVelocity = dir * finalSpeed;
        lastPushTime = Time.time;

        if (hitSound != null) hitSound.Play();
    }
}