using TMPro;
using UnityEngine;

public static class PinballScore
{
    private const string ScoreTextName = "Score Actuel";
    private static TextMeshProUGUI scoreText;
    private static int currentScore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        currentScore = 0;
        scoreText = FindScoreText();
        UpdateDisplay();
    }

    public static void Add(int points)
    {
        if (points <= 0) return;

        if (scoreText == null)
            scoreText = FindScoreText();

        currentScore += points;
        UpdateDisplay();
    }

    private static TextMeshProUGUI FindScoreText()
    {
        GameObject scoreObject = GameObject.Find(ScoreTextName);
        return scoreObject != null
            ? scoreObject.GetComponent<TextMeshProUGUI>()
            : null;
    }

    private static void UpdateDisplay()
    {
        if (scoreText != null)
            scoreText.text = currentScore.ToString("D7");
    }
}

[RequireComponent(typeof(Collider))]
public class IUTBumper : MonoBehaviour
{
    [Header("Propulsion")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private string ballTag = "Ball";

    [Header("Optionnel")]
    [SerializeField] private AudioSource hitSound;

    private float lastPushTime;

    private void OnCollisionEnter(Collision collision) => Push(collision, true);

    // Filet de sécurité si la bille reste collée (sans rajouter de points)
    private void OnCollisionStay(Collision collision) => Push(collision, false);

    private void Push(Collision collision, bool addScore)
    {
        if (!collision.collider.CompareTag(ballTag)) return;

        Rigidbody ballRb = collision.rigidbody;
        if (ballRb == null) return;

        if (Time.time - lastPushTime < 0.2f) return;

        ContactPoint contact = collision.GetContact(0);

        // Normale orientée vers l'EXTÉRIEUR de l'IUT (du côté de la bille)
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

        // Vitesse AVANT impact (relativeVelocity), orientée vers l'IUT
        Vector3 incoming = collision.relativeVelocity;
        incoming.y = 0f;
        if (Vector3.Dot(incoming, outward) > 0f) incoming = -incoming;

        // Rebond : réflexion de la vitesse d'arrivée
        Vector3 reflected = Vector3.Reflect(incoming, outward);
        reflected.y = 0f;

        // Si le rebond est quasi nul ou rentre dans l'IUT, on pousse simplement vers l'extérieur
        if (reflected.sqrMagnitude < 0.0001f || Vector3.Dot(reflected.normalized, outward) < 0.1f)
            reflected = outward;
        reflected.Normalize();

        ballRb.linearVelocity = reflected * Mathf.Max(speed, incoming.magnitude);
        lastPushTime = Time.time;

        if (addScore) PinballScore.Add(100);
        if (hitSound != null) hitSound.Play();
    }
}