using UnityEngine;

/// <summary>
/// Bumper de flipper : renvoie la bille dans la direction opposée au bumper.
/// 
/// Mise en place :
/// 1. Le bumper a un Collider (ex. cylindre) et ce script.
/// 2. La bille a un Rigidbody + un SphereCollider et le Tag "Ball".
/// 3. (Optionnel) Ajoute un AudioSource sur le bumper pour le son.
/// </summary>
public class Bumper : MonoBehaviour
{
    [Header("Rebond")]
    [Tooltip("Tag de la bille")]
    [SerializeField] private string ballTag = "Ball";

    [Tooltip("Force de l'impulsion appliquée à la bille")]
    [SerializeField] private float bounceForce = 12f;

    [Tooltip("Vitesse minimale garantie après le rebond (évite les rebonds mous)")]
    [SerializeField] private float minExitSpeed = 6f;

    [Tooltip("Garde la bille sur le plan du plateau (ignore l'axe Y)")]
    [SerializeField] private bool flattenOnTable = true;

    [Header("Feedback (optionnel)")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private float scorePoints = 100f;
    [SerializeField] private float punchScale = 1.15f;
    [SerializeField] private float punchDuration = 0.1f;

    private AudioSource audioSource;
    private Vector3 baseScale;
    private float punchTimer;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        baseScale = transform.localScale;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(ballTag)) return;

        Rigidbody ballRb = collision.rigidbody;
        if (ballRb == null) return;

        // Direction : du centre du bumper vers la bille
        Vector3 direction = collision.transform.position - transform.position;
        if (flattenOnTable) direction.y = 0f;
        direction.Normalize();

        // On annule la vitesse actuelle pour un rebond net et prévisible
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

        // Impulsion vers l'extérieur
        ballRb.AddForce(direction * bounceForce, ForceMode.Impulse);

        // Vitesse minimale garantie
        if (ballRb.linearVelocity.magnitude < minExitSpeed)
            ballRb.linearVelocity = direction * minExitSpeed;

        PlayFeedback();
    }

    private void PlayFeedback()
    {
        if (audioSource != null && hitSound != null)
            audioSource.PlayOneShot(hitSound);

        // Ici, branche ton gestionnaire de score :
        // GameManager.Instance.AddScore(scorePoints);
        Debug.Log($"Bumper touché ! +{scorePoints} points");

        punchTimer = punchDuration;
    }

    // Petit effet de "pulsation" visuelle
    private void Update()
    {
        if (punchTimer > 0f)
        {
            punchTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(punchTimer / punchDuration);
            // Va jusqu'à punchScale puis revient à la taille normale
            float scale = Mathf.Lerp(punchScale, 1f, t);
            transform.localScale = baseScale * scale;
        }
        else if (transform.localScale != baseScale)
        {
            transform.localScale = baseScale;
        }
    }
}