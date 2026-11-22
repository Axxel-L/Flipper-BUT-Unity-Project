using UnityEngine;
using TMPro;

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
        {
            scoreText = FindScoreText();
        }

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
        {
            scoreText.text = currentScore.ToString("D7");
        }
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

    private void OnCollisionEnter(Collision collision) => Push(collision);

    private void Push(Collision collision)
    {
        if (!collision.collider.CompareTag(ballTag)) return;

        Rigidbody ballRb = collision.rigidbody;
        if (ballRb == null) return;

        if (Time.time - lastPushTime < 0.2f) return;

        PinballScore.Add(100);

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
}