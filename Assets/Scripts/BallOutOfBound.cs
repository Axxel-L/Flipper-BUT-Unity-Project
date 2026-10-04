using UnityEngine;

public class BallOutOfBounds : MonoBehaviour
{
    [Tooltip("Position de départ de la boule (lanceur du flipper)")]
    public Transform startPosition;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            Debug.LogWarning(
                $"[Bille][Diagnostic] Reset demandée par le trigger '{other.name}' " +
                $"depuis '{name}' à t={Time.time:F3}.", this);
            ResetBall(other.attachedRigidbody);
        }
    }

    private void ResetBall(Rigidbody ballRb)
    {
        if (ballRb == null) return;
        if (startPosition == null)
        {
            Debug.LogError($"[Bille] Impossible de réinitialiser la bille : startPosition est vide sur '{name}'.", this);
            return;
        }

        // Stoppe tout mouvement
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        ballRb.useGravity = false;

        // Replace la boule au point de départ
        ballRb.transform.position = startPosition.position;
        ballRb.transform.rotation = startPosition.rotation;
        Debug.LogWarning(
            $"[Bille][Diagnostic] Bille replacée par '{name}' à {startPosition.position}.", this);
    }
}